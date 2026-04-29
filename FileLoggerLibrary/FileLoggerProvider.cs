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
    private const int DefaultQueueCapacity = 1024;

    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly BlockingCollection<LogMessage> _messageQueue = new(DefaultQueueCapacity);
    private readonly Task _processMessages;
    private readonly IDisposable? _onChangeRegistration;
    private FileStream? _logStream = null;
    private StreamWriter? _logWriter = null;
    private readonly object _lockObj = new();
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
            if (_logStream is null)
            {
                throw new InvalidOperationException("Log stream is not open.");
            }

            // Reading FileStream.Position is an O(1) internal field read — no
            // kernel stat call and no FileInfo allocation per message, unlike
            // the previous `new FileInfo(LogFilename).Length` check.
            if (_logStream.Position >= LogMaxBytes)
            {
                Open();
            }

            lock (_lockObj)
            {
                if (LogEntryFormatter != null)
                {
                    if (ConsoleLogging)
                    {
                        Console.WriteLine(LogEntryFormatter(message));
                    }

                    _logWriter?.WriteLine(LogEntryFormatter(message));
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

        if (ConsoleLogging)
        {
            if (EnableConsoleColors)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                ConsoleColor levelColor = GetLevelColor(message.LogLevel, originalColor);

                try
                {
                    Console.Write($"{message.TimeStamp}|");
                    Console.ForegroundColor = levelColor;
                    Console.Write(LogMessage.LogLevelToString(message.LogLevel));
                    Console.ForegroundColor = originalColor;
                    Console.Write($"|{message.CategoryName}|");
                    Console.ForegroundColor = levelColor;
                    Console.WriteLine(body);
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }
            }
            else
            {
                Console.WriteLine($"{message.Header}{body}");
            }
        }

        _logWriter?.WriteLine($"{message.Header}{body}");
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
        if (ConsoleLogging)
        {
            if (EnableConsoleColors)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                ConsoleColor levelColor = GetLevelColor(message.LogLevel, originalColor);

                try
                {
                    Console.Write($"[{message.TimeStamp}|");
                    Console.ForegroundColor = levelColor;
                    Console.Write(LogMessage.LogLevelToString(message.LogLevel));
                    Console.ForegroundColor = originalColor;
                    Console.Write($"|{message.CategoryName}]{Environment.NewLine}");
                    Console.ForegroundColor = levelColor;
                    Console.WriteLine($"{message.Message}{Environment.NewLine}");
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }
            }
            else
            {
                Console.Write($"[{message.TimeStamp}|");
                Console.Write(LogMessage.LogLevelToString(message.LogLevel));
                Console.Write($"|{message.CategoryName}]{Environment.NewLine}");
                Console.WriteLine($"{message.Message}{Environment.NewLine}");
            }
        }

        _logWriter?.Write($"[{message.TimeStamp}|");
        _logWriter?.Write(LogMessage.LogLevelToString(message.LogLevel));
        _logWriter?.Write($"|{message.CategoryName}]{Environment.NewLine}");
        _logWriter?.WriteLine($"{message.Message}{Environment.NewLine}");
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
    /// Checks if the specified file is in-use.
    /// </summary>
    /// <param name="fileName">The filename to check.</param>
    /// <returns></returns>
    private static bool IsFileInUse(string fileName)
    {
        if (File.Exists(fileName))
        {
            try
            {
                FileInfo fileInfo = new(fileName);
                FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                fileStream.Dispose();
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Opens a new log file or resumes an existing one.
    /// </summary>
    public void Open()
    {
        // If open, close the log file.
        if (LogFilename != null &&
            _logWriter != null &&
            _logWriter.BaseStream != null)
        {
            Close();
        }

        // Select next available log increment (sets LogFilename).
        IncrementLog();

        if (string.IsNullOrWhiteSpace(LogFilename))
        {
            throw new InvalidOperationException("Log filename is null or empty");
        }

        // Append the log file.
        _logStream = new FileStream(LogFilename, FileMode.Append, FileAccess.Write, FileShare.Read);
        _logWriter = new StreamWriter(_logStream)
        {
            AutoFlush = true
        };
    }

    /// <summary>
    /// Privately sets 'LogFilename' with next available increment in the
    /// log file rotation.
    /// </summary>
    private void IncrementLog()
    {
        if (_rollMode == false)
        {
            // After we find our starting point, we will permanently be in 
            // rollMode, meaning we will always increment/wrap to the next
            // available log file increment.
            _rollMode = true;

            // Base case -- Find nearest unfilled log to continue
            //              appending, or nearest unused increment
            //              to start writing a new file.
            for (int i = 0; i < LogMaxCount; i++)
            {
                string fileName = Path.Combine(LogFolder, $"{LogName}_{i}.log");

                if (File.Exists(fileName))
                {
                    long length = new FileInfo(fileName).Length;

                    if (length < LogMaxBytes && IsFileInUse(fileName) == false)
                    {
                        // Append unfilled log.
                        LogFilename = fileName;
                        LogIncrement = i;
                        return;
                    }
                }
                else
                {
                    // Take this unused increment.
                    LogFilename = fileName;
                    LogIncrement = i;
                    return;
                }
            }

            // Full house? -- Start over from the top.
            LogFilename = Path.Combine(LogFolder, $"{LogName}_0.log");
            LogIncrement = 0;
        }
        else
        {
            // Inductive case -- We are in roll mode, so we just
            //                   use the next increment file, or
            //                   wrap around to the starting point.
            if (LogIncrement + 1 < LogMaxCount)
            {
                // Next log increment.
                LogFilename = Path.Combine(LogFolder, $"{LogName}_{++LogIncrement}.log");
            }
            else
            {
                // Start over from the top.
                LogFilename = Path.Combine(LogFolder, $"{LogName}_0.log");
                LogIncrement = 0;
            }
        }

        // Delete existing log, before using it.
        File.Delete(LogFilename);
    }

    /// <summary>
    /// Closes the log file.
    /// </summary>
    /// <returns>Returns true if the log file successfully closed, false otherwise.</returns>
    public bool Close()
    {
        try
        {
            lock (_lockObj)
            {
                // Don't call Log() here, this will result in a -=#StackOverflow#=-.
                _logWriter?.Dispose();
                _logStream?.Dispose();
                _logWriter = null;
                _logStream = null;
                LogFilename = null;
                return true;
            }
        }
        catch (Exception)
        {
            return false;
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
