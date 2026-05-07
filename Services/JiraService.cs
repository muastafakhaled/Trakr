using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JiraTimeTracker.Models;

namespace JiraTimeTracker.Services;

public class JiraService
{
    private readonly ConfigService _config;
    private readonly LogService?   _log;
    private readonly HttpClient    _http = new();

    // Issue cache
    private List<JiraIssue> _cachedIssues    = new();
    private DateTime        _cacheExpiry     = DateTime.MinValue;
    private const int       CacheMinutes     = 5;

    public JiraService(ConfigService config, LogService? log = null)
    { _config = config; _log = log; }

    // ── Auth helper ──────────────────────────────────────────────
    private void SetAuth()
    {
        var cfg   = _config.Current.Jira;
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{cfg.Email}:{cfg.Token}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ── Fetch issues assigned to current user ─────────────────────
    public async Task<List<JiraIssue>> GetMyIssuesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && DateTime.Now < _cacheExpiry && _cachedIssues.Count > 0)
            return _cachedIssues;

        try
        {
            SetAuth();
            var cfg = _config.Current.Jira;

            // Get accountId first — more reliable than currentUser() with Basic Auth tokens
            var accountId = await GetMyAccountIdAsync();
            string jqlRaw;
            if (!string.IsNullOrEmpty(accountId))
                jqlRaw = $"assignee = \"{accountId}\" AND statusCategory != Done ORDER BY updated DESC";
            else
                jqlRaw = $"assignee = \"{cfg.Email}\" AND statusCategory != Done ORDER BY updated DESC";

            var url  = $"{cfg.Url.TrimEnd('/')}/rest/api/3/search/jql";
            var body = JsonSerializer.Serialize(new
            {
                jql        = jqlRaw,
                maxResults = 50,
                fields     = new[] { "summary", "status", "issuetype", "project", "parent" }
            });

            _log?.Debug("Jira", $"Fetching issues — JQL: {jqlRaw}");
            var response = await _http.PostAsync(url,
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();
                _log?.Error("Jira", $"GetMyIssues failed {(int)response.StatusCode} {response.ReasonPhrase} — {errBody}");
                return _cachedIssues;
            }

            var json = await response.Content.ReadAsStringAsync();
            _log?.Debug("Jira", $"Response: {json[..Math.Min(300, json.Length)]}…");
            using var doc = JsonDocument.Parse(json);

            var issues = new List<JiraIssue>();
            foreach (var item in doc.RootElement.GetProperty("issues").EnumerateArray())
            {
                try
                {
                    var fields = item.GetProperty("fields");
                    issues.Add(new JiraIssue
                    {
                        Key       = GetString(item,   "key"),
                        Summary   = GetString(fields, "summary"),
                        Project   = GetNestedString(fields, "project",   "name"),
                        Status    = GetNestedString(fields, "status",    "name"),
                        IssueType = GetNestedString(fields, "issuetype", "name"),
                        ParentKey = GetNestedString(fields, "parent",    "key")
                    });
                }
                catch (Exception ex) { _log?.Warning("Jira", $"Skipped malformed issue: {ex.Message}"); }
            }

            _log?.Info("Jira", $"Loaded {issues.Count} issues");
            _cachedIssues = issues;
            _cacheExpiry  = DateTime.Now.AddMinutes(CacheMinutes);
            return _cachedIssues;
        }
        catch (Exception ex)
        {
            _log?.Error("Jira", "GetMyIssuesAsync exception", ex);
            return _cachedIssues;
        }
    }

    // ── Log work ─────────────────────────────────────────────────
    public async Task<bool> LogWorkAsync(string issueKey, int seconds, string comment)
    {
        if (seconds < 60) return false;

        SetAuth();
        var cfg  = _config.Current.Jira;
        var body = JsonSerializer.Serialize(new
        {
            timeSpentSeconds = seconds,
            comment = new
            {
                type    = "doc",
                version = 1,
                content = new[]
                {
                    new
                    {
                        type    = "paragraph",
                        content = new[] { new { type = "text", text = comment } }
                    }
                }
            }
        });

        try
        {
            var url      = $"{cfg.Url.TrimEnd('/')}/rest/api/3/issue/{issueKey}/worklog";
            var response = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (response.IsSuccessStatusCode)
                _log?.Info("Jira", $"Logged {Session.FormatDuration(seconds)} to {issueKey}");
            else
            {
                var rb = await response.Content.ReadAsStringAsync();
                _log?.Error("Jira", $"LogWork failed {(int)response.StatusCode} for {issueKey} — {rb}");
            }
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { _log?.Error("Jira", "LogWorkAsync exception", ex); return false; }
    }

    // ── Get current user's accountId ─────────────────────────────
    private string? _cachedAccountId;
    public async Task<string?> GetMyAccountIdAsync()
    {
        if (_cachedAccountId != null) return _cachedAccountId;
        try
        {
            SetAuth();
            var cfg      = _config.Current.Jira;
            var url      = $"{cfg.Url.TrimEnd('/')}/rest/api/3/myself";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            _cachedAccountId = doc.RootElement.GetProperty("accountId").GetString();
            return _cachedAccountId;
        }
        catch { return null; }
    }

    // ── Search issues by text or key ──────────────────────────────
    public async Task<List<JiraIssue>> SearchIssuesAsync(string query)
    {
        try
        {
            SetAuth();
            var cfg    = _config.Current.Jira;
            var jqlRaw = $"(key = \"{query}\" OR text ~ \"{query}\") AND statusCategory != Done ORDER BY updated DESC";
            var url    = $"{cfg.Url.TrimEnd('/')}/rest/api/3/search/jql";
            var body   = JsonSerializer.Serialize(new
            {
                jql        = jqlRaw,
                maxResults = 20,
                fields     = new[] { "summary", "status", "issuetype", "project" }
            });

            var response = await _http.PostAsync(url,
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode) return new();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var issues = new List<JiraIssue>();
            foreach (var item in doc.RootElement.GetProperty("issues").EnumerateArray())
            {
                try
                {
                    var fields = item.GetProperty("fields");
                    issues.Add(new JiraIssue
                    {
                        Key       = GetString(item,   "key"),
                        Summary   = GetString(fields, "summary"),
                        Project   = GetNestedString(fields, "project",   "name"),
                        Status    = GetNestedString(fields, "status",    "name"),
                        IssueType = GetNestedString(fields, "issuetype", "name")
                    });
                }
                catch { }
            }
            return issues;
        }
        catch { return new(); }
    }

    // ── JSON helpers (null-safe) ──────────────────────────────────
    private static string GetString(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString() ?? "";
        return "";
    }

    private static string GetNestedString(JsonElement el, string prop, string nestedProp)
    {
        if (el.TryGetProperty(prop, out var outer) && outer.ValueKind == JsonValueKind.Object)
            return GetString(outer, nestedProp);
        return "";
    }

    // ── Transition issue to "In Progress" ────────────────────────
    public async Task<bool> TransitionToInProgressAsync(string issueKey)
    {
        try
        {
            SetAuth();
            var cfg = _config.Current.Jira;

            // 1. Get available transitions
            var transUrl  = $"{cfg.Url.TrimEnd('/')}/rest/api/3/issue/{issueKey}/transitions";
            var transResp = await _http.GetAsync(transUrl);
            if (!transResp.IsSuccessStatusCode) return false;

            var transJson = await transResp.Content.ReadAsStringAsync();
            using var transDoc = JsonDocument.Parse(transJson);

            string? transitionId = null;
            foreach (var t in transDoc.RootElement.GetProperty("transitions").EnumerateArray())
            {
                var toName = GetNestedString(t, "to", "name").ToLower();
                if (toName.Contains("progress"))
                {
                    transitionId = GetString(t, "id");
                    break;
                }
            }

            if (transitionId == null)
            {
                _log?.Warning("Jira", $"No 'In Progress' transition found for {issueKey}");
                return false;
            }

            // 2. Apply the transition
            var body = JsonSerializer.Serialize(new
            {
                transition = new { id = transitionId }
            });
            var resp = await _http.PostAsync(transUrl,
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (resp.IsSuccessStatusCode)
                _log?.Info("Jira", $"Transitioned {issueKey} to In Progress");
            else
                _log?.Warning("Jira", $"Transition failed for {issueKey}: {(int)resp.StatusCode}");

            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _log?.Error("Jira", $"TransitionToInProgressAsync exception", ex);
            return false;
        }
    }

    // ── Test connection ───────────────────────────────────────────
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            SetAuth();
            var cfg      = _config.Current.Jira;
            var url      = $"{cfg.Url.TrimEnd('/')}/rest/api/3/myself";
            var response = await _http.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
