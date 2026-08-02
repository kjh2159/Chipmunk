namespace Chipmunk.Models;

public sealed record HardwareDevice(
    string DeviceId,
    string Name,
    HardwareKind Kind,
    IReadOnlyList<SensorDescriptor> Sensors);

public sealed record MonitorDescriptor(
    string DeviceName,
    string DisplayName,
    bool IsPrimary);
