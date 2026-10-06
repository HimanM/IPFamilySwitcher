namespace IPFamilySwitcher.Models;

public sealed record FirewallRuleInfo(
    string Name,
    Guid? ApplicationId,
    string Program,
    string Group,
    bool Enabled,
    int Direction,
    int Action,
    string RemoteAddresses,
    int Protocol,
    int Profiles);
