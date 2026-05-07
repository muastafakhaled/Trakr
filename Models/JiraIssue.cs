using System.Text.Json.Serialization;

namespace JiraTimeTracker.Models;

public class JiraIssue
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("project")]
    public string Project { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("issueType")]
    public string IssueType { get; set; } = "";

    // Display helpers
    public string DisplayTitle => $"{Key}  ·  {Summary}";
    public string StatusDot    => Status switch
    {
        "In Progress"  => "🔵",
        "To Do"        => "⚪",
        "Done"         => "✅",
        "In Review"    => "🟣",
        "Blocked"      => "🔴",
        _              => "🟡"
    };

    public override string ToString() => DisplayTitle;
}
