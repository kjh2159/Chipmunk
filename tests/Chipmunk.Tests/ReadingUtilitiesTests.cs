using Chipmunk.Models;
using Chipmunk.Services;

namespace Chipmunk.Tests;

public sealed class ReadingUtilitiesTests
{
    private readonly ThresholdSettings _thresholds = new();

    [Theory]
    [InlineData(69, Severity.Normal)]
    [InlineData(70, Severity.Warning)]
    [InlineData(84.9, Severity.Warning)]
    [InlineData(85, Severity.Critical)]
    public void TemperatureSeverity_UsesConfiguredBoundaries(double value, Severity expected)
    {
        Assert.Equal(expected, SeverityClassifier.ForTemperature(value, _thresholds));
    }

    [Theory]
    [InlineData(79, Severity.Normal)]
    [InlineData(80, Severity.Warning)]
    [InlineData(94.9, Severity.Warning)]
    [InlineData(95, Severity.Critical)]
    public void UsageSeverity_UsesConfiguredBoundaries(double value, Severity expected)
    {
        Assert.Equal(expected, SeverityClassifier.ForUsage(value, _thresholds));
    }

    [Fact]
    public void NullReadings_AreUnavailable()
    {
        Assert.Equal(Severity.Unavailable, SeverityClassifier.ForTemperature(null, _thresholds));
        Assert.Equal(Severity.Unavailable, SeverityClassifier.ForUsage(null, _thresholds));
        Assert.Null(MemoryFormatter.Percentage(null, 32));
        Assert.Null(MemoryFormatter.Percentage(14, null));
    }

    [Fact]
    public void MemoryConversion_UsesBinaryGigabytes()
    {
        const double sixteenGib = 16d * 1024 * 1024 * 1024;

        Assert.Equal(16, MemoryFormatter.BytesToGibibytes(sixteenGib), 6);
        Assert.Equal(50, MemoryFormatter.Percentage(8, 16));
    }

    [Fact]
    public void GpuSelection_UsesExplicitGpuThenHighestActiveGpu()
    {
        var integrated = Gpu("intel", "Intel", 85);
        var discrete = Gpu("nvidia", "NVIDIA", 30);

        Assert.Equal("nvidia", GpuSelectionPolicy.Select([integrated, discrete], "nvidia")?.DeviceId);
        Assert.Equal("intel", GpuSelectionPolicy.Select([integrated, discrete], null)?.DeviceId);
    }

    private static GpuReading Gpu(string id, string name, double usage) =>
        new(id, name, HardwareKind.GpuNvidia, 50, usage, null, null);
}
