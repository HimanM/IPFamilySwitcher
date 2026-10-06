using System.IO;
using IPFamilySwitcher.Models;
using IPFamilySwitcher.Services;
using IPFamilySwitcher.Utilities;

namespace IPFamilySwitcher.Tests;

public sealed class ReconciliationTests
{
    [Fact]
    public async Task ReportsMissingRuleForConfiguredRestrictedApplication()
    {
        var application = CreateApplication(NetworkMode.IPv4Only, Environment.ProcessPath!);
        var service = new FakeFirewallService([]);
        var reconciliation = new RuleReconciliationService(
            service,
            new ExecutableService(),
            new Logger(Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"))));

        var result = await reconciliation.ReconcileAsync([application]);

        Assert.Equal(FirewallRuleState.Missing, result[0].State);
    }

    [Fact]
    public async Task ReportsIncorrectRuleWhenRemoteFamilyDoesNotMatchMode()
    {
        var application = CreateApplication(NetworkMode.IPv4Only, Environment.ProcessPath!);
        var rule = new FirewallRuleInfo(
            RuleNameGenerator.ForIpv6Block(application.Id),
            application.Id,
            application.ExecutablePath,
            RuleNameGenerator.GroupName,
            true,
            2,
            0,
            "0.0.0.0/0",
            256,
            2147);
        var reconciliation = new RuleReconciliationService(
            new FakeFirewallService([rule]),
            new ExecutableService(),
            new Logger(Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"))));

        var result = await reconciliation.ReconcileAsync([application]);

        Assert.Equal(FirewallRuleState.Incorrect, result[0].State);
    }

    [Fact]
    public async Task AcceptsWindowsNormalizedIpv6RuleProperties()
    {
        var application = CreateApplication(NetworkMode.IPv4Only, Environment.ProcessPath!);
        var rule = new FirewallRuleInfo(
            RuleNameGenerator.ForIpv6Block(application.Id),
            application.Id,
            application.ExecutablePath,
            RuleNameGenerator.GroupName,
            true,
            2,
            0,
            " 8000::/1 ; 0::/1 ",
            256,
            7);
        var reconciliation = new RuleReconciliationService(
            new FakeFirewallService([rule]),
            new ExecutableService(),
            new Logger(Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"))));

        var result = await reconciliation.ReconcileAsync([application]);

        Assert.Equal(FirewallRuleState.Correct, result[0].State);
    }

    [Fact]
    public async Task ReportsDisabledWhenRestrictedRuleExistsButApplicationIsDisabled()
    {
        var application = CreateApplication(NetworkMode.IPv4Only, Environment.ProcessPath!);
        application.Enabled = false;
        var rule = new FirewallRuleInfo(
            RuleNameGenerator.ForIpv6Block(application.Id),
            application.Id,
            application.ExecutablePath,
            RuleNameGenerator.GroupName,
            false,
            2,
            0,
            RuleNameGenerator.AllIpv6Ranges,
            256,
            2147);
        var reconciliation = new RuleReconciliationService(
            new FakeFirewallService([rule]),
            new ExecutableService(),
            new Logger(Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"))));

        var result = await reconciliation.ReconcileAsync([application]);

        Assert.Equal(FirewallRuleState.Correct, result[0].State);
        Assert.Equal("Disabled.", result[0].Message);
    }

    [Fact]
    public void FindsRulesWithoutConfiguredApplicationsAsOrphans()
    {
        var application = CreateApplication(NetworkMode.Default);
        var orphanId = Guid.NewGuid();
        var rules = new[]
        {
            new FirewallRuleInfo(
                RuleNameGenerator.ForIpv4Block(orphanId),
                orphanId,
                @"C:\Example\Example.exe",
                RuleNameGenerator.GroupName,
                true,
                2,
                0,
                "0.0.0.0/0",
                256,
                2147)
        };

        var orphaned = RuleReconciliationService.GetOrphanedRules([application], rules);

        Assert.Single(orphaned);
        Assert.Equal(orphanId, orphaned[0].ApplicationId);
    }

    private static ManagedApplication CreateApplication(
        NetworkMode mode,
        string path = @"C:\Example\Example.exe") =>
        new()
        {
            DisplayName = "Example",
            ExecutablePath = path,
            Mode = mode
        };

    private sealed class FakeFirewallService(IReadOnlyList<FirewallRuleInfo> rules) : IFirewallService
    {
        public Task ApplyModeAsync(ManagedApplication application, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveManagedRuleAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetRuleEnabledAsync(Guid applicationId, bool enabled, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<FirewallRuleInfo>> GetManagedRulesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(rules);

        public Task RemoveAllManagedRulesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
