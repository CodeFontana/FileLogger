using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

public interface IFileLoggerProvider
{
    string LogFilename { get; }
    string LogFolder { get; }
    int LogIncrement { get; }
    long LogMaxBytes { get; }
    uint LogMaxCount { get; }
    string LogName { get; }

    bool Close();
    IFileLogger CreateFileLogger(string categoryName);
    ILogger CreateLogger(string categoryName);
    void Dispose();
    void Log(Exception e, string message);
    void Log(string message, LogLevel logLevel = LogLevel.Information);
    void LogCritical(string message);
    void LogDebug(string message);
    void LogError(string message);
    void LogInformation(string message);
    void LogTrace(string message);
    void LogWarning(string message);
    void Open();
}
