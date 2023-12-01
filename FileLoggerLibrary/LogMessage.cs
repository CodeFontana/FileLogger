using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

public class LogMessage
{
    public string Message { get; init; }
    public Exception Exception { get; init; }
    public LogLevel LogLevel { get; init; }
    public string CategoryName { get; init; }
    public EventId EventId { get; init; }
    public string TimeStamp { get; init; }
    public string Header { get; init; }
    public string PaddedMessage { get; init; }

    /// <summary>
    /// Default constructor, builds a log message and publishes properties
    /// for each distinct part of the message structure.
    /// </summary>
    public LogMessage(string message, Exception exception, LogLevel logLevel, string categoryName, EventId eventId, bool useUtcTimestamp)
    {
        Message = message;
        Exception = exception;
        LogLevel = logLevel;
        CategoryName = categoryName;
        EventId = eventId;
        TimeStamp = useUtcTimestamp ? DateTime.UtcNow.ToString("yyyy-MM-dd--HH.mm.ss") : DateTime.Now.ToString("yyyy-MM-dd--HH.mm.ss");
        Header = $"{TimeStamp}|{LogLevelToString(logLevel)}|{categoryName}|";

        if (eventId.Id != 0)
        {
            Message += $" [{eventId.Id}]";
        }

        if (exception != null && message.Length > 0)
        {
            Message += $" [{exception.Message}]";
        }
        else if (exception != null && message.Length == 0)
        {
            Message = exception.Message;
        }

        PaddedMessage = PadMessage(Header, Message);
    }

    /// <summary>
    /// Converts a Microsoft.Extensions.Logging.LogLevel to a short string representation.
    /// </summary>
    /// <param name="logLevel">A log level to convert.</param>
    /// <returns>String representation of the log level.</returns>
    internal static string LogLevelToString(LogLevel logLevel)
    {
        string result = "";

        switch (logLevel)
        {
            case LogLevel.Trace:
                result += "TRCE";
                break;
            case LogLevel.Warning:
                result += "WARN";
                break;
            case LogLevel.Debug:
                result += "DBUG";
                break;
            case LogLevel.Information:
                result += "INFO";
                break;
            case LogLevel.Error:
                result += "ERRR";
                break;
            case LogLevel.Critical:
                result += "CRIT";
                break;
            case LogLevel.None:
                result += "    ";
                break;
        }

        return result;
    }

    /// <summary>
    /// Indents multi-line messages to align with the message header, for
    /// easier reading when glancing at log messages spanning multiple lines.
    /// </summary>
    /// <param name="header">Header text for length measurement.</param>
    /// <param name="message">Message text.</param>
    /// <returns></returns>
    private static string PadMessage(string header, string message)
    {
        string result;

        if (message.Contains("\r\n") || message.Contains('\n'))
        {
            string[] splitMsg = message.Replace("\r\n", "\n").Split(['\n']);

            for (int i = 1; i < splitMsg.Length; i++)
            {
                splitMsg[i] = new String(' ', header.Length) + splitMsg[i];
            }

            result = string.Join(Environment.NewLine, splitMsg);
        }
        else
        {
            result = message;
        }

        return result;
    }

    public override string ToString()
    {
        return $"{Header}{Message}";
    }
}
