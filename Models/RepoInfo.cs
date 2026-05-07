namespace JiraTimeTracker.Models;

public class RepoInfo
{
    public string RepoPath     { get; set; } = "";
    public string RepoName     { get; set; } = "";
    public string Branch       { get; set; } = "";
    public string IssueKey     { get; set; } = "";
    public int    LastActivitySec { get; set; } = 99999;
}