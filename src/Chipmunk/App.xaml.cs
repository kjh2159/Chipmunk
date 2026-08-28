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
    private readonly ILocalizationService _localization = new LocalizationService();
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

            _localization.Apply(_settings.Current.Language);
            _theme.Apply(_settings.Current.Theme);
            _widgetViewModel = new WidgetViewModel(_monitoring, _settings, _localization);
            _widget = new WidgetWindow(
                _widgetViewModel,
                _position,
                _settings,
                _logger,
                ExecuteDoubleClickAction);
            _widget.Closing += OnWidgetClosing;
            _widget.Show();
            if (!_settings.Current.WidgetVisible)
            {
                _widget.Hide();
            }

            _tray = new TrayIconService(
                _localization,
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
            _logger.Info("Chipmunk started.");
        }
        catch (Exception exception)
        {
            _logger.Error("startup", "The application could not be initialized.", exception);
            System.Windows.MessageBox.Show(
                _localization.Format("StartupErrorMessage", exception.Message),
                _localization.Get("StartupErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => OnSettingsChanged(settings));
            return;
        }

        _localization.Apply(settings.Language);
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

        var viewModel = new SettingsViewModel(
            _settings,
            _startup,
            _monitoring,
            _position,
            _localization);
        _settingsWindow = new SettingsWindow(viewModel, _localization);
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
            _logger?.Error("task-manager", "Windows Task Manager could not be started.", exception);
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
                    _localization.Get("PawnIoInstalledMessage"),
                    _localization.Get("PawnIoInstalledTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                break;

            case PawnIoInstallOutcome.RebootRequired:
                System.Windows.MessageBox.Show(
                    _localization.Get("PawnIoRebootMessage"),
                    _localization.Get("PawnIoRebootTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                break;

            case PawnIoInstallOutcome.Cancelled:
                break;

            case PawnIoInstallOutcome.InstallerMissing:
                System.Windows.MessageBox.Show(
                    _localization.Get("PawnIoMissingMessage"),
                    _localization.Get("PawnIoMissingTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                break;

            case PawnIoInstallOutcome.VerificationFailed:
                System.Windows.MessageBox.Show(
                    _localization.Get("PawnIoVerificationMessage"),
                    _localization.Get("PawnIoVerificationTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                break;

            default:
                System.Windows.MessageBox.Show(
                    _localization.Format(
                        "PawnIoFailedMessage",
                        result.ExitCode?.ToString() ?? "N/A"),
                    _localization.Get("PawnIoFailedTitle"),
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
            _logger?.Error("settings-save", "Settings could not be saved.", exception);
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
        _logger?.Error("dispatcher-unhandled", "A recoverable UI error occurred.", e.Exception);
        if (e.Exception is not (OutOfMemoryException or StackOverflowException))
        {
            e.Handled = true;
        }
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger?.Error("domain-unhandled", "An unhandled application error occurred.", exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Error("task-unobserved", "An unobserved background task error occurred.", e.Exception);
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
                _logger?.Error("monitor-dispose", "The sensor service could not be disposed.", exception);
            }
        }

        _singleInstance?.Dispose();
        _applicationCancellation.Dispose();
        _logger?.Info("Chipmunk stopped.");
        _logger?.Dispose();
        base.OnExit(e);
    }
}
