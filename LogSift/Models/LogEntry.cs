namespace LogSift.Models;

public enum LogLevel { TRACE, DEBUG, INFO, WARN, ERROR, FATAL, UNKNOWN }

public record LogEntry(
    int LineNumber,
    LogLevel Level,
    string Timestamp,
    string Source,
    string Message,
    string RawLine
);
