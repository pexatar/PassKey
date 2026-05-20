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
/// <para><b>Throttling.</b> HIBP's free tier has no hard rate limit but the courtesy
/// guidance is "no more than ~10 req/s". We stay conservative at 5 req/s (one call
/// every 200 ms).</para>
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
    private static readonly TimeSpan HibpThrottle = TimeSpan.FromMilliseconds(200); // 5 req/s

    private readonly IVaultStateService _vaultState;
    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;
    private readonly IHibpService _hibp;
    private readonly ISettingsService _settings;
    private readonly DispatcherQueue? _uiDispatcher;

    public bool IsScanning { get; private set; }
    public WatchtowerResult? LastResult { get; private set; }

    public event Action<double>? Progress;
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

        var compromised = new List<WatchtowerIssue>();
        var weak = new List<WatchtowerIssue>();
        var duplicates = new List<WatchtowerIssue>();
        int totalScore = 0, weakCount = 0;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var p = passwords[i];

            var strength = _strengthAnalyzer.Analyze(p.Password.AsSpan());
            totalScore += strength.Score;
            bool isWeak = strength.Score < 40;
            if (isWeak) weakCount++;
            bool isDup = duplicateIds.Contains(p.Id);

            int breachCount = 0;
            if (hibpEnabled && !string.IsNullOrEmpty(p.Password))
            {
                try
                {
                    breachCount = await _hibp.CheckPasswordAsync(p.Password, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // A single HIBP failure must not abort the whole scan — the rest of the
                    // audit (local strength + duplicates) still produces valid findings.
                    System.Diagnostics.Debug.WriteLine(
                        $"[Watchtower] HIBP check failed for entry {p.Id}: {ex.GetType().Name}: {ex.Message}");
                }
                // Courtesy throttle between successive HIBP calls — only when we actually called.
                if (i < total - 1) await Task.Delay(HibpThrottle, ct).ConfigureAwait(false);
            }

            var issue = new WatchtowerIssue(
                EntryId: p.Id,
                Title: p.Title,
                Username: p.Username,
                StrengthScore: strength.Score,
                StrengthLabel: strength.Label,
                BreachCount: breachCount,
                IsDuplicate: isDup);

            if (breachCount > 0) compromised.Add(issue);
            if (isWeak) weak.Add(issue);
            if (isDup) duplicates.Add(issue);

            RaiseProgress((double)(i + 1) / Math.Max(1, total));
        }

        var avg = total > 0 ? totalScore / total : 0;
        return new WatchtowerResult(
            TotalPasswords: total,
            CompromisedCount: compromised.Count,
            WeakCount: weakCount,
            DuplicateCount: duplicates.Count,
            HealthScore: avg,
            ScannedUtc: DateTime.UtcNow,
            Compromised: compromised,
            Weak: weak,
            Duplicates: duplicates);
    }

    private void RaiseProgress(double pct)
    {
        if (_uiDispatcher is null) { Progress?.Invoke(pct); return; }
        _uiDispatcher.TryEnqueue(() => Progress?.Invoke(pct));
    }

    private void RaiseCompleted(WatchtowerResult? result)
    {
        if (_uiDispatcher is null) { Completed?.Invoke(result); return; }
        _uiDispatcher.TryEnqueue(() => Completed?.Invoke(result));
    }
}
