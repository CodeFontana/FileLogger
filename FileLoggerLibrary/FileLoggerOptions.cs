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
    public string Name { get; set; }

    /// <summary>
    /// Determines the log file location.
    /// <remarks>
    /// If unspecified, the default location will be a 'log' folder created
    /// at the path of the currently exeecuting process.
    /// </remarks>
    /// </summary>
    public string Folder { get; set; } = "";

    /// <summary>
    /// Determines the maximum size, in bytes, of an individual log file.
    /// </summary>
    public long MaxBytes { get; set; } = 50 * 1048576;

    /// <summary>
    /// Determines the maximum number of log files.
    /// </summary>
    public uint MaxCount { get; set; } = 10;

    /// <summary>
    /// Determines the minimum logging level to be logged.
    /// </summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Trace;
}
