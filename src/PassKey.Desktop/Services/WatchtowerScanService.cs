using System.Collections.Concurrent;
using Microsoft.UI.Dispatching;
using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Desktop.Services;

/// <summary>
/// Orchestrates the Watchtower audit: walks the vault sequentially, computes local
/// strength scores + duplicate groups, optionally queries HIBP (k-anonymity) for each
/// password the user has opted in to check, and aggregates everything into a single
/// <see cref="WatchtowerResult"/> kept in <see cref="LastResult"/>.
/// </summary>
/// <remarks>
/// <para><b>Concurrency.</b> HIBP checks are network-bound, so they run with a bounded
/// degree of parallelism (<see cref="HibpConcurrency"/>) instead of one-at-a-time with a
/// courtesy delay — the old sequential path made a 1000-password vault take ~6 minutes.
/// The HIBP "range" (k-anonymity) endpoint is CDN-backed and built for volume, so a small
/// fixed concurrency is safe and well-behaved.</para>
/// <para><b>Caching.</b> The result is cached for 24 hours; calls within that window
/// return the cached value unless the caller passes <c>forceRefresh: true</c>. The
/// <see cref="ISettingsService.LastHibpScanUtc"/> field persists the cache anchor
/// across process restarts.</para>
/// <para><b>Privacy gate.</b> If <see cref="ISettingsService.HibpEnabled"/> is false
/// the scan still runs (so the user sees weak / duplicate stats) but skips every
/// HIBP call — no network traffic at all.</para>
/// </remarks>
public sealed class WatchtowerScanService : IWatchtowerScanService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    /// <summary>Maximum number of concurrent HIBP requests during a scan.</summary>
    private const int HibpConcurrency = 8;

    private readonly IVaultStateService _vaultState;
    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;
    private readonly IHibpService _hibp;
    private readonly ISettingsService _settings;
    private readonly DispatcherQueue? _uiDispatcher;

    public bool IsScanning { get; private set; }
    public WatchtowerResult? LastResult { get; private set; }

    public event Action<int, int>? Progress;
    public event Action<WatchtowerResult?>? Completed;

    public WatchtowerScanService(
        IVaultStateService vaultState,
        IPasswordStrengthAnalyzer strengthAnalyzer,
        IHibpService hibp,
        ISettingsService settings)
    {
        _vaultState = vaultState;
        _strengthAnalyzer = strengthAnalyzer;
        _hibp = hibp;
        _settings = settings;
        _uiDispatcher = DispatcherQueue.GetForCurrentThread();
    }

    public async Task<WatchtowerResult?> ScanAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var vault = _vaultState.CurrentVault;
        if (vault is null) return null;

        if (!forceRefresh && LastResult is { } cached && DateTime.UtcNow - cached.ScannedUtc < CacheTtl)
            return cached;

        if (IsScanning) return LastResult; // a concurrent scan is already producing the result

        IsScanning = true;
        try
        {
            var result = await Task.Run(() => RunScanAsync(vault, cancellationToken), cancellationToken)
                                   .ConfigureAwait(false);
            LastResult = result;
            _settings.LastHibpScanUtc = result.ScannedUtc;
            _settings.Save();
            RaiseCompleted(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            RaiseCompleted(null);
            throw;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task<WatchtowerResult> RunScanAsync(Vault vault, CancellationToken ct)
    {
        var passwords = vault.Passwords;
        var total = passwords.Count;
        var hibpEnabled = _settings.HibpEnabled;

        // Build duplicate detection map first (cheap, fully local).
        var groups = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);
        foreach (var p in passwords)
        {
            if (string.IsNullOrEmpty(p.Password)) continue;
            if (!groups.TryGetValue(p.Password, out var list))
            {
                list = [];
                groups[p.Password] = list;
            }
            list.Add(p.Id);
        }
        var duplicateIds = groups.Where(kv => kv.Value.Count > 1)
                                 .SelectMany(kv => kv.Value)
                                 .ToHashSet();

        var compromised = new ConcurrentBag<WatchtowerIssue>();
        var weak = new ConcurrentBag<WatchtowerIssue>();
        var duplicates = new ConcurrentBag<WatchtowerIssue>();
        int totalScore = 0, weakCount = 0, scanned = 0;

        // HIBP checks are network-bound: run them with bounded concurrency instead of
        // sequentially with a courtesy delay. When HIBP is disabled the work is purely
        // local (cheap strength + duplicate pass), so a single worker is enough.
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = hibpEnabled ? HibpConcurrency : 1,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(passwords, options, async (p, token) =>
        {
            // Local strength score — computed synchronously before any await so the Span
            // input never has to cross the await boundary.
            var strength = _strengthAnalyzer.Analyze(p.Password.AsSpan());
            int score = strength.Score;
            string label = strength.Label;

            Interlocked.Add(ref totalScore, score);
            bool isWeak = score < 40;
            if (isWeak) Interlocked.Increment(ref weakCount);
            bool isDup = duplicateIds.Contains(p.Id);

            int breachCount = 0;
            if (hibpEnabled && !string.IsNullOrEmpty(p.Password))
            {
                try
                {
                    breachCount = await _hibp.CheckPasswordAsync(p.Password, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // A single HIBP failure must not abort the whole scan — the rest of the
                    // audit (local strength + duplicates) still produces valid findings.
                    System.Diagnostics.Debug.WriteLine(
                        $"[Watchtower] HIBP check failed for entry {p.Id}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            var issue = new WatchtowerIssue(
                EntryId: p.Id,
                Title: p.Title,
                Username: p.Username,
                StrengthScore: score,
                StrengthLabel: label,
                BreachCount: breachCount,
                IsDuplicate: isDup);

            if (breachCount > 0) compromised.Add(issue);
            if (isWeak) weak.Add(issue);
            if (isDup) duplicates.Add(issue);

            // Report live progress (X of total) as each entry completes — order-independent.
            var done = Interlocked.Increment(ref scanned);
            RaiseProgress(done, total);
        }).ConfigureAwait(false);

        var avg = total > 0 ? totalScore / total : 0;
        return new WatchtowerResult(
            TotalPasswords: total,
            CompromisedCount: compromised.Count,
            WeakCount: weakCount,
            DuplicateCount: duplicates.Count,
            HealthScore: avg,
            ScannedUtc: DateTime.UtcNow,
            Compromised: compromised.ToList(),
            Weak: weak.ToList(),
            Duplicates: duplicates.ToList());
    }

    private void RaiseProgress(int scanned, int total)
    {
        if (_uiDispatcher is null) { Progress?.Invoke(scanned, total); return; }
        _uiDispatcher.TryEnqueue(() => Progress?.Invoke(scanned, total));
    }

    private void RaiseCompleted(WatchtowerResult? result)
    {
        if (_uiDispatcher is null) { Completed?.Invoke(result); return; }
        _uiDispatcher.TryEnqueue(() => Completed?.Invoke(result));
    }
}
