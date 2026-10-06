namespace IPFamilySwitcher.Models;

public sealed record ApplicationRuleStatus(
    Guid ApplicationId,
    FirewallRuleState State,
    string Message,
    IReadOnlyList<FirewallRuleInfo> Rules);
