using Chipmunk.Models;

namespace Chipmunk.Services;

/// <summary>
/// Deterministic provider used by tests and UI development on machines without
/// accessible sensors.
/// </summary>
public sealed class MockHardwareMonitoringService : IHardwareMonitoringService
{
    private CancellationTokenSource? _cancellation;
    private Task? _loop;
    private int _tick;

    public event Action<MonitoringSnapshot>? SnapshotUpdated;
    public event Action<IReadOnlyList<HardwareDevice>>? DevicesChanged;

    public IReadOnlyList<HardwareDevice> Devices { get; private set; } =
    [
        new HardwareDevice("mock-gpu-0", "Mock GPU", HardwareKind.GpuNvidia, [])
    ];

    public bool IsRunning => _loop is { IsCompleted: false };
    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int RescanCount { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => LoopAsync(_cancellation.Token), CancellationToken.None);
        DevicesChanged?.Invoke(Devices);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cancellation?.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }

        _cancellation?.Dispose();
        _cancellation = null;
        _loop = null;
    }

    public Task RescanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RescanCount++;
        DevicesChanged?.Invoke(Devices);
        return Task.CompletedTask;
    }

    public Task<MonitoringSnapshot> ReadOnceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var phase = Interlocked.Increment(ref _tick) % 20;
        var snapshot = new MonitoringSnapshot(
            DateTimeOffset.Now,
            48 + phase * 0.4,
            12 + phase * 2,
            [
                new GpuReading(
                    "mock-gpu-0",
                    "Mock GPU",
                    HardwareKind.GpuNvidia,
                    54 + phase * 0.35,
                    20 + phase * 2.5,
                    3.2 * 1024 * 1024 * 1024,
                    12d * 1024 * 1024 * 1024)
            ],
            14.8 * 1024 * 1024 * 1024,
            32d * 1024 * 1024 * 1024);
        return Task.FromResult(snapshot);
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            SnapshotUpdated?.Invoke(await ReadOnceAsync(cancellationToken).ConfigureAwait(false));
            await Task.Delay(UpdateInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
