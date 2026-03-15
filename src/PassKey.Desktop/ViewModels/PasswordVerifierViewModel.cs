using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Password Verifier page (Phase 10).
/// Tab 1: Manual password strength check.
/// Tab 2: Vault-wide audit (weak + duplicate passwords).
/// </summary>
public partial class PasswordVerifierViewModel : ObservableObject
{
    private readonly IPasswordStrengthAnalyzer _analyzer;
    private readonly IVaultStateService _vaultState;

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
    public partial int WeakCount { get; set; }

    [ObservableProperty]
    public partial int DuplicateCount { get; set; }

    [ObservableProperty]
    public partial bool IsAuditLoading { get; set; }

    [ObservableProperty]
    public partial bool HasAuditResults { get; set; }

    public ObservableCollection<AuditItem> WeakPasswords { get; } = [];
    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = [];

    public PasswordVerifierViewModel(
        IPasswordStrengthAnalyzer analyzer,
        IVaultStateService vaultState)
    {
        _analyzer = analyzer;
        _vaultState = vaultState;
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
    /// Runs a full audit of all passwords in the vault.
    /// </summary>
    [RelayCommand]
    private async Task RunAuditAsync()
    {
        var vault = _vaultState.CurrentVault;
        if (vault is null) return;

        IsAuditLoading = true;
        WeakPasswords.Clear();
        DuplicateGroups.Clear();

        // Heavy computation on background thread
        var (weak, dupes, totalCount, weakCount, dupeCount, avgScore, scoreLabel) =
            await Task.Run(() =>
            {
                var passwords = vault.Passwords;
                var auditItems = new List<AuditItem>(passwords.Count);
                var passwordGroups = new Dictionary<string, List<AuditItem>>();

                foreach (var entry in passwords)
                {
                    var result = _analyzer.Analyze(entry.Password.AsSpan());
                    var item = new AuditItem
                    {
                        Id = entry.Id,
                        Title = entry.Title,
                        Username = entry.Username,
                        Score = result.Score,
                        Label = result.Label
                    };

                    auditItems.Add(item);

                    if (!string.IsNullOrEmpty(entry.Password))
                    {
                        if (!passwordGroups.TryGetValue(entry.Password, out var group))
                        {
                            group = [];
                            passwordGroups[entry.Password] = group;
                        }
                        group.Add(item);
                    }
                }

                var w = auditItems.Where(a => a.Score < 60).OrderBy(a => a.Score).ToList();
                var d = passwordGroups
                    .Where(kv => kv.Value.Count > 1)
                    .Select(kv => new DuplicateGroup { Count = kv.Value.Count, Entries = kv.Value })
                    .ToList();

                var total = passwords.Count;
                var wCount = w.Count;
                var dCount = d.Sum(g => g.Entries.Count);
                var avg = total > 0 ? (int)auditItems.Average(a => a.Score) : 0;
                var label = avg switch
                {
                    < 20 => "VeryWeak",
                    < 40 => "Weak",
                    < 60 => "Medium",
                    < 80 => "Strong",
                    _ => "VeryStrong"
                };

                return (w, d, total, wCount, dCount, avg, label);
            });

        // Back on UI thread — update observable properties
        TotalPasswords = totalCount;
        WeakCount = weakCount;
        DuplicateCount = dupeCount;
        VaultScore = avgScore;
        VaultScoreLabel = scoreLabel;

        foreach (var w in weak)
            WeakPasswords.Add(w);
        foreach (var d in dupes)
            DuplicateGroups.Add(d);

        HasAuditResults = true;
        IsAuditLoading = false;
    }

    public void Initialize()
    {
        // Auto-run audit if vault has passwords
        if (_vaultState.CurrentVault?.Passwords.Count > 0)
            RunAuditCommand.Execute(null);
    }
}

public sealed class AuditItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public int Score { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class DuplicateGroup
{
    public int Count { get; init; }
    public List<AuditItem> Entries { get; init; } = [];
}
