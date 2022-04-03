using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FileLoggerLibrary;

public class FileLoggerProvider : ILoggerProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers =  new();
    private readonly BlockingCollection<string> _messageQueue = new(1024);
    private readonly Task _processMessages;
    private FileStream _logStream = null;
    private StreamWriter _logWriter = null;
    private readonly object _lockObj = new();
    private bool _rollMode = false;

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
        _processMessages = Task.Factory.StartNew(ProcessQueue, this, TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// Action method for initiating processing of log message queue. This method is input to
    /// the FileLogProvider constructor, where after the log file is Open(), the message queue
    /// will be processed.
    /// </summary>
    /// <param name="state"></param>
    private static void ProcessQueue(object state)
    {
        FileLoggerProvider fileLogger = (FileLoggerProvider)state;
        fileLogger.ProcessQueue();
    }

    /// <summary>
    /// Method for processing the queue of log messages and writing them out to the console
    /// and to file.
    /// </summary>
    private void ProcessQueue()
    {
        foreach (string message in _messageQueue.GetConsumingEnumerable())
        {
            long logSizeBytes = new FileInfo(LogFilename).Length;

            if (logSizeBytes >= LogMaxBytes)
            {
                Open();
            }

            lock (_lockObj)
            {
                Console.WriteLine(message);
                _logWriter.WriteLine(message);
            }
        }
    }

    /// <summary>
    /// Enqueues a log message for asynchronous write to file, allowing the caller to move
    /// on with business.
    /// </summary>
    /// <param name="message"></param>
    private void EnqueueMessage(string message)
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
    /// Generates a standard preamble for each log message. The preamble includes
    /// the current timestamp, a prefix and a formatted string with the specified
    /// log level. This method ensures log messages are consistently formatted.
    /// </summary>
    /// <param name="prefix">Message prefix.</param>
    /// <param name="entryType">The log level being annotated in the message preamble.</param>
    /// <returns>A consistently formatted preamble for human consumption.</returns>
    public static string MsgHeader(LogLevel logLevel)
    {
        string header = DateTime.Now.ToString("yyyy-MM-dd--HH.mm.ss|");

        switch (logLevel)
        {
            case LogLevel.Trace:
                header += "TRCE|";
                break;
            case LogLevel.Warning:
                header += "WARN|";
                break;
            case LogLevel.Debug:
                header += "DBUG|";
                break;
            case LogLevel.Information:
                header += "INFO|";
                break;
            case LogLevel.Error:
                header += "FAIL|";
                break;
            case LogLevel.Critical:
                header += "CRIT|";
                break;
            case LogLevel.None:
                header += "    |";
                break;
        }

        return header;
    }

    /// <summary>
    /// Indents multi-line messages to align with the message header, for
    /// easier reading when glancing at log files.
    /// </summary>
    /// <param name="header">Header text for length measurement.</param>
    /// <param name="message">Message text.</param>
    /// <returns></returns>
    private static string PadMessage(string header, string message)
    {
        string output;

        if (message.Contains(Environment.NewLine))
        {
            string[] splitMsg = message.Split(new char[] { '\n' });

            for (int i = 1; i < splitMsg.Length; i++)
            {
                splitMsg[i] = new String(' ', header.Length) + splitMsg[i];
            }

            output = header + string.Join(Environment.NewLine, splitMsg);
        }
        else
        {
            output = header + message;
        }

        return output;
    }

    /// <summary>
    /// Logs a message.
    /// </summary>
    /// <param name="message">Message to be written.</param>
    /// <param name="logLevel">Log level specification. If unspecified, the default is 'INFO'.</param>
    public void Log(string message, LogLevel logLevel)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string header = MsgHeader(logLevel);
        string output = PadMessage(header, message);

        EnqueueMessage(output);
    }

    /// <summary>
    /// Logs an exception message.
    /// </summary>
    /// <param name="e">Exception to be logged.</param>
    /// <param name="message">Additional message for debugging purposes.</param>
    public void Log(Exception e, string message)
    {
        string header = MsgHeader(LogLevel.Error);
        string excMessage = PadMessage(header, e.Message);
        string usrMessage = PadMessage(header, message);

        EnqueueMessage(excMessage);
        EnqueueMessage(usrMessage);
    }

    /// <summary>
    /// Logs a critical log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogCritical(string message)
    {
        Log(message, LogLevel.Critical);
    }

    /// <summary>
    /// Logs a debug log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogDebug(string message)
    {
        Log(message, LogLevel.Debug);
    }

    /// <summary>
    /// Logs an error log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogError(string message)
    {
        Log(message, LogLevel.Error);
    }

    /// <summary>
    /// Logs an informational log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogInformation(string message)
    {
        Log(message, LogLevel.Information);
    }

    /// <summary>
    /// Logs a trace log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogTrace(string message)
    {
        Log(message, LogLevel.Trace);
    }

    /// <summary>
    /// Logs a warning log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogWarning(string message)
    {
        Log(message, LogLevel.Warning);
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
