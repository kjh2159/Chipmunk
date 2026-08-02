using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Chipmunk.Models;

namespace Chipmunk.Views;

public partial class PawnIoConsentWindow : Window
{
    public PawnIoConsentWindow()
    {
        InitializeComponent();
    }

    public PawnIoConsentChoice Choice { get; private set; } = PawnIoConsentChoice.Later;

    private void OnInstall(object sender, RoutedEventArgs e)
    {
        Choice = PawnIoConsentChoice.Install;
        DialogResult = true;
    }

    private void OnLater(object sender, RoutedEventArgs e)
    {
        Choice = PawnIoConsentChoice.Later;
        DialogResult = false;
    }

    private void OnNeverAskAgain(object sender, RoutedEventArgs e)
    {
        Choice = PawnIoConsentChoice.NeverAskAgain;
        DialogResult = false;
    }

    private void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
