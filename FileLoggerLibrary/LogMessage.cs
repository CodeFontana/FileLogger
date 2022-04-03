using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

internal class LogMessage
{
    public string TimeStamp { get; init; }
    public LogLevel LogLevel { get; init; }
    public string CategoryName { get; init; }
    public string Header { get; init; }
    public string Message { get; init; }
    public string OutputMessage { get; init; }

    public LogMessage(LogLevel logLevel, string categoryName, string message)
    {
        TimeStamp = DateTime.Now.ToString("yyyy-MM-dd--HH.mm.ss");
        CategoryName = categoryName;
        Message = message;
        LogLevel = logLevel;
        Header = $"{TimeStamp}|{LogLevelToString(logLevel)}|{categoryName}|";
        OutputMessage = PadMessage(Header, Message);
    }

    /// <summary>
    /// Generates a standard preamble for each log message. The preamble includes
    /// the current timestamp, a prefix and a formatted string with the specified
    /// log level. This method ensures log messages are consistently formatted.
    /// </summary>
    /// <param name="prefix">Message prefix.</param>
    /// <param name="entryType">The log level being annotated in the message preamble.</param>
    /// <returns>A consistently formatted preamble for human consumption.</returns>
    public static string LogLevelToString(LogLevel logLevel)
    {
        string header = "";

        switch (logLevel)
        {
            case LogLevel.Trace:
                header += "TRCE";
                break;
            case LogLevel.Warning:
                header += "WARN";
                break;
            case LogLevel.Debug:
                header += "DBUG";
                break;
            case LogLevel.Information:
                header += "INFO";
                break;
            case LogLevel.Error:
                header += "ERRR";
                break;
            case LogLevel.Critical:
                header += "CRIT";
                break;
            case LogLevel.None:
                header += "    ";
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

    public override string ToString()
    {
        return OutputMessage;
    }
}
