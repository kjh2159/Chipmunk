using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Chipmunk.Models;
using Chipmunk.Services;

namespace Chipmunk.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;

    [ObservableProperty]
    private AppSettings _draft;

    [ObservableProperty]
    private GpuOption? _selectedGpu;

    [ObservableProperty]
    private string? _errorMessage;

    public SettingsViewModel(
        ISettingsService settingsService,
        IStartupService startupService,
        IHardwareMonitoringService monitoringService,
        IWindowPositionService windowPositionService)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        Draft = settingsService.Current.Clone();
        Draft.StartWithWindows = startupService.IsEnabled();
        GpuOptions = new ObservableCollection<GpuOption>(
        [
            new GpuOption(null, "자동 선택 (사용률이 가장 높은 GPU)"),
            .. monitoringService.Devices
                .Where(device => device.Kind is HardwareKind.GpuNvidia or HardwareKind.GpuAmd or HardwareKind.GpuIntel)
                .Select(device => new GpuOption(device.DeviceId, device.Name))
        ]);
        SelectedGpu = GpuOptions.FirstOrDefault(option => option.Id == Draft.SelectedGpuId)
                      ?? GpuOptions[0];
        MonitorOptions = new ObservableCollection<MonitorOption>(
        [
            new MonitorOption(null, "주 모니터"),
            .. windowPositionService.GetMonitors()
                .Select(monitor => new MonitorOption(monitor.DeviceName, monitor.DisplayName))
        ]);

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
    }

    public ObservableCollection<GpuOption> GpuOptions { get; }
    public ObservableCollection<MonitorOption> MonitorOptions { get; }
    public IReadOnlyList<int> UpdateIntervals { get; } = [500, 1000, 2000, 5000];
    public IReadOnlyList<TemperatureUnit> TemperatureUnits { get; } =
        Enum.GetValues<TemperatureUnit>();
    public IReadOnlyList<WidgetLayout> Layouts { get; } = Enum.GetValues<WidgetLayout>();
    public IReadOnlyList<WidgetTheme> Themes { get; } = Enum.GetValues<WidgetTheme>();
    public IReadOnlyList<DoubleClickAction> DoubleClickActions { get; } =
        Enum.GetValues<DoubleClickAction>();

    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand RestoreDefaultsCommand { get; }
    public event Action<bool>? CloseRequested;

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
            ErrorMessage = $"설정 내보내기 실패: {exception.Message}";
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            Draft.SelectedGpuId = SelectedGpu?.Id;
            Draft.Normalize();
            _startupService.SetEnabled(Draft.StartWithWindows);
            await _settingsService.SaveAsync(Draft);
            ErrorMessage = null;
            CloseRequested?.Invoke(true);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"설정을 저장하지 못했습니다: {exception.Message}";
        }
    }

    private void RestoreDefaults()
    {
        Draft = new AppSettings();
        SelectedGpu = GpuOptions[0];
        ErrorMessage = "기본값을 불러왔습니다. 적용을 눌러 저장하세요.";
    }
}

public sealed record GpuOption(string? Id, string DisplayName);
public sealed record MonitorOption(string? DeviceName, string DisplayName);
