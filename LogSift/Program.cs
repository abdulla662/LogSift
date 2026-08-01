using System.Diagnostics;
using LogSift.Models;
using LogSift.Services;
using Spectre.Console;

AnsiConsole.Write(new FigletText("LogSift").Color(Color.MediumPurple1));
AnsiConsole.MarkupLine("[grey]Stream. Sift. Report. — No file too large.[/]\n");

// ── Argument parsing ──────────────────────────────────────────────────────────
string? inputFile = null;
string outputDir = "./logsift-output";
bool generateDemo = false;
int demoLines = 50_000;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--input"  or "-i" when i + 1 < args.Length: inputFile = args[++i]; break;
        case "--output" or "-o" when i + 1 < args.Length: outputDir = args[++i]; break;
        case "--demo":   generateDemo = true; break;
        case "--lines"  when i + 1 < args.Length: int.TryParse(args[++i], out demoLines); break;
        case "--help"   or "-h":
            PrintHelp();
            return 0;
    }
}

// ── Demo mode: generate a sample log file ────────────────────────────────────
if (generateDemo || inputFile is null)
{
    inputFile = Path.Combine(Path.GetTempPath(), "logsift-demo.log");
    AnsiConsole.MarkupLine($"[yellow]Generating demo log file with {demoLines:N0} lines…[/]");
    await AnsiConsole.Progress()
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("Generating", maxValue: 1);
            await LogGenerator.GenerateAsync(inputFile, demoLines);
            task.Value = 1;
        });
    AnsiConsole.MarkupLine($"[green]✓[/] Demo file: {Path.GetFullPath(inputFile)}\n");
}

if (!File.Exists(inputFile))
{
    AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {inputFile}");
    return 1;
}

var fileInfo = new FileInfo(inputFile);
AnsiConsole.MarkupLine($"[bold]Input :[/] {fileInfo.FullName}");
AnsiConsole.MarkupLine($"[bold]Size  :[/] {fileInfo.Length / 1_048_576.0:F2} MB");
AnsiConsole.MarkupLine($"[bold]Output:[/] {Path.GetFullPath(outputDir)}\n");

// ── Parse ─────────────────────────────────────────────────────────────────────
var parser  = new LogParser();
var report  = new AnalysisReport
{
    InputFile      = fileInfo.FullName,
    OutputDirectory = Path.GetFullPath(outputDir),
    TotalBytes     = fileInfo.Length
};
var buckets = new Dictionary<LogLevel, List<LogEntry>>();
foreach (LogLevel lvl in Enum.GetValues<LogLevel>())
    buckets[lvl] = [];

var sw = Stopwatch.StartNew();

await AnsiConsole.Progress()
    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new SpinnerColumn())
    .StartAsync(async ctx =>
    {
        var task = ctx.AddTask("[green]Parsing log file[/]");
        task.IsIndeterminate = true;

        await foreach (var entry in parser.ParseAsync(inputFile))
        {
            report.TotalLines++;
            buckets[entry.Level].Add(entry);
            report.CountByLevel[entry.Level] = report.CountByLevel.GetValueOrDefault(entry.Level) + 1;

            if (!string.IsNullOrEmpty(entry.Timestamp))
            {
                report.EarliestTimestamp ??= entry.Timestamp;
                report.LatestTimestamp = entry.Timestamp;
            }
        }

        task.IsIndeterminate = false;
        task.Value = task.MaxValue;
    });

sw.Stop();
report.ElapsedSeconds  = sw.Elapsed.TotalSeconds;
report.TopErrors       = buckets[LogLevel.ERROR].Take(10).ToList();

// ── Write output ──────────────────────────────────────────────────────────────
AnsiConsole.MarkupLine("\n[grey]Writing output files…[/]");
var writer = new ReportWriter();
await writer.WriteAsync(report, buckets, outputDir);

// ── Summary table ─────────────────────────────────────────────────────────────
AnsiConsole.WriteLine();
var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("[bold]Level[/]")
    .AddColumn(new TableColumn("[bold]Count[/]").RightAligned())
    .AddColumn("[bold]Output File[/]");

void AddRow(LogLevel lvl, string color, string file)
{
    var count = report.CountByLevel.GetValueOrDefault(lvl);
    table.AddRow($"[{color}]{lvl}[/]", $"[{color}]{count:N0}[/]", count > 0 ? file : "[grey]—[/]");
}

AddRow(LogLevel.FATAL, "red1 bold", "fatals.log");
AddRow(LogLevel.ERROR, "red",       "errors.log");
AddRow(LogLevel.WARN,  "yellow",    "warnings.log");
AddRow(LogLevel.INFO,  "green",     "—");
AddRow(LogLevel.DEBUG, "grey",      "—");

AnsiConsole.Write(table);

AnsiConsole.MarkupLine(
    $"\n[green]✓ Done[/]  {report.TotalLines:N0} lines in [bold]{report.ElapsedSeconds:F2}s[/]" +
    $"  ·  [bold]{report.LinesPerSecond:N0}[/] lines/s  ·  [bold]{report.MbPerSecond:F1}[/] MB/s");
AnsiConsole.MarkupLine($"[grey]Report → {Path.GetFullPath(outputDir)}[/]");

return 0;

static void PrintHelp()
{
    AnsiConsole.MarkupLine("[bold]Usage:[/]  logsift [options]\n");
    AnsiConsole.MarkupLine("  [green]--input,  -i <path>[/]    Log file to analyse");
    AnsiConsole.MarkupLine("  [green]--output, -o <dir>[/]     Output directory  (default: ./logsift-output)");
    AnsiConsole.MarkupLine("  [green]--demo[/]                 Generate a demo log file and analyse it");
    AnsiConsole.MarkupLine("  [green]--lines  <n>[/]           Lines to generate in demo mode (default: 50000)");
    AnsiConsole.MarkupLine("  [green]--help,   -h[/]           Show this help\n");
    AnsiConsole.MarkupLine("[bold]Supported formats:[/] Serilog · NLog · log4net · ASP.NET Core · plain text");
}
