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
            Language = AppLanguage.Japanese,
            SelectedGpuId = "gpu-1",
            IsWidgetSizeFixed = false,
            HasFlexibleWidgetSize = true,
            FlexibleWidgetWidth = 640,
            FlexibleWidgetHeight = 120,
            SuppressPawnIoInstallPrompt = true
        };

        await service.SaveAsync(settings);
        var reloaded = new SettingsService(logger, environment.SettingsDirectory);
        var result = await reloaded.LoadAsync();

        Assert.Equal(17, result.FontSize);
        Assert.Equal(2000, result.UpdateIntervalMilliseconds);
        Assert.Equal(TemperatureUnit.Fahrenheit, result.TemperatureUnit);
        Assert.Equal(AppLanguage.Japanese, result.Language);
        Assert.Equal("gpu-1", result.SelectedGpuId);
        Assert.False(result.IsWidgetSizeFixed);
        Assert.True(result.HasFlexibleWidgetSize);
        Assert.Equal(640, result.FlexibleWidgetWidth);
        Assert.Equal(120, result.FlexibleWidgetHeight);
        Assert.True(result.SuppressPawnIoInstallPrompt);
    }

    [Fact]
    public async Task LegacySettingsWithoutLanguage_UseTheSystemLanguageDefault()
    {
        using var environment = new TestEnvironment();
        Directory.CreateDirectory(environment.SettingsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(environment.SettingsDirectory, "settings.json"),
            """{"SchemaVersion":1,"FontSize":15}""");
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var service = new SettingsService(logger, environment.SettingsDirectory);

        var result = await service.LoadAsync();

        Assert.Equal(AppLanguageDefaults.Detect(), result.Language);
        Assert.Equal(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
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
            FlexibleWidgetWidth = double.PositiveInfinity,
            FlexibleWidgetHeight = -100,
            BackgroundOpacity = -1,
            DecimalDigits = 9
        };

        await service.SaveAsync(settings);

        Assert.Equal(1000, service.Current.UpdateIntervalMilliseconds);
        Assert.Equal(30, service.Current.FontSize);
        Assert.Equal(420, service.Current.FlexibleWidgetWidth);
        Assert.Equal(54, service.Current.FlexibleWidgetHeight);
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
