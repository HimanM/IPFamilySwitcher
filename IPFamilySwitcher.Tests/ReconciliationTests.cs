using System.IO;
using System.Runtime.InteropServices;
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
            int.MaxValue);
        var reconciliation = new RuleReconciliationService(
            new FakeFirewallService([rule]),
            new ExecutableService(),
            new Logger(Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"))));

        var result = await reconciliation.ReconcileAsync([application]);

        Assert.Equal(FirewallRuleState.Incorrect, result[0].State);
    }

    [Theory]
    [InlineData(" 8000::/1 ; 0::/1 ", true, FirewallRuleState.Correct)]
    [InlineData("::/1,8000::/1", true, FirewallRuleState.Correct)]
    [InlineData("::/1,8000::/1", false, FirewallRuleState.Correct)]
    [InlineData("0000:0000:0000:0000:0000:0000:0000:0000/1,8000::/1", true, FirewallRuleState.Correct)]
    [InlineData("::/2,8000::/1", true, FirewallRuleState.Incorrect)]
    [InlineData("::/1", true, FirewallRuleState.Incorrect)]
    [InlineData("0.0.0.0/0", true, FirewallRuleState.Incorrect)]
    [InlineData("*", true, FirewallRuleState.Incorrect)]
    public async Task ValidatesWindowsNormalizedIpv6RuleProperties(
        string remoteAddresses, bool enabled, FirewallRuleState expectedState)
    {
        var application = CreateApplication(NetworkMode.IPv4Only, Environment.ProcessPath!);
        application.Enabled = enabled;
        var rule = new FirewallRuleInfo(
            RuleNameGenerator.ForIpv6Block(application.Id),
            application.Id,
            application.ExecutablePath,
            RuleNameGenerator.GroupName,
            enabled,
            2,
            0,
            remoteAddresses,
            256,
            int.MaxValue);
        var reconciliation = new RuleReconciliationService(
            new FakeFirewallService([rule]),
            new ExecutableService(),
            new Logger(Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"))));

        var result = await reconciliation.ReconcileAsync([application]);

        Assert.Equal(expectedState, result[0].State);
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
            int.MaxValue);
        var reconciliation = new RuleReconciliationService(
            new FakeFirewallService([rule]),
            new ExecutableService(),
            new Logger(Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"))));

        var result = await reconciliation.ReconcileAsync([application]);

        Assert.Equal(FirewallRuleState.Correct, result[0].State);
        Assert.Equal("Disabled.", result[0].Message);
    }

    [Theory]
    [InlineData(int.MaxValue, FirewallRuleState.Correct)]
    [InlineData(7, FirewallRuleState.Correct)]
    [InlineData(3, FirewallRuleState.Incorrect)]
    [InlineData(2147, FirewallRuleState.Incorrect)]
    public async Task RequiresAllProfiles(int profiles, FirewallRuleState expectedState)
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
            RuleNameGenerator.AllIpv6Ranges,
            256,
            profiles);
        var reconciliation = new RuleReconciliationService(
            new FakeFirewallService([rule]),
            new ExecutableService(),
            new Logger(Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"))));

        var result = await reconciliation.ReconcileAsync([application]);

        Assert.Equal(expectedState, result[0].State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ipv4BlockPropertiesAreAcceptedByWindowsAndReconcile(bool enabled)
    {
        var application = CreateApplication(NetworkMode.IPv6Only, Environment.ProcessPath!);
        application.Enabled = enabled;
        // A detached COM rule exercises Windows validation without installing a firewall rule.
        dynamic rule = WindowsFirewallService.CreateIpv4BlockRule(application);
        try
        {
            Assert.Equal("0.0.0.0-255.255.255.255", (string)rule.RemoteAddresses);
            Assert.Equal(int.MaxValue, (int)rule.Profiles);
            Assert.Equal(RuleNameGenerator.GroupName, (string)rule.Grouping);
            var info = new FirewallRuleInfo((string)rule.Name, application.Id,
                (string)rule.ApplicationName, (string)rule.Grouping, (bool)rule.Enabled,
                (int)rule.Direction, (int)rule.Action, (string)rule.RemoteAddresses,
                (int)rule.Protocol, (int)rule.Profiles);
            var reconciliation = new RuleReconciliationService(new FakeFirewallService([info]));
            var result = await reconciliation.ReconcileAsync([application]);
            Assert.Equal(FirewallRuleState.Correct, result[0].State);
        }
        finally
        {
            Marshal.FinalReleaseComObject((object)rule);
        }
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
