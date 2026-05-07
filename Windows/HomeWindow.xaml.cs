using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using JiraTimeTracker.Models;
using JiraTimeTracker.Services;
using JiraTimeTracker.Windows;

namespace JiraTimeTracker.Windows;

public partial class HomeWindow : Window
{
    private readonly TrackerService  _tracker;
    private readonly ConfigService   _config;
    private readonly DatabaseService _db;
    private readonly LogService      _log;
    private readonly JiraService     _jira;
    private readonly DispatcherTimer _liveTimer;
    private Storyboard? _pulseAnim;

    private enum Tab { Sessions, Issues, LiveLog }
    private Tab _activeTab = Tab.Sessions;

    private List<JiraIssue> _allIssues   = new();
    private bool            _issuesLoaded = false;

    public HomeWindow(TrackerService tracker, ConfigService config, DatabaseService db, LogService log)
    {
        _tracker = tracker;
        _config  = config;
        _db      = db;
        _log     = log;
        _jira    = new JiraService(config, log);

        InitializeComponent();

        // Restore persisted toggle state
        GroupSubtasksCheck.IsChecked = _config.Current.Settings.GroupSubtasks;

        _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveTimer.Tick += (_, _) => RefreshTimer();
        _liveTimer.Start();

        _tracker.StateChanged += () => Dispatcher.Invoke(OnStateChanged);
        _tracker.Notified     += (title, msg) => Dispatcher.Invoke(() => ShowBalloon(title, msg));

        InitPulseAnimation();
        ApplyTabStyle();
        Refresh();
        LoadRecent();
    }

    // ── Pulse animation ───────────────────────────────────────────
    private void InitPulseAnimation()
    {
        _pulseAnim = new Storyboard();
        var anim = new DoubleAnimation(1, 0.2, new Duration(TimeSpan.FromSeconds(0.9)))
        {
            AutoReverse    = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };
        Storyboard.SetTarget(anim, StatusDotOuter);
        Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.OpacityProperty));
        _pulseAnim.Children.Add(anim);
    }

    // ── Tab switching ─────────────────────────────────────────────
    private void TabSessions_Click(object sender, MouseButtonEventArgs e) => SwitchTab(Tab.Sessions);
    private void TabIssues_Click  (object sender, MouseButtonEventArgs e) => SwitchTab(Tab.Issues);
    private void TabLog_Click     (object sender, MouseButtonEventArgs e) => SwitchTab(Tab.LiveLog);

    private void SwitchTab(Tab tab)
    {
        _activeTab = tab;
        ApplyTabStyle();

        SessionsPanel.Visibility = tab == Tab.Sessions ? Visibility.Visible : Visibility.Collapsed;
        IssuesPanel  .Visibility = tab == Tab.Issues   ? Visibility.Visible : Visibility.Collapsed;
        LiveLogPanel .Visibility = tab == Tab.LiveLog  ? Visibility.Visible : Visibility.Collapsed;

        if (tab == Tab.Issues && !_issuesLoaded)
            _ = LoadIssuesAsync();

        if (tab == Tab.LiveLog)
        {
            LiveLogList.ItemsSource = _tracker.State.SessionLog;
            LiveLogScroller.ScrollToBottom();
        }
    }

    private void ApplyTabStyle()
    {
        var active   = new SolidColorBrush(Color.FromRgb(0x2f,0x81,0xf7));
        var inactive = new SolidColorBrush(Color.FromRgb(0x8b,0x94,0x9e));

        TabSessions.Foreground = _activeTab == Tab.Sessions ? active : inactive;
        TabIssues  .Foreground = _activeTab == Tab.Issues   ? active : inactive;
        TabLog     .Foreground = _activeTab == Tab.LiveLog  ? active : inactive;

        IssueTabActions.Visibility = _activeTab == Tab.Issues ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Issues loading ────────────────────────────────────────────
    private async Task LoadIssuesAsync(bool forceRefresh = false)
    {
        IssueStatusText.Text       = "Loading your Jira issues…";
        IssueStatusText.Visibility = Visibility.Visible;
        IssueEmptyText .Visibility = Visibility.Collapsed;
        IssueList      .ItemsSource = null;
        IssueStartBtn  .IsEnabled  = false;
        _allIssues     = new List<JiraIssue>();

        _allIssues = await Task.Run(() => _jira.GetMyIssuesAsync(forceRefresh));
        _issuesLoaded = true;

        IssueStatusText.Visibility = Visibility.Collapsed;

        if (_allIssues.Count == 0)
        {
            IssueEmptyText.Text       = "No open issues found. Check your Jira connection in Settings.";
            IssueEmptyText.Visibility = Visibility.Visible;
            return;
        }

        ApplyIssueSource(_allIssues);
    }

    // Apply current grouping preference to the list
    private void ApplyIssueSource(List<JiraIssue> issues)
    {
        IssueList.ItemsSource = ApplyGrouping(issues);
    }

    private List<JiraIssue> ApplyGrouping(List<JiraIssue> issues)
    {
        if (GroupSubtasksCheck.IsChecked == true)
            return GroupIssues(issues);

        // Flat mode — clear all grouping flags
        foreach (var i in issues) { i.HasSubtasks = false; i.IsGrouped = false; }
        return issues;
    }

    private void GroupSubtasks_Changed(object sender, RoutedEventArgs e)
    {
        _config.Current.Settings.GroupSubtasks = GroupSubtasksCheck.IsChecked == true;
        _config.Save();
        if (_allIssues.Count > 0) ApplyIssueSource(_allIssues);
    }

    // Chevron click — toggle parent expand/collapse
    private void IssueExpandToggle_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // prevent ListBox selection
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string key)
        {
            var issue = _allIssues.FirstOrDefault(i => i.Key == key);
            if (issue != null)
            {
                issue.IsExpanded = !issue.IsExpanded;
                ApplyIssueSource(_allIssues);
            }
        }
    }

    // Group issues: parents first, subtasks immediately after their parent (collapsed if !IsExpanded)
    private static List<JiraIssue> GroupIssues(List<JiraIssue> issues)
    {
        var parents  = issues.Where(i => !i.IsSubTask).ToList();
        var subtasks = issues.Where(i =>  i.IsSubTask).ToList();
        var result   = new List<JiraIssue>();
        foreach (var parent in parents)
        {
            var children = subtasks.Where(s => s.ParentKey == parent.Key).ToList();
            parent.HasSubtasks = children.Count > 0;
            parent.IsGrouped   = false; // parents are never indented
            result.Add(parent);
            if (parent.IsExpanded)
            {
                foreach (var c in children) c.IsGrouped = true;
                result.AddRange(children);
            }
        }
        // Orphan subtasks — parent not in list, show flat (no indent, no parent label)
        var orphans = subtasks.Where(s => !parents.Any(p => p.Key == s.ParentKey));
        foreach (var o in orphans) { o.HasSubtasks = false; o.IsGrouped = false; }
        result.AddRange(orphans);
        return result;
    }

    private void IssueSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var q = IssueSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(q))
        {
            ApplyIssueSource(_allIssues);
            IssueEmptyText.Visibility = _allIssues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        var local = _allIssues.FindAll(i =>
            i.Key    .Contains(q, StringComparison.OrdinalIgnoreCase) ||
            i.Summary.Contains(q, StringComparison.OrdinalIgnoreCase));

        IssueList.ItemsSource     = local.Count > 0 ? ApplyGrouping(local) : null;
        IssueEmptyText.Visibility = local.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (local.Count == 0) IssueEmptyText.Text = $"No issues matching \"{q}\".";
    }

    private void IssueList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => IssueStartBtn.IsEnabled = IssueList.SelectedItem != null;

    private async void IssueRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        _issuesLoaded = false;
        IssueSearchBox.Clear();
        await LoadIssuesAsync(forceRefresh: true);
    }

    private async void IssueStart_Click(object sender, RoutedEventArgs e)
    {
        if (IssueList.SelectedItem is not JiraIssue issue) return;

        IssueStartBtn.IsEnabled = false;
        IssueStartBtn.Content   = "Starting...";

        if (SetInProgressCheck.IsChecked == true)
            await Task.Run(() => _jira.TransitionToInProgressAsync(issue.Key));

        _tracker.StartFromIssue(issue, null, null);
        SwitchTab(Tab.Sessions);
        Refresh();
    }

    // ── State changed ─────────────────────────────────────────────
    private void OnStateChanged()
    {
        Refresh();
        LoadRecent();
    }

    // ── Full UI refresh ───────────────────────────────────────────
    private void Refresh()
    {
        var state = _tracker.State;

        SetupBanner.Visibility = !_config.IsConfigured() ? Visibility.Visible : Visibility.Collapsed;

        var todaySessions = _db.GetSessions(DateTime.Today, DateTime.Today);
        var todaySec      = todaySessions.Sum(s => s.DurationSeconds);
        var todayIssues   = todaySessions.Select(s => s.IssueKey).Distinct().Count();
        TodayLabel.Text   = todaySec > 0
            ? $"Today: {Session.FormatDuration(todaySec)}  ·  {todayIssues} issue{(todayIssues == 1 ? "" : "s")}"
            : "No sessions today";

        // Show "LIVE LOG" tab only while tracking
        TabLog.Visibility = state.IsTracking ? Visibility.Visible : Visibility.Collapsed;

        if (state.IsTracking)
        {
            StatusLabel.Text   = "TRACKING";
            IssueLabel.Text    = state.ActiveIssue ?? "—";
            BranchLabel.Text   = state.ActiveBranch ?? "";
            RepoLabel.Text     = state.ActiveRepo != null
                ? "📁  " + System.IO.Path.GetFileName(state.ActiveRepo)
                : "";
            StopBtn.IsEnabled  = true;

            if (state.IsPaused)
            {
                PauseBanner.Visibility = Visibility.Visible;
                PauseLabel.Text        = "⏸  Paused — idle or PC locked";
                var yellow = new SolidColorBrush(Color.FromRgb(210,153,34));
                StatusDot.Fill = StatusDotOuter.Fill = yellow;
                TimerLabel.Foreground = StatusLabel.Foreground = yellow;
                _pulseAnim?.Stop();
            }
            else
            {
                PauseBanner.Visibility = Visibility.Collapsed;
                var green = new SolidColorBrush(Color.FromRgb(63,185,80));
                StatusDot.Fill = StatusDotOuter.Fill = green;
                TimerLabel.Foreground  = new SolidColorBrush(Color.FromRgb(47,129,247));
                StatusLabel.Foreground = green;
                _pulseAnim?.Begin();
            }

            if (_activeTab == Tab.LiveLog)
                LiveLogList.ItemsSource = new List<string>(state.SessionLog);
        }
        else
        {
            StatusLabel.Text       = "NOT TRACKING";
            StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(139,148,158));
            IssueLabel.Text        = "—";
            BranchLabel.Text       = "Open an IDE or click Start to begin";
            RepoLabel.Text         = "";
            TimerLabel.Text        = "";
            TimerLabel.Foreground  = new SolidColorBrush(Color.FromRgb(139,148,158));
            var gray = new SolidColorBrush(Color.FromRgb(139,148,158));
            StatusDot.Fill = StatusDotOuter.Fill = gray;
            StopBtn.IsEnabled      = false;
            PauseBanner.Visibility = Visibility.Collapsed;
            _pulseAnim?.Stop();

            // If live log was active, fall back to sessions
            if (_activeTab == Tab.LiveLog) SwitchTab(Tab.Sessions);
        }
    }

    private void RefreshTimer()
    {
        if (!_tracker.State.IsTracking || _tracker.State.IsPaused) return;
        TimerLabel.Text = Session.FormatDuration(_tracker.GetWorkedSeconds());
    }

    private void LoadRecent()
    {
        var sessions = _db.GetAllSessions()
            .OrderByDescending(s => s.StartTime)
            .Take(7)
            .Select(s => new SessionRow
            {
                IssueKey      = s.IssueKey,
                RepoName      = s.RepoName,
                Date          = s.StartTime.ToString("MMM dd"),
                DurationHuman = s.DurationHuman,
                JiraDisplay   = s.JiraLogged ? "✔" : "—"
            }).ToList();

        RecentList.ItemsSource = sessions.Count > 0 ? sessions : null;
    }

    // ── Event handlers ────────────────────────────────────────────
    private void RecentList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentList.SelectedItem is not SessionRow row) return;
        var url = _config.Current.Jira.Url.TrimEnd('/') + "/browse/" + row.IssueKey;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private void ShowBalloon(string title, string msg) { }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        var win = new SessionProposalWindow(_tracker, new JiraService(_config, _log), new GitService(_config, _log));
        win.Owner = this;
        win.ShowDialog();
        Refresh();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _tracker.ManualStop();
        LoadRecent();
        Refresh();
    }

    private void Report_Click(object sender, RoutedEventArgs e)
    {
        var win = new SprintReportWindow(_db);
        win.Owner = this;
        win.Show();
    }

    private void Repos_Click(object sender, RoutedEventArgs e)
    {
        var win = new ManageReposWindow(_config);
        win.Owner = this;
        win.ShowDialog();
    }

    private void Logs_Click(object sender, RoutedEventArgs e)
        => ((App)System.Windows.Application.Current).OpenLogViewer();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_config, new JiraService(_config), _log);
        win.Owner = this;
        win.ShowDialog();
        Refresh();
    }

    private void SetupLink_Click(object sender, MouseButtonEventArgs e)
    {
        var win = new SettingsWindow(_config, new JiraService(_config), _log);
        win.Owner = this;
        win.ShowDialog();
        Refresh();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Hide();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    public new void Show()
    {
        Refresh();
        LoadRecent();
        base.Show();
        Activate();
        Focus();
    }
}
