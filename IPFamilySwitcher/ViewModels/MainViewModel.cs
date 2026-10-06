using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using IPFamilySwitcher.Models;
using IPFamilySwitcher.Services;

namespace IPFamilySwitcher.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ConfigurationService _configurationService;
    private readonly IFirewallService _firewallService;
    private readonly RuleReconciliationService _reconciliationService;
    private readonly ExecutableService _executableService;
    private readonly PrivilegeService _privilegeService;
    private ConfigurationDocument _configuration = new();
    private string _searchText = string.Empty;
    private string _summary = "Loading...";
    private string _errorMessage = string.Empty;

    public MainViewModel(
        ConfigurationService configurationService,
        IFirewallService firewallService,
        RuleReconciliationService reconciliationService,
        ExecutableService executableService,
        PrivilegeService? privilegeService = null)
    {
        _configurationService = configurationService;
        _firewallService = firewallService;
        _reconciliationService = reconciliationService;
        _executableService = executableService;
        _privilegeService = privilegeService ?? new PrivilegeService();

        Applications = [];
        Applications.CollectionChanged += (_, _) => OnPropertyChanged(nameof(FilteredApplications));
        AddApplicationCommand = new RelayCommand(_ => AddApplicationRequested?.Invoke(this, EventArgs.Empty));
        RemoveApplicationCommand = new RelayCommand(parameter => _ = RemoveAsync(parameter as ApplicationViewModel));
        RepairApplicationCommand = new RelayCommand(parameter => _ = RepairAsync(parameter as ApplicationViewModel));
        ApplyModeCommand = new RelayCommand(parameter => _ = ApplyPendingModeAsync(parameter as ApplicationViewModel));
        LocateApplicationCommand = new RelayCommand(parameter => LocateApplicationRequested?.Invoke(parameter as ApplicationViewModel));
        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync());
        DisableAllCommand = new RelayCommand(_ => _ = SetAllEnabledAsync(false));
        EnableAllCommand = new RelayCommand(_ => _ = SetAllEnabledAsync(true));
        RemoveAllRulesCommand = new RelayCommand(_ => _ = RemoveAllRulesAsync());
        RepairAllCommand = new RelayCommand(_ => _ = RepairAllAsync());
    }

    public ObservableCollection<ApplicationViewModel> Applications { get; }

    public IReadOnlyList<NetworkMode> Modes { get; } = Enum.GetValues<NetworkMode>();

    public Func<string, bool>? ConfirmAction { get; set; }

    public IEnumerable<ApplicationViewModel> FilteredApplications =>
        Applications.Where(application =>
            string.IsNullOrWhiteSpace(SearchText) ||
            application.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
            application.ExecutablePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredApplications));
        }
    }

    public string Summary
    {
        get => _summary;
        private set
        {
            if (_summary == value)
            {
                return;
            }

            _summary = value;
            OnPropertyChanged();
        }
    }

    public bool IsAdministrator => _privilegeService.IsAdministrator;

    public string AdministratorStatus =>
        IsAdministrator
            ? "Administrator privileges: Enabled"
            : "Administrator privileges: Not enabled — restart as administrator";

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand AddApplicationCommand { get; }
    public RelayCommand RemoveApplicationCommand { get; }
    public RelayCommand RepairApplicationCommand { get; }
    public RelayCommand ApplyModeCommand { get; }
    public RelayCommand LocateApplicationCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand DisableAllCommand { get; }
    public RelayCommand EnableAllCommand { get; }
    public RelayCommand RemoveAllRulesCommand { get; }
    public RelayCommand RepairAllCommand { get; }

    public event EventHandler? AddApplicationRequested;
    public event Action<ApplicationViewModel?>? LocateApplicationRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetError(string message) => ErrorMessage = message;

    public async Task LoadAsync()
    {
        _configuration = await _configurationService.LoadAsync();
        Applications.Clear();
        foreach (var application in _configuration.Applications)
        {
            Applications.Add(new ApplicationViewModel(application));
        }

        await RefreshAsync();
    }

    public async Task AddAsync(string path)
    {
        try
        {
            var canonicalPath = _executableService.Canonicalize(path);
            if (_executableService.IsDuplicate(_configuration.Applications.Select(app => app.ExecutablePath), canonicalPath))
            {
                throw new InvalidOperationException("This executable is already managed.");
            }

            var application = new ManagedApplication
            {
                DisplayName = Path.GetFileNameWithoutExtension(canonicalPath),
                ExecutablePath = canonicalPath
            };
            _configuration.Applications.Add(application);
            await _configurationService.SaveAsync(_configuration);
            Applications.Add(new ApplicationViewModel(application));
            UpdateSummary();
            ErrorMessage = string.Empty;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            ErrorMessage = exception.Message;
        }
    }

    public async Task ChangeModeAsync(ApplicationViewModel? application, NetworkMode mode)
    {
        if (application is null)
        {
            return;
        }
        application.PendingMode = mode;
        await ApplyPendingModeAsync(application);
    }

    private async Task ApplyPendingModeAsync(ApplicationViewModel? application)
    {
        if (application is null || !application.HasPendingModeChange)
        {
            return;
        }

        var previousMode = application.Mode;
        try
        {
            await _firewallService.ApplyModeAsync(new ManagedApplication
            {
                Id = application.Model.Id,
                DisplayName = application.Model.DisplayName,
                ExecutablePath = application.Model.ExecutablePath,
                Mode = application.PendingMode,
                Enabled = application.Model.Enabled,
                CreatedAt = application.Model.CreatedAt
            });
            application.CommitPendingMode();
            await _configurationService.SaveAsync(_configuration);
            await RefreshAsync();
            ErrorMessage = string.Empty;
        }
        catch (Exception exception) when (exception is FirewallOperationException or IOException or InvalidOperationException)
        {
            application.PendingMode = previousMode;
            ErrorMessage = exception.Message;
            await RefreshAsync();
        }
    }

    public async Task LocateAsync(ApplicationViewModel? application, string path)
    {
        if (application is null)
        {
            return;
        }

        try
        {
            var canonicalPath = _executableService.Canonicalize(path);
            if (_configuration.Applications.Any(item =>
                    item.Id != application.Id &&
                    string.Equals(item.ExecutablePath, canonicalPath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("This executable is already managed.");
            }

            await _firewallService.RemoveManagedRuleAsync(application.Id);
            application.Model.ExecutablePath = canonicalPath;
            application.Model.DisplayName = Path.GetFileNameWithoutExtension(canonicalPath);
            application.RefreshMetadata();
            await _firewallService.ApplyModeAsync(application.Model);
            await _configurationService.SaveAsync(_configuration);
            await RefreshAsync();
            ErrorMessage = string.Empty;
        }
        catch (Exception exception) when (exception is ArgumentException or FirewallOperationException or IOException or InvalidOperationException)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task RemoveAsync(ApplicationViewModel? application)
    {
        if (application is null)
        {
            return;
        }

        try
        {
            if (ConfirmAction is not null &&
                !ConfirmAction($"Remove {application.DisplayName} from IP Family Switcher? Its managed firewall rule will also be removed."))
            {
                return;
            }

            await _firewallService.RemoveManagedRuleAsync(application.Id);
            _configuration.Applications.Remove(application.Model);
            await _configurationService.SaveAsync(_configuration);
            Applications.Remove(application);
            UpdateSummary();
            ErrorMessage = string.Empty;
        }
        catch (Exception exception) when (exception is FirewallOperationException or IOException)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task RepairAsync(ApplicationViewModel? application)
    {
        if (application is null)
        {
            return;
        }

        try
        {
            await _firewallService.ApplyModeAsync(application.Model);
            await RefreshAsync();
            ErrorMessage = string.Empty;
        }
        catch (Exception exception) when (exception is FirewallOperationException or InvalidOperationException)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            var statuses = await _reconciliationService.ReconcileAsync(
                Applications.Select(application => application.Model).ToArray());
            foreach (var application in Applications)
            {
                var status = statuses.FirstOrDefault(item => item.ApplicationId == application.Id);
                if (status is not null)
                {
                    application.UpdateStatus(status);
                    application.Model.LastReconciledAt = DateTimeOffset.UtcNow;
                }
            }

            UpdateSummary();
        }
        catch (Exception exception) when (exception is FirewallOperationException or InvalidOperationException)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task SetAllEnabledAsync(bool enabled)
    {
        try
        {
            foreach (var application in Applications)
            {
                application.Model.Enabled = enabled;
                if (application.Mode != NetworkMode.Default)
                {
                    await _firewallService.ApplyModeAsync(application.Model);
                }
                else
                {
                    await _firewallService.SetRuleEnabledAsync(application.Id, enabled);
                }
            }

            await _configurationService.SaveAsync(_configuration);
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is FirewallOperationException or IOException)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task RemoveAllRulesAsync()
    {
        try
        {
            if (ConfirmAction is not null &&
                !ConfirmAction("Remove all IP Family Switcher firewall rules and reset applications to Default?"))
            {
                return;
            }

            await _firewallService.RemoveAllManagedRulesAsync();
            foreach (var application in Applications)
            {
                application.Mode = NetworkMode.Default;
                application.Model.Enabled = true;
            }

            await _configurationService.SaveAsync(_configuration);
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is FirewallOperationException or IOException)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task RepairAllAsync()
    {
        try
        {
            foreach (var application in Applications.Where(application => application.Mode != NetworkMode.Default))
            {
                await _firewallService.ApplyModeAsync(application.Model);
            }

            await RefreshAsync();
            ErrorMessage = string.Empty;
        }
        catch (Exception exception) when (exception is FirewallOperationException or InvalidOperationException)
        {
            ErrorMessage = exception.Message;
        }
    }

    private void UpdateSummary()
    {
        var active = Applications.Count(application =>
            application.Status is "Active" or "Disabled");
        var issues = Applications.Count(application =>
            application.Status is not "Active" and not "Default");
        Summary = $"Managed applications: {Applications.Count}    " +
                  $"IPv4 Only: {Applications.Count(app => app.Mode == NetworkMode.IPv4Only)}    " +
                  $"IPv6 Only: {Applications.Count(app => app.Mode == NetworkMode.IPv6Only)}    " +
                  $"Default: {Applications.Count(app => app.Mode == NetworkMode.Default)}    " +
                  $"Active firewall rules: {active}    Issues: {issues}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
