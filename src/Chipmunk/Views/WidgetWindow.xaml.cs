using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Chipmunk.Interop;
using Chipmunk.Services;
using Chipmunk.ViewModels;

namespace Chipmunk.Views;

public partial class WidgetWindow : Window
{
    private readonly IWindowPositionService _positionService;
    private readonly ISettingsService _settingsService;
    private readonly Action _doubleClickAction;
    private HwndSource? _source;
    private uint _taskbarCreatedMessage;
    private bool _loaded;

    public WidgetWindow(
        WidgetViewModel viewModel,
        IWindowPositionService positionService,
        ISettingsService settingsService,
        Action doubleClickAction)
    {
        InitializeComponent();
        DataContext = viewModel;
        _positionService = positionService;
        _settingsService = settingsService;
        _doubleClickAction = doubleClickAction;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Closed += (_, _) => _source?.RemoveHook(WndProc);
    }

    public void RefreshWindowStylesAndPosition(bool forceDefault = false)
    {
        if (!_loaded)
        {
            return;
        }

        _positionService.ApplyToolWindowStyle(this);
        _positionService.ApplyClickThrough(this, _settingsService.Current.ClickThrough);
        _positionService.PositionWindow(this, _settingsService.Current, forceDefault);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(WndProc);
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        _positionService.ApplyToolWindowStyle(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        RefreshWindowStylesAndPosition();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => _positionService.PositionWindow(this, _settingsService.Current));
    }

    private async void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _doubleClickAction();
            e.Handled = true;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || _settingsService.Current.ClickThrough)
        {
            return;
        }

        try
        {
            DragMove();
            _positionService.SaveCurrentPosition(this, _settingsService.Current);
            await _settingsService.SaveAsync(_settingsService.Current);
        }
        catch (InvalidOperationException)
        {
            // Mouse button can be released before DragMove starts.
        }
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeMethods.WmDisplayChange ||
            message == NativeMethods.WmSettingChange ||
            message == NativeMethods.WmDpiChanged ||
            (uint)message == _taskbarCreatedMessage)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                () => RefreshWindowStylesAndPosition());
        }

        return nint.Zero;
    }
}
