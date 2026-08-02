using System.Windows;
using Chipmunk.ViewModels;

namespace Chipmunk.Views;

public partial class DetailedMonitorWindow : Window
{
    public DetailedMonitorWindow(WidgetViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
