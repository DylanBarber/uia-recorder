using System.Windows;
using UiaRecorder.ViewModels;

namespace UiaRecorder;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Closing += (_, _) => _viewModel.Shutdown();
    }
}
