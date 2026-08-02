using LibreHardwareMonitor.Hardware;
using Chipmunk.Interop;
using Chipmunk.Models;

namespace Chipmunk.Services;

/// <summary>
/// Owns LibreHardwareMonitor and all native sampling resources. Discovery is
/// performed only on start/rescan; polling reuses selected ISensor references.
/// Every hardware access is serialized because the library's update graph is not
/// guaranteed to be thread-safe.
/// </summary>
public sealed class HardwareMonitoringService : IHardwareMonitoringService
{
    private const double Mebibyte = 1024d * 1024;
    private const double Gibibyte = 1024d * 1024 * 1024;
    private readonly ISensorDiscoveryService _discovery;
    private readonly IRateLimitedLogger _logger;
    private readonly SystemResourceReader _systemResources = new();
    private readonly SemaphoreSlim _hardwareGate = new(1, 1);
    private readonly object _stateLock = new();
    private Computer? _computer;
    private Dictionary<string, ISensor> _sensorById = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<HardwareDevice> _devices = [];
    private SensorSelection _cpuTemperature = SensorSelection.Empty;
    private SensorSelection _cpuUsage = SensorSelection.Empty;
    private IReadOnlyList<GpuBinding> _gpuBindings = [];
    private CancellationTokenSource? _loopCancellation;
    private Task? _loopTask;
    private int _updateIntervalMilliseconds = 1000;

    public HardwareMonitoringService(
        ISensorDiscoveryService discovery,
        IRateLimitedLogger logger)
    {
        _discovery = discovery;
        _logger = logger;
    }

    public event Action<MonitoringSnapshot>? SnapshotUpdated;
    public event Action<IReadOnlyList<HardwareDevice>>? DevicesChanged;

    public IReadOnlyList<HardwareDevice> Devices
    {
        get
        {
            lock (_stateLock)
            {
                return _devices;
            }
        }
    }

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public TimeSpan UpdateInterval
    {
        get => TimeSpan.FromMilliseconds(Volatile.Read(ref _updateIntervalMilliseconds));
        set
        {
            var milliseconds = (int)Math.Clamp(value.TotalMilliseconds, 500, 5000);
            Volatile.Write(ref _updateIntervalMilliseconds, milliseconds);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        await RescanAsync(cancellationToken).ConfigureAwait(false);
        _loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(
            () => MonitorLoopAsync(_loopCancellation.Token),
            CancellationToken.None);
    }

    public async Task StopAsync()
    {
        var cancellation = _loopCancellation;
        var loop = _loopTask;
        _loopCancellation = null;
        _loopTask = null;
        cancellation?.Cancel();

        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during application shutdown.
            }
        }

        cancellation?.Dispose();
        await _hardwareGate.WaitAsync().ConfigureAwait(false);
        try
        {
            CloseComputer();
        }
        finally
        {
            _hardwareGate.Release();
        }
    }

    public async Task RescanAsync(CancellationToken cancellationToken = default)
    {
        await _hardwareGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(RebuildComputer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error(
                "hardware-discovery",
                "하드웨어 센서 초기화에 실패했습니다. 접근 가능한 항목만 계속 표시합니다.",
                exception);
            CloseComputer();
            PublishDevices([]);
        }
        finally
        {
            _hardwareGate.Release();
        }
    }

    public async Task<MonitoringSnapshot> ReadOnceAsync(CancellationToken cancellationToken = default)
    {
        await _hardwareGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            UpdateHardware();
            var timestamp = DateTimeOffset.Now;
            var cpuTemperature = ReadSelection(_cpuTemperature);
            var cpuUsage = ReadSelection(_cpuUsage) ?? _systemResources.ReadCpuUsage();
            var memory = _systemResources.ReadPhysicalMemory();
            var gpus = _gpuBindings.Select(ReadGpu).ToArray();

            return new MonitoringSnapshot(
                timestamp,
                ClampTemperature(cpuTemperature),
                ClampPercent(cpuUsage),
                gpus,
                memory.UsedBytes,
                memory.TotalBytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error("hardware-read", "센서 값을 읽는 중 오류가 발생했습니다.", exception);
            var memory = _systemResources.ReadPhysicalMemory();
            return new MonitoringSnapshot(
                DateTimeOffset.Now,
                null,
                _systemResources.ReadCpuUsage(),
                [],
                memory.UsedBytes,
                memory.TotalBytes,
                exception.Message);
        }
        finally
        {
            _hardwareGate.Release();
        }
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        var consecutiveFailures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var started = Environment.TickCount64;
            var snapshot = await ReadOnceAsync(cancellationToken).ConfigureAwait(false);
            SnapshotUpdated?.Invoke(snapshot);

            if (snapshot.LastError is null)
            {
                consecutiveFailures = 0;
            }
            else if (++consecutiveFailures >= 3)
            {
                consecutiveFailures = 0;
                await RescanAsync(cancellationToken).ConfigureAwait(false);
            }

            var elapsed = Environment.TickCount64 - started;
            var delay = Math.Max(10, Volatile.Read(ref _updateIntervalMilliseconds) - elapsed);
            await Task.Delay((int)delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private void RebuildComputer()
    {
        CloseComputer();
        _systemResources.ResetCpuBaseline();
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };

        computer.Open();
        _computer = computer;
        UpdateHardware();

        var sensorMap = new Dictionary<string, ISensor>(StringComparer.OrdinalIgnoreCase);
        var descriptors = new List<SensorDescriptor>();
        var deviceList = new List<HardwareDevice>();

        foreach (var hardware in EnumerateHardware(computer.Hardware))
        {
            var kind = MapHardwareKind(hardware.HardwareType.ToString());
            if (kind is not (HardwareKind.Cpu or HardwareKind.GpuNvidia or HardwareKind.GpuAmd or HardwareKind.GpuIntel))
            {
                continue;
            }

            var deviceSensors = new List<SensorDescriptor>();
            foreach (var sensor in hardware.Sensors)
            {
                var id = sensor.Identifier.ToString();
                var descriptor = new SensorDescriptor(
                    id,
                    sensor.Name,
                    hardware.Identifier.ToString(),
                    hardware.Name,
                    kind,
                    MapMetric(sensor.SensorType.ToString(), sensor.Name));
                sensorMap[id] = sensor;
                descriptors.Add(descriptor);
                deviceSensors.Add(descriptor);
            }

            deviceList.Add(new HardwareDevice(
                hardware.Identifier.ToString(),
                hardware.Name,
                kind,
                deviceSensors));
        }

        _sensorById = sensorMap;
        _cpuTemperature = _discovery.SelectCpuTemperature(descriptors);
        _cpuUsage = _discovery.SelectCpuUsage(descriptors);
        _gpuBindings = deviceList
            .Where(device => device.Kind is HardwareKind.GpuNvidia or HardwareKind.GpuAmd or HardwareKind.GpuIntel)
            .Select(device => BuildGpuBinding(device, descriptors))
            .ToArray();

        _logger.Debug($"CPU temperature sensor: {_cpuTemperature.Description}");
        _logger.Debug($"CPU usage sensor: {_cpuUsage.Description}; Win32 fallback is enabled.");
        foreach (var gpu in _gpuBindings)
        {
            _logger.Debug(
                $"GPU '{gpu.Name}': temperature={gpu.Temperature.Description}, " +
                $"usage={gpu.Usage.Description}, memory={gpu.MemoryUsed.Description}/{gpu.MemoryTotal.Description}");
        }

        PublishDevices(deviceList);
    }

    private GpuBinding BuildGpuBinding(
        HardwareDevice device,
        IReadOnlyList<SensorDescriptor> allSensors)
    {
        var memoryPercent = device.Sensors.FirstOrDefault(sensor =>
        {
            var normalized = Normalize(sensor.Name);
            return sensor.MetricType == SensorMetricType.Load &&
                   normalized.Contains("memory") &&
                   !normalized.Contains("controller");
        });

        return new GpuBinding(
            device.DeviceId,
            device.Name,
            device.Kind,
            _discovery.SelectGpuTemperature(allSensors, device.DeviceId),
            _discovery.SelectGpuUsage(allSensors, device.DeviceId),
            _discovery.SelectGpuMemoryUsed(allSensors, device.DeviceId),
            _discovery.SelectGpuMemoryTotal(allSensors, device.DeviceId),
            memoryPercent?.SensorId);
    }

    private GpuReading ReadGpu(GpuBinding binding)
    {
        var used = ReadMemorySelection(binding.MemoryUsed);
        var total = ReadMemorySelection(binding.MemoryTotal);
        if (total is null && used is > 0 && binding.MemoryPercentSensorId is not null)
        {
            var percent = ReadSensor(binding.MemoryPercentSensorId);
            if (percent is > 0)
            {
                total = used / (percent.Value / 100d);
            }
        }

        return new GpuReading(
            binding.DeviceId,
            binding.Name,
            binding.Kind,
            ClampTemperature(ReadSelection(binding.Temperature)),
            ClampPercent(ReadSelection(binding.Usage)),
            used,
            total);
    }

    private double? ReadMemorySelection(SensorSelection selection)
    {
        if (selection.SensorIds.Count == 0)
        {
            return null;
        }

        var id = selection.SensorIds[0];
        if (!_sensorById.TryGetValue(id, out var sensor) || sensor.Value is not float value)
        {
            return null;
        }

        return sensor.SensorType.ToString() switch
        {
            "SmallData" => value * Mebibyte,
            "Data" => value * Gibibyte,
            _ => value
        };
    }

    private double? ReadSelection(SensorSelection selection)
    {
        if (selection.SensorIds.Count == 0)
        {
            return null;
        }

        var values = selection.SensorIds
            .Select(ReadSensor)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    private double? ReadSensor(string id) =>
        _sensorById.TryGetValue(id, out var sensor) && sensor.Value is float value
            ? value
            : null;

    private void UpdateHardware()
    {
        if (_computer is null)
        {
            return;
        }

        foreach (var hardware in EnumerateHardware(_computer.Hardware))
        {
            hardware.Update();
        }
    }

    private void PublishDevices(IReadOnlyList<HardwareDevice> devices)
    {
        lock (_stateLock)
        {
            _devices = devices.ToArray();
        }

        DevicesChanged?.Invoke(Devices);
    }

    private void CloseComputer()
    {
        try
        {
            _computer?.Close();
        }
        catch (Exception exception)
        {
            _logger.Error("hardware-close", "센서 라이브러리 종료 중 오류가 발생했습니다.", exception);
        }
        finally
        {
            _computer = null;
            _sensorById = new Dictionary<string, ISensor>(StringComparer.OrdinalIgnoreCase);
            _cpuTemperature = SensorSelection.Empty;
            _cpuUsage = SensorSelection.Empty;
            _gpuBindings = [];
        }
    }

    private static IEnumerable<IHardware> EnumerateHardware(IEnumerable<IHardware> roots)
    {
        foreach (var hardware in roots)
        {
            yield return hardware;
            foreach (var child in EnumerateHardware(hardware.SubHardware))
            {
                yield return child;
            }
        }
    }

    private static HardwareKind MapHardwareKind(string hardwareType) => hardwareType switch
    {
        "Cpu" => HardwareKind.Cpu,
        "GpuNvidia" => HardwareKind.GpuNvidia,
        "GpuAmd" => HardwareKind.GpuAmd,
        "GpuIntel" => HardwareKind.GpuIntel,
        "Memory" => HardwareKind.Memory,
        _ => HardwareKind.Unknown
    };

    private static SensorMetricType MapMetric(string sensorType, string name)
    {
        if (sensorType == "Temperature")
        {
            return SensorMetricType.Temperature;
        }

        if (sensorType == "Load")
        {
            return SensorMetricType.Load;
        }

        if (sensorType is "SmallData" or "Data")
        {
            var normalized = Normalize(name);
            if (normalized.Contains("memory") && normalized.Contains("total"))
            {
                return SensorMetricType.MemoryTotal;
            }

            if (normalized.Contains("memory") &&
                (normalized.Contains("used") || normalized.Contains("usage") || normalized.Contains("dedicated")))
            {
                return SensorMetricType.MemoryUsed;
            }
        }

        return SensorMetricType.Unknown;
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static double? ClampTemperature(double? value) =>
        value is >= -50 and <= 200 ? value : null;

    private static double? ClampPercent(double? value) =>
        value is null ? null : Math.Clamp(value.Value, 0, 100);

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _hardwareGate.Dispose();
    }

    private sealed record GpuBinding(
        string DeviceId,
        string Name,
        HardwareKind Kind,
        SensorSelection Temperature,
        SensorSelection Usage,
        SensorSelection MemoryUsed,
        SensorSelection MemoryTotal,
        string? MemoryPercentSensorId);
}
