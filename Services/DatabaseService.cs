using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JiraTimeTracker.Models;

namespace JiraTimeTracker.Services;

public class DatabaseService
{
    private readonly ConfigService _config;
    private const string Header = "date,issue_key,repo_name,branch,start_time,end_time,duration_seconds,duration_human,session_mode,pauses,jira_logged";

    public DatabaseService(ConfigService config)
    {
        _config = config;
        EnsureFile();
    }

    private void EnsureFile()
    {
        if (!File.Exists(_config.DbPath))
            File.WriteAllText(_config.DbPath, Header + Environment.NewLine);
    }

    public void SaveSession(Session s)
    {
        EnsureFile();
        var line = string.Join(",",
            s.Date,
            s.IssueKey,
            s.RepoName,
            $"\"{s.Branch}\"",
            s.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
            s.EndTime.ToString("yyyy-MM-dd HH:mm:ss"),
            s.DurationSeconds,
            s.DurationHuman,
            s.SessionMode,
            s.Pauses,
            s.JiraLogged);
        File.AppendAllText(_config.DbPath, line + Environment.NewLine);
    }

    public List<Session> GetSessions(DateTime from, DateTime to)
    {
        EnsureFile();
        var sessions = new List<Session>();
        var lines    = File.ReadAllLines(_config.DbPath);

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var s = ParseLine(line);
                if (s != null && s.StartTime.Date >= from.Date && s.StartTime.Date <= to.Date)
                    sessions.Add(s);
            }
            catch { }
        }
        return sessions;
    }

    public List<Session> GetAllSessions() => GetSessions(DateTime.MinValue, DateTime.MaxValue);

    private static Session? ParseLine(string line)
    {
        var parts = SplitCsv(line);
        if (parts.Length < 11) return null;
        return new Session
        {
            Date            = parts[0],
            IssueKey        = parts[1],
            RepoName        = parts[2],
            Branch          = parts[3].Trim('"'),
            StartTime       = DateTime.Parse(parts[4]),
            EndTime         = DateTime.Parse(parts[5]),
            DurationSeconds = int.Parse(parts[6]),
            DurationHuman   = parts[7],
            SessionMode     = parts[8],
            Pauses          = int.Parse(parts[9]),
            JiraLogged      = bool.Parse(parts[10])
        };
    }

    private static string[] SplitCsv(string line)
    {
        var result = new List<string>();
        var inQuote = false;
        var current = new System.Text.StringBuilder();
        foreach (var c in line)
        {
            if (c == '"') { inQuote = !inQuote; current.Append(c); }
            else if (c == ',' && !inQuote) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}