using System.ComponentModel;
using System.Runtime.CompilerServices;
using IPFamilySwitcher.Models;

namespace IPFamilySwitcher.ViewModels;

public sealed class ApplicationViewModel : INotifyPropertyChanged
{
    private NetworkMode _mode;
    private NetworkMode _pendingMode;
    private string _status = "Default";

    public ApplicationViewModel(ManagedApplication application)
    {
        Model = application;
        _mode = application.Mode;
        _pendingMode = application.Mode;
    }

    public ManagedApplication Model { get; }

    public Guid Id => Model.Id;

    public string DisplayName => Model.DisplayName;

    public string ExecutablePath => Model.ExecutablePath;

    public NetworkMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            Model.Mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPendingModeChange));
        }
    }

    public NetworkMode PendingMode
    {
        get => _pendingMode;
        set
        {
            if (_pendingMode == value)
            {
                return;
            }

            _pendingMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPendingModeChange));
        }
    }

    public bool HasPendingModeChange => PendingMode != Mode;

    public void CommitPendingMode() => Mode = PendingMode;

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    public void UpdateStatus(ApplicationRuleStatus status)
    {
        Status = status.State switch
        {
            FirewallRuleState.Correct when Model.Mode == NetworkMode.Default => "Default",
            FirewallRuleState.Correct when !Model.Enabled => "Disabled",
            FirewallRuleState.Correct => "Active",
            FirewallRuleState.Missing => "Missing",
            FirewallRuleState.Disabled => "Disabled",
            FirewallRuleState.Incorrect => "Incorrect rule",
            FirewallRuleState.Orphaned => "Orphaned",
            FirewallRuleState.ExecutableMissing => "Executable missing",
            FirewallRuleState.Duplicate => "Duplicate",
            _ => "Error"
        };
    }

    public void RefreshMetadata()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ExecutablePath));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
