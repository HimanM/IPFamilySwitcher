using System.IO;
using IPFamilySwitcher.Models;
using IPFamilySwitcher.Services;
using IPFamilySwitcher.Utilities;

namespace IPFamilySwitcher.Tests;

public sealed class DomainTests
{
    [Fact]
    public void RuleNamesAreStableAndIncludeTheApplicationGuid()
    {
        var id = Guid.Parse("c68da715-d660-4b18-a0fb-f2dd1be9335e");

        Assert.Equal(
            "IPFamilySwitcher-c68da715-d660-4b18-a0fb-f2dd1be9335e-BlockIPv6",
            RuleNameGenerator.ForIpv6Block(id));
        Assert.Equal(
            "IPFamilySwitcher-c68da715-d660-4b18-a0fb-f2dd1be9335e-BlockIPv4",
            RuleNameGenerator.ForIpv4Block(id));
        Assert.True(RuleNameGenerator.TryGetApplicationId(
            RuleNameGenerator.ForIpv6Block(id), out var parsedId));
        Assert.Equal(id, parsedId);
    }

    [Fact]
    public void AllIpv6RangesCoverBothHalvesOfTheAddressSpace()
    {
        Assert.Equal("0::/1,8000::/1", RuleNameGenerator.AllIpv6Ranges);
    }

    [Fact]
    public void ExecutablePathsAreCanonicalizedAndComparedCaseInsensitively()
    {
        var service = new ExecutableService();
        var path = service.Canonicalize(@"C:\Folder\..\Folder\App.EXE");

        Assert.Equal(@"C:\Folder\App.EXE", path);
        Assert.True(service.IsDuplicate([path], @"c:\folder\app.exe"));
    }

    [Fact]
    public void NonExecutablesAreRejected()
    {
        var service = new ExecutableService();

        Assert.Throws<ArgumentException>(() => service.Canonicalize(@"C:\Folder\App.dll"));
    }

    [Fact]
    public async Task ConfigurationRoundTripsVersionedApplications()
    {
        var directory = Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"));
        try
        {
            var application = new ManagedApplication
            {
                DisplayName = "Example",
                ExecutablePath = @"C:\Example\Example.exe",
                Mode = NetworkMode.IPv4Only
            };
            var service = new ConfigurationService(directory);
            await service.SaveAsync(new ConfigurationDocument { Applications = [application] });

            var loaded = await service.LoadAsync();

            Assert.Equal(ConfigurationDocument.CurrentVersion, loaded.Version);
            Assert.Single(loaded.Applications);
            Assert.Equal(NetworkMode.IPv4Only, loaded.Applications[0].Mode);
            Assert.Equal(application.Id, loaded.Applications[0].Id);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
