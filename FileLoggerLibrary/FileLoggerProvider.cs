using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileLoggerLibrary;

[UnsupportedOSPlatform("browser")]
[ProviderAlias("FileLogger")]
internal sealed class FileLoggerProvider : ILoggerProvider, IDisposable
{
    private const int s_defaultQueueCapacity = 1024;

    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly BlockingCollection<LogMessage> _messageQueue = new(s_defaultQueueCapacity);
    private readonly Task _processMessages;
    private readonly IDisposable? _onChangeRegistration;
    private FileStream? _logStream = null;
    private StreamWriter? _logWriter = null;
#if NET9_0_OR_GREATER
    private readonly Lock _lockObj = new();
#else
    private readonly object _lockObj = new();
#endif
    private bool _rollMode = false;
    private long _droppedMessageCount;

    /// <summary>
    /// Number of messages that were dropped because the queue was full at
    /// the time of enqueue. Useful for diagnostics under bursty load.
    /// </summary>
    public long DroppedMessageCount => Interlocked.Read(ref _droppedMessageCount);

    // File-lifecycle properties — captured at construction; not reloaded.
    public string LogName { get; }
    public string? LogFilename { get; private set; }
    public string LogFolder { get; }
    public int LogIncrement { get; private set; } = 0;
    public long LogMaxBytes { get; }
    public uint LogMaxCount { get; }
    public bool AutoFlush { get; }

    // Runtime-tunable properties — reloaded from IOptionsMonitor on change.
    public LogLevel LogMinLevel { get; private set; } = LogLevel.Trace;
    public bool UseUtcTimestamp { get; private set; }
    public bool MultiLineFormat { get; private set; }
    public bool IndentMultilineMessages { get; private set; } = true;
    public bool ConsoleLogging { get; private set; } = true;
    public bool EnableConsoleColors { get; private set; } = true;
    public Func<LogMessage, string>? LogEntryFormatter { get; private set; }

    /// <summary>
    /// Immutable fallback palette used when no LogLevelColors are supplied
    /// via options. FrozenDictionary gives optimal lookup performance for
    /// the dequeue-thread hot path.
    /// </summary>
    private static readonly FrozenDictionary<LogLevel, ConsoleColor> s_defaultLevelColors =
        new Dictionary<LogLevel, ConsoleColor>
        {
            [LogLevel.Trace] = ConsoleColor.Cyan,
            [LogLevel.Debug] = ConsoleColor.Blue,
            [LogLevel.Information] = ConsoleColor.Green,
            [LogLevel.Warning] = ConsoleColor.Yellow,
            [LogLevel.Error] = ConsoleColor.Red,
            [LogLevel.Critical] = ConsoleColor.DarkRed,
            [LogLevel.None] = ConsoleColor.White,
        }.ToFrozenDictionary();

    /// <summary>
    /// Immutable snapshot of the level-to-color map. Built from the supplied
    /// options at construction so callers cannot downcast and mutate the
    /// provider's color map, and post-construction mutations on the source
    /// options dictionary cannot race with the dequeue thread.
    /// </summary>
    public IReadOnlyDictionary<LogLevel, ConsoleColor> LogLevelColors { get; private set; } = s_defaultLevelColors;

    /// <summary>
    /// Constructs a <see cref="FileLoggerProvider"/> driven by the options
    /// pattern.
    /// </summary>
    /// <remarks>
    /// File-lifecycle options (LogName, LogFolder, LogMaxBytes, LogMaxCount)
    /// are captured once at construction and are not reloaded on subsequent
    /// option changes — restart the host to change them. Runtime-tunable
    /// options (LogMinLevel, formatting flags, console colors,
    /// LogEntryFormatter) are re-applied automatically when the bound
    /// <see cref="IOptionsMonitor{TOptions}"/> reports a change.
    /// </remarks>
    public FileLoggerProvider(IOptionsMonitor<FileLoggerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        FileLoggerOptions current = options.CurrentValue;

        // File-lifecycle: captured once.
        LogName = string.IsNullOrWhiteSpace(current.LogName)
            ? Assembly.GetEntryAssembly()?.GetName().Name ?? "log"
            : current.LogName;

        LogFolder = string.IsNullOrWhiteSpace(current.LogFolder)
            ? Path.Combine(Environment.CurrentDirectory, "log")
            : current.LogFolder;
        Directory.CreateDirectory(LogFolder);

        LogMaxBytes = current.LogMaxBytes;
        LogMaxCount = current.LogMaxCount;
        AutoFlush = current.AutoFlush;

        // Runtime tunables: applied now and re-applied on every change.
        ApplyRuntimeOptions(current);
        _onChangeRegistration = options.OnChange(ApplyRuntimeOptions);

        Open();

        // Start processing message queue
        _processMessages = Task.Factory.StartNew(DequeueMessages, this, TaskCreationOptions.LongRunning);
    }

    private void ApplyRuntimeOptions(FileLoggerOptions options)
    {
        if (options is null)
        {
            return;
        }

        LogMinLevel = options.LogMinLevel;
        UseUtcTimestamp = options.UseUtcTimestamp;
        MultiLineFormat = options.MultiLineFormat;
        IndentMultilineMessages = options.IndentMultilineMessages;
        ConsoleLogging = options.ConsoleLogging;
        EnableConsoleColors = options.EnableConsoleColors;

        // Snapshot the caller-supplied dictionary so post-bind mutations on
        // the options instance cannot race with the dequeue thread.
        LogLevelColors = options.LogLevelColors is null
            ? s_defaultLevelColors
            : options.LogLevelColors.ToFrozenDictionary();

        LogEntryFormatter = options.LogEntryFormatter;
    }

    /// <summary>
    /// Action method for initiating processing of log message queue. This method is input to
    /// the FileLogProvider constructor, where after the log file is Open(), the message queue
    /// will be processed.
    /// </summary>
    /// <param name="state"></param>
    private static void DequeueMessages(object? state)
    {
        FileLoggerProvider fileLogger = (FileLoggerProvider)state!;
        fileLogger.DequeueMessages();
    }

    /// <summary>
    /// Method for processing the queue of log messages and writing them out to the console
    /// and to file.
    /// </summary>
    private void DequeueMessages()
    {
        foreach (LogMessage message in _messageQueue.GetConsumingEnumerable())
        {
            if (_logStream is null || _logWriter is null)
            {
                throw new InvalidOperationException("Log stream is not open.");
            }

            // Reading FileStream.Position is an O(1) internal field read — no
            // kernel stat call and no FileInfo allocation per message, unlike
            // the previous `new FileInfo(LogFilename).Length` check. With
            // AutoFlush=false, position can lag the StreamWriter buffer by
            // up to ~4 KB; rotation may fire that much past LogMaxBytes.
            if (_logStream.Position >= LogMaxBytes)
            {
                Open();
            }

            lock (_lockObj)
            {
                if (LogEntryFormatter != null)
                {
                    string formatted = LogEntryFormatter(message);

                    if (ConsoleLogging)
                    {
                        Console.WriteLine(formatted);
                    }

                    _logWriter!.WriteLine(formatted);
                }
                else if (MultiLineFormat)
                {
                    WriteMultiLineFormatMessage(message);
                }
                else
                {
                    WriteSingleLineFormatMessage(message);
                }
            }
        }
    }

    /// <summary>
    /// Serializes the multi-segment colored writes of a single log entry so
    /// concurrent providers or external Console writers cannot interleave
    /// between the prefix/level/middle/body runs and bleed colors.
    /// </summary>
#if NET9_0_OR_GREATER
    private static readonly Lock s_consoleSync = new();
#else
    private static readonly object s_consoleSync = new();
#endif

    /// <summary>
    /// Writes single-line formatted log message.
    /// 
    /// Example:
    /// 2022-07-07--21.53.14|TRCE|FileLoggerDemo.App|Hello, Trace!
    /// 2022-07-07--21.53.14|DBUG|FileLoggerDemo.App|Hello, Debug!
    /// 2022-07-07--21.53.14|INFO|FileLoggerDemo.App|Hello, World!
    /// 2022-07-07--21.53.14|WARN|FileLoggerDemo.App|Hello, Warning!
    /// 2022-07-07--21.53.14|ERRR|FileLoggerDemo.App|Hello, Error!
    /// 2022-07-07--21.53.14|CRIT|FileLoggerDemo.App|Hello, Critical! [Meltdown imminent!!]
    /// </summary>
    /// <param name="message">LogMessage object to be logged.</param>
    private void WriteSingleLineFormatMessage(LogMessage message)
    {
        string body = IndentMultilineMessages ? message.PaddedMessage : message.Message;
        string fullLine = $"{message.Header}{body}";

        if (ConsoleLogging)
        {
            if (EnableConsoleColors)
            {
                // Pre-build segments before entering the color critical section
                // so no allocations happen mid-write.
                string prefix = $"{message.TimeStamp}|";
                string levelText = LogMessage.LogLevelToString(message.LogLevel);
                string middle = message.EventIdText.Length > 0
                    ? $"|{message.CategoryName}|{message.EventIdText}|"
                    : $"|{message.CategoryName}|";

                WriteColoredLine(prefix, levelText, middle, body, GetLevelColor(message.LogLevel, ConsoleColor.Gray));
            }
            else
            {
                Console.Out.WriteLine(fullLine);
            }
        }

        _logWriter!.WriteLine(fullLine);
    }

    /// <summary>
    /// Writes multi-line formatted log message.
    /// 
    /// Example:
    /// [2022-07-07--21.53.14|TRCE|FileLoggerDemo.App]
    /// Hello, Trace!
    /// 
    /// [2022-07-07--21.53.14|DBUG|FileLoggerDemo.App]
    /// Hello, Debug!
    /// 
    /// [2022-07-07--21.53.14|INFO|FileLoggerDemo.App]
    /// Hello, World!
    /// 
    /// [2022-07-07--21.53.14|WARN|FileLoggerDemo.App]
    /// Hello, Warning!
    /// 
    /// [2022-07-07--21.53.14|ERRR|FileLoggerDemo.App]
    /// Hello, Error!
    /// 
    /// [2022-07-07--21.53.14|CRIT|FileLoggerDemo.App]
    /// Hello, Critical! [Meltdown imminent!!]
    /// 
    /// </summary>
    /// <param name="message">LogMessage object to be logged.</param>
    private void WriteMultiLineFormatMessage(LogMessage message)
    {
        string levelText = LogMessage.LogLevelToString(message.LogLevel);
        string headerTail = message.EventIdText.Length > 0
            ? $"|{message.CategoryName}|{message.EventIdText}]"
            : $"|{message.CategoryName}]";
        string bodyLine = $"{message.Message}{Environment.NewLine}";

        // Pre-built single-string form used by both the no-color console path
        // and the file path so each is one atomic WriteLine.
        string flatLine = $"[{message.TimeStamp}|{levelText}{headerTail}{Environment.NewLine}{bodyLine}";

        if (ConsoleLogging)
        {
            if (EnableConsoleColors)
            {
                string prefix = $"[{message.TimeStamp}|";
                string middle = $"{headerTail}{Environment.NewLine}";

                WriteColoredLine(prefix, levelText, middle, bodyLine, GetLevelColor(message.LogLevel, ConsoleColor.Gray));
            }
            else
            {
                Console.Out.WriteLine(flatLine);
            }
        }

        _logWriter!.WriteLine(flatLine);
    }

    /// <summary>
    /// Emits a four-segment colored line atomically: <paramref name="prefix"/>
    /// in the default color, <paramref name="levelText"/> and
    /// <paramref name="body"/> in <paramref name="levelColor"/>, and
    /// <paramref name="middle"/> in the default color. The whole sequence is
    /// held under a single console lock so concurrent writers cannot
    /// interleave between segments.
    /// </summary>
    private static void WriteColoredLine(string prefix, string levelText, string middle, string body, ConsoleColor levelColor)
    {
        lock (s_consoleSync)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            try
            {
                Console.Out.Write(prefix);
                Console.ForegroundColor = levelColor;
                Console.Out.Write(levelText);
                Console.ForegroundColor = originalColor;
                Console.Out.Write(middle);
                Console.ForegroundColor = levelColor;
                Console.Out.WriteLine(body);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }

    private ConsoleColor GetLevelColor(LogLevel logLevel, ConsoleColor fallback)
    {
        return LogLevelColors.TryGetValue(logLevel, out ConsoleColor color) ? color : fallback;
    }

    /// <summary>
    /// Enqueues a log message for asynchronous write to file, allowing the
    /// caller to move on with business.
    /// </summary>
    /// <remarks>
    /// Uses a non-blocking <c>TryAdd</c> so a saturated queue can never block
    /// application threads — over-capacity messages are dropped and counted
    /// via <see cref="DroppedMessageCount"/>.
    /// </remarks>
    internal void EnqueueMessage(LogMessage message)
    {
        if (_messageQueue.IsAddingCompleted)
        {
            return;
        }

        try
        {
            if (_messageQueue.TryAdd(message) == false)
            {
                Interlocked.Increment(ref _droppedMessageCount);
            }
        }
        catch (InvalidOperationException)
        {
            // Lost the race with CompleteAdding(); safe to ignore.
        }
    }

    /// <summary>
    /// Creates a new ILogger instance of the specified category.
    /// </summary>
    /// <param name="categoryName">Category name</param>
    /// <returns>The ILogger for requested category was created.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, CreateLoggerImplementation);
    }

    /// <summary>
    /// Implementation method, returns a FileLogger initialized with this
    /// FileLoggerProvider, for the requested ILogger.
    /// </summary>
    /// <param name="categoryName"></param>
    /// <returns></returns>
    private FileLogger CreateLoggerImplementation(string categoryName)
    {
        return new FileLogger(this, categoryName);
    }

    /// <summary>
    /// Opens a new log file (rotating to the next slot) or, on the very
    /// first call, resumes an existing partial log.
    /// </summary>
    /// <remarks>
    /// Called only from the constructor and from the dequeue thread when a
    /// rotation threshold is hit, so concurrent invocation is impossible.
    /// </remarks>
    private void Open()
    {
        if (_logWriter != null)
        {
            Close();
        }

        if (_rollMode == false)
        {
            // First open: latch into roll mode unconditionally so a partial
            // failure here does not put us back in resume-mode on the next
            // attempt.
            _rollMode = true;

            if (TryResumeOrTakeUnused())
            {
                return;
            }

            // Every slot exists, is full, or is locked — wrap to slot 0 and
            // truncate. FileMode.Create is atomic truncate-or-create, which
            // closes the previous Delete-then-Append TOCTOU window.
            if (TryOpenSlot(0, FileMode.Create))
            {
                return;
            }
        }
        else
        {
            // Subsequent rotation: try the next slot first; if it is locked
            // by another writer, scan forward until something opens.
            int start = (LogIncrement + 1) % (int)LogMaxCount;
            for (int step = 0; step < LogMaxCount; step++)
            {
                int candidate = (start + step) % (int)LogMaxCount;
                if (TryOpenSlot(candidate, FileMode.Create))
                {
                    return;
                }
            }
        }

        throw new InvalidOperationException(
            $"Unable to open any log slot under '{LogFolder}'; all {LogMaxCount} candidates are locked by another process.");
    }

    /// <summary>
    /// Initial-open scan: walks slots 0..N-1 looking for the first
    /// appendable partial log, or the first unused slot. Returns false if
    /// every slot is full or locked.
    /// </summary>
    private bool TryResumeOrTakeUnused()
    {
        for (int i = 0; i < LogMaxCount; i++)
        {
            string candidate = Path.Combine(LogFolder, $"{LogName}_{i}.log");

            if (File.Exists(candidate) == false)
            {
                // Unused slot — claim it. CreateNew fails atomically if
                // another process raced us into the same name, in which
                // case we move on rather than overwriting their data.
                if (TryOpenSlot(i, FileMode.CreateNew))
                {
                    return true;
                }

                continue;
            }

            if (new FileInfo(candidate).Length >= LogMaxBytes)
            {
                continue;
            }

            // Partial existing log: try to grab it for append. A failure
            // here means another process has it open with restrictive
            // sharing — try the next slot.
            if (TryOpenSlot(i, FileMode.Append))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Opens the requested slot with the supplied <see cref="FileMode"/>,
    /// publishes <see cref="_logStream"/> / <see cref="_logWriter"/> /
    /// <see cref="LogFilename"/> / <see cref="LogIncrement"/> on success,
    /// or returns false on a sharing failure so the caller can keep
    /// scanning.
    /// </summary>
    private bool TryOpenSlot(int increment, FileMode mode)
    {
        string fileName = Path.Combine(LogFolder, $"{LogName}_{increment}.log");

        try
        {
            FileStream stream = new(fileName, mode, FileAccess.Write, FileShare.Read);
            StreamWriter writer = new(stream)
            {
                AutoFlush = AutoFlush,
            };
            _logStream = stream;
            _logWriter = writer;
            LogFilename = fileName;
            LogIncrement = increment;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Closes the log file. Called only on the dequeue thread (rotation
    /// path) or during Dispose after the dequeue task has drained.
    /// </summary>
    private void Close()
    {
        lock (_lockObj)
        {
            // Don't call Log() here — would re-enqueue and recurse on
            // shutdown, producing a stack overflow.
            try
            {
                _logWriter?.Dispose();
                _logStream?.Dispose();
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }

            _logWriter = null;
            _logStream = null;
            LogFilename = null;
        }
    }

    /// <summary>
    /// Dispose resources.
    /// </summary>
    public void Dispose()
    {
        _onChangeRegistration?.Dispose();
        _messageQueue.CompleteAdding();

        try
        {
            _processMessages.Wait();
        }
        catch (TaskCanceledException) { }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException) { }

        _messageQueue.Dispose();
        _loggers.Clear();
        Close();
    }
}
