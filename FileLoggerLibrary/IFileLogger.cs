using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

public interface IFileLogger
{
    IDisposable BeginScope<TState>(TState state);
    IFileLogger CreateFileLogger(string categoryName);
    ILogger CreateLogger(string categoryName);
    void Dispose();
    bool IsEnabled(LogLevel logLevel);
    void Log(Exception e, string message);
    void Log(string message, LogLevel logLevel = LogLevel.Information);
    void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter);
    void LogCritical(string message);
    void LogDebug(string message);
    void LogError(string message);
    void LogInformation(string message);
    void LogTrace(string message);
    void LogWarning(string message);
}
