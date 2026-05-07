using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JiraTimeTracker.Models;

public class AppConfig
{
    [JsonPropertyName("jira")]
    public JiraConfig Jira { get; set; } = new();

    [JsonPropertyName("repos")]
    public List<string> Repos { get; set; } = new();

    [JsonPropertyName("settings")]
    public TrackerSettings Settings { get; set; } = new();
}

public class JiraConfig
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";
}

public class TrackerSettings
{
    [JsonPropertyName("idle_timeout_minutes")]
    public int IdleTimeoutMinutes { get; set; } = 10;

    [JsonPropertyName("scan_interval_seconds")]
    public int ScanIntervalSeconds { get; set; } = 30;

    [JsonPropertyName("activity_window_minutes")]
    public int ActivityWindowMinutes { get; set; } = 5;

    [JsonPropertyName("log_level")]
    public string LogLevel { get; set; } = "Debug";   // Debug | Info | Warning | Error

    [JsonPropertyName("log_to_file")]
    public bool LogToFile { get; set; } = true;

    [JsonPropertyName("auto_update")]
    public bool AutoUpdate { get; set; } = false;

    [JsonPropertyName("watched_processes")]
    public List<string> WatchedProcesses { get; set; } = new()
    {
        "Code", "devenv", "rider64", "rider", "idea64", "webstorm64", "phpstorm64", "clion64"
    };
}