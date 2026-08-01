using LogSift.Models;

namespace LogSift.Services;

public class ReportWriter
{
    public async Task WriteAsync(AnalysisReport report, Dictionary<LogLevel, List<LogEntry>> buckets, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var writeTasks = new List<Task>
        {
            WriteBucketAsync(buckets, LogLevel.ERROR, Path.Combine(outputDir, "errors.log")),
            WriteBucketAsync(buckets, LogLevel.FATAL, Path.Combine(outputDir, "fatals.log")),
            WriteBucketAsync(buckets, LogLevel.WARN,  Path.Combine(outputDir, "warnings.log")),
            WriteSummaryAsync(report, buckets, Path.Combine(outputDir, "summary.txt")),
        };

        await Task.WhenAll(writeTasks);
    }

    private static async Task WriteBucketAsync(Dictionary<LogLevel, List<LogEntry>> buckets, LogLevel level, string path)
    {
        if (!buckets.TryGetValue(level, out var entries) || entries.Count == 0)
        {
            File.WriteAllText(path, $"No {level} entries found.\n");
            return;
        }

        await using var writer = new StreamWriter(path, append: false);
        await writer.WriteLineAsync($"# {level} entries — {entries.Count} total");
        await writer.WriteLineAsync(new string('=', 60));

        foreach (var e in entries)
        {
            await writer.WriteLineAsync($"[Line {e.LineNumber,7}] {e.RawLine}");
        }
    }

    private static async Task WriteSummaryAsync(AnalysisReport report, Dictionary<LogLevel, List<LogEntry>> buckets, string path)
    {
        await using var w = new StreamWriter(path, append: false);

        await w.WriteLineAsync("╔══════════════════════════════════════════════════════════╗");
        await w.WriteLineAsync("║                   LOGSIFT — ANALYSIS REPORT              ║");
        await w.WriteLineAsync("╚══════════════════════════════════════════════════════════╝");
        await w.WriteLineAsync();
        await w.WriteLineAsync($"  Input file   : {report.InputFile}");
        await w.WriteLineAsync($"  Output dir   : {report.OutputDirectory}");
        await w.WriteLineAsync($"  Analysed at  : {report.AnalysedAt:yyyy-MM-dd HH:mm:ss} UTC");
        await w.WriteLineAsync($"  File size    : {report.TotalBytes / 1_048_576.0:F2} MB");
        await w.WriteLineAsync($"  Total lines  : {report.TotalLines:N0}");
        await w.WriteLineAsync($"  Elapsed      : {report.ElapsedSeconds:F2}s  ({report.LinesPerSecond:N0} lines/s  ·  {report.MbPerSecond:F1} MB/s)");
        if (report.EarliestTimestamp is not null)
            await w.WriteLineAsync($"  Time range   : {report.EarliestTimestamp}  →  {report.LatestTimestamp}");
        await w.WriteLineAsync();
        await w.WriteLineAsync("  ┌──────────────┬──────────┬──────────┐");
        await w.WriteLineAsync("  │ Level        │  Count   │  Output  │");
        await w.WriteLineAsync("  ├──────────────┼──────────┼──────────┤");

        var ordered = new[] { LogLevel.FATAL, LogLevel.ERROR, LogLevel.WARN, LogLevel.INFO, LogLevel.DEBUG, LogLevel.TRACE };
        foreach (var lvl in ordered)
        {
            var count = report.CountByLevel.GetValueOrDefault(lvl);
            var file = lvl is LogLevel.FATAL or LogLevel.ERROR or LogLevel.WARN
                ? $"{lvl.ToString().ToLower()}s.log" : "—";
            await w.WriteLineAsync($"  │ {lvl,-12} │ {count,8:N0} │ {file,-8} │");
        }

        await w.WriteLineAsync("  └──────────────┴──────────┴──────────┘");
        await w.WriteLineAsync();

        if (buckets.TryGetValue(LogLevel.ERROR, out var topErrors) && topErrors.Count > 0)
        {
            await w.WriteLineAsync("  TOP 5 ERRORS:");
            await w.WriteLineAsync("  " + new string('-', 56));
            foreach (var e in topErrors.Take(5))
                await w.WriteLineAsync($"  [Line {e.LineNumber}] {Truncate(e.Message, 80)}");
        }

        if (buckets.TryGetValue(LogLevel.FATAL, out var fatals) && fatals.Count > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("  ⚠  FATAL ENTRIES:");
            await w.WriteLineAsync("  " + new string('-', 56));
            foreach (var e in fatals)
                await w.WriteLineAsync($"  [Line {e.LineNumber}] {Truncate(e.Message, 80)}");
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
