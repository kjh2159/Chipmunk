using System.Text.Json.Serialization;

namespace Chipmunk.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;
    private static readonly int[] AllowedIntervals = [500, 1000, 2000, 5000];

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public bool ShowCpuTemperature { get; set; } = true;
    public bool ShowCpuUsage { get; set; } = true;
    public bool ShowGpuTemperature { get; set; } = true;
    public bool ShowGpuUsage { get; set; } = true;
    public bool ShowGpuMemory { get; set; } = true;
    public bool ShowSystemMemory { get; set; } = true;
    public string? SelectedGpuId { get; set; }
    public int UpdateIntervalMilliseconds { get; set; } = 1000;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TemperatureUnit TemperatureUnit { get; set; } = TemperatureUnit.Celsius;
    public ThresholdSettings Thresholds { get; set; } = new();
    public double FontSize { get; set; } = 13;
    public double BackgroundOpacity { get; set; } = 0.86;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WidgetLayout Layout { get; set; } = WidgetLayout.TwoLines;
    public bool StartWithWindows { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool ClickThrough { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WidgetTheme Theme { get; set; } = WidgetTheme.System;
    public string? MonitorDeviceName { get; set; }
    public double TaskbarMargin { get; set; } = 8;
    public int DecimalDigits { get; set; } = 0;
    public bool HasCustomPosition { get; set; }
    public double CustomLeft { get; set; }
    public double CustomTop { get; set; }
    public bool WidgetVisible { get; set; } = true;
    public bool SuppressPawnIoInstallPrompt { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DoubleClickAction DoubleClickAction { get; set; } = DoubleClickAction.TaskManager;

    public void Normalize()
    {
        SchemaVersion = CurrentSchemaVersion;
        if (!AllowedIntervals.Contains(UpdateIntervalMilliseconds))
        {
            UpdateIntervalMilliseconds = 1000;
        }

        Thresholds ??= new ThresholdSettings();
        Thresholds.Normalize();
        FontSize = Math.Clamp(FontSize, 9, 30);
        BackgroundOpacity = Math.Clamp(BackgroundOpacity, 0.2, 1);
        TaskbarMargin = Math.Clamp(TaskbarMargin, 0, 100);
        DecimalDigits = Math.Clamp(DecimalDigits, 0, 2);

        if (!double.IsFinite(CustomLeft) || !double.IsFinite(CustomTop))
        {
            HasCustomPosition = false;
            CustomLeft = 0;
            CustomTop = 0;
        }
    }

    public AppSettings Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        ShowCpuTemperature = ShowCpuTemperature,
        ShowCpuUsage = ShowCpuUsage,
        ShowGpuTemperature = ShowGpuTemperature,
        ShowGpuUsage = ShowGpuUsage,
        ShowGpuMemory = ShowGpuMemory,
        ShowSystemMemory = ShowSystemMemory,
        SelectedGpuId = SelectedGpuId,
        UpdateIntervalMilliseconds = UpdateIntervalMilliseconds,
        TemperatureUnit = TemperatureUnit,
        Thresholds = Thresholds.Clone(),
        FontSize = FontSize,
        BackgroundOpacity = BackgroundOpacity,
        Layout = Layout,
        StartWithWindows = StartWithWindows,
        AlwaysOnTop = AlwaysOnTop,
        ClickThrough = ClickThrough,
        Theme = Theme,
        MonitorDeviceName = MonitorDeviceName,
        TaskbarMargin = TaskbarMargin,
        DecimalDigits = DecimalDigits,
        HasCustomPosition = HasCustomPosition,
        CustomLeft = CustomLeft,
        CustomTop = CustomTop,
        WidgetVisible = WidgetVisible,
        SuppressPawnIoInstallPrompt = SuppressPawnIoInstallPrompt,
        DoubleClickAction = DoubleClickAction
    };
}
