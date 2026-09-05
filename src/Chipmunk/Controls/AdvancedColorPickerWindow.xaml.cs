using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Chipmunk.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfPoint = System.Windows.Point;
using WpfWindow = System.Windows.Window;

namespace Chipmunk.Controls;

/// <summary>
/// Dependency-free HSV picker used for colors outside the quick palette.
/// UI interaction is intentionally local; the accepted ARGB string is passed
/// back to ColorPalettePicker, whose dependency property owns the draft value.
/// </summary>
public partial class AdvancedColorPickerWindow : WpfWindow
{
    private DragTarget _dragTarget;
    private double _hue;
    private double _saturation;
    private double _value;
    private byte _alpha = byte.MaxValue;
    private bool _updatingText;

    public AdvancedColorPickerWindow(string? initialColor)
    {
        InitializeComponent();

        var normalized = ArgbColorHex.Normalize(
            initialColor,
            ThresholdSettings.DefaultWarningColor);
        ArgbColorHex.TryParse(normalized, out var color);
        _alpha = color.Alpha;
        var hsv = HsvColorConverter.ToHsv(color.Red, color.Green, color.Blue);
        _hue = hsv.Hue;
        _saturation = hsv.Saturation;
        _value = hsv.Value;
        SelectedColor = normalized;
        SetHexText(normalized);
        UpdateVisuals();
    }

    public string SelectedColor { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e) => UpdateVisuals();

    private void OnColorFieldMouseDown(object sender, WpfMouseButtonEventArgs e)
    {
        _dragTarget = DragTarget.ColorField;
        Mouse.Capture(ColorField);
        UpdateFromColorField(e.GetPosition(ColorField));
    }

    private void OnColorFieldMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_dragTarget == DragTarget.ColorField && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateFromColorField(e.GetPosition(ColorField));
        }
    }

    private void OnHueMouseDown(object sender, WpfMouseButtonEventArgs e)
    {
        _dragTarget = DragTarget.Hue;
        Mouse.Capture(HueStrip);
        UpdateFromHue(e.GetPosition(HueStrip).Y);
    }

    private void OnHueMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_dragTarget == DragTarget.Hue && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateFromHue(e.GetPosition(HueStrip).Y);
        }
    }

    private void OnPickerMouseUp(object sender, WpfMouseButtonEventArgs e)
    {
        _dragTarget = DragTarget.None;
        Mouse.Capture(null);
    }

    private void OnPickerSizeChanged(object sender, SizeChangedEventArgs e) => UpdateMarkers();

    private void UpdateFromColorField(WpfPoint point)
    {
        if (ColorField.ActualWidth <= 0 || ColorField.ActualHeight <= 0)
        {
            return;
        }

        _saturation = Math.Clamp(point.X / ColorField.ActualWidth, 0, 1);
        _value = 1 - Math.Clamp(point.Y / ColorField.ActualHeight, 0, 1);
        CommitPickerColor();
    }

    private void UpdateFromHue(double y)
    {
        if (HueStrip.ActualHeight <= 0)
        {
            return;
        }

        _hue = Math.Clamp(y / HueStrip.ActualHeight, 0, 1) * 360;
        CommitPickerColor();
    }

    private void CommitPickerColor()
    {
        var rgb = HsvColorConverter.ToRgb(_hue, _saturation, _value);
        SelectedColor = ArgbColorHex.Format(new ArgbColorValue(
            _alpha,
            rgb.Red,
            rgb.Green,
            rgb.Blue));
        SetHexText(SelectedColor);
        ValidationMessage.Visibility = Visibility.Collapsed;
        UpdateVisuals();
    }

    private void OnHexTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updatingText)
        {
            return;
        }

        if (!ArgbColorHex.TryParseUserInput(HexTextBox.Text.Trim(), out var color))
        {
            ValidationMessage.Visibility = Visibility.Visible;
            return;
        }

        _alpha = color.Alpha;
        var hsv = HsvColorConverter.ToHsv(color.Red, color.Green, color.Blue);
        _hue = hsv.Hue;
        _saturation = hsv.Saturation;
        _value = hsv.Value;
        SelectedColor = ArgbColorHex.Format(color);
        ValidationMessage.Visibility = Visibility.Collapsed;
        UpdateVisuals();
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (!ArgbColorHex.TryParseUserInput(HexTextBox.Text.Trim(), out var color))
        {
            ValidationMessage.Visibility = Visibility.Visible;
            HexTextBox.Focus();
            HexTextBox.SelectAll();
            return;
        }

        SelectedColor = ArgbColorHex.Format(color);
        DialogResult = true;
    }

    private void SetHexText(string value)
    {
        _updatingText = true;
        HexTextBox.Text = value;
        _updatingText = false;
    }

    private void UpdateVisuals()
    {
        var hueRgb = HsvColorConverter.ToRgb(_hue, 1, 1);
        HueBase.Background = FrozenBrush(byte.MaxValue, hueRgb);

        if (ArgbColorHex.TryParse(SelectedColor, out var selected))
        {
            ColorPreview.Background = FrozenBrush(selected.Alpha, new RgbColorValue(
                selected.Red,
                selected.Green,
                selected.Blue));
        }

        UpdateMarkers();
    }

    private void UpdateMarkers()
    {
        if (ColorField.ActualWidth > 0 && ColorField.ActualHeight > 0)
        {
            System.Windows.Controls.Canvas.SetLeft(
                ColorMarker,
                _saturation * ColorField.ActualWidth - ColorMarker.Width / 2);
            System.Windows.Controls.Canvas.SetTop(
                ColorMarker,
                (1 - _value) * ColorField.ActualHeight - ColorMarker.Height / 2);
        }

        if (HueStrip.ActualHeight > 0)
        {
            System.Windows.Controls.Canvas.SetLeft(HueMarker, -4);
            System.Windows.Controls.Canvas.SetTop(
                HueMarker,
                _hue / 360 * HueStrip.ActualHeight - HueMarker.Height / 2);
        }
    }

    private static MediaBrush FrozenBrush(byte alpha, RgbColorValue rgb)
    {
        var brush = new SolidColorBrush(MediaColor.FromArgb(alpha, rgb.Red, rgb.Green, rgb.Blue));
        brush.Freeze();
        return brush;
    }

    private enum DragTarget
    {
        None,
        ColorField,
        Hue
    }
}
