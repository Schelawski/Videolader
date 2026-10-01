using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Videolader.Core;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as <c>Videolader.settings.json</c> next to the executable.
/// Falls back to <c>%APPDATA%\Videolader\</c> when the executable's folder is not writable.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "Videolader.settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _primaryPath;
    private readonly string _fallbackPath;
    private bool _primaryWritable = true;

    private readonly ISecretProtector? _protector;

    public SettingsStore(ISecretProtector? protector = null)
        : this(
            AppContext.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Videolader"),
            protector)
    {
    }

    /// <param name="protector">Encrypts the API key in the file. Without one the key is stored in plain text.</param>
    public SettingsStore(string primaryDirectory, string fallbackDirectory, ISecretProtector? protector = null)
    {
        _protector = protector;
        _primaryPath = Path.Combine(primaryDirectory, FileName);
        _fallbackPath = Path.Combine(fallbackDirectory, FileName);
        CurrentPath = _primaryPath;
    }

    /// <summary>Where the settings were last loaded from or saved to.</summary>
    public string CurrentPath { get; private set; }

    /// <summary>Loads the settings; returns defaults if there is no readable file. The newer file wins.</summary>
    public AppSettings Load()
    {
        var candidates = new[] { _primaryPath, _fallbackPath }
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc);

        foreach (var path in candidates)
        {
            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
                if (settings is null)
                    continue;

                var migrate = ReadApiKey(settings);
                settings.Normalize();
                CurrentPath = path;
                if (migrate)
                    TryRewrite(settings);
                return settings;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Corrupt or unreadable: try the other location, otherwise use defaults.
            }
        }

        var defaults = new AppSettings();
        defaults.Normalize();
        return defaults;
    }

    /// <summary>Saves next to the executable, or in the fallback folder if that fails.</summary>
    /// <exception cref="IOException">Neither location is writable.</exception>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        PrepareApiKey(settings);
        var json =JsonSerializer.Serialize(settings, JsonOptions);

        if (_primaryWritable)
        {
            try
            {
                WriteAtomically(_primaryPath, json);
                CurrentPath = _primaryPath;
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _primaryWritable = false;
            }
        }

        try
        {
            WriteAtomically(_fallbackPath, json);
            CurrentPath = _fallbackPath;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Fills <see cref="AppSettings.ApiKey"/> from the encrypted value, or from the plain text of an older version.
    /// Returns true if a plain text key should now be rewritten encrypted.
    /// </summary>
    private bool ReadApiKey(AppSettings settings)
    {
        var plain = settings.PlainApiKey?.Trim() ?? string.Empty;
        settings.ApiKey = string.Empty;

        if (_protector is not null && !string.IsNullOrEmpty(settings.ApiKeyProtected))
        {
            // Null = encrypted by another Windows user or on another computer: the key has to be entered again.
            settings.ApiKey = _protector.Unprotect(settings.ApiKeyProtected) ?? string.Empty;
            return false;
        }

        settings.ApiKey = plain;
        return _protector is not null && plain.Length > 0;
    }

    /// <summary>Sets the file fields: encrypted if possible, otherwise (no protector) plain text as before.</summary>
    private void PrepareApiKey(AppSettings settings)
    {
        var key = (settings.ApiKey ?? string.Empty).Trim();
        if (_protector is not null)
        {
            settings.ApiKeyProtected = key.Length > 0 ? _protector.Protect(key) : string.Empty;
            settings.PlainApiKey = null;
        }
        else
        {
            settings.ApiKeyProtected = string.Empty;
            settings.PlainApiKey = key;
        }
    }

    private void TryRewrite(AppSettings settings)
    {
        try
        {
            Save(settings);
        }
        catch (IOException)
        {
            // Not migrated this time; the next save or start tries again.
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }
}
