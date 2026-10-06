using IPFamilySwitcher.Models;

namespace IPFamilySwitcher.Services;

public interface IFirewallService
{
    Task ApplyModeAsync(ManagedApplication application, CancellationToken cancellationToken = default);

    Task RemoveManagedRuleAsync(Guid applicationId, CancellationToken cancellationToken = default);

    Task SetRuleEnabledAsync(Guid applicationId, bool enabled, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FirewallRuleInfo>> GetManagedRulesAsync(
        CancellationToken cancellationToken = default);

    Task RemoveAllManagedRulesAsync(CancellationToken cancellationToken = default);
}
