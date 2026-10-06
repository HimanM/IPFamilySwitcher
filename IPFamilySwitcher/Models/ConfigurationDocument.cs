namespace IPFamilySwitcher.Models;

public sealed class ConfigurationDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public List<ManagedApplication> Applications { get; set; } = [];
}
