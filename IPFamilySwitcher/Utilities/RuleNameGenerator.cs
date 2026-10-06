namespace IPFamilySwitcher.Utilities;

public static class RuleNameGenerator
{
    private const string NamePrefix = "IPFamilySwitcher-";
    public const string AllIpv6Ranges = "0::/1,8000::/1";

    public const string GroupName = "IPFamilySwitcher.ManagedRules";

    public static string ForIpv4Block(Guid applicationId) =>
        $"IPFamilySwitcher-{applicationId:D}-BlockIPv4";

    public static string ForIpv6Block(Guid applicationId) =>
        $"IPFamilySwitcher-{applicationId:D}-BlockIPv6";

    public static string Description(Guid applicationId) =>
        $"Managed by IP Family Switcher. Application ID: {applicationId:D}.";

    public static bool TryGetApplicationId(string ruleName, out Guid applicationId)
    {
        applicationId = Guid.Empty;
        if (!ruleName.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var identifier = ruleName[NamePrefix.Length..];
        foreach (var suffix in new[] { "-BlockIPv4", "-BlockIPv6" })
        {
            if (identifier.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return Guid.TryParse(identifier[..^suffix.Length], out applicationId);
            }
        }

        return false;
    }
}
