using Chipmunk.Models;

namespace Chipmunk.Services;

public interface IHardwareMonitoringService : IAsyncDisposable
{
    event Action<MonitoringSnapshot>? SnapshotUpdated;
    event Action<IReadOnlyList<HardwareDevice>>? DevicesChanged;
    IReadOnlyList<HardwareDevice> Devices { get; }
    bool IsRunning { get; }
    TimeSpan UpdateInterval { get; set; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task RescanAsync(CancellationToken cancellationToken = default);
    Task<MonitoringSnapshot> ReadOnceAsync(CancellationToken cancellationToken = default);
}
