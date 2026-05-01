using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.UI.Xaml;

namespace PassKey.Desktop.Services;

/// <summary>
/// Checks for updates via the GitHub Releases API and handles download + launch.
/// Uses a static singleton HttpClient for thread safety and TCP connection reuse.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private const string ApiUrl =
        "https://api.github.com/repos/pexatar/PassKey/releases/latest";

    private const string InstallerAssetName = "PassKey-Setup-x64.exe";

    // Static singleton: thread-safe, reuses TCP connections across calls.
    // Timeout here is an outer safety net; the inner CancellationToken handles per-call limits.
    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "PassKey-Desktop-Updater/1.0" },
            { "Accept",     "application/vnd.github+json" }
        },
        Timeout = TimeSpan.FromSeconds(20)
    };

    // ── Singleton bus state ──────────────────────────────────────────────────
    public UpdateCheckResult? PendingUpdate { get; private set; }
    public event Action<UpdateCheckResult>? UpdateDetected;

    public void Publish(UpdateCheckResult result)
    {
        PendingUpdate = result;
        UpdateDetected?.Invoke(result);
    }

    // ── Core logic ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<UpdateCheckResult?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GitHubRelease>(
                ApiUrl,
                UpdateJsonContext.Default.GitHubRelease,
                ct);

            if (release is null) return null;

            // Parse semver from tag: "v1.0.5" → Version(1,0,5)
            var tagVersion = ParseVersion(release.TagName);
            if (tagVersion is null) return null;

            var current = GetCurrentVersion();
            if (current is null) return null;

            // Compare only Major.Minor.Build — ignore Revision (.0 suffix)
            var currentComparable = new Version(current.Major, current.Minor, current.Build);

            if (tagVersion <= currentComparable)
                return new UpdateCheckResult { UpdateAvailable = false };

            // Find the setup installer asset
            var installer = release.Assets.FirstOrDefault(a =>
                string.Equals(a.Name, InstallerAssetName, StringComparison.OrdinalIgnoreCase));

            return new UpdateCheckResult
            {
                UpdateAvailable      = true,
                NewVersion           = $"{tagVersion.Major}.{tagVersion.Minor}.{tagVersion.Build}",
                ReleasePageUrl       = release.HtmlUrl,
                InstallerDownloadUrl = installer?.BrowserDownloadUrl ?? string.Empty
            };
        }
        catch (OperationCanceledException)
        {
            return null; // timeout or cancellation — silent
        }
        catch
        {
            return null; // network failure, parse error — silent
        }
    }

    /// <inheritdoc />
    public async Task<string?> DownloadInstallerAsync(
        string url,
        IProgress<double> progress,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(url)) return null;

        var destPath = Path.Combine(Path.GetTempPath(), InstallerAssetName);

        try
        {
            using var response = await _http.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            await using var stream     = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(
                destPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81_920, useAsync: true);

            var buffer    = new byte[81_920];
            long bytesRead = 0;
            int  read;

            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesRead += read;

                if (totalBytes > 0)
                    progress.Report((double)bytesRead / totalBytes);
            }

            progress.Report(1.0); // ensure 100% is always reported
            return destPath;
        }
        catch
        {
            // Clean up any partial download
            try { File.Delete(destPath); } catch { /* ignore */ }
            return null;
        }
    }

    /// <inheritdoc />
    public void LaunchInstallerAndExit(string installerPath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName       = installerPath,
                UseShellExecute = true
            });
        }
        catch
        {
            // Launch failed — do NOT exit; let the user try again.
            return;
        }

        Application.Current.Exit();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Version? ParseVersion(string tag)
    {
        // "v1.0.5" → "1.0.5" → Version(1,0,5)
        var normalized = tag.TrimStart('v', 'V');
        return Version.TryParse(normalized, out var v) ? v : null;
    }

    private static Version? GetCurrentVersion()
    {
#if DEBUG
        // Force a low version in DEBUG so the update InfoBar can be tested without a real release.
        // Comment out this line to restore normal behaviour.
        // return new Version(0, 0, 1);
#endif
        return Assembly.GetExecutingAssembly().GetName().Version;
    }
}
