using Chipmunk.Models;
using Chipmunk.Services;

namespace Chipmunk.Tests;

public sealed class SensorDiscoveryServiceTests
{
    private readonly SensorDiscoveryService _service = new();

    [Fact]
    public void CpuTemperature_PrefersPackageOverOtherCandidates()
    {
        var sensors = new[]
        {
            Sensor("core-0", "Core #0", SensorMetricType.Temperature),
            Sensor("average", "Core Average", SensorMetricType.Temperature),
            Sensor("tdie", "CPU Tctl/Tdie", SensorMetricType.Temperature),
            Sensor("package", "CPU Package", SensorMetricType.Temperature)
        };

        var result = _service.SelectCpuTemperature(sensors);

        Assert.Equal(["package"], result.SensorIds);
    }

    [Fact]
    public void CpuTemperature_UsesTctlThenAverageThenIndividualCoreAverage()
    {
        var tctl = _service.SelectCpuTemperature(
        [
            Sensor("core-0", "CPU Core #0", SensorMetricType.Temperature),
            Sensor("average", "Core Average", SensorMetricType.Temperature),
            Sensor("tdie", "CPU (Tctl/Tdie)", SensorMetricType.Temperature)
        ]);
        var average = _service.SelectCpuTemperature(
        [
            Sensor("core-0", "CPU Core #0", SensorMetricType.Temperature),
            Sensor("average", "Core Average", SensorMetricType.Temperature)
        ]);
        var cores = _service.SelectCpuTemperature(
        [
            Sensor("core-0", "CPU Core #0", SensorMetricType.Temperature),
            Sensor("core-1", "CPU Core #1", SensorMetricType.Temperature)
        ]);

        Assert.Equal(["tdie"], tctl.SensorIds);
        Assert.Equal(["average"], average.SensorIds);
        Assert.Equal(["core-0", "core-1"], cores.SensorIds);
    }

    [Fact]
    public void GpuTemperature_ExcludesHotSpotWhenRepresentativeSensorExists()
    {
        var sensors = new[]
        {
            GpuSensor("hot", "GPU Hot Spot", SensorMetricType.Temperature),
            GpuSensor("core", "GPU Core", SensorMetricType.Temperature)
        };

        var result = _service.SelectGpuTemperature(sensors, "gpu-0");

        Assert.Equal(["core"], result.SensorIds);
    }

    [Fact]
    public void UsageSelection_PrefersTotalCpuAndGpuCore()
    {
        var sensors = new[]
        {
            Sensor("thread", "CPU Core #0 Thread #0", SensorMetricType.Load),
            Sensor("total", "CPU Total", SensorMetricType.Load),
            GpuSensor("memory", "GPU Memory Controller", SensorMetricType.Load),
            GpuSensor("gpu-core", "GPU Core", SensorMetricType.Load)
        };

        Assert.Equal(["total"], _service.SelectCpuUsage(sensors).SensorIds);
        Assert.Equal(["gpu-core"], _service.SelectGpuUsage(sensors, "gpu-0").SensorIds);
    }

    private static SensorDescriptor Sensor(
        string id,
        string name,
        SensorMetricType metric) =>
        new(id, name, "cpu-0", "Test CPU", HardwareKind.Cpu, metric);

    private static SensorDescriptor GpuSensor(
        string id,
        string name,
        SensorMetricType metric) =>
        new(id, name, "gpu-0", "Test GPU", HardwareKind.GpuNvidia, metric);
}
