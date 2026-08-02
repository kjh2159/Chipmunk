using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Chipmunk.Models;

namespace Chipmunk.Converters;

public sealed class SeverityToBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush WarningBrush = Frozen("#FFFFC857");
    private static readonly System.Windows.Media.Brush CriticalBrush = Frozen("#FFFF6B6B");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            Severity.Warning => WarningBrush,
            Severity.Critical => CriticalBrush,
            Severity.Unavailable => System.Windows.Application.Current.Resources["SubtleForegroundBrush"],
            _ => System.Windows.Application.Current.Resources["WidgetForegroundBrush"]
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;

    private static SolidColorBrush Frozen(string value)
    {
        var brush = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}
