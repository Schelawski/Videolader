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

    public SettingsStore()
        : this(
            AppContext.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Videolader"))
    {
    }

    public SettingsStore(string primaryDirectory, string fallbackDirectory)
    {
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

                settings.Normalize();
                CurrentPath = path;
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
        var json = JsonSerializer.Serialize(settings, JsonOptions);

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

    private static void WriteAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }
}
