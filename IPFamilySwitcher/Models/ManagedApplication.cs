namespace IPFamilySwitcher.Models;

public sealed class ManagedApplication
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string DisplayName { get; set; }

    public required string ExecutablePath { get; set; }

    public NetworkMode Mode { get; set; } = NetworkMode.Default;

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastReconciledAt { get; set; }

    public string? ExecutableSha256 { get; set; }
}
