using System;

namespace JiraTimeTracker.Models;

public class Session
{
    public string   Date            { get; set; } = "";
    public string   IssueKey        { get; set; } = "";
    public string   RepoName        { get; set; } = "";
    public string   Branch          { get; set; } = "";
    public DateTime StartTime       { get; set; }
    public DateTime EndTime         { get; set; }
    public int      DurationSeconds { get; set; }
    public string   DurationHuman   { get; set; } = "";
    public string   SessionMode     { get; set; } = "";
    public int      Pauses          { get; set; }
    public bool     JiraLogged      { get; set; }

    public static string FormatDuration(int seconds)
    {
        var h = seconds / 3600;
        var m = (seconds % 3600) / 60;
        var s = seconds % 60;
        if (h > 0) return $"{h}h {m}m";
        if (m > 0) return $"{m}m {s}s";
        return $"{s}s";
    }
}