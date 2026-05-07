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

    [JsonPropertyName("parentKey")]
    public string ParentKey { get; set; } = "";

    // An issue is treated as a sub-task if the type name says so OR it has a parent key.
    // (Some Jira configs return "Task"/"Child Issue" with a ParentKey for sub-tasks.)
    /// Normalised type string — always "sub-task" for sub-tasks regardless of what Jira returns.
    /// Used by the geometry + color converters.
    public string ResolvedType => IsSubTask ? "sub-task" : IssueType;

    public bool   IsSubTask    => IssueType.ToLower().Contains("sub")
                               || IssueType.ToLower().Contains("child");
    public int    IndentMargin => IsSubTask ? 20 : 0;
    public string ParentLabel  => IsSubTask && !string.IsNullOrEmpty(ParentKey) ? ParentKey : "";

    // Collapse/expand state — set by HomeWindow when building the grouped list
    public bool IsExpanded  { get; set; } = true;
    public bool HasSubtasks { get; set; } = false;
    public string ChevronIcon => HasSubtasks ? (IsExpanded ? "▼" : "▶") : "";

    /// True only when this subtask's parent IS present in the current issue list.
    /// Controls indent + parent-label visibility. Orphan subtasks show flat.
    public bool IsGrouped    { get; set; } = false;
    public int  GroupedIndent => IsGrouped ? 20 : 0;

    // Issue type icon — \u escapes so encoding can never corrupt these
    // Task ✔  Sub-task ⤷  Story ▣  Bug ✱  Spike △  Epic ⚡  Initiative ↺  Theme ◎  Milestone ◆
    public string IssueTypeIcon => IssueType.ToLower() switch
    {
        var t when t.Contains("sub")         => "⤷",   // ⤷ Sub-task
        var t when t.Contains("bug")         => "✱",   // ✱ Bug
        var t when t.Contains("epic")        => "⚡",   // ⚡ Epic
        var t when t.Contains("story")       => "▣",   // ▣ Story
        var t when t.Contains("spike")       => "△",   // △ Spike
        var t when t.Contains("initiative")  => "↺",   // ↺ Initiative
        var t when t.Contains("theme")       => "◎",   // ◎ Theme
        var t when t.Contains("milestone")   => "◆",   // ◆ Milestone
        var t when t.Contains("task")        => "✔",   // ✔ Task
        _                                    => "◆"    // ◆
    };

    public string IssueTypeBg => IsSubTask ? "#1a2535" : IssueType.ToLower() switch
    {
        var t when t.Contains("sub")                               => "#1a2535",   // gray-blue (fallback)
        var t when t.Contains("bug")                               => "#3d1a1a",   // red
        var t when t.Contains("epic")                              => "#2d1f47",   // purple
        var t when t.Contains("story")                             => "#1a2f1a",   // green
        var t when t.Contains("spike")                             => "#2a2000",   // orange
        var t when t.Contains("initiative")                        => "#1c3a5e",   // blue
        var t when t.Contains("theme")                             => "#1f2d3a",   // teal
        var t when t.Contains("milestone")                         => "#2d1a1a",   // red
        var t when t.Contains("task")                              => "#1c3a5e",   // blue
        _                                                          => "#21262d"
    };

    public string IssueTypeColor => IsSubTask ? "#579dff" : IssueType.ToLower() switch
    {
        var t when t.Contains("sub")                               => "#579dff",   // blue (fallback)
        var t when t.Contains("bug")                               => "#f85149",   // red
        var t when t.Contains("epic")                              => "#c084fc",   // purple
        var t when t.Contains("story")                             => "#3fb950",   // green
        var t when t.Contains("spike")                             => "#f0883e",   // orange
        var t when t.Contains("initiative")                        => "#58a6ff",   // blue
        var t when t.Contains("theme")                             => "#39d0d8",   // teal
        var t when t.Contains("milestone")                         => "#ff7b72",   // light red
        var t when t.Contains("task")                              => "#58a6ff",   // blue
        _                                                          => "#8b949e"
    };

    // Display helpers
    public string DisplayTitle => $"{Key}  ·  {Summary}";

    public override string ToString() => DisplayTitle;
}
