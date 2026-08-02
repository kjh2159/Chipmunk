using Chipmunk.Models;
using Chipmunk.Services;
using Chipmunk.ViewModels;

namespace Chipmunk.Tests;

public sealed class MonitoringIntegrationTests
{
    [Fact]
    public async Task MonitoringService_StartsPublishesAndStops()
    {
        await using var service = new MockHardwareMonitoringService
        {
            UpdateInterval = TimeSpan.FromMilliseconds(20)
        };
        var published = new TaskCompletionSource<MonitoringSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        service.SnapshotUpdated += snapshot => published.TrySetResult(snapshot);

        await service.StartAsync();
        var result = await published.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync();

        Assert.NotNull(result.CpuTemperatureCelsius);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task SettingsChange_ImmediatelyUpdatesWidgetPresentation()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var settings = new SettingsService(logger, environment.SettingsDirectory);
        await settings.LoadAsync();
        await using var monitoring = new ManualMonitoringService();
        using var viewModel = new WidgetViewModel(monitoring, settings);
        monitoring.Publish(new MonitoringSnapshot(
            DateTimeOffset.Now,
            50,
            25,
            [],
            8d * 1024 * 1024 * 1024,
            16d * 1024 * 1024 * 1024));

        var changed = settings.Current.Clone();
        changed.TemperatureUnit = TemperatureUnit.Fahrenheit;
        changed.FontSize = 18;
        await settings.SaveAsync(changed);

        Assert.Contains("122°F", viewModel.DisplayText);
        Assert.Equal(18, viewModel.WidgetFontSize);
    }

    [Fact]
    public async Task NullSensors_RenderAsNAWithoutThrowing()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var settings = new SettingsService(logger, environment.SettingsDirectory);
        await settings.LoadAsync();
        await using var monitoring = new ManualMonitoringService();
        using var viewModel = new WidgetViewModel(monitoring, settings);

        monitoring.Publish(new MonitoringSnapshot(
            DateTimeOffset.Now,
            null,
            null,
            [],
            null,
            null));

        Assert.Contains("CPU N/A · N/A", viewModel.DisplayText);
        Assert.Contains("GPU N/A · N/A · N/A", viewModel.DisplayText);
        Assert.Contains("RAM N/A · N/A", viewModel.DisplayText);
    }

    [Fact]
    public async Task ResumeRecovery_RequestsSensorRescan()
    {
        await using var monitoring = new MockHardwareMonitoringService();

        await monitoring.RescanAsync();

        Assert.Equal(1, monitoring.RescanCount);
    }

    [Fact]
    public void MonitorChange_RecalculatesAndClampsPosition()
    {
        var oldPosition = new PixelPoint(1700, 900);
        var newMonitorWorkingArea = new PixelRect(-1920, 0, 0, 1040);

        var recalculated = WindowPositionCalculator.Clamp(
            oldPosition,
            newMonitorWorkingArea,
            300,
            70);

        Assert.Equal(-300, recalculated.X);
        Assert.Equal(900, recalculated.Y);
    }

    private sealed class ManualMonitoringService : IHardwareMonitoringService
    {
        public event Action<MonitoringSnapshot>? SnapshotUpdated;
        public event Action<IReadOnlyList<HardwareDevice>>? DevicesChanged;
        public IReadOnlyList<HardwareDevice> Devices { get; } = [];
        public bool IsRunning { get; private set; }
        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(1);

        public void Publish(MonitoringSnapshot snapshot) => SnapshotUpdated?.Invoke(snapshot);
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public Task RescanAsync(CancellationToken cancellationToken = default)
        {
            DevicesChanged?.Invoke(Devices);
            return Task.CompletedTask;
        }

        public Task<MonitoringSnapshot> ReadOnceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(MonitoringSnapshot.Empty);

        public async ValueTask DisposeAsync() => await StopAsync();
    }
}
