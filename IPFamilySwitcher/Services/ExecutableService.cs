using System.IO;

namespace IPFamilySwitcher.Services;

public sealed class ExecutableService
{
    public string Canonicalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path.Trim());
        if (!string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The selected file must have an .exe extension.", nameof(path));
        }

        return fullPath;
    }

    public bool Exists(string path) => File.Exists(path);

    public bool IsDuplicate(IEnumerable<string> existingPaths, string candidatePath) =>
        existingPaths.Any(path =>
            string.Equals(Canonicalize(path), Canonicalize(candidatePath), StringComparison.OrdinalIgnoreCase));
}
