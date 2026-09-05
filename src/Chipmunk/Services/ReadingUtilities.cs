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

public static class AlertStateResolver
{
    /// <summary>
    /// Resolves the indicator state deterministically. Severity is primary;
    /// temperature wins over usage within the same severity because it represents
    /// the more direct hardware-risk signal. Sensor enumeration order is ignored.
    /// </summary>
    public static SeverityVisualState Resolve(
        IEnumerable<AlertCandidate> candidates,
        ThresholdSettings thresholds)
    {
        var selected = candidates
            .OrderByDescending(candidate => SeverityRank(candidate.Severity))
            .ThenBy(candidate => ThresholdKindRank(candidate.ThresholdKind))
            .FirstOrDefault(new AlertCandidate(Severity.Unavailable, ThresholdKind.None));

        var color = selected.Severity is Severity.Warning or Severity.Critical
            ? thresholds.GetColor(selected.ThresholdKind)
            : null;
        return new SeverityVisualState(selected.Severity, selected.ThresholdKind, color);
    }

    public static AlertCandidate ForTemperature(
        double? valueCelsius,
        ThresholdSettings thresholds)
    {
        var severity = SeverityClassifier.ForTemperature(valueCelsius, thresholds);
        var kind = severity switch
        {
            Severity.Warning => ThresholdKind.TemperatureWarning,
            Severity.Critical => ThresholdKind.TemperatureCritical,
            _ => ThresholdKind.None
        };
        return new AlertCandidate(severity, kind);
    }

    public static AlertCandidate ForUsage(double? percent, ThresholdSettings thresholds)
    {
        var severity = SeverityClassifier.ForUsage(percent, thresholds);
        var kind = severity switch
        {
            Severity.Warning => ThresholdKind.UsageWarning,
            Severity.Critical => ThresholdKind.UsageCritical,
            _ => ThresholdKind.None
        };
        return new AlertCandidate(severity, kind);
    }

    private static int SeverityRank(Severity severity) => severity switch
    {
        Severity.Critical => 3,
        Severity.Warning => 2,
        Severity.Normal => 1,
        _ => 0
    };

    private static int ThresholdKindRank(ThresholdKind kind) => kind switch
    {
        ThresholdKind.TemperatureCritical => 0,
        ThresholdKind.TemperatureWarning => 0,
        ThresholdKind.UsageCritical => 1,
        ThresholdKind.UsageWarning => 1,
        _ => 2
    };
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
