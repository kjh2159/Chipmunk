using CommunityToolkit.Mvvm.ComponentModel;
using Chipmunk.Models;
using Chipmunk.Services;

namespace Chipmunk.ViewModels;

public sealed partial class WidgetViewModel : ObservableObject, IDisposable
{
    private readonly IHardwareMonitoringService _monitoringService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localization;
    private MonitoringSnapshot _lastSnapshot = MonitoringSnapshot.Empty;

    [ObservableProperty]
    private string _displayText = "Chipmunk";

    [ObservableProperty]
    private string _toolTipText = "Chipmunk";

    [ObservableProperty]
    private Severity _overallSeverity = Severity.Unavailable;

    [ObservableProperty]
    private SeverityVisualState _overallAlertState =
        new(Severity.Unavailable, ThresholdKind.None, null);

    [ObservableProperty]
    private double _widgetFontSize = 13;

    [ObservableProperty]
    private double _backgroundOpacity = 0.86;

    [ObservableProperty]
    private bool _alwaysOnTop = true;

    [ObservableProperty]
    private bool _clickThrough;

    public WidgetViewModel(
        IHardwareMonitoringService monitoringService,
        ISettingsService settingsService,
        ILocalizationService localization)
    {
        _monitoringService = monitoringService;
        _settingsService = settingsService;
        _localization = localization;
        DisplayText = localization.Get("WidgetSensorSearching");
        _monitoringService.SnapshotUpdated += OnSnapshotUpdated;
        _settingsService.SettingsChanged += OnSettingsChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        ApplySettings(_settingsService.Current);
    }

    public MonitoringSnapshot LastSnapshot => _lastSnapshot;

    private void OnSnapshotUpdated(MonitoringSnapshot snapshot)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));
        }
        else
        {
            ApplySnapshot(snapshot);
        }
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() =>
            {
                ApplySettings(settings);
                ApplySnapshot(_lastSnapshot);
            });
        }
        else
        {
            ApplySettings(settings);
            ApplySnapshot(_lastSnapshot);
        }
    }

    private void OnLanguageChanged(AppLanguage language)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => ApplySnapshot(_lastSnapshot));
        }
        else
        {
            ApplySnapshot(_lastSnapshot);
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        WidgetFontSize = settings.FontSize;
        BackgroundOpacity = settings.BackgroundOpacity;
        AlwaysOnTop = settings.AlwaysOnTop;
        ClickThrough = settings.ClickThrough;
    }

    private void ApplySnapshot(MonitoringSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        OnPropertyChanged(nameof(LastSnapshot));
        var settings = _settingsService.Current;
        var gpu = GpuSelectionPolicy.Select(snapshot.Gpus, settings.SelectedGpuId);
        var sections = new List<string>();
        var alertCandidates = new List<AlertCandidate>();

        var cpuParts = new List<string>();
        if (settings.ShowCpuTemperature)
        {
            cpuParts.Add(FormatTemperature(snapshot.CpuTemperatureCelsius, settings));
            alertCandidates.Add(AlertStateResolver.ForTemperature(
                snapshot.CpuTemperatureCelsius,
                settings.Thresholds));
        }

        if (settings.ShowCpuUsage)
        {
            cpuParts.Add(FormatPercent(snapshot.CpuUsagePercent, settings.DecimalDigits));
            alertCandidates.Add(AlertStateResolver.ForUsage(
                snapshot.CpuUsagePercent,
                settings.Thresholds));
        }

        if (cpuParts.Count > 0)
        {
            sections.Add($"CPU {string.Join(" · ", cpuParts)}");
        }

        var gpuParts = new List<string>();
        if (settings.ShowGpuTemperature)
        {
            gpuParts.Add(FormatTemperature(gpu?.TemperatureCelsius, settings));
            alertCandidates.Add(AlertStateResolver.ForTemperature(
                gpu?.TemperatureCelsius,
                settings.Thresholds));
        }

        if (settings.ShowGpuUsage)
        {
            gpuParts.Add(FormatPercent(gpu?.UsagePercent, settings.DecimalDigits));
            alertCandidates.Add(AlertStateResolver.ForUsage(
                gpu?.UsagePercent,
                settings.Thresholds));
        }

        if (settings.ShowGpuMemory)
        {
            gpuParts.Add(FormatMemoryPair(
                gpu?.MemoryUsedBytes,
                gpu?.MemoryTotalBytes,
                settings.DecimalDigits));
        }

        if (gpuParts.Count > 0)
        {
            sections.Add($"GPU {string.Join(" · ", gpuParts)}");
        }

        if (settings.ShowSystemMemory)
        {
            var ramPercent = MemoryFormatter.Percentage(
                snapshot.SystemMemoryUsedBytes,
                snapshot.SystemMemoryTotalBytes);
            sections.Add(
                $"RAM {FormatMemoryPair(snapshot.SystemMemoryUsedBytes, snapshot.SystemMemoryTotalBytes, settings.DecimalDigits)}" +
                $" · {FormatPercent(ramPercent, settings.DecimalDigits)}");
            alertCandidates.Add(AlertStateResolver.ForUsage(ramPercent, settings.Thresholds));
        }

        if (sections.Count == 0)
        {
            DisplayText = _localization.Get("WidgetNoSensorsSelected");
        }
        else if (settings.Layout == WidgetLayout.OneLine || sections.Count == 1)
        {
            DisplayText = string.Join("   ", sections);
        }
        else if (settings.Layout == WidgetLayout.ThreeLines)
        {
            // Every hardware category owns one visual row. Individual section
            // strings never contain line breaks, so GPU details cannot wrap.
            DisplayText = string.Join(Environment.NewLine, sections);
        }
        else
        {
            var firstLine = string.Join("   ", sections.Take(2));
            var secondLine = string.Join("   ", sections.Skip(2));
            DisplayText = string.IsNullOrWhiteSpace(secondLine)
                ? firstLine
                : firstLine + Environment.NewLine + secondLine;
        }

        OverallAlertState = AlertStateResolver.Resolve(alertCandidates, settings.Thresholds);
        OverallSeverity = OverallAlertState.Severity;
        var timestamp = snapshot.Timestamp == DateTimeOffset.MinValue
            ? "--:--:--"
            : snapshot.Timestamp.ToString("T");
        ToolTipText = _localization.Format(
            "WidgetUpdated",
            gpu?.Name ?? _localization.Get("WidgetGpuUnavailable"),
            timestamp);
        if (snapshot.LastError is not null)
        {
            ToolTipText += Environment.NewLine + _localization.Get("WidgetSensorReadError");
        }
    }

    private static string FormatTemperature(double? celsius, AppSettings settings)
    {
        if (celsius is null)
        {
            return "N/A";
        }

        var value = settings.TemperatureUnit == TemperatureUnit.Fahrenheit
            ? celsius.Value * 9d / 5 + 32
            : celsius.Value;
        var symbol = settings.TemperatureUnit == TemperatureUnit.Fahrenheit ? "°F" : "°C";
        return $"{value.ToString($"F{settings.DecimalDigits}")}{symbol}";
    }

    private static string FormatPercent(double? value, int digits) =>
        value is null ? "N/A" : $"{value.Value.ToString($"F{digits}")}%";

    private static string FormatMemoryPair(double? used, double? total, int digits)
    {
        if (used is null || total is null || total <= 0)
        {
            return "N/A";
        }

        return $"{MemoryFormatter.BytesToGibibytes(used.Value).ToString($"F{digits}")}/" +
               $"{MemoryFormatter.BytesToGibibytes(total.Value).ToString($"F{digits}")} GB";
    }

    public void Dispose()
    {
        _monitoringService.SnapshotUpdated -= OnSnapshotUpdated;
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
    }
}
