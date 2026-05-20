using PassKey.Core.Models;

namespace PassKey.Desktop.Services;

/// <summary>
/// Runs a full-vault audit combining the local strength analyser and the remote
/// Have I Been Pwned k-anonymity check. The service is the orchestrator: throttling,
/// caching, and progress reporting live here; <see cref="PassKey.Core.Services.IHibpService"/>
/// stays a pure HTTP call.
/// </summary>
public interface IWatchtowerScanService
{
    /// <summary>Indicates that <see cref="ScanAsync"/> is currently running.</summary>
    bool IsScanning { get; }

    /// <summary>Cached result of the most recent successful scan (null until the first scan).</summary>
    WatchtowerResult? LastResult { get; }

    /// <summary>
    /// Raised on the UI thread every time a single entry has been checked. The argument
    /// is in <c>[0, 1]</c>. Useful for progress bars during long scans.
    /// </summary>
    event Action<double>? Progress;

    /// <summary>Raised on the UI thread when the scan finishes (success, cancel, or error).</summary>
    event Action<WatchtowerResult?>? Completed;

    /// <summary>
    /// Runs (or returns the cached value from) the full audit.
    /// </summary>
    /// <param name="forceRefresh">If true, ignore the 24-hour cache and rescan now.</param>
    Task<WatchtowerResult?> ScanAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

/// <summary>Per-entry findings produced by the scan.</summary>
public sealed record WatchtowerIssue(
    Guid EntryId,
    string Title,
    string Username,
    int StrengthScore,
    string StrengthLabel,
    int BreachCount,
    bool IsDuplicate);

/// <summary>Aggregated result of a full Watchtower scan.</summary>
public sealed record WatchtowerResult(
    int TotalPasswords,
    int CompromisedCount,
    int WeakCount,
    int DuplicateCount,
    int HealthScore,
    DateTime ScannedUtc,
    IReadOnlyList<WatchtowerIssue> Compromised,
    IReadOnlyList<WatchtowerIssue> Weak,
    IReadOnlyList<WatchtowerIssue> Duplicates);
