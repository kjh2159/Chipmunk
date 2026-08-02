using Microsoft.Win32;
using System.Windows;
using Chipmunk.ViewModels;

namespace Chipmunk.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => _viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(bool saved)
    {
        DialogResult = saved;
        Close();
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "설정 내보내기",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
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
        _viewModel.ErrorMessage = "위치를 기본값으로 설정했습니다. 적용을 눌러 저장하세요.";
    }
}
