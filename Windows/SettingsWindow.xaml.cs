using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using JiraTimeTracker.Services;
using static JiraTimeTracker.Services.StartupService;

namespace JiraTimeTracker.Windows;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _config;
    private readonly JiraService   _jira;
    private readonly LogService?   _log;
    private List<string>           _processes = new();

    public SettingsWindow(ConfigService config, JiraService jira, LogService? log = null)
    {
        _config = config;
        _jira   = jira;
        _log    = log;
        InitializeComponent();
        Load();
    }

    private void Load()
    {
        UrlBox.Text       = _config.Current.Jira.Url;
        EmailBox.Text     = _config.Current.Jira.Email;
        TokenBox.Password = _config.Current.Jira.Token;
        IdleBox.Text      = _config.Current.Settings.IdleTimeoutMinutes.ToString();
        ScanBox.Text      = _config.Current.Settings.ScanIntervalSeconds.ToString();

        _processes              = new List<string>(_config.Current.Settings.WatchedProcesses);
        ProcessList.ItemsSource = _processes.ToList();
        StartupCheck.IsChecked    = IsEnabled();
        AutoUpdateCheck.IsChecked = _config.Current.Settings.AutoUpdate;
        LogToFileCheck.IsChecked = _config.Current.Settings.LogToFile;
        LogPathLabel.Text        = _log?.LogFilePath ?? _config.LogPath;
        LogLevelCombo.SelectedIndex = _config.Current.Settings.LogLevel switch
        {
            "Info"    => 1,
            "Warning" => 2,
            "Error"   => 3,
            _         => 0
        };
    }

    private void AddProcess_Click(object sender, RoutedEventArgs e)
    {
        AddProcessFromBox();
    }

    private void NewProcessBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddProcessFromBox();
    }

    private void AddProcessFromBox()
    {
        var name = NewProcessBox.Text.Trim().TrimEnd('.');
        if (string.IsNullOrEmpty(name) || _processes.Contains(name)) return;
        _processes.Add(name);
        ProcessList.ItemsSource = _processes.ToList();
        NewProcessBox.Clear();
    }

    private void RemoveProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string name)
        {
            _processes.Remove(name);
            ProcessList.ItemsSource = _processes.ToList();
        }
    }

    private async void TestConn_Click(object s, RoutedEventArgs e)
    {
        ConnStatus.Text       = "Testing…";
        ConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(125, 133, 144));
        ApplyToConfig();
        var ok = await _jira.TestConnectionAsync();
        ConnStatus.Text       = ok ? "✔  Connected" : "✘  Failed — check URL, email, and token";
        ConnStatus.Foreground = ok
            ? new SolidColorBrush(Color.FromRgb(63, 185, 80))
            : new SolidColorBrush(Color.FromRgb(248, 81, 73));
    }

    private void Save_Click(object s, RoutedEventArgs e)
    {
        ApplyToConfig();
        _config.Save();
        Close();
    }

    private void ApplyToConfig()
    {
        _config.Current.Jira.Url   = UrlBox.Text.Trim();
        _config.Current.Jira.Email = EmailBox.Text.Trim();
        _config.Current.Jira.Token = TokenBox.Password.Trim();
        if (int.TryParse(IdleBox.Text, out var idle)) _config.Current.Settings.IdleTimeoutMinutes  = idle;
        if (int.TryParse(ScanBox.Text, out var scan)) _config.Current.Settings.ScanIntervalSeconds = scan;
        _config.Current.Settings.WatchedProcesses = _processes.ToList();

        if (StartupCheck.IsChecked == true) Enable(); else Disable();
        _config.Current.Settings.AutoUpdate = AutoUpdateCheck.IsChecked == true;

        var logToFile = LogToFileCheck.IsChecked == true;
        var logLevel  = LogLevelCombo.SelectedIndex switch { 1 => "Info", 2 => "Warning", 3 => "Error", _ => "Debug" };
        _config.Current.Settings.LogToFile = logToFile;
        _config.Current.Settings.LogLevel  = logLevel;

        // Apply to live logger
        if (_log != null)
        {
            _log.WriteToFile = logToFile;
            _log.MinLevel    = logLevel switch
            {
                "Info"    => LogLevel.Info,
                "Warning" => LogLevel.Warning,
                "Error"   => LogLevel.Error,
                _         => LogLevel.Debug
            };
        }
    }

    private void LogPath_Click(object sender, MouseButtonEventArgs e)
    {
        var path = _log?.LogFilePath ?? _config.LogPath;
        if (!string.IsNullOrEmpty(path))
            try { Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
    }

    private void Cancel_Click(object s, RoutedEventArgs e) => Close();
}
