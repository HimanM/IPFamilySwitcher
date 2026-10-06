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
    private readonly System.Windows.Threading.DispatcherTimer _processTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _inspecting;

    private async Task RefreshProcessesAsync()
    {
        if (_inspecting || !IsVisible || WindowState == WindowState.Minimized) return;
        _inspecting = true;
        try
        {
            var paths = _viewModel.Applications.Select(app => app.ExecutablePath).ToArray();
            var states = await Task.Run(() => ProcessStatusService.Inspect(paths));
            foreach (var app in _viewModel.Applications)
                if (states.TryGetValue(app.ExecutablePath, out var state)) app.UpdateRunningStatus(state);
        }
        catch (Exception exception) { _viewModel.SetError("Unable to inspect running applications: " + exception.Message); }
        finally { _inspecting = false; }
    }

    private void OpenRepository(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ReleaseInfo.RepositoryUrl) { UseShellExecute = true }); }
        catch (Exception exception) { _viewModel.SetError("Unable to open GitHub: " + exception.Message); }
    }

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
        _processTimer.Tick += async (_, _) => await RefreshProcessesAsync();
        StateChanged += async (_, _) =>
        {
            if (WindowState == WindowState.Minimized) _processTimer.Stop();
            else { _processTimer.Start(); await RefreshProcessesAsync(); }
        };
        Closed += (_, _) => _processTimer.Stop();
        Loaded += async (_, _) =>
        {
            try
            {
                await _viewModel.LoadAsync();
                await RefreshProcessesAsync();
                _processTimer.Start();
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
