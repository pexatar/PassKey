using System.Text.Json.Serialization;

namespace PassKey.Desktop.Services;

/// <summary>
/// Represents the GitHub Releases API response for the latest release.
/// Only fields required by the auto-updater are mapped; unknown fields are ignored.
/// </summary>
internal sealed record GitHubRelease
{
    /// <summary>Tag name of the release, e.g. "v1.0.5".</summary>
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    /// <summary>Browser URL of the release page on GitHub.</summary>
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    /// <summary>List of downloadable assets attached to the release.</summary>
    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; init; } = [];
}

/// <summary>
/// A single downloadable asset attached to a GitHub release.
/// </summary>
internal sealed record GitHubAsset
{
    /// <summary>File name of the asset, e.g. "PassKey-Setup-x64.exe".</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Direct download URL for the asset binary.</summary>
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; init; } = string.Empty;
}

/// <summary>
/// Source-generated JSON serializer context for GitHub API response models.
/// AOT-safe: no reflection at runtime.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GitHubRelease))]
internal partial class UpdateJsonContext : JsonSerializerContext
{
}
