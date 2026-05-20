using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the "Verifica" page. Hosts two tabs:
/// <list type="bullet">
///   <item><b>Password</b> — manual single-password strength check (paste/type a password
///         and see the strength meter / suggestions / breach status update live).</item>
///   <item><b>Vault</b> — vault-wide audit combining local strength + duplicate detection
///         with optional HIBP k-anonymity checks (when the user has opted in). Owned by
///         <see cref="IWatchtowerScanService"/> for the scan orchestration; this VM is
///         the presentation layer.</item>
/// </list>
/// In PassKey 2.0 the standalone "Watchtower" page was folded into Tab 2 to remove the
/// redundancy between "Audit Vault" (local only) and "Watchtower" (HIBP only).
/// </summary>
public partial class PasswordVerifierViewModel : ObservableObject, IDisposable
{
    private readonly IPasswordStrengthAnalyzer _analyzer;
    private readonly IVaultStateService _vaultState;
    private readonly IWatchtowerScanService _scan;
    private readonly ISettingsService _settings;
    private bool _disposed;

    // ===== Tab 1: Manual Verify =====

    [ObservableProperty]
    public partial PasswordStrengthResult? AnalysisResult { get; set; }

    [ObservableProperty]
    public partial bool HasInput { get; set; }

    // ===== Tab 2: Vault Audit =====

    [ObservableProperty]
    public partial int VaultScore { get; set; }

    [ObservableProperty]
    public partial string VaultScoreLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalPasswords { get; set; }

    [ObservableProperty]
    public partial int CompromisedCount { get; set; }

    [ObservableProperty]
    public partial int WeakCount { get; set; }

    [ObservableProperty]
    public partial int DuplicateCount { get; set; }

    [ObservableProperty]
    public partial bool IsAuditLoading { get; set; }

    [ObservableProperty]
    public partial double AuditProgress { get; set; }

    [ObservableProperty]
    public partial bool HasAuditResults { get; set; }

    [ObservableProperty]
    public partial bool HibpEnabled { get; set; }

    public ObservableCollection<WatchtowerIssue> CompromisedPasswords { get; } = [];
    public ObservableCollection<WatchtowerIssue> WeakPasswords { get; } = [];
    public ObservableCollection<WatchtowerIssue> DuplicateEntries { get; } = [];

    public PasswordVerifierViewModel(
        IPasswordStrengthAnalyzer analyzer,
        IVaultStateService vaultState,
        IWatchtowerScanService scan,
        ISettingsService settings)
    {
        _analyzer = analyzer;
        _vaultState = vaultState;
        _scan = scan;
        _settings = settings;
        HibpEnabled = settings.HibpEnabled;

        _scan.Progress += OnScanProgress;
        _scan.Completed += OnScanCompleted;
    }

    /// <summary>
    /// Called from the View when SecureInputBox text changes.
    /// Analyzes the password in real-time.
    /// </summary>
    public void AnalyzePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            HasInput = false;
            AnalysisResult = null;
            return;
        }

        HasInput = true;
        AnalysisResult = _analyzer.Analyze(password.AsSpan());
    }

    /// <summary>
    /// Runs a full audit of the vault: local strength + duplicate detection always; HIBP
    /// k-anonymity checks only when the user has explicitly opted in (privacy-by-default).
    /// </summary>
    [RelayCommand]
    private async Task RunAuditAsync()
    {
        var vault = _vaultState.CurrentVault;
        if (vault is null) return;

        try
        {
            IsAuditLoading = true;
            AuditProgress = 0;

            // Reset *both* the collections and the count properties to a known-blank state
            // so the UI does not briefly show stale numbers from a previous scan while the
            // new one is in flight.
            CompromisedPasswords.Clear();
            WeakPasswords.Clear();
            DuplicateEntries.Clear();
            TotalPasswords = 0;
            CompromisedCount = 0;
            WeakCount = 0;
            DuplicateCount = 0;
            VaultScore = 0;
            VaultScoreLabel = string.Empty;
            HasAuditResults = false;

            await _scan.ScanAsync(forceRefresh: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Verifier] Audit failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void Initialize()
    {
        // Refresh the read-only opt-in mirror on every page entry (the user may have toggled
        // it in Settings since the last visit).
        HibpEnabled = _settings.HibpEnabled;

        var currentCount = _vaultState.CurrentVault?.Passwords.Count ?? 0;

        // If we have a cached scan whose total still matches the current vault size, show
        // it immediately so the UI never blanks. We then ALWAYS kick off a fresh scan in the
        // background — the user explicitly asked for an auto-refresh on every navigation
        // to Verifica/Vault. Cached entries get superseded smoothly when the scan completes.
        if (_scan.LastResult is { } cached && cached.TotalPasswords == currentCount)
        {
            ApplyResult(cached);
        }

        if (currentCount > 0)
            RunAuditCommand.Execute(null);
    }

    // ─── Scan service event handlers ──────────────────────────────────────────

    private void OnScanProgress(double pct) => AuditProgress = pct;

    private void OnScanCompleted(WatchtowerResult? result)
    {
        IsAuditLoading = false;
        if (result is not null) ApplyResult(result);
    }

    private void ApplyResult(WatchtowerResult result)
    {
        // IMPORTANT — populate the ObservableCollections *before* mutating the count
        // properties. The View rebuilds each expander when its corresponding *Count
        // property changes; if the collection were still empty at that moment the
        // expander header would say "(N)" but the body would be blank.
        CompromisedPasswords.Clear();
        foreach (var i in result.Compromised) CompromisedPasswords.Add(i);
        WeakPasswords.Clear();
        foreach (var i in result.Weak) WeakPasswords.Add(i);
        DuplicateEntries.Clear();
        foreach (var i in result.Duplicates) DuplicateEntries.Add(i);

        TotalPasswords = result.TotalPasswords;
        CompromisedCount = result.CompromisedCount;
        WeakCount = result.WeakCount;
        DuplicateCount = result.DuplicateCount;
        VaultScore = result.HealthScore;
        VaultScoreLabel = result.HealthScore switch
        {
            < 20 => "VeryWeak",
            < 40 => "Weak",
            < 60 => "Medium",
            < 80 => "Strong",
            _    => "VeryStrong",
        };

        HasAuditResults = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scan.Progress -= OnScanProgress;
        _scan.Completed -= OnScanCompleted;
    }
}
