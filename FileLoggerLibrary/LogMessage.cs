using System.Text;
using Microsoft.Extensions.Logging;

namespace FileLoggerLibrary;

public sealed record LogMessage
{
    public string Message { get; init; }
    public Exception? Exception { get; init; }
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
    public LogMessage(string message, Exception? exception, LogLevel logLevel, string categoryName, EventId eventId, bool useUtcTimestamp)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(categoryName);

        Exception = exception;
        LogLevel = logLevel;
        CategoryName = categoryName;
        EventId = eventId;
        TimeStamp = (useUtcTimestamp ? DateTime.UtcNow : DateTime.Now).ToString("yyyy-MM-dd--HH.mm.ss");
        Header = $"{TimeStamp}|{LogLevelToString(logLevel)}|{categoryName}|";

        // Build the final message text once so Message stays effectively
        // immutable after construction.
        string finalMessage = message;

        if (eventId.Id != 0)
        {
            finalMessage += $" [{eventId.Id}]";
        }

        if (exception is not null)
        {
            finalMessage = finalMessage.Length > 0
                ? $"{finalMessage} [{exception.Message}]"
                : exception.Message;
        }

        Message = finalMessage;
        PaddedMessage = PadMessage(Header, finalMessage);
    }

    /// <summary>
    /// Converts a Microsoft.Extensions.Logging.LogLevel to a short string representation.
    /// </summary>
    /// <param name="logLevel">A log level to convert.</param>
    /// <returns>String representation of the log level.</returns>
    internal static string LogLevelToString(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "TRCE",
        LogLevel.Debug => "DBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERRR",
        LogLevel.Critical => "CRIT",
        LogLevel.None => "    ",
        _ => "????",
    };

    /// <summary>
    /// Indents multi-line messages to align with the message header, for
    /// easier reading when glancing at log messages spanning multiple lines.
    /// </summary>
    /// <param name="header">Header text for length measurement.</param>
    /// <param name="message">Message text.</param>
    /// <returns>Message text with continuation lines indented to header width.</returns>
    private static string PadMessage(string header, string message)
    {
        ReadOnlySpan<char> remaining = message.AsSpan();

        // Single-line fast path: no allocations beyond the original string.
        if (remaining.IndexOf('\n') < 0)
        {
            return message;
        }

        int padWidth = header.Length;
        StringBuilder builder = new(message.Length + padWidth * 4);
        bool isFirstLine = true;

        while (remaining.IsEmpty == false)
        {
            int newlineIndex = remaining.IndexOf('\n');
            ReadOnlySpan<char> line;

            if (newlineIndex < 0)
            {
                line = remaining;
                remaining = ReadOnlySpan<char>.Empty;
            }
            else
            {
                int lineEnd = newlineIndex;

                // Strip a trailing '\r' so CRLF sources are normalized to Environment.NewLine.
                if (lineEnd > 0 && remaining[lineEnd - 1] == '\r')
                {
                    lineEnd--;
                }

                line = remaining[..lineEnd];
                remaining = remaining[(newlineIndex + 1)..];
            }

            if (isFirstLine)
            {
                isFirstLine = false;
            }
            else
            {
                builder.Append(Environment.NewLine);
                builder.Append(' ', padWidth);
            }

            builder.Append(line);

            // A trailing newline in the input must produce a final padded (empty) line,
            // matching the prior Split + Join behavior.
            if (remaining.IsEmpty && newlineIndex >= 0)
            {
                builder.Append(Environment.NewLine);
                builder.Append(' ', padWidth);
                break;
            }
        }

        return builder.ToString();
    }

    public override string ToString()
    {
        return $"{Header}{Message}";
    }
}
