using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Chipmunk.Interop;
using Chipmunk.Services;
using Chipmunk.ViewModels;

namespace Chipmunk.Views;

public partial class WidgetWindow : Window
{
    private const double MinimumFontSize = 9;
    private const double MaximumFontSize = 30;
    private const double AbsoluteMinimumWidth = 180;
    private const double AbsoluteMinimumHeight = 54;
    private const double HorizontalChrome = 50;
    private const double VerticalChrome = 38;
    private readonly IWindowPositionService _positionService;
    private readonly ISettingsService _settingsService;
    private readonly IRateLimitedLogger _logger;
    private readonly WidgetViewModel _viewModel;
    private readonly Action _doubleClickAction;
    private HwndSource? _source;
    private uint _taskbarCreatedMessage;
    private bool _loaded;
    private bool _isUserResizing;
    private double _resizeStartWidth;
    private double _resizeStartHeight;
    private double _resizeStartFontSize;
    private double _resizeHorizontalChange;
    private double _resizeVerticalChange;

    public WidgetWindow(
        WidgetViewModel viewModel,
        IWindowPositionService positionService,
        ISettingsService settingsService,
        IRateLimitedLogger logger,
        Action doubleClickAction)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _positionService = positionService;
        _settingsService = settingsService;
        _logger = logger;
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
        ApplySizeMode();
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
        if (!_loaded || _isUserResizing)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => _positionService.PositionWindow(this, _settingsService.Current));
    }

    /// <summary>
    /// Fixed mode keeps the original content-sized overlay. Flexible mode restores
    /// the last explicit dimensions and exposes the resize handle when it can receive input.
    /// </summary>
    private void ApplySizeMode()
    {
        var settings = _settingsService.Current;
        var naturalSize = MeasureNaturalSize();
        ResizeThumb.Visibility = !settings.IsWidgetSizeFixed && !settings.ClickThrough
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (settings.IsWidgetSizeFixed)
        {
            ClearValue(WidthProperty);
            ClearValue(HeightProperty);
            SizeToContent = SizeToContent.WidthAndHeight;
            return;
        }

        SizeToContent = SizeToContent.Manual;
        var requestedWidth = settings.HasFlexibleWidgetSize
            ? Math.Max(settings.FlexibleWidgetWidth, naturalSize.Width)
            : naturalSize.Width;
        var requestedHeight = settings.HasFlexibleWidgetSize
            ? Math.Max(settings.FlexibleWidgetHeight, naturalSize.Height)
            : naturalSize.Height;
        Width = Math.Clamp(requestedWidth, MinWidth, MaxWidth);
        Height = Math.Clamp(requestedHeight, MinHeight, MaxHeight);
    }

    private WidgetMinimumSize MeasureNaturalSize()
    {
        SensorTextBlock.Measure(new System.Windows.Size(
            double.PositiveInfinity,
            double.PositiveInfinity));
        var textSize = SensorTextBlock.DesiredSize;
        var minimumSize = WidgetResizeCalculator.CalculateMinimumSize(
            textSize.Width,
            textSize.Height,
            _viewModel.WidgetFontSize,
            MinimumFontSize,
            HorizontalChrome,
            VerticalChrome,
            AbsoluteMinimumWidth,
            AbsoluteMinimumHeight);
        MinWidth = Math.Min(minimumSize.Width, MaxWidth);
        MinHeight = Math.Min(minimumSize.Height, MaxHeight);

        return new WidgetMinimumSize(
            Math.Clamp(HorizontalChrome + textSize.Width, MinWidth, MaxWidth),
            Math.Clamp(VerticalChrome + textSize.Height, MinHeight, MaxHeight));
    }

    private void OnResizeStarted(object sender, DragStartedEventArgs e)
    {
        if (_settingsService.Current.IsWidgetSizeFixed || _settingsService.Current.ClickThrough)
        {
            return;
        }

        _isUserResizing = true;
        _resizeStartWidth = Math.Max(MinWidth, ActualWidth);
        _resizeStartHeight = Math.Max(MinHeight, ActualHeight);
        _resizeStartFontSize = _viewModel.WidgetFontSize;
        _resizeHorizontalChange = 0;
        _resizeVerticalChange = 0;
        e.Handled = true;
    }

    private void OnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_isUserResizing)
        {
            return;
        }

        _resizeHorizontalChange += e.HorizontalChange;
        _resizeVerticalChange += e.VerticalChange;
        var result = WidgetResizeCalculator.Calculate(
            _resizeStartWidth,
            _resizeStartHeight,
            _resizeStartFontSize,
            _resizeHorizontalChange,
            _resizeVerticalChange,
            MinWidth,
            MaxWidth,
            MinHeight,
            MaxHeight,
            MinimumFontSize,
            MaximumFontSize,
            HorizontalChrome,
            VerticalChrome);

        Width = result.Width;
        Height = result.Height;
        _viewModel.WidgetFontSize = result.FontSize;
        e.Handled = true;
    }

    private async void OnResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_isUserResizing)
        {
            return;
        }

        _isUserResizing = false;
        if (e.Canceled)
        {
            Width = _resizeStartWidth;
            Height = _resizeStartHeight;
            _viewModel.WidgetFontSize = _resizeStartFontSize;
            return;
        }

        var draft = _settingsService.Current.Clone();
        draft.IsWidgetSizeFixed = false;
        draft.HasFlexibleWidgetSize = true;
        draft.FlexibleWidgetWidth = ActualWidth;
        draft.FlexibleWidgetHeight = ActualHeight;
        draft.FontSize = _viewModel.WidgetFontSize;

        try
        {
            await _settingsService.SaveAsync(draft);
        }
        catch (Exception exception)
        {
            // Rate limiting keeps repeated storage failures from flooding the log.
            _logger.Error("widget-size-save", "The resized widget dimensions could not be saved.", exception);
        }
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
