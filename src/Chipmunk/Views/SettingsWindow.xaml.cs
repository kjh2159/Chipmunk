using Microsoft.Win32;
using System.Windows;
using Chipmunk.Services;
using Chipmunk.ViewModels;

namespace Chipmunk.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly ILocalizationService _localization;

    public SettingsWindow(SettingsViewModel viewModel, ILocalizationService localization)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _localization = localization;
        DataContext = viewModel;
        _viewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    private void OnCloseRequested(bool saved)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.Dispose();
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = _localization.Get("ExportDialogTitle"),
            Filter = _localization.Get("ExportJsonFilter"),
            FileName = "Chipmunk.settings.json",
            AddExtension = true,
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.ExportAsync(dialog.FileName);
        }
    }

    private void OnResetPositionClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Draft.HasCustomPosition = false;
        _viewModel.ErrorMessage = _localization.Get("SettingsPositionReset");
    }
}
