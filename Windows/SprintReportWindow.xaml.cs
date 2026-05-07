using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using JiraTimeTracker.Models;
using JiraTimeTracker.Services;

namespace JiraTimeTracker.Windows;

public class SessionRow
{
    public string Date          { get; set; } = "";
    public string IssueKey      { get; set; } = "";
    public string RepoName      { get; set; } = "";
    public string Branch        { get; set; } = "";
    public string DurationHuman { get; set; } = "";
    public string StartDisplay  { get; set; } = "";
    public string EndDisplay    { get; set; } = "";
    public int    Pauses        { get; set; }
    public string SessionMode   { get; set; } = "";
    public string JiraDisplay   { get; set; } = "";
}

public partial class SprintReportWindow : Window
{
    private readonly DatabaseService _db;

    public SprintReportWindow(DatabaseService db)
    {
        _db = db;
        InitializeComponent();
        Load();
    }

    private void Load()
    {
        var (from, to) = GetRange();
        var sessions   = _db.GetSessions(from, to);
        var rows = sessions.OrderByDescending(s => s.StartTime).Select(s => new SessionRow
        {
            Date          = s.Date,
            IssueKey      = s.IssueKey,
            RepoName      = s.RepoName,
            Branch        = s.Branch,
            DurationHuman = s.DurationHuman,
            StartDisplay  = s.StartTime.ToString("HH:mm"),
            EndDisplay    = s.EndTime.ToString("HH:mm"),
            Pauses        = s.Pauses,
            SessionMode   = s.SessionMode,
            JiraDisplay   = s.JiraLogged ? "✔" : "—"
        }).ToList();

        SessionGrid.ItemsSource = rows;

        var totalSec  = sessions.Sum(s => s.DurationSeconds);
        var issues    = sessions.Select(s => s.IssueKey).Distinct().Count();
        var logged    = sessions.Count(s => s.JiraLogged);

        TotalTimeText.Text  = Session.FormatDuration(totalSec);
        IssuesText.Text     = issues.ToString();
        SessionsText.Text   = sessions.Count.ToString();
        LoggedText.Text     = logged.ToString();
    }

    private (DateTime from, DateTime to) GetRange()
    {
        var now = DateTime.Now;
        return (RangeCombo.SelectedIndex) switch
        {
            0 => (now.AddDays(-(int)now.DayOfWeek).Date, now),
            1 => (now.AddDays(-14).Date, now),
            2 => (new DateTime(now.Year, now.Month, 1), now),
            3 => (DateTime.MinValue, now),
            4 => (FromDate.SelectedDate ?? now.AddDays(-7), (ToDate.SelectedDate ?? now).AddDays(1)),
            _ => (now.AddDays(-7), now)
        };
    }

    private void RangeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Guard: fires during XAML init before all controls exist
        if (RangeCombo == null || SessionGrid == null) return;
        var custom = RangeCombo.SelectedIndex == 4;
        var vis    = custom ? Visibility.Visible : Visibility.Collapsed;
        if (FromLabel != null) FromLabel.Visibility = vis;
        if (FromDate  != null) FromDate.Visibility  = vis;
        if (ToLabel   != null) ToLabel.Visibility   = vis;
        if (ToDate    != null) ToDate.Visibility    = vis;
        Load();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Load();

    private void OpenCsv_Click(object sender, RoutedEventArgs e)
    {
        var db = new DatabaseService(new ConfigService());
        Process.Start(new ProcessStartInfo { FileName = new ConfigService().DbPath, UseShellExecute = true });
    }
}