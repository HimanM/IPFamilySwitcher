namespace IPFamilySwitcher.Utilities;

public static class RuleNameGenerator
{
    public const string GroupName = "IPFamilySwitcher.ManagedRules";

    public static string ForIpv4Block(Guid applicationId) =>
        $"IPFamilySwitcher-{applicationId:D}-BlockIPv4";

    public static string ForIpv6Block(Guid applicationId) =>
        $"IPFamilySwitcher-{applicationId:D}-BlockIPv6";

    public static string Description(Guid applicationId) =>
        $"Managed by IP Family Switcher. Application ID: {applicationId:D}.";
}
