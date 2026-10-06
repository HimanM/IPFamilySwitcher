using System.Reflection;
using System.Text.Json;

namespace IPFamilySwitcher.Services;

public sealed record ReleaseInfo(string Version, string[] ReleaseNotes)
{
    public const string RepositoryUrl = "https://github.com/HimanM/IPFamilySwitcher";
    public static ReleaseInfo Current { get; } = Load();

    private static ReleaseInfo Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("IPFamilySwitcher.version.json")
            ?? throw new InvalidOperationException("Release metadata is missing.");
        return JsonSerializer.Deserialize<ReleaseInfo>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Release metadata is invalid.");
    }
}
