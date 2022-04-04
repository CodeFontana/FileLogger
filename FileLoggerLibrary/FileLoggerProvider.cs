using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FileLoggerLibrary;

internal class FileLoggerProvider : ILoggerProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers =  new();
    private readonly BlockingCollection<LogMessage> _messageQueue = new(1024);
    private readonly Task _processMessages;
    private FileStream _logStream = null;
    private StreamWriter _logWriter = null;
    private readonly object _lockObj = new();
    private bool _rollMode = false;

    public Dictionary<LogLevel, ConsoleColor> LogLevels { get; set; } = new()
    {
        [LogLevel.Trace] = ConsoleColor.Cyan,
        [LogLevel.Debug] = ConsoleColor.Blue,
        [LogLevel.Information] = ConsoleColor.Green,
        [LogLevel.Warning] = ConsoleColor.Yellow,
        [LogLevel.Error] = ConsoleColor.Red,
        [LogLevel.Critical] = ConsoleColor.DarkRed,
        [LogLevel.None] = ConsoleColor.White

    };

    public string LogName { get; private set; }
    public string LogFilename { get; private set; }
    public string LogFolder { get; private set; } = "";
    public int LogIncrement { get; private set; } = 0;
    public long LogMaxBytes { get; private set; } = 50 * 1048576;
    public uint LogMaxCount { get; private set; } = 10;
    public LogLevel LogMinLevel { get; private set; } = LogLevel.Trace;

    /// <summary>
    /// Default FileLoggerProvider constructor, instantiates a new log file instance.
    /// 
    /// For reference:
    ///   1 MB = 1000000 Bytes (in decimal)
    ///   1 MB = 1048576 Bytes (in binary)
    /// </summary>
    /// <param name="logName">Name for log file.</param>
    /// <param name="logFolder">Path where logs files will be saved.</param>
    /// <param name="logMaxBytes">Maximum size (in bytes) for the log file. If unspecified, the default is 50MB per log.</param>
    /// <param name="logMaxCount">Maximum count of log files for rotation. If unspecified, the default is 10 logs.</param>
    /// <param name="logMinLevel">Minimum log level for output.</param>
    /// <returns></returns>
    public FileLoggerProvider(string logName,
                              string logFolder = null,
                              long logMaxBytes = 50 * 1048576,
                              uint logMaxCount = 10,
                              LogLevel logMinLevel = LogLevel.Trace) : this(new()
                              {
                                  LogName = logName,
                                  LogFolder = logFolder,
                                  LogMaxBytes = logMaxBytes,
                                  LogMaxCount = logMaxCount,
                                  LogMinLevel = logMinLevel
                              })
    {

    }

    /// <summary>
    /// FileLogger constructor, based on FileLoggerOptions configuration.
    /// </summary>
    /// <param name="options">Configuration options to configure the FileLoggerProvider instance.</param>
    public FileLoggerProvider(FileLoggerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LogFolder))
        {
            string processName = Environment.ProcessPath;
            string processPath = processName[..processName.LastIndexOf("\\")];
            LogFolder = processPath + @"\log";
        }
        else if (Directory.Exists(options.LogFolder) == false)
        {
            LogFolder = options.LogFolder;
        }
        else
        {
            LogFolder = options.LogFolder;
        }

        Directory.CreateDirectory(LogFolder);
        LogName = options.LogName;
        LogMaxBytes = options.LogMaxBytes;
        LogMaxCount = options.LogMaxCount;
        LogMinLevel = options.LogMinLevel;
        Open();

        // Start processing message queue
        _processMessages = Task.Factory.StartNew(DequeueMessages, this, TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// Action method for initiating processing of log message queue. This method is input to
    /// the FileLogProvider constructor, where after the log file is Open(), the message queue
    /// will be processed.
    /// </summary>
    /// <param name="state"></param>
    private static void DequeueMessages(object state)
    {
        FileLoggerProvider fileLogger = (FileLoggerProvider)state;
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
            long logSizeBytes = new FileInfo(LogFilename).Length;

            if (logSizeBytes >= LogMaxBytes)
            {
                Open();
            }

            lock (_lockObj)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.Write($"{message.TimeStamp}|");
                Console.ForegroundColor = LogLevels[message.LogLevel];
                Console.Write(LogMessage.LogLevelToString(message.LogLevel));
                Console.ForegroundColor = originalColor;
                Console.Write($"|{message.CategoryName}|");
                Console.ForegroundColor = LogLevels[message.LogLevel];
                Console.WriteLine(message.PaddedMessage);
                Console.ForegroundColor = originalColor;
                _logWriter.WriteLine(message);
            }
        }
    }

    /// <summary>
    /// Enqueues a log message for asynchronous write to file, allowing the caller to move
    /// on with business.
    /// </summary>
    /// <param name="message"></param>
    private void EnqueueMessage(LogMessage message)
    {
        if (_messageQueue.IsAddingCompleted == false)
        {
            try
            {
                _messageQueue.Add(message);
                return;
            }
            catch (InvalidOperationException) { }
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
            // After we find our starting point, we will permanetly be in 
            // rollMode, meaning we will always increment/wrap to the next
            // available log file increment.
            _rollMode = true;

            // Base case -- Find nearest unfilled log to continue
            //              appending, or nearest unused increment
            //              to start writing a new file.
            for (int i = 0; i < LogMaxCount; i++)
            {
                string fileName = $@"{LogFolder}\{LogName}_{i}.log";

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
            LogFilename = $@"{LogFolder}\{LogName}_0.log";
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
                LogFilename = $@"{LogFolder}\{LogName}_{++LogIncrement}.log";
            }
            else
            {
                // Start over from the top.
                LogFilename = $@"{LogFolder}\{LogName}_0.log";
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
    /// Logs a message.
    /// </summary>
    /// <param name="message">Message to be written.</param>
    /// <param name="logLevel">Log level specification. If unspecified, the default is 'INFO'.</param>
    public void Log(LogMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Message))
        {
            return;
        }

        EnqueueMessage(message);
    }

    /// <summary>
    /// Dispose resources.
    /// </summary>
    public void Dispose()
    {
        _messageQueue.CompleteAdding();

        try
        {
            _processMessages.Wait();
        }
        catch (TaskCanceledException) { }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException) { }

        _loggers.Clear();
        Close();
    }
}
