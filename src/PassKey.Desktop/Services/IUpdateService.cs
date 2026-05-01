namespace PassKey.Desktop.Services;

/// <summary>
/// Result of an update check against the GitHub Releases API.
/// </summary>
public sealed record UpdateCheckResult
{
    /// <summary>True if a version newer than the currently installed one is available.</summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>Version string parsed from the release tag, e.g. "1.0.5". Empty if no update.</summary>
    public string NewVersion { get; init; } = string.Empty;

    /// <summary>Browser URL of the GitHub release page (used by the "Novità" button).</summary>
    public string ReleasePageUrl { get; init; } = string.Empty;

    /// <summary>Direct download URL for the PassKey-Setup-x64.exe asset. Empty if asset not found.</summary>
    public string InstallerDownloadUrl { get; init; } = string.Empty;
}

/// <summary>
/// Checks for application updates via the GitHub Releases API and coordinates
/// download and installation of the installer asset.
/// Also acts as a lightweight singleton bus: App publishes the result via
/// <see cref="Publish"/>, and ShellViewModel subscribes to <see cref="UpdateDetected"/>.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks GitHub for a newer release. Returns <c>null</c> on network failure or timeout.
    /// Never throws: all exceptions are caught internally.
    /// </summary>
    Task<UpdateCheckResult?> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the installer to %TEMP%\PassKey-Setup-x64.exe, reporting progress [0.0–1.0].
    /// Returns the full path to the downloaded file, or <c>null</c> on failure.
    /// </summary>
    Task<string?> DownloadInstallerAsync(
        string url,
        IProgress<double> progress,
        CancellationToken ct = default);

    /// <summary>
    /// Launches the downloaded installer via shell execute and exits the application.
    /// Must be called on the UI thread. Does NOT exit if the launch itself fails.
    /// </summary>
    void LaunchInstallerAndExit(string installerPath);

    // ── Singleton bus ────────────────────────────────────────────────────────

    /// <summary>
    /// The last update result published via <see cref="Publish"/>, or <c>null</c> if
    /// no update has been detected yet. ShellViewModel reads this on construction to
    /// handle the race where the background check finishes before the VM is created.
    /// </summary>
    UpdateCheckResult? PendingUpdate { get; }

    /// <summary>
    /// Fired when a new update is detected. Always invoked on the UI thread
    /// (the caller of <see cref="Publish"/> is responsible for dispatching).
    /// </summary>
    event Action<UpdateCheckResult>? UpdateDetected;

    /// <summary>
    /// Stores <paramref name="result"/> as <see cref="PendingUpdate"/> and fires
    /// <see cref="UpdateDetected"/>. Must be called on the UI thread.
    /// </summary>
    void Publish(UpdateCheckResult result);
}
