using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using IPFamilySwitcher.Models;

namespace IPFamilySwitcher.Services;

public sealed class ConfigurationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _directory;
    private readonly string _configurationPath;
    private readonly string _backupPath;
    private readonly ExecutableService _executableService;
    private readonly Logger _logger;

    public ConfigurationService(
        string? directory = null,
        ExecutableService? executableService = null,
        Logger? logger = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "IPFamilySwitcher");
        _configurationPath = Path.Combine(_directory, "config.json");
        _backupPath = Path.Combine(_directory, "config.json.bak");
        _executableService = executableService ?? new ExecutableService();
        _logger = logger ?? new Logger(Path.Combine(_directory, "logs"));
    }

    public string ConfigurationPath => _configurationPath;

    public async Task<ConfigurationDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        try
        {
            if (!File.Exists(_configurationPath))
            {
                return new ConfigurationDocument();
            }

            await using var stream = File.OpenRead(_configurationPath);
            var document = await JsonSerializer.DeserializeAsync<ConfigurationDocument>(
                stream, SerializerOptions, cancellationToken);
            return Normalize(document ?? new ConfigurationDocument());
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            _logger.Error("Config load failure.", exception);
            return await LoadBackupAsync(cancellationToken);
        }
    }

    public async Task SaveAsync(ConfigurationDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(_directory);
        document.Version = ConfigurationDocument.CurrentVersion;
        var temporaryPath = _configurationPath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(_configurationPath))
            {
                File.Replace(temporaryPath, _configurationPath, _backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _configurationPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Config write failure.", exception);
            TryDeleteTemporary(temporaryPath);
            throw;
        }
    }

    private async Task<ConfigurationDocument> LoadBackupAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_backupPath))
        {
            return new ConfigurationDocument();
        }

        try
        {
            await using var stream = File.OpenRead(_backupPath);
            return Normalize(await JsonSerializer.DeserializeAsync<ConfigurationDocument>(
                stream, SerializerOptions, cancellationToken) ?? new ConfigurationDocument());
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            _logger.Error("Config backup recovery failure.", exception);
            return new ConfigurationDocument();
        }
    }

    private ConfigurationDocument Normalize(ConfigurationDocument document)
    {
        document.Version = ConfigurationDocument.CurrentVersion;
        document.Applications ??= [];
        return document;
    }

    private static void TryDeleteTemporary(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // The original write error is more useful to the caller.
        }
    }
}
