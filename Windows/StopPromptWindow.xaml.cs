using System.Windows;
using JiraTimeTracker.Models;
using JiraTimeTracker.Services;

namespace JiraTimeTracker.Windows;

public partial class StopPromptWindow : Window
{
    private readonly TrackerService _tracker;

    public StopPromptWindow(TrackerService tracker, string ideName)
    {
        _tracker = tracker;
        InitializeComponent();
        PositionBottomRight();

        IdeLabel.Text    = $"💻  {ideName} closed";
        var issue        = tracker.State.ActiveIssue ?? "your issue";
        var time         = Session.FormatDuration(tracker.GetWorkedSeconds());
        QuestionLabel.Text = $"Still working on {issue}?";
        DetailLabel.Text   = $"Session time so far: {time}";
    }

    private void PositionBottomRight()
    {
        var screen = SystemParameters.WorkArea;
        Left = screen.Right  - Width  - 16;
        Top  = screen.Bottom - Height - 16;
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _tracker.ManualStop();
        Close();
    }

    private void Keep_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
