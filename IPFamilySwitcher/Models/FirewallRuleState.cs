namespace IPFamilySwitcher.Models;

public enum FirewallRuleState
{
    Correct,
    Missing,
    Disabled,
    Incorrect,
    Orphaned,
    ExecutableMissing,
    Duplicate,
    Error
}
