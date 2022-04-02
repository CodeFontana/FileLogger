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
    /// at the path of the currently exeecuting process.
    /// </remarks>
    /// </summary>
    public string LogFolder { get; set; } = "";

    /// <summary>
    /// Determines the maximum size, in bytes, of an individual log file.
    /// </summary>
    public long LogMaxBytes { get; set; } = 50 * 1000000;

    /// <summary>
    /// Determines the maximum number of log files.
    /// </summary>
    public uint LogMaxCount { get; set; } = 10;
}
