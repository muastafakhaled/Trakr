using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace JiraTimeTracker.Converters;

/// <summary>
/// Converts an issue-type string (e.g. "Story", "Sub-task") into a WPF Geometry
/// built from the official Atlassian Atlaskit SVG paths.
/// </summary>
public class IssueTypeGeometryConverter : IValueConverter
{
    // ── SVG path data from @atlaskit/icon (16x16 viewBox) ─────────────────────

    private const string TaskPath =
        "M1 3C1 1.89543 1.89543 1 3 1H13C14.1046 1 15 1.89543 15 3V13C15 14.1046 14.1046 15 13 15H3" +
        "C1.89543 15 1 14.1046 1 13V3ZM3 2.5C2.72386 2.5 2.5 2.72386 2.5 3V13C2.5 13.2761 2.72386 13.5 3 13.5H13" +
        "C13.2761 13.5 13.5 13.2761 13.5 13V3C13.5 2.72386 13.2761 2.5 13 2.5H3Z" +
        "M12.3262 5.48014L7.32617 11.4801C7.18367 11.6511 6.97258 11.75 6.75 11.75" +
        "C6.52742 11.75 6.31633 11.6511 6.17383 11.4801L3.67383 8.48014L4.82617 7.51986" +
        "L6.75 9.82846L11.1738 4.51986Z";

    private const string SubtaskPath =
        "M13.5 9.25C13.5 8.97386 13.2761 8.75 13 8.75H8.75V13C8.75 13.2761 8.97386 13.5 9.25 13.5H13" +
        "C13.2761 13.5 13.5 13.2761 13.5 13V9.25ZM7.25 3C7.25 2.72386 7.02614 2.5 6.75 2.5H3" +
        "C2.72386 2.5 2.5 2.72386 2.5 3V6.75C2.5 7.02614 2.72386 7.25 3 7.25H7.25V3Z" +
        "M8.75 7.25H13C14.1046 7.25 15 8.14543 15 9.25V13C15 14.1046 14.1046 15 13 15H9.25" +
        "C8.14543 15 7.25 14.1046 7.25 13V8.75H3C1.89543 8.75 1 7.85457 1 6.75V3" +
        "C1 1.89543 1.89543 1 3 1H6.75C7.85457 1 8.75 1.89543 8.75 3V7.25Z";

    private const string BugPath =
        "M8 2.5C7.17157 2.5 6.5 3.17157 6.5 4H9.5C9.5 3.17157 8.82843 2.5 8 2.5Z" +
        "M11 4.02074V4C11 2.34315 9.65685 1 8 1C6.34315 1 5 2.34315 5 4V4.02074" +
        "C4.70952 4.06947 4.44734 4.2017 4.239 4.39189L3.35231 3.75855L2.43536 1.6954L1.06464 2.3046" +
        "L2.00928 4.43004C2.0999 4.63393 2.24344 4.80985 2.425 4.93953L3.75 5.88596V7H0.5V8.5H3.75V9.64872" +
        "L2.60021 10.6069C2.46773 10.7173 2.35953 10.8539 2.28241 11.0081L1.07918 13.4146L2.42082 14.0854" +
        "L3.60066 11.7257L4.06124 11.3419C4.69682 12.9183 6.23827 14 8.00146 14H8.25" +
        "C9.96908 14 11.4349 12.9156 12.0006 11.3934L12.3993 11.7257L13.5792 14.0854L14.9208 13.4146" +
        "L13.7176 11.0081C13.6405 10.8539 13.5323 10.7173 13.3998 10.6069L12.25 9.64872V8.5H15.5V7H12.25V5.88596" +
        "L13.575 4.93953C13.7566 4.80985 13.9001 4.63393 13.9907 4.43004L14.9354 2.3046L13.5646 1.6954" +
        "L12.6477 3.75855L11.761 4.39189C11.5527 4.2017 11.2905 4.06947 11 4.02074Z" +
        "M10.75 10V5.5H5.25V9.94265L5.28488 10.1694C5.49117 11.5102 6.64486 12.5 8.00146 12.5H8.25" +
        "C9.63071 12.5 10.75 11.3807 10.75 10Z";

    private const string StoryPath =
        "M11.5179 3C11.5179 2.72392 11.2939 2.5001 11.0179 2.5H4.98273C4.70659 2.5 4.48273 2.72386 4.48273 3" +
        "V12.8057L8.00031 10.333L8.43098 10.6367L11.5179 12.8057V3Z" +
        "M13.0179 14.2021C13.0177 14.7903 12.3965 15.1497 11.8958 14.8955L11.7972 14.8359L7.99934 12.166" +
        "L4.20344 14.8359C3.68999 15.1969 2.98295 14.8296 2.98273 14.2021V3" +
        "C2.98273 1.89543 3.87817 1 4.98273 1H11.0179C12.1224 1.0001 13.0179 1.89549 13.0179 3V14.2021Z";

    private const string EpicPath =
        "M10.271 0.0506865C10.5597 0.162558 10.75 0.440377 10.75 0.75003V5.38518L13.8971 6.01459" +
        "C14.1622 6.06762 14.3783 6.25928 14.4626 6.5162C14.5469 6.77312 14.4864 7.05553 14.3042 7.25535" +
        "L6.55422 15.7553C6.34559 15.9842 6.01778 16.0612 5.72904 15.9494" +
        "C5.4403 15.8375 5.25 15.5597 5.25 15.25V10.6149L2.10291 9.98547" +
        "C1.83776 9.93244 1.62169 9.74078 1.53738 9.48386C1.45308 9.22694 1.5136 8.94453 1.69578 8.74471" +
        "L9.44578 0.244715C9.65441 0.0158944 9.98222 -0.0611845 10.271 0.0506865Z" +
        "M3.69822 8.77482L6.75 9.38518V13.3143L12.3018 7.22524L9.25 6.61488V2.68578Z";

    // Spike → question-circle (two sub-paths, combined)
    private const string SpikePath =
        "M8 1.5C4.41015 1.5 1.5 4.41015 1.5 8C1.5 11.5899 4.41015 14.5 8 14.5" +
        "C11.5899 14.5 14.5 11.5899 14.5 8C14.5 4.41015 11.5899 1.5 8 1.5Z" +
        "M0 8C0 3.58172 3.58172 0 8 0C12.4183 0 16 3.58172 16 8C16 12.4183 12.4183 16 8 16" +
        "C3.58172 16 0 12.4183 0 8Z" +
        "M8 5C7.41421 5 7 5.41421 7 6H5.5C5.5 4.58579 6.58579 3.5 8 3.5" +
        "C9.41421 3.5 10.5 4.58579 10.5 6C10.5 7.13259 9.78738 7.7057 9.33837 8.05849" +
        "C8.82691 8.46035 8.75 8.55198 8.75 8.75V9.5H7.25V8.75C7.25 7.77259 7.93895 7.24287 8.32821 6.94359" +
        "C8.35791 6.92075 8.38586 6.89926 8.41163 6.87901C8.83763 6.5443 9 6.36741 9 6C9 5.41421 8.58579 5 8 5Z" +
        "M9 11.5C9 12.0523 8.55228 12.5 8 12.5C7.44772 12.5 7 12.0523 7 11.5" +
        "C7 10.9477 7.44772 10.5 8 10.5C8.55228 10.5 9 10.9477 9 11.5Z";

    // Initiative → flag
    private const string InitiativePath =
        "M2.21967 1.21967C2.36032 1.07902 2.55109 1 2.75 1H14" +
        "C14.2841 1 14.5438 1.1605 14.6708 1.41459C14.7979 1.66868 14.7704 1.97274 14.6 2.2" +
        "L11.9375 5.75L14.6 9.3C14.7704 9.52726 14.7979 9.83132 14.6708 10.0854" +
        "C14.5438 10.3395 14.2841 10.5 14 10.5H3.5V15H2V1.75C2 1.55109 2.07902 1.36032 2.21967 1.21967Z" +
        "M3.5 9H12.5L10.4 6.2C10.2 5.93333 10.2 5.56667 10.4 5.3L12.5 2.5H3.5V9Z";

    // Theme → Atlaskit theme icon (half-circle)
    private const string ThemePath =
        "M8 1.5C4.41015 1.5 1.5 4.41015 1.5 8C1.5 11.5899 4.41015 14.5 8 14.5" +
        "C11.5899 14.5 14.5 11.5899 14.5 8C14.5 4.41015 11.5899 1.5 8 1.5Z" +
        "M0 8C0 3.58172 3.58172 0 8 0C12.4183 0 16 3.58172 16 8C16 12.4183 12.4183 16 8 16" +
        "C3.58172 16 0 12.4183 0 8Z" +
        "M7.25 3H8C10.7614 3 13 5.23858 13 8C13 10.7614 10.7614 13 8 13H7.25V3Z";

    // Milestone → target / crosshair
    private const string MilestonePath =
        "M1.5428 7.25H4V8.75H1.5428C1.88638 11.7405 4.2595 14.1136 7.25 14.4572V12.0001H8.75V14.4572" +
        "C11.7405 14.1136 14.1136 11.7405 14.4572 8.75H12V7.25H14.4572" +
        "C14.1136 4.2595 11.7405 1.88638 8.75 1.5428V4.00008H7.25V1.5428" +
        "C4.2595 1.88638 1.88638 4.2595 1.5428 7.25Z" +
        "M0 8C-3.57628e-07 3.58172 3.58172 -1.78814e-07 8 0C12.4183 -1.78814e-07 16 3.58172 16 8" +
        "C16 12.4183 12.4183 16 8 16C3.58172 16 1.78814e-07 12.4183 0 8Z";

    // ── Lookup table ───────────────────────────────────────────────────────────

    private static readonly (string keyword, string pathData, bool evenOdd)[] _entries =
    [
        ("sub",         SubtaskPath,    false),
        ("child",       SubtaskPath,    false),
        ("bug",         BugPath,        true),
        ("epic",        EpicPath,       true),
        ("story",       StoryPath,      false),
        ("spike",       SpikePath,      true),
        ("initiative",  InitiativePath, true),
        ("theme",       ThemePath,      true),
        ("milestone",   MilestonePath,  true),
        ("task",        TaskPath,       true),
    ];

    private static readonly Geometry _fallback = BuildGeometry(TaskPath, true);

    // ── Chevron geometries (Atlaskit chevron-right / chevron-down) ────────────

    public static readonly Geometry ChevronRight = BuildGeometry(
        "M6.03027 1.46973L12.0303 7.46973C12.3049 7.74433 12.3223 8.17905 12.082 8.47363" +
        "L12.0303 8.53027L6.03027 14.5303L4.96973 13.4697L10.4395 8L4.96973 2.53027Z", false);

    public static readonly Geometry ChevronDown = BuildGeometry(
        "M14.5303 6.03027L8.53027 12.0303C8.25567 12.3049 7.82095 12.3223 7.52637 12.082" +
        "L7.46973 12.0303L1.46973 6.03027L2.53027 4.96973L8 10.4395L13.4697 4.96973Z", false);

    // ── IValueConverter ────────────────────────────────────────────────────────

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value?.ToString()?.ToLower() ?? "";
        foreach (var (keyword, pathData, evenOdd) in _entries)
            if (type.Contains(keyword))
                return BuildGeometry(pathData, evenOdd);
        return _fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Geometry BuildGeometry(string pathData, bool evenOdd)
    {
        var pg = PathGeometry.CreateFromGeometry(Geometry.Parse(pathData));
        if (evenOdd) pg.FillRule = FillRule.EvenOdd;
        pg.Freeze();
        return pg;
    }
}

/// <summary>
/// Converts a JiraIssue.IsExpanded bool to a chevron Geometry:
/// true  → chevron-down  (group is open)
/// false → chevron-right (group is collapsed)
/// </summary>
public class ChevronGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool expanded && expanded
            ? IssueTypeGeometryConverter.ChevronDown
            : IssueTypeGeometryConverter.ChevronRight;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
