using System.Globalization;

namespace Chipmunk.Models;

public enum AppLanguage
{
    English,
    Korean,
    Japanese,
    ChineseSimplified,
    Spanish
}

public static class AppLanguageDefaults
{
    public static AppLanguage Detect(CultureInfo? culture = null)
    {
        var language = (culture ?? CultureInfo.InstalledUICulture)
            .TwoLetterISOLanguageName
            .ToLowerInvariant();
        return language switch
        {
            "ko" => AppLanguage.Korean,
            "ja" => AppLanguage.Japanese,
            "zh" => AppLanguage.ChineseSimplified,
            "es" => AppLanguage.Spanish,
            _ => AppLanguage.English
        };
    }
}

public enum HardwareKind
{
    Unknown,
    Cpu,
    GpuNvidia,
    GpuAmd,
    GpuIntel,
    Memory
}

public enum SensorMetricType
{
    Unknown,
    Temperature,
    Load,
    MemoryUsed,
    MemoryTotal
}

public enum TemperatureUnit
{
    Celsius,
    Fahrenheit
}

public enum WidgetTheme
{
    System,
    Dark,
    Light
}

public enum WidgetLayout
{
    OneLine,
    TwoLines,
    ThreeLines
}

public enum Severity
{
    Normal,
    Warning,
    Critical,
    Unavailable
}

public enum DoubleClickAction
{
    TaskManager,
    DetailedMonitor
}

public enum TaskbarEdge
{
    Left,
    Top,
    Right,
    Bottom,
    Unknown
}

public enum PawnIoConsentChoice
{
    Install,
    Later,
    NeverAskAgain
}

public enum PawnIoInstallOutcome
{
    Installed,
    RebootRequired,
    Cancelled,
    InstallerMissing,
    VerificationFailed,
    Failed
}
