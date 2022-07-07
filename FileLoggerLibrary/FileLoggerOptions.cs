using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

public class FileLoggerOptions
{
    /// <summary>
    /// Determines the log file name.
    /// <remarks>
    /// A log name is required, no default will be provided.
    /// </remarks>
    /// </summary>
    public string LogName { get; set; }

    /// <summary>
    /// Determines the log file location.
    /// <remarks>
    /// If unspecified, the default location will be a 'log' folder created
    /// at the path of the currently executing process.
    /// </remarks>
    /// </summary>
    public string LogFolder { get; set; } = "";

    /// <summary>
    /// Determines the maximum size, in bytes, of an individual log file.
    /// <remarks>
    /// The default log file size is 50MB.
    /// </remarks>
    /// </summary>
    public long LogMaxBytes { get; set; } = 50 * 1048576;

    /// <summary>
    /// Determines the maximum number of log files.
    /// <remarks>
    /// The default is 10 log files.
    /// </remarks>
    /// </summary>
    public uint LogMaxCount { get; set; } = 10;

    /// <summary>
    /// Determines the minimum logging level to be logged.
    /// <remarks>
    /// The default is LogLevel.Trace, for maximum logging.
    /// </remarks>
    /// </summary>
    public LogLevel LogMinLevel { get; set; } = LogLevel.Trace;

    /// <summary>
    /// Determines if multiline messages (messages that contain \n),
    /// should be indented to align with the log message header.
    /// <remarks>
    /// By default, indentation is enabled.
    /// </remarks>
    /// </summary>
    public bool IndentMultilineMessages { get; set; } = true;

    /// <summary>
    /// Enable logging to Console.
    /// <remarks>
    /// Default is enabled.
    /// </remarks>
    /// </summary>
    public bool ConsoleLogging { get; set; } = true;

    /// <summary>
    /// Enable colors in Console logging.
    /// <remarks>
    /// Default is to use colors.
    /// </remarks>
    /// </summary>
    public bool EnableConsoleColors { get; set; } = true;

    /// <summary>
    /// Mapping of Microsoft.Extensions.Logging.LogLevel to different
    /// console colors.
    /// </summary>
    public Dictionary<LogLevel, ConsoleColor> LogLevelColors { get; set; } = new()
    {
        [LogLevel.Trace] = ConsoleColor.Cyan,
        [LogLevel.Debug] = ConsoleColor.Blue,
        [LogLevel.Information] = ConsoleColor.Green,
        [LogLevel.Warning] = ConsoleColor.Yellow,
        [LogLevel.Error] = ConsoleColor.Red,
        [LogLevel.Critical] = ConsoleColor.DarkRed,
        [LogLevel.None] = ConsoleColor.White
    };

    /// <summary>
    /// Custom formatter for logging entry. 
    /// </summary>
    public Func<LogMessage, string> LogEntryFormatter { get; set; }
}
