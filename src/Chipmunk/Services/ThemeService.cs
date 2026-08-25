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
            SetBrush(resources, "WidgetBackgroundBrush", "#E622252A");
            SetBrush(resources, "WidgetForegroundBrush", "#FFF3F5F7");
            SetBrush(resources, "WindowBackgroundBrush", "#FF202124");
            SetBrush(resources, "WindowForegroundBrush", "#FFF3F5F7");
            SetBrush(resources, "SubtleForegroundBrush", "#FFB0B6BF");
            SetBrush(resources, "ControlBackgroundBrush", "#FF2B2D31");
            SetBrush(resources, "ControlForegroundBrush", "#FFF3F5F7");
            SetBrush(resources, "ControlBorderBrush", "#FF555A64");
        }
        else
        {
            SetBrush(resources, "WidgetBackgroundBrush", "#E6FFFFFF");
            SetBrush(resources, "WidgetForegroundBrush", "#FF202124");
            SetBrush(resources, "WindowBackgroundBrush", "#FFF7F7F8");
            SetBrush(resources, "WindowForegroundBrush", "#FF202124");
            SetBrush(resources, "SubtleForegroundBrush", "#FF6B7280");
            SetBrush(resources, "ControlBackgroundBrush", "#FFFFFFFF");
            SetBrush(resources, "ControlForegroundBrush", "#FF202124");
            SetBrush(resources, "ControlBorderBrush", "#FFB8BDC7");
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

    private static void SetBrush(ResourceDictionary resources, string key, string value)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);

        // Preserve the brush instance whenever possible. Converters and control
        // templates can retain a direct brush reference, so replacing the resource
        // would leave them on the previous theme until their binding is evaluated.
        if (resources[key] is SolidColorBrush existing && !existing.IsFrozen)
        {
            existing.Color = color;
            return;
        }

        resources[key] = new SolidColorBrush(color);
    }
}
