using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using JiraTimeTracker.Services;
using JiraTimeTracker.Tray;
using JiraTimeTracker.Windows;
using Velopack;
using Velopack.Sources;
using static JiraTimeTracker.Services.StartupService;

namespace JiraTimeTracker;

public partial class App : Application
{
    private static Mutex?           _mutex;
    private TrayManager?            _tray;
    private TrackerService?         _tracker;
    private ProcessWatcherService?  _processWatcher;
    private HomeWindow?             _home;
    public  LogService              Log { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "Trakr_SingleInstance", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("Trakr is already running.\nCheck the system tray.",
                "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("App", "Unhandled UI exception", ex.Exception);
            MessageBox.Show(
                $"Unexpected error:\n\n{ex.Exception.Message}\n\nDetails have been written to the log file.",
                "Trakr — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        var config  = new ConfigService();

        // Configure logger
        Log.LogFilePath = config.LogPath;
        Log.WriteToFile = config.Current.Settings.LogToFile;
        Log.MinLevel    = ParseLevel(config.Current.Settings.LogLevel);

        var git     = new GitService(config, Log);
        var jira    = new JiraService(config, Log);
        var db      = new DatabaseService(config);
        var idle    = new IdleService();

        _tracker        = new TrackerService(config, git, jira, db, idle, Log);
        _processWatcher = new ProcessWatcherService(config, Log);
        _home           = new HomeWindow(_tracker, config, db, Log);
        _tray           = new TrayManager(_tracker, config, _home);

        _processWatcher.IdeStarted += ideName => Dispatcher.Invoke(() =>
            _tracker.OnIdeStarted(ideName));
        _processWatcher.IdeClosed  += ideName => Dispatcher.Invoke(() =>
            _tracker.OnIdeClosed(ideName));

        _tracker.SessionProposed += ideName => Dispatcher.Invoke(() =>
            ShowSessionProposal(ideName, config, git, jira));
        _tracker.StopProposed    += ideName => Dispatcher.Invoke(() =>
            ShowStopPrompt(ideName));

        _tracker.Start();
        _processWatcher.Start();
        _tray.Initialize();

        if (!IsEnabled()) Enable();

        Log.Info("App", "Trakr started");
        _home.Show();

        // Check for updates silently in the background (only if the user opted in)
        if (config.Current.Settings.AutoUpdate)
            _ = Task.Run(() => CheckForUpdatesAsync());
    }

    private void ShowSessionProposal(string ideName, ConfigService config,
                                      GitService git, JiraService jira)
    {
        foreach (Window w in Current.Windows)
            if (w is SessionProposalWindow) return;

        var win = new SessionProposalWindow(_tracker!, jira, git, ideName);
        win.Show();
    }

    private void ShowStopPrompt(string ideName)
    {
        foreach (Window w in Current.Windows)
            if (w is StopPromptWindow) return;

        var win = new StopPromptWindow(_tracker!, ideName);
        win.Show();
    }

    // ── Auto-update via Velopack + GitHub Releases ────────────────────────────
    // Replace the URL below with your actual GitHub repo URL before publishing.
    private const string GitHubRepo = "https://github.com/OWNER/Trakr";

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(GitHubRepo, null, false));
            if (!mgr.IsInstalled) return;   // running from source / not installed yet

            var info = await mgr.CheckForUpdatesAsync();
            if (info == null) return;       // already up to date

            Log.Info("App", $"Update available: {info.TargetFullRelease.Version} — downloading…");
            await mgr.DownloadUpdatesAsync(info);

            // Notify the user and let them decide when to restart
            var result = Dispatcher.Invoke(() =>
                MessageBox.Show(
                    $"A new version ({info.TargetFullRelease.Version}) has been downloaded.\n\nRestart now to apply the update?",
                    "Update Ready", MessageBoxButton.YesNo, MessageBoxImage.Information));

            if (result == MessageBoxResult.Yes)
                mgr.ApplyUpdatesAndRestart(info);
        }
        catch (Exception ex)
        {
            Log.Warning("App", $"Update check failed: {ex.Message}");
        }
    }

    public void OpenLogViewer()
    {
        foreach (Window w in Current.Windows)
            if (w is LogViewerWindow lv) { lv.Activate(); return; }

        var win = new LogViewerWindow(Log);
        win.Show();
    }

    private static LogLevel ParseLevel(string s) => s switch
    {
        "Info"    => LogLevel.Info,
        "Warning" => LogLevel.Warning,
        "Error"   => LogLevel.Error,
        _         => LogLevel.Debug
    };

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("App", "Trakr shutting down");
        _tracker?.Stop();
        _processWatcher?.Stop();
        _tray?.Dispose();
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
