using System.Text.Json.Serialization;

namespace Videolader.Core;

/// <summary>
/// Everything Videolader remembers between sessions. Stored as <c>Videolader.settings.json</c>.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Folder the videos are saved to.</summary>
    public string OutputFolder { get; set; } = string.Empty;

    /// <summary>
    /// Optional YouTube Data API v3 key. Only used to read playlists and titles.
    /// Not written to the file directly: see <see cref="ApiKeyProtected"/> and <see cref="PlainApiKey"/>.
    /// </summary>
    [JsonIgnore]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The API key encrypted by the <see cref="ISecretProtector"/> (Base64). Set by <see cref="SettingsStore"/>.</summary>
    public string ApiKeyProtected { get; set; } = string.Empty;

    /// <summary>
    /// The API key in plain text, as older versions stored it (<c>"ApiKey"</c> in the file). Only read for migration,
    /// and written only when no <see cref="ISecretProtector"/> is available.
    /// </summary>
    [JsonPropertyName("ApiKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlainApiKey { get; set; }

    public VideoQuality Quality { get; set; } = VideoQuality.Max1080;

    public bool DownloadSubtitles { get; set; } = true;

    /// <summary>Comma-separated subtitle languages, e.g. "ru,de".</summary>
    public string SubtitleLanguages { get; set; } = "ru";

    /// <summary>Skip videos that already exist in the output folder or are listed in the archive.</summary>
    public bool SkipExisting { get; set; } = true;

    /// <summary>Browser to read YouTube cookies from ("" = none, "firefox", "chrome", ...).</summary>
    public string CookiesBrowser { get; set; } = string.Empty;

    /// <summary>Pause between two downloads in seconds (protects against YouTube rate limits).</summary>
    public int PauseSeconds { get; set; } = 3;

    /// <summary>Last content of the input box.</summary>
    public string LastInput { get; set; } = string.Empty;

    /// <summary>Day of the last automatic yt-dlp update (yyyy-MM-dd).</summary>
    public string LastToolUpdate { get; set; } = string.Empty;

    public const int MaxPauseSeconds = 600;

    /// <summary>Repairs values that are missing or out of range (e.g. from an edited file).</summary>
    public void Normalize()
    {
        OutputFolder = (OutputFolder ?? string.Empty).Trim();
        ApiKey = (ApiKey ?? string.Empty).Trim();
        SubtitleLanguages = SubtitleLanguageList.Normalize(SubtitleLanguages);
        if (SubtitleLanguages.Length == 0)
            SubtitleLanguages = "ru";
        CookiesBrowser = (CookiesBrowser ?? string.Empty).Trim().ToLowerInvariant();
        if (!Enum.IsDefined(Quality))
            Quality = VideoQuality.Max1080;
        PauseSeconds = Math.Clamp(PauseSeconds, 0, MaxPauseSeconds);
        LastInput ??= string.Empty;
        LastToolUpdate ??= string.Empty;
    }
}

/// <summary>Helpers for the comma-separated list of subtitle languages.</summary>
public static class SubtitleLanguageList
{
    public static IReadOnlyList<string> Split(string? text) =>
        (text ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string Normalize(string? text) => string.Join(",", Split(text));
}
