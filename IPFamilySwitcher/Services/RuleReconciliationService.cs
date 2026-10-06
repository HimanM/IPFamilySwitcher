using System.IO;
using IPFamilySwitcher.Models;
using IPFamilySwitcher.Utilities;

namespace IPFamilySwitcher.Services;

public sealed class RuleReconciliationService
{
    private readonly IFirewallService _firewallService;
    private readonly ExecutableService _executableService;
    private readonly Logger _logger;

    public RuleReconciliationService(
        IFirewallService firewallService,
        ExecutableService? executableService = null,
        Logger? logger = null)
    {
        _firewallService = firewallService;
        _executableService = executableService ?? new ExecutableService();
        _logger = logger ?? new Logger(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "IPFamilySwitcher", "logs"));
    }

    public async Task<IReadOnlyList<ApplicationRuleStatus>> ReconcileAsync(
        IReadOnlyList<ManagedApplication> applications,
        CancellationToken cancellationToken = default)
    {
        var rules = await _firewallService.GetManagedRulesAsync(cancellationToken);
        var configuredIds = applications.Select(application => application.Id).ToHashSet();
        var results = applications.Select(application => GetStatus(application, rules)).ToList();

        foreach (var orphan in rules.Where(rule => !rule.ApplicationId.HasValue || !configuredIds.Contains(rule.ApplicationId.Value)))
        {
            _logger.Info($"Orphaned managed firewall rule detected: {orphan.Name}.");
        }

        return results;
    }

    public static IReadOnlyList<FirewallRuleInfo> GetOrphanedRules(
        IReadOnlyList<ManagedApplication> applications,
        IReadOnlyList<FirewallRuleInfo> rules)
    {
        var configuredIds = applications.Select(application => application.Id).ToHashSet();
        return rules
            .Where(rule => !rule.ApplicationId.HasValue || !configuredIds.Contains(rule.ApplicationId.Value))
            .ToArray();
    }

    private ApplicationRuleStatus GetStatus(
        ManagedApplication application,
        IReadOnlyList<FirewallRuleInfo> allRules)
    {
        if (!_executableService.Exists(application.ExecutablePath))
        {
            return new(application.Id, FirewallRuleState.ExecutableMissing,
                "Executable not found.", RulesFor(allRules, application.Id));
        }

        var rules = RulesFor(allRules, application.Id);
        if (application.Mode == NetworkMode.Default)
        {
            return rules.Count == 0
                ? new(application.Id, FirewallRuleState.Correct, "Default", rules)
                : new(application.Id, FirewallRuleState.Incorrect,
                    "An IP-family rule exists for a mode that should not block traffic.", rules);
        }

        var expectedName = application.Mode == NetworkMode.IPv4Only
            ? RuleNameGenerator.ForIpv6Block(application.Id)
            : RuleNameGenerator.ForIpv4Block(application.Id);
        var expectedRemote = application.Mode == NetworkMode.IPv4Only
            ? RuleNameGenerator.AllIpv6Ranges
            : "0.0.0.0/0";
        var expected = rules.Where(rule =>
            string.Equals(rule.Name, expectedName, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (expected.Length == 0)
        {
            return new(application.Id, FirewallRuleState.Missing, "Firewall rule missing.", rules);
        }

        if (expected.Length > 1)
        {
            return new(application.Id, FirewallRuleState.Duplicate, "Duplicate managed rules found.", expected);
        }

        var rule = expected[0];
        var valid = string.Equals(rule.Program, application.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&
                    rule.Direction == 2 &&
                    rule.Action == 0 &&
                    AddressListsEqual(rule.RemoteAddresses, expectedRemote) &&
                    rule.Protocol == 256 &&
                    (rule.Profiles == 7 || rule.Profiles == 2147);
        if (!valid)
        {
            return new(application.Id, FirewallRuleState.Incorrect, "Incorrect firewall rule.", expected);
        }

        if (application.Enabled != rule.Enabled)
        {
            return new(application.Id, FirewallRuleState.Disabled,
                "Firewall rule disabled.", expected);
        }

        return application.Enabled
            ? new(application.Id, FirewallRuleState.Correct, "Active.", expected)
            : new(application.Id, FirewallRuleState.Correct, "Disabled.", expected);
    }

    private static IReadOnlyList<FirewallRuleInfo> RulesFor(
        IReadOnlyList<FirewallRuleInfo> rules,
        Guid applicationId) =>
        rules.Where(rule => rule.ApplicationId == applicationId).ToArray();

    private static bool AddressListsEqual(string actual, string expected)
    {
        static string[] Normalize(string value) =>
            value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return Normalize(actual).SequenceEqual(Normalize(expected), StringComparer.OrdinalIgnoreCase);
    }
}
