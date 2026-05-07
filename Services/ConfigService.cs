using System;
using System.IO;
using System.Text.Json;
using JiraTimeTracker.Models;

namespace JiraTimeTracker.Services;

public class ConfigService
{
    public string AppDataDir   { get; }
    public string ConfigPath   { get; }
    public string DbPath       { get; }
    public string LogPath      { get; }
    public string TriggerPath  { get; }

    private AppConfig _config = new();

    public ConfigService()
    {
        AppDataDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Trakr");
        Directory.CreateDirectory(AppDataDir);
        ConfigPath  = Path.Combine(AppDataDir, "config.json");
        DbPath      = Path.Combine(AppDataDir, "sessions.csv");
        LogPath     = Path.Combine(AppDataDir, "tracker.log");
        TriggerPath = Path.Combine(AppDataDir, "manual_trigger.json");
        Load();
    }

    public AppConfig Current => _config;

    public void Load()
    {
        if (!File.Exists(ConfigPath)) { Save(); return; }
        try
        {
            var json = File.ReadAllText(ConfigPath);
            _config  = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch { _config = new AppConfig(); }
    }

    public void Save()
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_config, opts));
    }

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_config.Jira.Url)   &&
        !string.IsNullOrWhiteSpace(_config.Jira.Email) &&
        !string.IsNullOrWhiteSpace(_config.Jira.Token) &&
        _config.Repos.Count > 0;
}