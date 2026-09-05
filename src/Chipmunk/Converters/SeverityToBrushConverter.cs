using System.Globalization;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Chipmunk.Models;
using MediaBrush = System.Windows.Media.Brush;

namespace Chipmunk.Converters;

public sealed class SeverityToBrushConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, MediaBrush> AlertBrushes =
        new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SeverityVisualState state)
        {
            if (state.Severity is Severity.Warning or Severity.Critical)
            {
                var fallback = state.Severity == Severity.Warning
                    ? ThresholdSettings.DefaultWarningColor
                    : ThresholdSettings.DefaultCriticalColor;
                var colorHex = ArgbColorHex.Normalize(state.ColorHex, fallback);
                return AlertBrushes.GetOrAdd(colorHex, Frozen);
            }

            return ThemeBrush(state.Severity);
        }

        return value is Severity severity
            ? ThemeBrush(severity)
            : DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;

    private static SolidColorBrush Frozen(string value)
    {
        var brush = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }

    private static object ThemeBrush(Severity severity) =>
        severity == Severity.Unavailable
            ? System.Windows.Application.Current.Resources["SubtleForegroundBrush"]
            : System.Windows.Application.Current.Resources["WidgetForegroundBrush"];
}
