using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JiraTimeTracker.Models;

namespace JiraTimeTracker.Services;

public class ProcessWatcherService
{
    private readonly ConfigService _config;
    private HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource _cts = new();

    // Friendly display names for known IDEs
    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rider64"]      = "JetBrains Rider",
        ["rider"]        = "JetBrains Rider",
        ["Code"]         = "Visual Studio Code",
        ["devenv"]       = "Visual Studio",
        ["idea64"]       = "IntelliJ IDEA",
        ["idea"]         = "IntelliJ IDEA",
        ["webstorm64"]   = "WebStorm",
        ["webstorm"]     = "WebStorm",
        ["phpstorm64"]   = "PhpStorm",
        ["clion64"]      = "CLion",
        ["pycharm64"]    = "PyCharm",
        ["goland64"]     = "GoLand",
    };

    private readonly LogService? _log;

    public event Action<string>? IdeStarted;
    public event Action<string>? IdeClosed;

    public ProcessWatcherService(ConfigService config, LogService? log = null)
    {
        _config = config; _log = log;
    }

    public static string FriendlyName(string exeName)
    {
        return DisplayNames.TryGetValue(exeName, out var name) ? name : exeName;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        // Seed with currently running processes so we don't fire on startup
        _running = GetRunningWatched();
        Task.Run(() => WatchLoop(_cts.Token));
    }

    public void Stop() => _cts.Cancel();

    private async Task WatchLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, ct);
                var current = GetRunningWatched();

                var started = current.Except(_running).ToList();
                var stopped = _running.Except(current).ToList();

                foreach (var exe in started)
                {
                    var name = FriendlyName(exe);
                    _log?.Info("Watcher", $"{name} started ({exe}.exe)");
                    IdeStarted?.Invoke(name);
                }

                foreach (var exe in stopped)
                {
                    var name = FriendlyName(exe);
                    _log?.Info("Watcher", $"{name} closed ({exe}.exe)");
                    IdeClosed?.Invoke(name);
                }

                _running = current;
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private HashSet<string> GetRunningWatched()
    {
        var watched = _config.Current.Settings.WatchedProcesses;
        var result  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in watched)
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0)
                    result.Add(name);
            }
            catch { }
        }
        return result;
    }
}
