using System.Runtime.InteropServices;
using System.IO;
using IPFamilySwitcher.Models;
using IPFamilySwitcher.Utilities;

namespace IPFamilySwitcher.Services;

public sealed class WindowsFirewallService : IFirewallService
{
    private const int OutboundDirection = 2;
    private const int BlockAction = 0;
    private const int AnyProtocol = 256;
    private const int AllProfiles = 2147;

    private readonly Logger _logger;

    public WindowsFirewallService(Logger? logger = null)
    {
        _logger = logger ?? new Logger(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "IPFamilySwitcher", "logs"));
    }

    public Task ApplyModeAsync(
        ManagedApplication application,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ApplyMode(application, cancellationToken), cancellationToken);

    public Task RemoveManagedRuleAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => RemoveManagedRule(applicationId, cancellationToken), cancellationToken);

    public Task SetRuleEnabledAsync(
        Guid applicationId,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => SetRuleEnabled(applicationId, enabled, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<FirewallRuleInfo>> GetManagedRulesAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => EnumerateManagedRules(cancellationToken), cancellationToken);

    public Task RemoveAllManagedRulesAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => RemoveAllManagedRules(cancellationToken), cancellationToken);

    private void ApplyMode(ManagedApplication application, CancellationToken cancellationToken)
    {
        ValidateExecutable(application);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            dynamic rules = CreatePolicy().Rules;
            RemoveRulesForApplication(rules, application.Id);
            cancellationToken.ThrowIfCancellationRequested();

            if (!application.Enabled || application.Mode == NetworkMode.Default)
            {
                return;
            }

            dynamic rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")!)
                ?? throw new InvalidOperationException("Windows Firewall rule type is unavailable.");
            var blockIpv6 = application.Mode == NetworkMode.IPv4Only;
            rule.Name = blockIpv6
                ? RuleNameGenerator.ForIpv6Block(application.Id)
                : RuleNameGenerator.ForIpv4Block(application.Id);
            rule.DisplayName = $"[IPFamilySwitcher] {application.DisplayName} - {(blockIpv6 ? "Block IPv6" : "Block IPv4")}";
            rule.Description = RuleNameGenerator.Description(application.Id);
            rule.Grouping = RuleNameGenerator.GroupName;
            rule.ApplicationName = application.ExecutablePath;
            rule.Direction = OutboundDirection;
            rule.Action = BlockAction;
            rule.Enabled = application.Enabled;
            rule.Protocol = AnyProtocol;
            rule.Profiles = AllProfiles;
            rule.RemoteAddresses = blockIpv6 ? "::/0" : "0.0.0.0/0";
            rules.Add(rule);

            var expected = blockIpv6
                ? RuleNameGenerator.ForIpv6Block(application.Id)
                : RuleNameGenerator.ForIpv4Block(application.Id);
            if (!EnumerateManagedRules(cancellationToken).Any(ruleInfo =>
                    string.Equals(ruleInfo.Name, expected, StringComparison.OrdinalIgnoreCase) &&
                    ruleInfo.Enabled == application.Enabled))
            {
                throw new InvalidOperationException("The firewall rule was created but could not be verified.");
            }

            _logger.Info($"Firewall rule created for application {application.Id:D}.");
        }
        catch (Exception exception)
        {
            _logger.Error($"Firewall operation failed for application {application.Id:D}.", exception);
            throw new FirewallOperationException(
                "Unable to apply the firewall rule. Administrator permission may be required.", exception);
        }
    }

    private void RemoveManagedRule(Guid applicationId, CancellationToken cancellationToken)
    {
        try
        {
            dynamic rules = CreatePolicy().Rules;
            RemoveRulesForApplication(rules, applicationId, cancellationToken);
            _logger.Info($"Firewall rules removed for application {applicationId:D}.");
        }
        catch (Exception exception)
        {
            _logger.Error($"Firewall rule removal failed for application {applicationId:D}.", exception);
            throw new FirewallOperationException(
                "Unable to remove the managed firewall rule.", exception);
        }
    }

    private void SetRuleEnabled(Guid applicationId, bool enabled, CancellationToken cancellationToken)
    {
        try
        {
            dynamic rules = CreatePolicy().Rules;
            foreach (dynamic rule in EnumerateRules(rules))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsOwnedRule(rule, applicationId))
                {
                    rule.Enabled = enabled;
                }
            }
            _logger.Info($"Firewall rules {(enabled ? "enabled" : "disabled")} for application {applicationId:D}.");
        }
        catch (Exception exception)
        {
            _logger.Error($"Firewall rule state change failed for application {applicationId:D}.", exception);
            throw new FirewallOperationException(
                "Unable to change the managed firewall rule state.", exception);
        }
    }

    private IReadOnlyList<FirewallRuleInfo> EnumerateManagedRules(CancellationToken cancellationToken)
    {
        try
        {
            dynamic rules = CreatePolicy().Rules;
            var result = new List<FirewallRuleInfo>();
            foreach (dynamic rule in EnumerateRules(rules))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals((string)rule.Grouping, RuleNameGenerator.GroupName, StringComparison.Ordinal))
                {
                    continue;
                }

                result.Add(ToInfo(rule));
            }

            return result;
        }
        catch (Exception exception)
        {
            _logger.Error("Firewall rule enumeration failed.", exception);
            throw new FirewallOperationException(
                "Unable to inspect managed Windows Firewall rules.", exception);
        }
    }

    private void RemoveAllManagedRules(CancellationToken cancellationToken)
    {
        try
        {
            dynamic rules = CreatePolicy().Rules;
            var names = EnumerateRules((object)rules)
                .Where(rule => string.Equals((string)rule.Grouping, RuleNameGenerator.GroupName, StringComparison.Ordinal))
                .Select(rule => (string)rule.Name)
                .ToArray();
            foreach (var name in names)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rules.Remove(name);
            }

            _logger.Info("All managed firewall rules removed.");
        }
        catch (Exception exception)
        {
            _logger.Error("Managed firewall rule cleanup failed.", exception);
            throw new FirewallOperationException(
                "Unable to remove all managed firewall rules.", exception);
        }
    }

    private static dynamic CreatePolicy()
    {
        var policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2")
            ?? throw new InvalidOperationException("Windows Firewall is unavailable.");
        return Activator.CreateInstance(policyType)
            ?? throw new InvalidOperationException("Windows Firewall policy is unavailable.");
    }

    private static IReadOnlyList<dynamic> EnumerateRules(object rules)
    {
        var result = new List<dynamic>();
        foreach (dynamic rule in (dynamic)rules)
        {
            result.Add(rule);
        }

        return result;
    }

    private static bool IsOwnedRule(dynamic rule, Guid applicationId)
    {
        if (!string.Equals((string)rule.Grouping, RuleNameGenerator.GroupName, StringComparison.Ordinal))
        {
            return false;
        }

        return (string)rule.Name == RuleNameGenerator.ForIpv4Block(applicationId) ||
               (string)rule.Name == RuleNameGenerator.ForIpv6Block(applicationId);
    }

    private static void RemoveRulesForApplication(
        object rules,
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        var names = EnumerateRules(rules)
            .Where(rule => IsOwnedRule(rule, applicationId))
            .Select(rule => (string)rule.Name)
            .ToArray();
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ((dynamic)rules).Remove(name);
        }
    }

    private static FirewallRuleInfo ToInfo(dynamic rule)
    {
        var name = (string)rule.Name;
        return new FirewallRuleInfo(
            name,
            TryGetApplicationId(name),
            (string)rule.ApplicationName,
            (string)rule.Grouping,
            (bool)rule.Enabled,
            (int)rule.Direction,
            (int)rule.Action,
            (string)rule.RemoteAddresses,
            (int)rule.Protocol,
            (int)rule.Profiles);
    }

    private static Guid? TryGetApplicationId(string name)
    {
        return RuleNameGenerator.TryGetApplicationId(name, out var id) ? id : null;
    }

    private static void ValidateExecutable(ManagedApplication application)
    {
        if (!File.Exists(application.ExecutablePath) ||
            !string.Equals(Path.GetExtension(application.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The configured executable does not exist or is not an .exe file.");
        }
    }
}
