using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

internal sealed class FileLogger : ILogger
{
    private readonly FileLoggerProvider _fileLoggerProvider;
    private readonly string _categoryName;

    /// <summary>
    /// Default constructor for a FileLogger object.
    /// </summary>
    /// <param name="fileLoggerProvider">The log provider this FileLogger instance is based.</param>
    /// <param name="categoryName">Log or category name for this FileLogger instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileLoggerProvider"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="categoryName"/> is null, empty, or whitespace.</exception>
    public FileLogger(FileLoggerProvider fileLoggerProvider, string categoryName)
    {
        ArgumentNullException.ThrowIfNull(fileLoggerProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        _fileLoggerProvider = fileLoggerProvider;
        _categoryName = categoryName;
    }

    /// <summary>
    /// Checks if the given logLevel is enabled.
    /// </summary>
    /// <param name="logLevel">The log level to check.</param>
    /// <returns></returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None && logLevel >= _fileLoggerProvider.LogMinLevel;
    }

    /// <summary>
    /// Begins a logical operation scope.
    /// </summary>
    /// <typeparam name="TState">Type parameter.</typeparam>
    /// <param name="state">The entry to be written. Can be also an object.</param>
    /// <returns>A disposable object that ends the logical operation scope on dispose.</returns>
    IDisposable? ILogger.BeginScope<TState>(TState state)
    {
        return NullScope.Instance;
    }

    /// <summary>
    /// Write a log entry.
    /// </summary>
    /// <typeparam name="TState">Type parameter.</typeparam>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="eventId">Id of the event.</param>
    /// <param name="state">The entry to be written. Can be also an object.</param>
    /// <param name="exception">The exception related to this entry.</param>
    /// <param name="formatter">Function to create a String message of the state and exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="formatter"/> is null.</exception>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel) == false)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(formatter);
        string message = formatter(state, exception);
        LogMessage logMessage = new(message, exception, logLevel, _categoryName, eventId, _fileLoggerProvider.UseUtcTimestamp);
        _fileLoggerProvider.EnqueueMessage(logMessage);
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        private NullScope() { }
        public void Dispose() { }
    }
}
