namespace Chipmunk.Models;

public sealed class ThresholdSettings
{
    public double TemperatureWarning { get; set; } = 70;
    public double TemperatureCritical { get; set; } = 85;
    public double UsageWarning { get; set; } = 80;
    public double UsageCritical { get; set; } = 95;

    public void Normalize()
    {
        TemperatureWarning = Math.Clamp(TemperatureWarning, 0, 150);
        TemperatureCritical = Math.Clamp(TemperatureCritical, TemperatureWarning, 150);
        UsageWarning = Math.Clamp(UsageWarning, 0, 100);
        UsageCritical = Math.Clamp(UsageCritical, UsageWarning, 100);
    }

    public ThresholdSettings Clone() => new()
    {
        TemperatureWarning = TemperatureWarning,
        TemperatureCritical = TemperatureCritical,
        UsageWarning = UsageWarning,
        UsageCritical = UsageCritical
    };
}
