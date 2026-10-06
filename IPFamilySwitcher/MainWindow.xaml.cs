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
            executableService);
        _viewModel.AddApplicationRequested += AddApplication;
        _viewModel.ConfirmAction = message =>
            MessageBox.Show(this, message, "Confirm action", MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
            MessageBoxResult.Yes;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
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

    private async void ModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox comboBox &&
            comboBox.DataContext is ApplicationViewModel application &&
            comboBox.SelectedItem is NetworkMode mode &&
            IsLoaded)
        {
            await _viewModel.ChangeModeAsync(application, mode);
        }
    }
}