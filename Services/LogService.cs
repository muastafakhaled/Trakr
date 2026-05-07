using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JiraTimeTracker.Services;

public enum LogLevel { Debug, Info, Warning, Error }

public class LogEntry
{
    public DateTime Time     { get; init; }
    public LogLevel Level    { get; init; }
    public string   Category { get; init; } = "";
    public string   Message  { get; init; } = "";

    public string LevelTag => Level switch
    {
        LogLevel.Debug   => "DBG",
        LogLevel.Info    => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error   => "ERR",
        _                => "   "
    };

    public string LevelColor => Level switch
    {
        LogLevel.Debug   => "#484f58",
        LogLevel.Info    => "#8b949e",
        LogLevel.Warning => "#d29922",
        LogLevel.Error   => "#f85149",
        _                => "#8b949e"
    };

    public string Formatted =>
        $"{Time:HH:mm:ss.fff}  [{LevelTag}]  {Category,-12}  {Message}";
}

public class LogService
{
    // ── Settings ──────────────────────────────────────────────────
    public LogLevel MinLevel       { get; set; } = LogLevel.Debug;
    public bool     WriteToFile    { get; set; } = true;

    // Default to %AppData%\Trakr\tracker.log so logging works out of the box
    public string LogFilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Trakr", "tracker.log");

    // ── In-memory buffer for the viewer ──────────────────────────
    private readonly List<LogEntry>      _entries = new();
    private readonly object              _lock    = new();
    private const    int                 MaxBuffer = 2000;

    public event Action<LogEntry>? EntryAdded;

    // ── Logging methods ──────────────────────────────────────────
    public void Debug  (string cat, string msg) => Write(LogLevel.Debug,   cat, msg);
    public void Info   (string cat, string msg) => Write(LogLevel.Info,    cat, msg);
    public void Warning(string cat, string msg) => Write(LogLevel.Warning, cat, msg);
    public void Error  (string cat, string msg, Exception? ex = null)
    {
        var full = ex == null ? msg : $"{msg} — {ex.GetType().Name}: {ex.Message}";
        Write(LogLevel.Error, cat, full);
    }

    public void Write(LogLevel level, string category, string message)
    {
        if (level < MinLevel) return;

        var entry = new LogEntry
        {
            Time     = DateTime.Now,
            Level    = level,
            Category = category,
            Message  = message
        };

        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxBuffer)
                _entries.RemoveAt(0);
        }

        if (WriteToFile && !string.IsNullOrEmpty(LogFilePath))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                File.AppendAllText(LogFilePath, entry.Formatted + Environment.NewLine);
            }
            catch { /* never crash on log write */ }
        }

        EntryAdded?.Invoke(entry);
    }

    // ── Query ────────────────────────────────────────────────────
    public List<LogEntry> GetAll()
    {
        lock (_lock) return _entries.ToList();
    }

    public List<LogEntry> GetFiltered(LogLevel minLevel, string? category, string? search)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Level >= minLevel)
                .Where(e => string.IsNullOrEmpty(category) || e.Category == category)
                .Where(e => string.IsNullOrEmpty(search)   ||
                            e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            e.Category.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public IEnumerable<string> GetCategories()
    {
        lock (_lock) return _entries.Select(e => e.Category).Distinct().OrderBy(c => c).ToList();
    }

    public void Clear()
    {
        lock (_lock) _entries.Clear();
    }

    public void ExportToFile(string path)
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            foreach (var e in _entries)
                sb.AppendLine(e.Formatted);
            File.WriteAllText(path, sb.ToString());
        }
    }
}
