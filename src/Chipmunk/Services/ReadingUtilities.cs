using Chipmunk.Models;

namespace Chipmunk.Services;

public static class SeverityClassifier
{
    public static Severity ForTemperature(double? valueCelsius, ThresholdSettings thresholds) =>
        valueCelsius is null
            ? Severity.Unavailable
            : valueCelsius >= thresholds.TemperatureCritical
                ? Severity.Critical
                : valueCelsius >= thresholds.TemperatureWarning
                    ? Severity.Warning
                    : Severity.Normal;

    public static Severity ForUsage(double? percent, ThresholdSettings thresholds) =>
        percent is null
            ? Severity.Unavailable
            : percent >= thresholds.UsageCritical
                ? Severity.Critical
                : percent >= thresholds.UsageWarning
                    ? Severity.Warning
                    : Severity.Normal;

    public static Severity Maximum(params Severity[] severities)
    {
        if (severities.Contains(Severity.Critical))
        {
            return Severity.Critical;
        }

        if (severities.Contains(Severity.Warning))
        {
            return Severity.Warning;
        }

        if (severities.Contains(Severity.Normal))
        {
            return Severity.Normal;
        }

        return Severity.Unavailable;
    }
}

public static class MemoryFormatter
{
    private const double BytesPerGibibyte = 1024d * 1024 * 1024;

    public static double BytesToGibibytes(double bytes) => bytes / BytesPerGibibyte;

    public static double? Percentage(double? usedBytes, double? totalBytes) =>
        usedBytes is null || totalBytes is null || totalBytes <= 0
            ? null
            : Math.Clamp(usedBytes.Value / totalBytes.Value * 100, 0, 100);
}

public static class GpuSelectionPolicy
{
    public static GpuReading? Select(IReadOnlyList<GpuReading> gpus, string? selectedGpuId)
    {
        if (!string.IsNullOrWhiteSpace(selectedGpuId))
        {
            var selected = gpus.FirstOrDefault(gpu =>
                string.Equals(gpu.DeviceId, selectedGpuId, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected;
            }
        }

        return gpus
            .OrderByDescending(gpu => gpu.UsagePercent ?? -1)
            .ThenBy(gpu => gpu.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
