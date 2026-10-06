using System.Globalization;
using System.IO;

namespace IPFamilySwitcher.Services;

public sealed class Logger
{
    private const long MaximumLogBytes = 5 * 1024 * 1024;
    private readonly string _directory;
    private readonly object _gate = new();

    public Logger(string directory)
    {
        _directory = directory;
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, $"app-{DateTime.UtcNow:yyyy-MM-dd}.log");
                if (File.Exists(path) && new FileInfo(path).Length > MaximumLogBytes)
                {
                    File.Move(path, path + ".1", overwrite: true);
                }

                var detail = exception is null ? string.Empty : $" {exception}";
                File.AppendAllText(
                    path,
                    $"{DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)} [{level}] {message}{detail}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Logging must not hide the operation's original result.
        }
    }
}
