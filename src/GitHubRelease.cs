using System.Text.RegularExpressions;

namespace MySwitcher;

/// <summary>Нужные поля из ответа GitHub API /releases/latest (без JSON-библиотек).</summary>
public sealed class GitHubRelease
{
    public Version Version { get; }
    public string ExeUrl { get; }

    private GitHubRelease(Version version, string exeUrl)
    {
        Version = version;
        ExeUrl = exeUrl;
    }

    private static readonly Regex TagRegex = new("\"tag_name\"\\s*:\\s*\"v?(?<v>[0-9][^\"]*)\"");
    private static readonly Regex ExeRegex = new("\"browser_download_url\"\\s*:\\s*\"(?<u>https://[^\"]+/MySwitcher\\.exe)\"");

    public static GitHubRelease? Parse(string json)
    {
        var tag = TagRegex.Match(json);
        var exe = ExeRegex.Match(json);
        if (!tag.Success || !exe.Success)
            return null;
        if (!Version.TryParse(tag.Groups["v"].Value, out var version))
            return null;
        return new GitHubRelease(Normalize(version), exe.Groups["u"].Value);
    }

    /// <summary>Сравниваем только major.minor.build: 1.2.0 и 1.2.0.0 — одно и то же.</summary>
    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
