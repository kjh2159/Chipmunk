namespace Chipmunk.Models;

public sealed class ThresholdSettings
{
    public const string DefaultWarningColor = "#FFFFC857";
    public const string DefaultCriticalColor = "#FFFF6B6B";

    public double TemperatureWarning { get; set; } = 70;
    public double TemperatureCritical { get; set; } = 85;
    public double UsageWarning { get; set; } = 80;
    public double UsageCritical { get; set; } = 95;
    public string TemperatureWarningColor { get; set; } = DefaultWarningColor;
    public string TemperatureCriticalColor { get; set; } = DefaultCriticalColor;
    public string UsageWarningColor { get; set; } = DefaultWarningColor;
    public string UsageCriticalColor { get; set; } = DefaultCriticalColor;

    public void Normalize()
    {
        TemperatureWarning = Math.Clamp(TemperatureWarning, 0, 150);
        TemperatureCritical = Math.Clamp(TemperatureCritical, TemperatureWarning, 150);
        UsageWarning = Math.Clamp(UsageWarning, 0, 100);
        UsageCritical = Math.Clamp(UsageCritical, UsageWarning, 100);
        TemperatureWarningColor = ArgbColorHex.Normalize(
            TemperatureWarningColor,
            DefaultWarningColor);
        TemperatureCriticalColor = ArgbColorHex.Normalize(
            TemperatureCriticalColor,
            DefaultCriticalColor);
        UsageWarningColor = ArgbColorHex.Normalize(
            UsageWarningColor,
            DefaultWarningColor);
        UsageCriticalColor = ArgbColorHex.Normalize(
            UsageCriticalColor,
            DefaultCriticalColor);
    }

    public ThresholdSettings Clone() => new()
    {
        TemperatureWarning = TemperatureWarning,
        TemperatureCritical = TemperatureCritical,
        UsageWarning = UsageWarning,
        UsageCritical = UsageCritical,
        TemperatureWarningColor = TemperatureWarningColor,
        TemperatureCriticalColor = TemperatureCriticalColor,
        UsageWarningColor = UsageWarningColor,
        UsageCriticalColor = UsageCriticalColor
    };

    public string GetColor(ThresholdKind kind) => kind switch
    {
        ThresholdKind.TemperatureWarning => TemperatureWarningColor,
        ThresholdKind.TemperatureCritical => TemperatureCriticalColor,
        ThresholdKind.UsageWarning => UsageWarningColor,
        ThresholdKind.UsageCritical => UsageCriticalColor,
        _ => DefaultWarningColor
    };
}
