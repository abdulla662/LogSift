using System.Text.RegularExpressions;
using LogSift.Models;

namespace LogSift.Services;

public class LogParser
{
    // Matches common log formats:
    // Serilog:   [2024-01-15 10:23:45.123 +00:00 ERR] ...
    // NLog:      2024-01-15 10:23:45.1234|ERROR|MyApp.Service|message
    // Log4net:   2024-01-15 10:23:45,123 ERROR [Thread-1] MyApp - message
    // ASP.NET:   info: Microsoft.Hosting.Lifetime[0]
    // Plain:     ERROR: something went wrong
    private static readonly Regex[] _patterns =
    [
        new(@"\[(?<ts>\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}[^\]]*)\s+(?<lvl>TRC|DBG|INF|WRN|ERR|FTL|TRACE|DEBUG|INFO|WARN(?:ING)?|ERROR|FATAL|CRITICAL)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(?<ts>\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}[.,:]\d+)\s*[|\s]+(?<lvl>TRACE|DEBUG|INFO|WARN(?:ING)?|ERROR|FATAL|CRITICAL)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(?<ts>\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}[.,:]\d*)\s+(?<lvl>TRACE|DEBUG|INFO|WARN(?:ING)?|ERROR|FATAL|CRITICAL)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(?<lvl>trace|debug|info|warn|error|crit|fatal):\s+(?<src>[^\[]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(?<lvl>TRACE|DEBUG|INFO|WARN(?:ING)?|ERROR|FATAL|CRITICAL)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public IAsyncEnumerable<LogEntry> ParseAsync(string filePath, CancellationToken ct = default)
        => ParseCoreAsync(filePath, ct);

    private static async IAsyncEnumerable<LogEntry> ParseCoreAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 65536, useAsync: true);
        using var reader = new StreamReader(stream);

        int lineNumber = 0;
        string? line;

        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNumber++;
            yield return Parse(line, lineNumber);
        }
    }

    private static LogEntry Parse(string line, int lineNumber)
    {
        foreach (var pattern in _patterns)
        {
            var m = pattern.Match(line);
            if (!m.Success) continue;

            var lvlStr = m.Groups["lvl"].Value.ToUpperInvariant();
            var ts = m.Groups["ts"].Success ? m.Groups["ts"].Value.Trim() : string.Empty;
            var src = m.Groups["src"].Success ? m.Groups["src"].Value.Trim() : string.Empty;

            return new LogEntry(lineNumber, ParseLevel(lvlStr), ts, src, line, line);
        }

        return new LogEntry(lineNumber, Models.LogLevel.UNKNOWN, string.Empty, string.Empty, line, line);
    }

    private static Models.LogLevel ParseLevel(string s) => s switch
    {
        "TRC" or "TRACE"                   => Models.LogLevel.TRACE,
        "DBG" or "DEBUG"                   => Models.LogLevel.DEBUG,
        "INF" or "INFO"                    => Models.LogLevel.INFO,
        "WRN" or "WARN" or "WARNING"       => Models.LogLevel.WARN,
        "ERR" or "ERROR"                   => Models.LogLevel.ERROR,
        "FTL" or "FATAL" or "CRIT"
             or "CRITICAL"                 => Models.LogLevel.FATAL,
        _                                  => Models.LogLevel.UNKNOWN,
    };
}
