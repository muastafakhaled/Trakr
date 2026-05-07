using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JiraTimeTracker.Models;

namespace JiraTimeTracker.Services;

public class GitService
{
    private readonly ConfigService _config;
    private readonly LogService?   _log;
    private static readonly Regex JiraKeyRegex = new(@"([A-Z]+-\d+)", RegexOptions.Compiled);

    public GitService(ConfigService config, LogService? log = null)
    { _config = config; _log = log; }

    public List<string> GetAllRepos()
    {
        var repos = new List<string>();
        foreach (var root in _config.Current.Repos)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                if (Directory.Exists(Path.Combine(root, ".git")))
                    repos.Add(root);

                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    if (Directory.Exists(Path.Combine(dir, ".git")))
                        repos.Add(dir);
                }
            }
            catch (Exception ex) { _log?.Warning("Git", $"Error scanning {root}: {ex.Message}"); }
        }
        _log?.Debug("Git", $"Found {repos.Count} repos");
        return repos;
    }

    public string? GetCurrentBranch(string repoPath)
    {
        var headFile = Path.Combine(repoPath, ".git", "HEAD");
        if (!File.Exists(headFile)) return null;
        var head = File.ReadAllText(headFile).Trim();
        var match = Regex.Match(head, @"ref: refs/heads/(.+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    public string? ExtractJiraKey(string branch)
    {
        var match = JiraKeyRegex.Match(branch);
        return match.Success ? match.Value : null;
    }

    public int GetRepoLastActivitySeconds(string repoPath)
    {
        // Use .git/index as a fast proxy for last git activity (updated on every stage/commit)
        // Also check a few common source dirs one level deep for recent file writes
        try
        {
            var candidates = new List<DateTime>();

            // .git/index is updated whenever files are staged
            var gitIndex = Path.Combine(repoPath, ".git", "index");
            if (File.Exists(gitIndex))
                candidates.Add(File.GetLastWriteTime(gitIndex));

            // .git/COMMIT_EDITMSG updated on each commit
            var commitMsg = Path.Combine(repoPath, ".git", "COMMIT_EDITMSG");
            if (File.Exists(commitMsg))
                candidates.Add(File.GetLastWriteTime(commitMsg));

            // Check source files one level deep (top-level files + immediate subdirs, skipping build dirs)
            var skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".git", "bin", "obj", "node_modules", ".vs", "packages", "dist", "out", "build", ".idea" };

            foreach (var f in Directory.EnumerateFiles(repoPath))
                candidates.Add(File.GetLastWriteTime(f));

            foreach (var dir in Directory.EnumerateDirectories(repoPath))
            {
                if (skipDirs.Contains(Path.GetFileName(dir))) continue;
                foreach (var f in Directory.EnumerateFiles(dir))
                    candidates.Add(File.GetLastWriteTime(f));
            }

            if (candidates.Count > 0)
                return (int)(DateTime.Now - candidates.Max()).TotalSeconds;
        }
        catch { }
        return 99999;
    }

    public RepoInfo? GetActiveRepo()
    {
        var windowSec = _config.Current.Settings.ActivityWindowMinutes * 60;
        RepoInfo? best = null;
        var bestSec = 99999;

        foreach (var repo in GetAllRepos())
        {
            var branch = GetCurrentBranch(repo);
            if (branch == null) continue;
            var issue = ExtractJiraKey(branch);
            if (issue == null) continue;
            var sec = GetRepoLastActivitySeconds(repo);
            if (sec < bestSec)
            {
                bestSec = sec;
                best = new RepoInfo
                {
                    RepoPath      = repo,
                    RepoName      = Path.GetFileName(repo),
                    Branch        = branch,
                    IssueKey      = issue,
                    LastActivitySec = sec
                };
            }
        }

        return (best != null && best.LastActivitySec <= windowSec) ? best : null;
    }

    public List<RepoInfo> GetAllJiraRepos()
    {
        var result = new List<RepoInfo>();
        foreach (var repo in GetAllRepos())
        {
            var branch = GetCurrentBranch(repo);
            if (branch == null) continue;
            var issue = ExtractJiraKey(branch);
            if (issue == null) continue;
            result.Add(new RepoInfo
            {
                RepoPath = repo,
                RepoName = Path.GetFileName(repo),
                Branch   = branch,
                IssueKey = issue
            });
        }
        return result;
    }
}