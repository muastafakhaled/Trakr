using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace JiraTimeTracker.Converters;

public class StatusBgConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var hex = Classify(value as string ?? "") switch
        {
            "blue"   => "#1c3a5e",
            "purple" => "#2d1f47",
            "green"  => "#1a2f1a",
            "red"    => "#3d1f1f",
            "yellow" => "#2d2200",
            _        => "#21262d"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();

    internal static string Classify(string status)
    {
        var s = status.ToLower();
        if (s.Contains("done")     || s.Contains("closed")   ||
            s.Contains("resolved") || s.Contains("fixed")    ||
            s.Contains("complete") || s.Contains("released"))
            return "green";
        if (s.Contains("block")  || s.Contains("imped")   ||
            s.Contains("failed") || s.Contains("rejected"))
            return "red";
        if (s.Contains("progress")    || s.Contains("development") ||
            s.Contains("staging")     || s.Contains("bug fix")     ||
            s.Contains("in bug")      || s.Contains("active")      ||
            s.Contains("started"))
            return "blue";
        if (s.Contains("review")       || s.Contains("testing")    ||
            s.Contains("verification") || s.Contains("qa")         ||
            s.Contains("test")         || s.Contains("validat"))
            return "purple";
        if (s.Contains("defer")   || s.Contains("hold")    ||
            s.Contains("wait")    || s.Contains("pending") ||
            s.Contains("paused")  || s.Contains("postpone"))
            return "yellow";
        return "gray";
    }
}

public class StatusFgConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var hex = StatusBgConverter.Classify(value as string ?? "") switch
        {
            "blue"   => "#58a6ff",
            "purple" => "#c084fc",
            "green"  => "#3fb950",
            "red"    => "#f85149",
            "yellow" => "#e3b341",
            _        => "#8b949e"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
