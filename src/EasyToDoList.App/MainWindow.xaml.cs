using System.ComponentModel;
using System.Windows;
using EasyToDoList.App.Shell;

namespace EasyToDoList.App;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly WindowModeController _modeController;
    private bool _exitRequested;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _modeController = new WindowModeController(this);
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.ExitRequested += OnExitRequested;
        _modeController.Apply(viewModel.DisplayMode);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.DisplayMode))
        {
            _modeController.Apply(_viewModel.DisplayMode);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        _viewModel.HideToTray();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _exitRequested = true;
        Close();
    }
}
