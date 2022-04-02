using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

public class FileLogger : ILogger, ILoggerProvider, IFileLogger
{
    private readonly FileLoggerProvider _fileLoggerProvider;
    private readonly string _logName;

    /// <summary>
    /// Default constructor for a FileLogger object.
    /// </summary>
    /// <param name="fileLoggerProvider">The log provider this FileLogger instance is based.</param>
    /// <param name="logName">Log or category name for this FileLogger instance</param>
    /// <exception cref="ArgumentException">Null or empty arguments are not accepted.</exception>
    public FileLogger(FileLoggerProvider fileLoggerProvider, string logName)
    {
        _fileLoggerProvider = fileLoggerProvider ?? throw new ArgumentException("Log provider must not be NULL");

        if (string.IsNullOrWhiteSpace(logName))
        {
            throw new ArgumentException("Log name must not be NULL or empty");
        }

        _logName = logName;
    }

    /// <summary>
    /// Checks if the given logLevel is enabled.
    /// </summary>
    /// <param name="logLevel">The log level to check</param>
    /// <returns></returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _fileLoggerProvider.LogMinLevel;
    }

    /// <summary>
    /// Begins a logical operation scope.
    /// </summary>
    /// <typeparam name="TState">Type parameter</typeparam>
    /// <param name="state">The entry to be written. Can be also an object.</param>
    /// <returns>A disposable object that ends the logical operation scope on dispose.</returns>
    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    /// <summary>
    /// Creates a new FileLogger instance of the specified category.
    /// </summary>
    /// <param name="categoryName">Category name</param>
    /// <returns>The ILogger for requested category was created.</returns>
    public IFileLogger CreateFileLogger(string categoryName)
    {
        return new FileLogger(_fileLoggerProvider, $"{_fileLoggerProvider.LogName}|{categoryName}");
    }

    /// <summary>
    /// Creates a new nested FileLogger instance of the specified category. This is useful for providing an in-depth callstack.
    /// </summary>
    /// <param name="categoryName">Category name</param>
    /// <returns>The ILogger for requested category was created.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_fileLoggerProvider, categoryName);
    }

    /// <summary>
    /// Formats the message and submits it to the Log Provider's Log() method.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="logLevel">The log level entry.</param>
    public void Log(string message, LogLevel logLevel = LogLevel.Information)
    {
        _fileLoggerProvider.Log($"{_logName}|{message}", logLevel);
    }

    /// <summary>
    /// Formats the exception message and submits it to the Log Provider's Log() method.
    /// </summary>
    /// <param name="e">An exception.</param>
    /// <param name="message">Added message to correspond with the exception.</param>
    public void Log(Exception e, string message)
    {
        _fileLoggerProvider.Log(e, $"{_logName}|{message}");
    }

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogCritical(string message)
    {
        _fileLoggerProvider.Log($"{_logName}|{message}", LogLevel.Critical);
    }

    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogDebug(string message)
    {
        _fileLoggerProvider.Log($"{_logName}|{message}", LogLevel.Debug);
    }

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogError(string message)
    {
        _fileLoggerProvider.Log($"{_logName}|{message}", LogLevel.Error);
    }

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogInformation(string message)
    {
        _fileLoggerProvider.Log($"{_logName}|{message}", LogLevel.Information);
    }

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogTrace(string message)
    {
        _fileLoggerProvider.Log($"{_logName}|{message}", LogLevel.Trace);
    }

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogWarning(string message)
    {
        _fileLoggerProvider.Log($"{_logName}|{message}", LogLevel.Warning);
    }

    /// <summary>
    /// Write a log entry.
    /// </summary>
    /// <typeparam name="TState">Type parameter</typeparam>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="eventId">Id of the event.</param>
    /// <param name="state">The entry to be written. Can be also an object.</param>
    /// <param name="exception">The exception related to this entry.</param>
    /// <param name="formatter">Function to create a String message of the state and exception.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (IsEnabled(logLevel) == false)
        {
            return;
        }

        if (formatter == null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        if (exception != null)
        {
            Log(exception, "");
        }
        else
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    LogCritical(formatter(state, exception));
                    break;
                case LogLevel.Debug:
                    LogDebug(formatter(state, exception));
                    break;
                case LogLevel.Error:
                    LogError(formatter(state, exception));
                    break;
                case LogLevel.Trace:
                    LogTrace(formatter(state, exception));
                    break;
                case LogLevel.Warning:
                    LogWarning(formatter(state, exception));
                    break;
                case LogLevel.None:
                    Log(formatter(state, exception), LogLevel.None);
                    break;
                case LogLevel.Information:
                default:
                    LogInformation(formatter(state, exception));
                    break;
            }
        }
    }

    /// <summary>
    /// Dispose resources.
    /// </summary>
    public void Dispose()
    {

    }
}
