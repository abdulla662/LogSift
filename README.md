# LogSift

A .NET 8 CLI tool that **streams** large log files of any size, extracts and categorises ERROR / WARN / FATAL entries into separate files, and generates a summary report — all without loading the full file into memory.

[![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://docker.com)

---

## The Engineering Problem

Server log files can reach 10+ GB. A naive implementation (`File.ReadAllLines`) loads the entire file into RAM and crashes. LogSift uses **`FileStream` + `StreamReader` with async streaming** — it reads one line at a time, keeping memory usage constant regardless of file size. Processing 50,000 lines takes roughly 0.1s.

---

## Features

- **Zero memory growth** — streams line-by-line via `IAsyncEnumerable<LogEntry>`
- **Multi-format detection** — Serilog, NLog, log4net, ASP.NET Core, plain text
- **Categorised output** — `errors.log`, `fatals.log`, `warnings.log`
- **Summary report** — line counts, time range, top 5 errors, throughput (lines/s, MB/s)
- **Demo mode** — generates a realistic 50K-line log file to test with instantly
- **Docker support** — runs anywhere, no .NET install required

---

## Quick Start

```bash
# Run with demo (generates 50,000 lines and analyses them)
dotnet run --project LogSift -- --demo

# Analyse your own log file
dotnet run --project LogSift -- --input /path/to/app.log --output ./results

# Large demo (1 million lines)
dotnet run --project LogSift -- --demo --lines 1000000
```

### Docker

```bash
docker build -t logsift .
docker run --rm -v /your/logs:/logs logsift --input /logs/app.log --output /logs/output
```

---

## Output

```
logsift-output/
├── errors.log       # All ERROR entries with line numbers
├── fatals.log       # All FATAL entries
├── warnings.log     # All WARN entries
└── summary.txt      # Full analysis report
```

**Sample summary:**
```
╔══════════════════════════════════════════════════════════╗
║                   LOGSIFT — ANALYSIS REPORT              ║
╚══════════════════════════════════════════════════════════╝

  Input file   : /logs/app.log
  File size    : 5.23 MB
  Total lines  : 50,000
  Elapsed      : 0.09s  (555,555 lines/s  ·  58.1 MB/s)
  Time range   : 2024-01-01 00:00:00  →  2024-01-02 14:06:13

  ┌──────────────┬──────────┬──────────┐
  │ Level        │  Count   │  Output  │
  ├──────────────┼──────────┼──────────┤
  │ FATAL        │       12 │ fatals   │
  │ ERROR        │    1,842 │ errors   │
  │ WARN         │    6,731 │ warnings │
  │ INFO         │   26,194 │ —        │
  │ DEBUG        │   15,221 │ —        │
  └──────────────┴──────────┴──────────┘
```

---

## Architecture

```
LogSift/
├── Program.cs                    # CLI entry, argument parsing, progress UI
├── Models/
│   ├── LogEntry.cs               # Immutable record per log line
│   └── AnalysisReport.cs         # Aggregated stats after a full pass
└── Services/
    ├── LogParser.cs              # Streaming parser — IAsyncEnumerable<LogEntry>
    ├── ReportWriter.cs           # Async parallel output writer
    └── LogGenerator.cs           # Realistic demo log file generator
```

**Key decisions:**
- `IAsyncEnumerable<LogEntry>` — caller pulls one entry at a time, backpressure is natural
- `Task.WhenAll` for output writing — all four files written in parallel
- Regex patterns ordered by specificity — fast-path for structured formats, fallback for plain text
- `FileStream` with 64 KB buffer — optimal for sequential disk reads

---

Built by [Abdulla Hamdy](https://github.com/abdulla662)
