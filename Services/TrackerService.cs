using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JiraTimeTracker.Models;

namespace JiraTimeTracker.Services;

public class TrackerState
{
    public string?   ActiveIssue    { get; set; }
    public string?   ActiveBranch   { get; set; }
    public string?   ActiveRepo     { get; set; }
    public bool      IsManual       { get; set; }
    public bool      IsPaused       { get; set; }
    public DateTime? SessionStart   { get; set; }
    public DateTime? SegmentStart   { get; set; }
    public DateTime? PauseStart     { get; set; }
    public int       PauseCount     { get; set; }
    public List<(DateTime Start, DateTime End)> WorkSegments { get; set; } = new();
    public List<string> SessionLog  { get; set; } = new();
    public bool IsTracking          => ActiveIssue != null;
}

public class TrackerService
{
    private readonly ConfigService    _config;
    private readonly GitService       _git;
    private readonly JiraService      _jira;
    private readonly DatabaseService  _db;
    private readonly IdleService      _idle;
    private readonly LogService?      _log;
    private CancellationTokenSource   _cts = new();

    public TrackerState State { get; } = new();

    public event Action?                 StateChanged;
    public event Action<string, string>? Notified;
    public event Action<string>?         SessionProposed;
    public event Action<string>?         StopProposed;

    public TrackerService(ConfigService config, GitService git, JiraService jira,
                          DatabaseService db, IdleService idle, LogService? log = null)
    {
        _config = config; _git = git; _jira = jira; _db = db; _idle = idle; _log = log;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        if (State.IsTracking) EndSession("tracker stopped");
    }

    // Called by ProcessWatcherService via App.xaml.cs
    public void OnIdeStarted(string ideName)
    {
        if (!State.IsTracking)
            SessionProposed?.Invoke(ideName);
    }

    public void OnIdeClosed(string ideName)
    {
        if (State.IsTracking)
            StopProposed?.Invoke(ideName);
    }

    // Start a session from a Jira issue (from proposal window or manual start)
    public void StartFromIssue(JiraIssue issue, string? repoPath = null, string? branch = null)
    {
        if (State.IsTracking) EndSession("new session started");
        var repoName = repoPath != null ? Path.GetFileName(repoPath) : null;
        StartSession(issue.Key, repoPath, repoName, branch, manual: true);
    }

    public void ManualStop()
    {
        if (State.IsTracking) EndSession("manually stopped");
    }

    public int GetWorkedSeconds()
    {
        var total = 0;
        foreach (var seg in State.WorkSegments)
            total += (int)(seg.End - seg.Start).TotalSeconds;
        if (State.SegmentStart.HasValue && !State.IsPaused)
            total += (int)(DateTime.Now - State.SegmentStart.Value).TotalSeconds;
        return total;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var interval = _config.Current.Settings.ScanIntervalSeconds;
                await Task.Delay(interval * 1000, ct);

                var idleSec     = _idle.GetIdleSeconds();
                var locked      = _idle.IsPCLocked();
                var idleTimeout = _config.Current.Settings.IdleTimeoutMinutes * 60;
                var shouldPause = idleSec >= idleTimeout || locked;
                var pauseReason = locked ? "PC locked" : $"idle for {Session.FormatDuration(idleSec)}";

                if (State.IsTracking)
                {
                    if (shouldPause && !State.IsPaused)
                        PauseSession(pauseReason);
                    else if (!shouldPause && State.IsPaused)
                        ResumeSession("activity detected");

                    // Check if branch changed (optional git hint)
                    if (State.ActiveRepo != null)
                    {
                        var currentBranch = _git.GetCurrentBranch(State.ActiveRepo);
                        if (currentBranch != null && currentBranch != State.ActiveBranch)
                        {
                            AddLog($"Branch changed to {currentBranch}");
                            State.ActiveBranch = currentBranch;
                        }
                    }
                }

                StateChanged?.Invoke();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log?.Error("Tracker", "Loop error", ex); }
        }
    }

    private void StartSession(string issueKey, string? repoPath, string? repoName,
                              string? branch, bool manual)
    {
        State.ActiveIssue   = issueKey;
        State.ActiveBranch  = branch;
        State.ActiveRepo    = repoPath;
        State.IsManual      = manual;
        State.IsPaused      = false;
        State.PauseStart    = null;
        State.PauseCount    = 0;
        State.WorkSegments  = new();
        State.SessionLog    = new();
        State.SessionStart  = DateTime.Now;
        State.SegmentStart  = DateTime.Now;
        var repoDisplay = repoName ?? "no repo";
        AddLog($"Started - issue: {issueKey}, repo: {repoDisplay}");
        Notified?.Invoke("▶ Tracking started", $"{issueKey}  ·  {repoDisplay}");
        StateChanged?.Invoke();
    }

    private void PauseSession(string reason)
    {
        if (State.IsPaused) return;
        State.IsPaused   = true;
        State.PauseStart = DateTime.Now;
        State.PauseCount++;
        if (State.SegmentStart.HasValue)
        {
            State.WorkSegments.Add((State.SegmentStart.Value, DateTime.Now));
            State.SegmentStart = null;
        }
        AddLog($"Paused - {reason} ({Session.FormatDuration(GetWorkedSeconds())} worked so far)");
        Notified?.Invoke("⏸ Paused", $"{State.ActiveIssue}  ·  {reason}");
        StateChanged?.Invoke();
    }

    private void ResumeSession(string reason)
    {
        if (!State.IsPaused) return;
        var pausedFor = (int)(DateTime.Now - State.PauseStart!.Value).TotalSeconds;
        State.IsPaused     = false;
        State.PauseStart   = null;
        State.SegmentStart = DateTime.Now;
        AddLog($"Resumed - {reason} (paused for {Session.FormatDuration(pausedFor)})");
        StateChanged?.Invoke();
    }

    private void EndSession(string reason)
    {
        if (!State.IsTracking) return;
        if (State.SegmentStart.HasValue && !State.IsPaused)
        {
            State.WorkSegments.Add((State.SegmentStart.Value, DateTime.Now));
            State.SegmentStart = null;
        }
        var workedSec = GetWorkedSeconds();
        var endTime   = DateTime.Now;
        AddLog($"Work done - {reason} | Total: {Session.FormatDuration(workedSec)}");

        var issue   = State.ActiveIssue!;
        var repo    = State.ActiveRepo ?? "";
        var branch  = State.ActiveBranch ?? "";
        var start   = State.SessionStart!.Value;
        var pauses  = State.PauseCount;
        var comment = string.Join("\n", State.SessionLog);

        _ = Task.Run(async () =>
        {
            var logged = workedSec >= 60 && await _jira.LogWorkAsync(issue, workedSec, comment);
            if (logged)
                Notified?.Invoke("✅ Logged to Jira", $"{issue}  ·  {Session.FormatDuration(workedSec)}");
            var session = new Session
            {
                Date            = start.ToString("yyyy-MM-dd"),
                IssueKey        = issue,
                RepoName        = Path.GetFileName(repo),
                Branch          = branch,
                StartTime       = start,
                EndTime         = endTime,
                DurationSeconds = workedSec,
                DurationHuman   = Session.FormatDuration(workedSec),
                SessionMode     = "manual",
                Pauses          = pauses,
                JiraLogged      = logged
            };
            _db.SaveSession(session);
            AddLog(logged
                ? $"[OK] Logged {Session.FormatDuration(workedSec)} to {issue}"
                : $"[SKIP] {issue} - less than 1 min or API error");
        });

        // Reset state
        State.ActiveIssue  = null; State.ActiveBranch = null; State.ActiveRepo = null;
        State.IsManual     = false; State.IsPaused = false; State.PauseCount = 0;
        State.WorkSegments = new(); State.SessionLog = new(); State.SegmentStart = null;
        StateChanged?.Invoke();
    }

    private void AddLog(string msg)
    {
        State.SessionLog.Add($"{DateTime.Now:HH:mm}  {msg}");
        _log?.Info("Tracker", msg);
    }
}
