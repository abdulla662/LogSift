namespace LogSift.Models;

public class AnalysisReport
{
    public string InputFile { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public DateTime AnalysedAt { get; set; } = DateTime.UtcNow;
    public long TotalLines { get; set; }
    public long TotalBytes { get; set; }
    public double ElapsedSeconds { get; set; }
    public Dictionary<LogLevel, int> CountByLevel { get; set; } = new();
    public List<LogEntry> TopErrors { get; set; } = new();
    public string? EarliestTimestamp { get; set; }
    public string? LatestTimestamp { get; set; }

    public double LinesPerSecond => TotalLines / Math.Max(ElapsedSeconds, 0.001);
    public double MbPerSecond => (TotalBytes / 1_048_576.0) / Math.Max(ElapsedSeconds, 0.001);
}
