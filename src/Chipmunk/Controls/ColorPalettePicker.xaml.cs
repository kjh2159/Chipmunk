using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chipmunk.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace Chipmunk.Controls;

/// <summary>
/// Lightweight reusable ARGB color picker. The selected string remains owned by
/// the settings model; this control only presents and updates the palette choice.
/// </summary>
public partial class ColorPalettePicker : WpfUserControl
{
    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor),
        typeof(string),
        typeof(ColorPalettePicker),
        new FrameworkPropertyMetadata(
            ThresholdSettings.DefaultWarningColor,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedColorChanged));

    private static readonly DependencyPropertyKey SelectedBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SelectedBrush),
            typeof(MediaBrush),
            typeof(ColorPalettePicker),
            new PropertyMetadata(System.Windows.Media.Brushes.Transparent));

    public static readonly DependencyProperty SelectedBrushProperty =
        SelectedBrushPropertyKey.DependencyProperty;

    public ColorPalettePicker()
    {
        Palette = new ObservableCollection<PaletteColorOption>(CreatePalette());
        InitializeComponent();
        RefreshSelection(SelectedColor);
    }

    public ObservableCollection<PaletteColorOption> Palette { get; }

    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public MediaBrush SelectedBrush
    {
        get => (MediaBrush)GetValue(SelectedBrushProperty);
        private set => SetValue(SelectedBrushPropertyKey, value);
    }

    private static void OnSelectedColorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((ColorPalettePicker)dependencyObject).RefreshSelection(args.NewValue as string);
    }

    private void RefreshSelection(string? value)
    {
        var normalized = ArgbColorHex.Normalize(value, ThresholdSettings.DefaultWarningColor);
        SelectedBrush = CreateBrush(normalized);

        foreach (var option in Palette)
        {
            option.IsSelected = string.Equals(option.Hex, normalized, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void OnPaletteColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: PaletteColorOption option })
        {
            SelectedColor = option.Hex;
            PalettePopup.IsOpen = false;
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        SwatchButton.IsChecked = false;
    }

    private void OnMoreColorsClick(object sender, RoutedEventArgs e)
    {
        PalettePopup.IsOpen = false;
        var dialog = new AdvancedColorPickerWindow(SelectedColor)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedColor = dialog.SelectedColor;
        }
    }

    private static IEnumerable<PaletteColorOption> CreatePalette()
    {
        string[] colors =
        [
            "#FFFFD54F", "#FFFFC857", "#FFFFB300", "#FFFF9800", "#FFFF7043", "#FFFF6B6B",
            "#FFE53935", "#FFEC407A", "#FFAB47BC", "#FF7E57C2", "#FF5C6BC0", "#FF42A5F5",
            "#FF26C6DA", "#FF26A69A", "#FF66BB6A", "#FF9CCC65", "#FFC0CA33", "#FFCDDC39",
            "#FFFFFFFF", "#FFCFD8DC", "#FFB0BEC5", "#FF78909C", "#FF455A64", "#FF212121"
        ];

        return colors.Select(hex => new PaletteColorOption(hex, CreateBrush(hex)));
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush(
            (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}

public sealed partial class PaletteColorOption(string hex, MediaBrush brush) : ObservableObject
{
    public string Hex { get; } = hex;
    public MediaBrush Brush { get; } = brush;

    [ObservableProperty]
    private bool _isSelected;
}
