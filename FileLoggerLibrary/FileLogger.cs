using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

internal class FileLogger : ILogger
{
    private readonly FileLoggerProvider _fileLoggerProvider;
    private readonly string _categoryName;

    /// <summary>
    /// Default constructor for a FileLogger object.
    /// </summary>
    /// <param name="fileLoggerProvider">The log provider this FileLogger instance is based.</param>
    /// <param name="categoryName">Log or category name for this FileLogger instance</param>
    /// <exception cref="ArgumentException">Null or empty arguments are not accepted.</exception>
    public FileLogger(FileLoggerProvider fileLoggerProvider, string categoryName)
    {
        _fileLoggerProvider = fileLoggerProvider ?? throw new ArgumentException("Log provider must not be NULL");

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            throw new ArgumentException("Log name must not be NULL or empty");
        }

        _categoryName = categoryName;
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
    /// Formats the message and submits it to the Log Provider's Log() method.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="logLevel">The log level entry.</param>
    public void Log(string message, LogLevel logLevel)
    {
        LogMessage msg = new(logLevel, _categoryName, message);
        _fileLoggerProvider.Log(msg);
    }

    /// <summary>
    /// Formats the exception message and submits it to the Log Provider's Log() method.
    /// </summary>
    /// <param name="e">An exception.</param>
    public void Log(Exception e)
    {
        LogMessage msg = new(LogLevel.Error, _categoryName, e.Message);
        _fileLoggerProvider.Log(msg);
    }

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogCritical(string message)
    {
        LogMessage msg = new(LogLevel.Critical, _categoryName, message);
        _fileLoggerProvider.Log(msg);
    }

    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogDebug(string message)
    {
        LogMessage msg = new(LogLevel.Debug, _categoryName, message);
        _fileLoggerProvider.Log(msg);
    }

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogError(string message)
    {
        LogMessage msg = new(LogLevel.Error, _categoryName, message);
        _fileLoggerProvider.Log(msg);
    }

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogInformation(string message)
    {
        LogMessage msg = new(LogLevel.Information, _categoryName, message);
        _fileLoggerProvider.Log(msg);
    }

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogTrace(string message)
    {
        LogMessage msg = new(LogLevel.Trace, _categoryName, message);
        _fileLoggerProvider.Log(msg);
    }

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogWarning(string message)
    {
        LogMessage msg = new(LogLevel.Warning, _categoryName, message);
        _fileLoggerProvider.Log(msg);
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

        ArgumentNullException.ThrowIfNull(nameof(formatter));

        switch (logLevel)
        {
            case LogLevel.Trace:
                LogTrace(formatter(state, exception));
                break;
            case LogLevel.Debug:
                LogDebug(formatter(state, exception));
                break;
            case LogLevel.Warning:
                LogWarning(formatter(state, exception));
                break;
            case LogLevel.Error:
                LogError(formatter(state, exception));
                break;
            case LogLevel.Critical:
                LogCritical(formatter(state, exception));
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
