namespace Chipmunk.Models;

/// <summary>
/// A nullable reading. A missing value is a normal state and is rendered as N/A.
/// </summary>
public sealed record SensorReading(
    string SensorId,
    string Name,
    SensorMetricType MetricType,
    double? Value,
    DateTimeOffset Timestamp);

public sealed record SensorDescriptor(
    string SensorId,
    string Name,
    string HardwareId,
    string HardwareName,
    HardwareKind HardwareKind,
    SensorMetricType MetricType);

public sealed record SensorSelection(
    IReadOnlyList<string> SensorIds,
    string Description)
{
    public static SensorSelection Empty { get; } = new([], "Unavailable");
    public bool IsAvailable => SensorIds.Count > 0;
}

public sealed record GpuReading(
    string DeviceId,
    string Name,
    HardwareKind Kind,
    double? TemperatureCelsius,
    double? UsagePercent,
    double? MemoryUsedBytes,
    double? MemoryTotalBytes);

public sealed record MonitoringSnapshot(
    DateTimeOffset Timestamp,
    double? CpuTemperatureCelsius,
    double? CpuUsagePercent,
    IReadOnlyList<GpuReading> Gpus,
    double? SystemMemoryUsedBytes,
    double? SystemMemoryTotalBytes,
    string? LastError = null)
{
    public static MonitoringSnapshot Empty { get; } =
        new(DateTimeOffset.MinValue, null, null, [], null, null);
}
