using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Chipmunk.Models;
using Chipmunk.Services;
using Chipmunk.ViewModels;
using Chipmunk.Views;

namespace Chipmunk;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource _applicationCancellation = new();
    private IRateLimitedLogger? _logger;
    private ISingleInstanceService? _singleInstance;
    private ISettingsService? _settings;
    private IStartupService? _startup;
    private IThemeService? _theme;
    private IWindowPositionService? _position;
    private IHardwareMonitoringService? _monitoring;
    private IPawnIoService? _pawnIo;
    private WidgetViewModel? _widgetViewModel;
    private WidgetWindow? _widget;
    private SettingsWindow? _settingsWindow;
    private DetailedMonitorWindow? _detailsWindow;
    private TrayIconService? _tray;
    private bool _exitStarted;
    private int _missingCpuTemperatureSamples;
    private int _pawnIoPromptHandled;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _logger = new RateLimitedFileLogger();
        _singleInstance = new SingleInstanceService();

        if (!_singleInstance.TryAcquire())
        {
            _singleInstance.SignalFirstInstance();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _settings = new SettingsService(_logger);
            await _settings.LoadAsync(_applicationCancellation.Token);
            _startup = new StartupService();
            _theme = new ThemeService();
            _position = new WindowPositionService();
            var discovery = new SensorDiscoveryService();
            _monitoring = new HardwareMonitoringService(discovery, _logger)
            {
                UpdateInterval = TimeSpan.FromMilliseconds(
                    _settings.Current.UpdateIntervalMilliseconds)
            };
            _pawnIo = new PawnIoService(_logger);

            _theme.Apply(_settings.Current.Theme);
            _widgetViewModel = new WidgetViewModel(_monitoring, _settings);
            _widget = new WidgetWindow(
                _widgetViewModel,
                _position,
                _settings,
                ExecuteDoubleClickAction);
            _widget.Closing += OnWidgetClosing;
            _widget.Show();
            if (!_settings.Current.WidgetVisible)
            {
                _widget.Hide();
            }

            _tray = new TrayIconService(
                ToggleWidget,
                ShowSettings,
                ShowDetails,
                RescanSensorsAsync,
                SetClickThroughAsync,
                ResetPositionAsync,
                ExitApplicationAsync);
            _tray.Synchronize(_settings.Current.WidgetVisible, _settings.Current.ClickThrough);
            _widgetViewModel.PropertyChanged += OnWidgetViewModelPropertyChanged;
            _settings.SettingsChanged += OnSettingsChanged;
            _monitoring.SnapshotUpdated += OnMonitoringSnapshotUpdated;

            _singleInstance.ActivationRequested += OnActivationRequested;
            _singleInstance.StartListening(_applicationCancellation.Token);
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            await _monitoring.StartAsync(_applicationCancellation.Token);
            _logger.Info("Chipmunk가 시작되었습니다.");
        }
        catch (Exception exception)
        {
            _logger.Error("startup", "애플리케이션을 초기화하지 못했습니다.", exception);
            System.Windows.MessageBox.Show(
                $"Chipmunk를 시작하지 못했습니다.{Environment.NewLine}{exception.Message}",
                "시작 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        if (_monitoring is not null)
        {
            _monitoring.UpdateInterval = TimeSpan.FromMilliseconds(settings.UpdateIntervalMilliseconds);
        }

        _theme?.Apply(settings.Theme);
        _tray?.Synchronize(settings.WidgetVisible, settings.ClickThrough);
        if (_widget is not null)
        {
            _widget.Topmost = settings.AlwaysOnTop;
            _widget.RefreshWindowStylesAndPosition();
        }
    }

    private void ToggleWidget()
    {
        if (_widget is null || _settings is null)
        {
            return;
        }

        var draft = _settings.Current.Clone();
        draft.WidgetVisible = !_widget.IsVisible;
        if (draft.WidgetVisible)
        {
            _widget.Show();
            _widget.RefreshWindowStylesAndPosition();
        }
        else
        {
            _widget.Hide();
        }

        _ = SaveSettingsSafelyAsync(draft);
    }

    private void ShowSettings()
    {
        if (_settings is null || _startup is null || _monitoring is null || _position is null)
        {
            return;
        }

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var viewModel = new SettingsViewModel(_settings, _startup, _monitoring, _position);
        _settingsWindow = new SettingsWindow(viewModel);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ShowDetails()
    {
        if (_widgetViewModel is null)
        {
            return;
        }

        if (_detailsWindow is { IsVisible: true })
        {
            _detailsWindow.Activate();
            return;
        }

        _detailsWindow = new DetailedMonitorWindow(_widgetViewModel);
        _detailsWindow.Closed += (_, _) => _detailsWindow = null;
        _detailsWindow.Show();
        _detailsWindow.Activate();
    }

    private void ExecuteDoubleClickAction()
    {
        if (_settings?.Current.DoubleClickAction == DoubleClickAction.DetailedMonitor)
        {
            ShowDetails();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            _logger?.Error("task-manager", "작업 관리자를 실행하지 못했습니다.", exception);
        }
    }

    private async Task RescanSensorsAsync()
    {
        if (_monitoring is null)
        {
            return;
        }

        try
        {
            await _monitoring.RescanAsync(_applicationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Application is closing.
        }
    }

    private void OnMonitoringSnapshotUpdated(MonitoringSnapshot snapshot)
    {
        if (snapshot.CpuTemperatureCelsius is not null)
        {
            Interlocked.Exchange(ref _missingCpuTemperatureSamples, 0);
            return;
        }

        if (Interlocked.Increment(ref _missingCpuTemperatureSamples) < 3 ||
            Volatile.Read(ref _pawnIoPromptHandled) != 0 ||
            _pawnIo is null ||
            _settings is null)
        {
            return;
        }

        var status = _pawnIo.GetStatus();
        if (!PawnIoPromptPolicy.ShouldOfferInstallation(status, snapshot, _settings.Current))
        {
            Interlocked.Exchange(ref _pawnIoPromptHandled, 1);
            return;
        }

        if (Interlocked.CompareExchange(ref _pawnIoPromptHandled, 1, 0) == 0)
        {
            _ = Dispatcher.BeginInvoke(() => _ = ShowPawnIoConsentAsync());
        }
    }

    private async Task ShowPawnIoConsentAsync()
    {
        if (_pawnIo is null || _settings is null)
        {
            return;
        }

        var consentWindow = new PawnIoConsentWindow();
        consentWindow.ShowDialog();

        if (consentWindow.Choice == PawnIoConsentChoice.NeverAskAgain)
        {
            var updated = _settings.Current.Clone();
            updated.SuppressPawnIoInstallPrompt = true;
            await SaveSettingsSafelyAsync(updated);
            return;
        }

        if (consentWindow.Choice != PawnIoConsentChoice.Install)
        {
            return;
        }

        var result = await _pawnIo.InstallWithConsentAsync(_applicationCancellation.Token);
        switch (result.Outcome)
        {
            case PawnIoInstallOutcome.Installed:
                System.Windows.MessageBox.Show(
                    "PawnIO 설치가 완료되었습니다.\n" +
                    "프로그램을 종료한 뒤 다시 실행하면 CPU 센서가 새 드라이버로 초기화됩니다.",
                    "PawnIO 설치 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                break;

            case PawnIoInstallOutcome.RebootRequired:
                System.Windows.MessageBox.Show(
                    "PawnIO 설치가 완료되었으며 Windows 재시작이 필요합니다.",
                    "재시작 필요",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                break;

            case PawnIoInstallOutcome.Cancelled:
                break;

            case PawnIoInstallOutcome.InstallerMissing:
                System.Windows.MessageBox.Show(
                    "공식 PawnIO 설치 파일이 배포 폴더에 없습니다.\n" +
                    "Dependencies 폴더를 포함한 전체 배포본으로 다시 실행해 주세요.",
                    "PawnIO 설치 파일 없음",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                break;

            case PawnIoInstallOutcome.VerificationFailed:
                System.Windows.MessageBox.Show(
                    "PawnIO 설치 파일의 SHA-256이 공식 고정값과 다르므로 실행하지 않았습니다.",
                    "PawnIO 무결성 검증 실패",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                break;

            default:
                System.Windows.MessageBox.Show(
                    result.ErrorMessage ?? "PawnIO 설치에 실패했습니다.",
                    "PawnIO 설치 실패",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                break;
        }
    }

    private async Task SetClickThroughAsync(bool enabled)
    {
        if (_settings is null)
        {
            return;
        }

        var draft = _settings.Current.Clone();
        draft.ClickThrough = enabled;
        await SaveSettingsSafelyAsync(draft);
    }

    private async Task ResetPositionAsync()
    {
        if (_settings is null || _widget is null)
        {
            return;
        }

        var draft = _settings.Current.Clone();
        draft.HasCustomPosition = false;
        await SaveSettingsSafelyAsync(draft);
        _widget.RefreshWindowStylesAndPosition(forceDefault: true);
    }

    private async Task SaveSettingsSafelyAsync(AppSettings settings)
    {
        try
        {
            if (_settings is not null)
            {
                await _settings.SaveAsync(settings, _applicationCancellation.Token);
            }
        }
        catch (Exception exception)
        {
            _logger?.Error("settings-save", "설정을 저장하지 못했습니다.", exception);
        }
    }

    private void OnWidgetClosing(object? sender, CancelEventArgs e)
    {
        if (_exitStarted)
        {
            return;
        }

        e.Cancel = true;
        _widget?.Hide();
        if (_settings is not null)
        {
            var draft = _settings.Current.Clone();
            draft.WidgetVisible = false;
            _ = SaveSettingsSafelyAsync(draft);
        }
    }

    private void OnWidgetViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.ToolTipText) && _widgetViewModel is not null)
        {
            _tray?.SetToolTip(_widgetViewModel.ToolTipText.Replace(Environment.NewLine, " "));
        }
    }

    private void OnActivationRequested()
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (_widget is not null)
            {
                _widget.Show();
                _widget.RefreshWindowStylesAndPosition();
            }
        });
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => _widget?.RefreshWindowStylesAndPosition());

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            _ = Dispatcher.BeginInvoke(() =>
                _theme?.Apply(_settings?.Current.Theme ?? WidgetTheme.System));
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _applicationCancellation.Token);
                await RescanSensorsAsync();
            }, _applicationCancellation.Token);
        }
    }

    private async Task ExitApplicationAsync()
    {
        if (_exitStarted)
        {
            return;
        }

        if (_monitoring is not null)
        {
            _monitoring.SnapshotUpdated -= OnMonitoringSnapshotUpdated;
        }

        _exitStarted = true;
        _applicationCancellation.Cancel();
        if (_monitoring is not null)
        {
            await _monitoring.StopAsync();
        }

        Dispatcher.Invoke(Shutdown);
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("dispatcher-unhandled", "UI 처리 중 복구 가능한 오류가 발생했습니다.", e.Exception);
        if (e.Exception is not (OutOfMemoryException or StackOverflowException))
        {
            e.Handled = true;
        }
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger?.Error("domain-unhandled", "처리되지 않은 오류가 발생했습니다.", exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Error("task-unobserved", "백그라운드 작업 오류가 관찰되지 않았습니다.", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exitStarted = true;
        _applicationCancellation.Cancel();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;

        if (_settings is not null)
        {
            _settings.SettingsChanged -= OnSettingsChanged;
        }

        if (_widgetViewModel is not null)
        {
            _widgetViewModel.PropertyChanged -= OnWidgetViewModelPropertyChanged;
            _widgetViewModel.Dispose();
        }

        _tray?.Dispose();
        if (_monitoring is not null)
        {
            try
            {
                _monitoring.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _logger?.Error("monitor-dispose", "센서 서비스를 종료하지 못했습니다.", exception);
            }
        }

        _singleInstance?.Dispose();
        _applicationCancellation.Dispose();
        _logger?.Info("Chipmunk가 종료되었습니다.");
        _logger?.Dispose();
        base.OnExit(e);
    }
}
