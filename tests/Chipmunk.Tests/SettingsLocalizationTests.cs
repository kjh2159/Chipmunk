using System.Windows;
using Chipmunk.Models;
using Chipmunk.Services;
using Chipmunk.ViewModels;

namespace Chipmunk.Tests;

public sealed class SettingsLocalizationTests
{
    [Fact]
    public async Task LanguagePreview_RefreshesEveryLocalizedDropDown()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var settings = new SettingsService(logger, environment.SettingsDirectory);
        await settings.SaveAsync(new AppSettings { Language = AppLanguage.Korean });
        await using var monitoring = new MockHardwareMonitoringService();
        var localization = new FakeLocalizationService(AppLanguage.Korean);
        using var viewModel = new SettingsViewModel(
            settings,
            new FakeStartupService(),
            monitoring,
            new FakeWindowPositionService(),
            localization);

        viewModel.SelectedLanguage = AppLanguage.English;

        Assert.Equal("English:OptionGpuAutomatic", viewModel.GpuOptions[0].DisplayName);
        Assert.Equal("English:OptionPrimaryMonitor", viewModel.MonitorOptions[0].DisplayName);
        Assert.Equal("English:OptionSecond:1", viewModel.UpdateIntervals[1].DisplayName);
        Assert.Equal("English:OptionCelsius", viewModel.TemperatureUnits[0].DisplayName);
        Assert.Equal("English:OptionOneLine", viewModel.Layouts[0].DisplayName);
        Assert.Equal("English:OptionThreeLines", viewModel.Layouts[2].DisplayName);
        Assert.Equal("English:OptionSystemTheme", viewModel.Themes[0].DisplayName);
        Assert.Equal("English:OptionTaskManager", viewModel.DoubleClickActions[0].DisplayName);
    }

    [Fact]
    public async Task SavingAnotherLayout_DiscardsDimensionsMeasuredForTheOldLayout()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var settings = new SettingsService(logger, environment.SettingsDirectory);
        await settings.SaveAsync(new AppSettings
        {
            Layout = WidgetLayout.OneLine,
            IsWidgetSizeFixed = false,
            HasFlexibleWidgetSize = true,
            FlexibleWidgetWidth = 900
        });
        await using var monitoring = new MockHardwareMonitoringService();
        using var viewModel = new SettingsViewModel(
            settings,
            new FakeStartupService(),
            monitoring,
            new FakeWindowPositionService(),
            new FakeLocalizationService(AppLanguage.English));
        viewModel.Draft.Layout = WidgetLayout.ThreeLines;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(WidgetLayout.ThreeLines, settings.Current.Layout);
        Assert.False(settings.Current.HasFlexibleWidgetSize);
    }

    [Fact]
    public async Task ClosingWithoutSaving_RestoresTheOriginalLanguage()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var settings = new SettingsService(logger, environment.SettingsDirectory);
        await settings.SaveAsync(new AppSettings { Language = AppLanguage.Korean });
        await using var monitoring = new MockHardwareMonitoringService();
        var localization = new FakeLocalizationService(AppLanguage.Korean);
        var viewModel = new SettingsViewModel(
            settings,
            new FakeStartupService(),
            monitoring,
            new FakeWindowPositionService(),
            localization);

        viewModel.SelectedLanguage = AppLanguage.Spanish;
        viewModel.Dispose();

        Assert.Equal(AppLanguage.Korean, localization.CurrentLanguage);
    }

    [Fact]
    public async Task RestoreDefaults_ResetsThresholdNumbersAndColors()
    {
        using var environment = new TestEnvironment();
        using var logger = new RateLimitedFileLogger(environment.LogDirectory);
        var settings = new SettingsService(logger, environment.SettingsDirectory);
        await settings.SaveAsync(new AppSettings
        {
            Language = AppLanguage.English,
            Thresholds = new ThresholdSettings
            {
                TemperatureWarning = 74,
                TemperatureWarningColor = "#FF010101",
                TemperatureCriticalColor = "#FF020202",
                UsageWarningColor = "#FF030303",
                UsageCriticalColor = "#FF040404"
            }
        });
        await using var monitoring = new MockHardwareMonitoringService();
        using var viewModel = new SettingsViewModel(
            settings,
            new FakeStartupService(),
            monitoring,
            new FakeWindowPositionService(),
            new FakeLocalizationService(AppLanguage.English));

        viewModel.RestoreDefaultsCommand.Execute(null);

        Assert.Equal(70, viewModel.Draft.Thresholds.TemperatureWarning);
        Assert.Equal(ThresholdSettings.DefaultWarningColor, viewModel.Draft.Thresholds.TemperatureWarningColor);
        Assert.Equal(ThresholdSettings.DefaultCriticalColor, viewModel.Draft.Thresholds.TemperatureCriticalColor);
        Assert.Equal(ThresholdSettings.DefaultWarningColor, viewModel.Draft.Thresholds.UsageWarningColor);
        Assert.Equal(ThresholdSettings.DefaultCriticalColor, viewModel.Draft.Thresholds.UsageCriticalColor);
    }

    private sealed class FakeLocalizationService(AppLanguage initialLanguage)
        : ILocalizationService
    {
        public AppLanguage CurrentLanguage { get; private set; } = initialLanguage;
        public event Action<AppLanguage>? LanguageChanged;

        public void Apply(AppLanguage language)
        {
            if (CurrentLanguage == language)
            {
                return;
            }

            CurrentLanguage = language;
            LanguageChanged?.Invoke(language);
        }

        public string Get(string key) => $"{CurrentLanguage}:{key}";

        public string Format(string key, params object?[] arguments) =>
            $"{Get(key)}:{string.Join(':', arguments)}";
    }

    private sealed class FakeStartupService : IStartupService
    {
        public bool IsEnabled() => false;
        public void SetEnabled(bool enabled)
        {
        }
    }

    private sealed class FakeWindowPositionService : IWindowPositionService
    {
        public IReadOnlyList<MonitorDescriptor> GetMonitors() => [];
        public void PositionWindow(Window window, AppSettings settings, bool forceDefault = false)
        {
        }

        public void SaveCurrentPosition(Window window, AppSettings settings)
        {
        }

        public void ApplyClickThrough(Window window, bool enabled)
        {
        }

        public void ApplyToolWindowStyle(Window window)
        {
        }
    }
}
