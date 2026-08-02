using Chipmunk.Models;
using Chipmunk.Services;

namespace Chipmunk.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsSettings()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var service = new SettingsService(logger, environment.SettingsDirectory);
        var settings = new AppSettings
        {
            FontSize = 17,
            UpdateIntervalMilliseconds = 2000,
            TemperatureUnit = TemperatureUnit.Fahrenheit,
            SelectedGpuId = "gpu-1",
            SuppressPawnIoInstallPrompt = true
        };

        await service.SaveAsync(settings);
        var reloaded = new SettingsService(logger, environment.SettingsDirectory);
        var result = await reloaded.LoadAsync();

        Assert.Equal(17, result.FontSize);
        Assert.Equal(2000, result.UpdateIntervalMilliseconds);
        Assert.Equal(TemperatureUnit.Fahrenheit, result.TemperatureUnit);
        Assert.Equal("gpu-1", result.SelectedGpuId);
        Assert.True(result.SuppressPawnIoInstallPrompt);
    }

    [Fact]
    public async Task CorruptJson_RecoversDefaultsAndPreservesBackup()
    {
        using var environment = new TestEnvironment();
        Directory.CreateDirectory(environment.SettingsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(environment.SettingsDirectory, "settings.json"),
            "{ this is not json");
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var service = new SettingsService(logger, environment.SettingsDirectory);

        var result = await service.LoadAsync();

        Assert.Equal(1000, result.UpdateIntervalMilliseconds);
        Assert.NotEmpty(Directory.GetFiles(environment.SettingsDirectory, "*.corrupt-*"));
    }

    [Fact]
    public async Task InvalidValues_AreNormalized()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var service = new SettingsService(logger, environment.SettingsDirectory);
        var settings = new AppSettings
        {
            UpdateIntervalMilliseconds = 123,
            FontSize = 100,
            BackgroundOpacity = -1,
            DecimalDigits = 9
        };

        await service.SaveAsync(settings);

        Assert.Equal(1000, service.Current.UpdateIntervalMilliseconds);
        Assert.Equal(30, service.Current.FontSize);
        Assert.Equal(0.2, service.Current.BackgroundOpacity);
        Assert.Equal(2, service.Current.DecimalDigits);
    }

    [Fact]
    public async Task Export_WritesTheProvidedDraftWithoutChangingCurrentSettings()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var service = new SettingsService(logger, environment.SettingsDirectory);
        await service.LoadAsync();
        var exportPath = Path.Combine(environment.Root, "export.json");
        var draft = service.Current.Clone();
        draft.FontSize = 19;

        await service.ExportAsync(draft, exportPath);

        var exported = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("\"FontSize\": 19", exported);
        Assert.Equal(13, service.Current.FontSize);
    }
}
