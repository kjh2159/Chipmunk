namespace Chipmunk.Models;

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
    TwoLines
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
