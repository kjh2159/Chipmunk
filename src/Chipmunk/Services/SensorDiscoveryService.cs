using System.Text.RegularExpressions;
using Chipmunk.Models;

namespace Chipmunk.Services;

public interface ISensorDiscoveryService
{
    SensorSelection SelectCpuTemperature(IEnumerable<SensorDescriptor> sensors);
    SensorSelection SelectCpuUsage(IEnumerable<SensorDescriptor> sensors);
    SensorSelection SelectGpuTemperature(IEnumerable<SensorDescriptor> sensors, string hardwareId);
    SensorSelection SelectGpuUsage(IEnumerable<SensorDescriptor> sensors, string hardwareId);
    SensorSelection SelectGpuMemoryUsed(IEnumerable<SensorDescriptor> sensors, string hardwareId);
    SensorSelection SelectGpuMemoryTotal(IEnumerable<SensorDescriptor> sensors, string hardwareId);
}

public sealed partial class SensorDiscoveryService : ISensorDiscoveryService
{
    public SensorSelection SelectCpuTemperature(IEnumerable<SensorDescriptor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.HardwareKind == HardwareKind.Cpu &&
                             sensor.MetricType == SensorMetricType.Temperature)
            .ToArray();

        var package = FirstByNames(candidates, "cpupackage", "package");
        if (package is not null)
        {
            return Single(package, "CPU Package");
        }

        var tctl = candidates.FirstOrDefault(sensor =>
        {
            var name = Normalize(sensor.Name);
            return name.Contains("tctltdie") || name.Contains("tctl") || name.Contains("tdie");
        });
        if (tctl is not null)
        {
            return Single(tctl, "CPU Tctl/Tdie");
        }

        var average = candidates.FirstOrDefault(sensor =>
        {
            var name = Normalize(sensor.Name);
            return name.Contains("coreaverage") || name.Contains("coretemperaturesaverage");
        });
        if (average is not null)
        {
            return Single(average, "Core Average");
        }

        var cores = candidates.Where(sensor => IndividualCoreRegex().IsMatch(sensor.Name)).ToArray();
        return cores.Length > 0
            ? new SensorSelection(cores.Select(sensor => sensor.SensorId).ToArray(), "Average of individual cores")
            : SensorSelection.Empty;
    }

    public SensorSelection SelectCpuUsage(IEnumerable<SensorDescriptor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.HardwareKind == HardwareKind.Cpu &&
                             sensor.MetricType == SensorMetricType.Load)
            .ToArray();
        var selected = FirstByNames(candidates, "cputotal", "totalcpu", "total");
        return selected is null ? SensorSelection.Empty : Single(selected, "Total CPU");
    }

    public SensorSelection SelectGpuTemperature(IEnumerable<SensorDescriptor> sensors, string hardwareId)
    {
        var candidates = ForGpu(sensors, hardwareId, SensorMetricType.Temperature).ToArray();
        var core = FirstByNames(candidates, "gpucore", "core");
        if (core is not null)
        {
            return Single(core, "GPU Core");
        }

        var representative = candidates.FirstOrDefault(sensor =>
            Normalize(sensor.Name) is "gputemperature" or "temperature");
        if (representative is not null)
        {
            return Single(representative, "Representative GPU temperature");
        }

        var safe = candidates.FirstOrDefault(sensor =>
        {
            var name = Normalize(sensor.Name);
            return !name.Contains("hotspot") &&
                   !name.Contains("junction") &&
                   !name.Contains("memory") &&
                   !name.Contains("vrm") &&
                   !name.Contains("soc");
        });
        return safe is null ? SensorSelection.Empty : Single(safe, "GPU temperature fallback");
    }

    public SensorSelection SelectGpuUsage(IEnumerable<SensorDescriptor> sensors, string hardwareId)
    {
        var candidates = ForGpu(sensors, hardwareId, SensorMetricType.Load).ToArray();
        var selected = FirstByNames(
            candidates,
            "gpucore",
            "gpuutilization",
            "gpu3d",
            "d3d3d",
            "3d");
        return selected is null ? SensorSelection.Empty : Single(selected, "GPU Core/3D usage");
    }

    public SensorSelection SelectGpuMemoryUsed(IEnumerable<SensorDescriptor> sensors, string hardwareId)
    {
        var candidates = ForGpu(sensors, hardwareId, SensorMetricType.MemoryUsed).ToArray();
        var selected = FirstByNames(
            candidates,
            "gpumemoryused",
            "gpumemoryusage",
            "d3ddedicatedmemoryused",
            "dedicatedmemoryused",
            "memoryused");
        return selected is null ? SensorSelection.Empty : Single(selected, "GPU memory used");
    }

    public SensorSelection SelectGpuMemoryTotal(IEnumerable<SensorDescriptor> sensors, string hardwareId)
    {
        var candidates = ForGpu(sensors, hardwareId, SensorMetricType.MemoryTotal).ToArray();
        var selected = FirstByNames(
            candidates,
            "gpumemorytotal",
            "dedicatedmemorytotal",
            "memorytotal");
        return selected is null ? SensorSelection.Empty : Single(selected, "GPU memory total");
    }

    private static IEnumerable<SensorDescriptor> ForGpu(
        IEnumerable<SensorDescriptor> sensors,
        string hardwareId,
        SensorMetricType metric) =>
        sensors.Where(sensor =>
            sensor.HardwareId == hardwareId &&
            sensor.MetricType == metric);

    private static SensorDescriptor? FirstByNames(
        IReadOnlyList<SensorDescriptor> candidates,
        params string[] normalizedNames)
    {
        foreach (var name in normalizedNames)
        {
            var exact = candidates.FirstOrDefault(sensor => Normalize(sensor.Name) == name);
            if (exact is not null)
            {
                return exact;
            }
        }

        foreach (var name in normalizedNames)
        {
            var contains = candidates.FirstOrDefault(sensor => Normalize(sensor.Name).Contains(name));
            if (contains is not null)
            {
                return contains;
            }
        }

        return null;
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static SensorSelection Single(SensorDescriptor sensor, string description) =>
        new([sensor.SensorId], $"{description}: {sensor.Name}");

    [GeneratedRegex(@"(^|\b)(cpu\s*)?core[\s#]*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex IndividualCoreRegex();
}
