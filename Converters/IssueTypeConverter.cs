using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace JiraTimeTracker.Converters;

// Returns a foreground brush based on the IssueTypeColor string (already a hex color)
public class IssueTypeColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var hex = value as string ?? "#8b949e";
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return new SolidColorBrush(Color.FromRgb(0x8b, 0x94, 0x9e)); }
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
