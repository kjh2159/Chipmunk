using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Chipmunk.Models;

namespace Chipmunk.Services;

public interface IThemeService
{
    void Apply(WidgetTheme theme);
}

public sealed class ThemeService : IThemeService
{
    public void Apply(WidgetTheme theme)
    {
        var effective = theme == WidgetTheme.System ? ReadSystemTheme() : theme;
        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        if (effective == WidgetTheme.Dark)
        {
            resources["WidgetBackgroundBrush"] = Brush("#E622252A");
            resources["WidgetForegroundBrush"] = Brush("#FFF3F5F7");
            resources["WindowBackgroundBrush"] = Brush("#FF202124");
            resources["WindowForegroundBrush"] = Brush("#FFF3F5F7");
            resources["SubtleForegroundBrush"] = Brush("#FFB0B6BF");
        }
        else
        {
            resources["WidgetBackgroundBrush"] = Brush("#E6FFFFFF");
            resources["WidgetForegroundBrush"] = Brush("#FF202124");
            resources["WindowBackgroundBrush"] = Brush("#FFF7F7F8");
            resources["WindowForegroundBrush"] = Brush("#FF202124");
            resources["SubtleForegroundBrush"] = Brush("#FF6B7280");
        }
    }

    private static WidgetTheme ReadSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var light = key?.GetValue("AppsUseLightTheme") as int?;
            return light == 0 ? WidgetTheme.Dark : WidgetTheme.Light;
        }
        catch
        {
            return WidgetTheme.Dark;
        }
    }

    private static SolidColorBrush Brush(string color) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
}
