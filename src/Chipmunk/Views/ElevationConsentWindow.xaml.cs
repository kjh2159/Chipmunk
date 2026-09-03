using System.Windows;

namespace Chipmunk.Views;

public partial class ElevationConsentWindow : Window
{
    public ElevationConsentWindow()
    {
        InitializeComponent();
    }

    public bool RestartRequested { get; private set; }

    private void OnContinueNormally(object sender, RoutedEventArgs e)
    {
        RestartRequested = false;
        DialogResult = false;
    }

    private void OnRestartAsAdministrator(object sender, RoutedEventArgs e)
    {
        RestartRequested = true;
        DialogResult = true;
    }
}
