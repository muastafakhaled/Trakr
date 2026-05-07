using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace JiraTimeTracker.Converters;

// Returns a background SolidColorBrush for a Jira status string
public class StatusBgConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var s = (value as string ?? "").ToLower();
        var hex = s switch
        {
            var x when x.Contains("progress") || x.Contains("development") => "#1c3a5e",
            var x when x.Contains("review")   || x.Contains("testing")     => "#2d1f47",
            var x when x.Contains("done")     || x.Contains("closed")
                                              || x.Contains("resolved")    => "#1a2f1a",
            var x when x.Contains("blocked")  || x.Contains("impeded")     => "#3d1f1f",
            _                                                               => "#21262d"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Returns a foreground SolidColorBrush for a Jira status string
public class StatusFgConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var s = (value as string ?? "").ToLower();
        var hex = s switch
        {
            var x when x.Contains("progress") || x.Contains("development") => "#58a6ff",
            var x when x.Contains("review")   || x.Contains("testing")     => "#c084fc",
            var x when x.Contains("done")     || x.Contains("closed")
                                              || x.Contains("resolved")    => "#3fb950",
            var x when x.Contains("blocked")  || x.Contains("impeded")     => "#f85149",
            _                                                               => "#8b949e"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
