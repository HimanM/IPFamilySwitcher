using System.Diagnostics;
using System.IO;
using System.ComponentModel;

namespace IPFamilySwitcher.Services;

public static class ProcessStatusService
{
    public static IReadOnlyDictionary<string, string> Inspect(IEnumerable<string> paths)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var running = false;
            var inaccessible = false;
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path)))
            {
                using (process)
                {
                    try
                    {
                        running |= string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Win32Exception) { inaccessible = true; }
                    catch (InvalidOperationException) { /* Process exited during inspection. */ }
                }
            }
            results[path] = running ? "Running" : inaccessible ? "Unavailable" : "Not running";
        }
        return results;
    }
}
