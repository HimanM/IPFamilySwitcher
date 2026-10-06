using Microsoft.Win32;
using System.Windows;
using IPFamilySwitcher.Models;
using IPFamilySwitcher.Services;
using IPFamilySwitcher.ViewModels;

namespace IPFamilySwitcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        var logger = new Logger(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "IPFamilySwitcher", "logs"));
        var executableService = new ExecutableService();
        var firewallService = new WindowsFirewallService(logger);
        _viewModel = new MainViewModel(
            new ConfigurationService(logger: logger),
            firewallService,
            new RuleReconciliationService(firewallService, executableService, logger),
            executableService,
            new PrivilegeService());
        _viewModel.AddApplicationRequested += AddApplication;
        _viewModel.LocateApplicationRequested += LocateApplication;
        _viewModel.ConfirmAction = message =>
            MessageBox.Show(this, message, "Confirm action", MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
            MessageBoxResult.Yes;
        DataContext = _viewModel;
        Loaded += async (_, _) =>
        {
            try
            {
                await _viewModel.LoadAsync();
            }
            catch (Exception exception)
            {
                _viewModel.SetError(exception.Message);
            }
        };
    }

    private async void AddApplication(object? sender, EventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Select an application executable"
        };
        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.AddAsync(dialog.FileName);
        }

    }

    private async void LocateApplication(ApplicationViewModel? application)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Locate executable"
        };
        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.LocateAsync(application, dialog.FileName);
        }
    }

    private void CopyError(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.ErrorMessage))
        {
            Clipboard.SetText(_viewModel.ErrorMessage);
        }
    }

}