using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using JiraTimeTracker.Models;
using JiraTimeTracker.Services;

namespace JiraTimeTracker.Windows;

public partial class SessionProposalWindow : Window
{
    private readonly TrackerService      _tracker;
    private readonly JiraService         _jira;
    private readonly GitService          _git;
    private List<JiraIssue>              _allIssues = new();
    private CancellationTokenSource      _searchCts = new();

    public SessionProposalWindow(TrackerService tracker, JiraService jira,
                                  GitService git, string ideName = "")
    {
        _tracker = tracker;
        _jira    = jira;
        _git     = git;
        InitializeComponent();
        PositionBottomRight();

        if (!string.IsNullOrEmpty(ideName))
            IdeLabel.Text = $"💻  {ideName} detected";

        SearchBox.IsEnabled = false;
        _ = LoadIssuesAsync();
    }

    private void PositionBottomRight()
    {
        var screen = SystemParameters.WorkArea;
        Left = screen.Right  - Width  - 16;
        Top  = screen.Bottom - Height - 16;
    }

    private async Task LoadIssuesAsync()
    {
        StatusText.Visibility   = Visibility.Visible;
        StatusText.Text         = "Loading your issues…";
        IssueList.ItemsSource   = null;
        StartBtn.IsEnabled      = false;
        NoIssuesText.Visibility = Visibility.Collapsed;
        SearchBox.IsEnabled     = false;

        _allIssues = await Task.Run(() => _jira.GetMyIssuesAsync());

        SearchBox.IsEnabled   = true;
        StatusText.Visibility = Visibility.Collapsed;

        if (_allIssues.Count == 0)
        {
            NoIssuesText.Visibility = Visibility.Visible;
            NoIssuesText.Text = "No open issues found assigned to you. Use the search box above.";
            return;
        }

        IssueList.ItemsSource = _allIssues;

        // Pre-select if current git branch matches an issue
        try
        {
            var jiraRepos = await Task.Run(() => _git.GetAllJiraRepos());
            if (jiraRepos.Count > 0)
            {
                var hint = jiraRepos[0].IssueKey;
                foreach (var item in _allIssues)
                {
                    if (item.Key.Equals(hint, StringComparison.OrdinalIgnoreCase))
                    {
                        IssueList.SelectedItem = item;
                        IssueList.ScrollIntoView(item);
                        break;
                    }
                }
            }
        }
        catch { }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();

        // Show local filter first (instant)
        if (string.IsNullOrEmpty(query))
        {
            IssueList.ItemsSource   = _allIssues;
            NoIssuesText.Visibility = _allIssues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        // Local filter on cached issues
        var local = _allIssues.FindAll(i =>
            i.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            i.Summary.Contains(query, StringComparison.OrdinalIgnoreCase));

        if (local.Count > 0)
        {
            IssueList.ItemsSource   = local;
            NoIssuesText.Visibility = Visibility.Collapsed;
            return;
        }

        // Nothing locally — search Jira API with debounce
        _searchCts.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            StatusText.Text       = "Searching Jira…";
            StatusText.Visibility = Visibility.Visible;

            await Task.Delay(400, token); // debounce
            if (token.IsCancellationRequested) return;

            var results = await _jira.SearchIssuesAsync(query);

            if (token.IsCancellationRequested) return;

            StatusText.Visibility = Visibility.Collapsed;

            if (results.Count == 0)
            {
                IssueList.ItemsSource   = null;
                NoIssuesText.Text       = $"No issues found for \"{query}\".";
                NoIssuesText.Visibility = Visibility.Visible;
            }
            else
            {
                IssueList.ItemsSource   = results;
                NoIssuesText.Visibility = Visibility.Collapsed;
            }
        }
        catch (TaskCanceledException) { }
    }

    private void IssueList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        StartBtn.IsEnabled = IssueList.SelectedItem != null;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (IssueList.SelectedItem is not JiraIssue issue) return;

        string? repoPath = null, branch = null;
        try
        {
            var repos = _git.GetAllJiraRepos();
            var match = repos.Find(r => r.IssueKey == issue.Key);
            if (match != null) { repoPath = match.RepoPath; branch = match.Branch; }
        }
        catch { }

        _tracker.StartFromIssue(issue, repoPath, branch);
        Close();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        await _jira.GetMyIssuesAsync(forceRefresh: true);
        await LoadIssuesAsync();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { }
}
