using System.Reflection;
using System.Text.RegularExpressions;

namespace Videolader.Core;

/// <summary>
/// The version of Videolader. Release builds get it from the Git tag (<c>v1.2.3</c> → <c>-p:Version=1.2.3</c>,
/// see <c>.github/workflows/release.yml</c>); local builds use the <c>Version</c> in Directory.Build.props.
/// </summary>
public static partial class AppVersion
{
    /// <summary>E.g. "1.2.3" or "1.3.0-beta.1" (without the "+commit" suffix the SDK appends).</summary>
    public static string Current { get; } = Read();

    private static string Read()
    {
        var informational = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
            return typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }

    /// <summary>"1.2.3", "v1.2.3", "1.2.3-beta.1"; the part after "-" marks a pre-release.</summary>
    [GeneratedRegex(@"^v?(?<number>\d+\.\d+\.\d+)(?:-(?<pre>[0-9A-Za-z.-]+))?$")]
    private static partial Regex VersionRegex();

    /// <summary>Parses a version or tag; <c>null</c> if it is not of the form 1.2.3(-pre).</summary>
    public static (Version Number, string? PreRelease)? Parse(string? text)
    {
        var match = VersionRegex().Match(text?.Trim() ?? string.Empty);
        if (!match.Success)
            return null;

        var pre = match.Groups["pre"].Success ? match.Groups["pre"].Value : null;
        return (Version.Parse(match.Groups["number"].Value), pre);
    }

    /// <summary>
    /// True when <paramref name="candidate"/> is newer than <paramref name="current"/>: a higher number, or the
    /// same number as a final version while <paramref name="current"/> is a pre-release (1.3.0 after 1.3.0-beta.1).
    /// Unparsable versions are never newer.
    /// </summary>
    public static bool IsNewer(string? candidate, string? current)
    {
        if (Parse(candidate) is not { } newer || Parse(current) is not { } now)
            return false;

        var compared = newer.Number.CompareTo(now.Number);
        return compared > 0 || (compared == 0 && newer.PreRelease is null && now.PreRelease is not null);
    }
}
