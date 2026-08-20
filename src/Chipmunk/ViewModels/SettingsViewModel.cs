using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Chipmunk.Models;
using Chipmunk.Services;

namespace Chipmunk.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly ILocalizationService _localization;
    private readonly AppLanguage _originalLanguage;
    private bool _suppressLanguagePreview = true;
    private bool _saved;
    private bool _disposed;

    [ObservableProperty]
    private AppSettings _draft;

    [ObservableProperty]
    private GpuOption? _selectedGpu;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private AppLanguage _selectedLanguage;

    public SettingsViewModel(
        ISettingsService settingsService,
        IStartupService startupService,
        IHardwareMonitoringService monitoringService,
        IWindowPositionService windowPositionService,
        ILocalizationService localization)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        _localization = localization;
        Draft = settingsService.Current.Clone();
        _originalLanguage = Draft.Language;
        SelectedLanguage = Draft.Language;
        Draft.StartWithWindows = startupService.IsEnabled();
        GpuOptions = new ObservableCollection<GpuOption>(
        [
            new GpuOption(null, localization.Get("OptionGpuAutomatic")),
            .. monitoringService.Devices
                .Where(device => device.Kind is HardwareKind.GpuNvidia or HardwareKind.GpuAmd or HardwareKind.GpuIntel)
                .Select(device => new GpuOption(device.DeviceId, device.Name))
        ]);
        SelectedGpu = GpuOptions.FirstOrDefault(option => option.Id == Draft.SelectedGpuId)
                      ?? GpuOptions[0];
        MonitorOptions = new ObservableCollection<MonitorOption>(
        [
            new MonitorOption(null, localization.Get("OptionPrimaryMonitor")),
            .. windowPositionService.GetMonitors()
                .Select(monitor => new MonitorOption(monitor.DeviceName, monitor.DisplayName))
        ]);

        LanguageOptions =
        [
            new(AppLanguage.English, "English"),
            new(AppLanguage.Korean, "한국어"),
            new(AppLanguage.Japanese, "日本語"),
            new(AppLanguage.ChineseSimplified, "简体中文"),
            new(AppLanguage.Spanish, "Español")
        ];
        RefreshLocalizedOptions();
        _localization.LanguageChanged += OnLanguageChanged;
        _suppressLanguagePreview = false;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
    }

    public ObservableCollection<GpuOption> GpuOptions { get; }
    public ObservableCollection<MonitorOption> MonitorOptions { get; }
    public IReadOnlyList<LocalizedOption<AppLanguage>> LanguageOptions { get; }
    public IReadOnlyList<LocalizedOption<int>> UpdateIntervals { get; private set; } = [];
    public IReadOnlyList<LocalizedOption<TemperatureUnit>> TemperatureUnits { get; private set; } = [];
    public IReadOnlyList<LocalizedOption<WidgetLayout>> Layouts { get; private set; } = [];
    public IReadOnlyList<LocalizedOption<WidgetTheme>> Themes { get; private set; } = [];
    public IReadOnlyList<LocalizedOption<DoubleClickAction>> DoubleClickActions { get; private set; } = [];

    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand RestoreDefaultsCommand { get; }
    public event Action<bool>? CloseRequested;

    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        if (_suppressLanguagePreview)
        {
            return;
        }

        Draft.Language = value;
        _localization.Apply(value);
    }

    public async Task ExportAsync(string path)
    {
        try
        {
            Draft.SelectedGpuId = SelectedGpu?.Id;
            await _settingsService.ExportAsync(Draft, path);
            ErrorMessage = null;
        }
        catch (Exception exception)
        {
            ErrorMessage = _localization.Format("SettingsExportFailed", exception.Message);
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            Draft.SelectedGpuId = SelectedGpu?.Id;
            Draft.Language = SelectedLanguage;
            Draft.Normalize();
            _startupService.SetEnabled(Draft.StartWithWindows);
            await _settingsService.SaveAsync(Draft);
            _saved = true;
            ErrorMessage = null;
            CloseRequested?.Invoke(true);
        }
        catch (Exception exception)
        {
            ErrorMessage = _localization.Format("SettingsSaveFailed", exception.Message);
        }
    }

    private void RestoreDefaults()
    {
        Draft = new AppSettings { Language = SelectedLanguage };
        SelectedGpu = GpuOptions[0];
        ErrorMessage = _localization.Get("SettingsDefaultsLoaded");
    }

    private void OnLanguageChanged(AppLanguage language) => RefreshLocalizedOptions();

    private void RefreshLocalizedOptions()
    {
        var selectedGpuId = SelectedGpu?.Id ?? Draft.SelectedGpuId;
        GpuOptions[0] = new GpuOption(null, _localization.Get("OptionGpuAutomatic"));
        SelectedGpu = GpuOptions.FirstOrDefault(option => option.Id == selectedGpuId)
                      ?? GpuOptions[0];
        MonitorOptions[0] = new MonitorOption(null, _localization.Get("OptionPrimaryMonitor"));

        UpdateIntervals =
        [
            new(500, _localization.Format("OptionSeconds", 0.5)),
            new(1000, _localization.Format("OptionSecond", 1)),
            new(2000, _localization.Format("OptionSeconds", 2)),
            new(5000, _localization.Format("OptionSeconds", 5))
        ];
        TemperatureUnits =
        [
            new(TemperatureUnit.Celsius, _localization.Get("OptionCelsius")),
            new(TemperatureUnit.Fahrenheit, _localization.Get("OptionFahrenheit"))
        ];
        Layouts =
        [
            new(WidgetLayout.OneLine, _localization.Get("OptionOneLine")),
            new(WidgetLayout.TwoLines, _localization.Get("OptionTwoLines"))
        ];
        Themes =
        [
            new(WidgetTheme.System, _localization.Get("OptionSystemTheme")),
            new(WidgetTheme.Dark, _localization.Get("OptionDarkTheme")),
            new(WidgetTheme.Light, _localization.Get("OptionLightTheme"))
        ];
        DoubleClickActions =
        [
            new(DoubleClickAction.TaskManager, _localization.Get("OptionTaskManager")),
            new(DoubleClickAction.DetailedMonitor, _localization.Get("OptionDetailedMonitor"))
        ];

        OnPropertyChanged(nameof(UpdateIntervals));
        OnPropertyChanged(nameof(TemperatureUnits));
        OnPropertyChanged(nameof(Layouts));
        OnPropertyChanged(nameof(Themes));
        OnPropertyChanged(nameof(DoubleClickActions));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= OnLanguageChanged;
        if (!_saved)
        {
            _localization.Apply(_originalLanguage);
        }
    }
}

public sealed record GpuOption(string? Id, string DisplayName);
public sealed record MonitorOption(string? DeviceName, string DisplayName);
public sealed record LocalizedOption<T>(T Value, string DisplayName);
