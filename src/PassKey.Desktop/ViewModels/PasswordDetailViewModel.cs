using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels.Base;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Password detail ViewModel for add/edit panel.
/// Fields: Title, URL, Username, Password, Notes — and (PassKey 2.0) optional TOTP
/// fields exposed live to the View so the user sees a refreshing 2FA code.
/// Shared add/edit/save/delete plumbing is provided by <see cref="BaseDetailViewModel{TEntry}"/>.
/// </summary>
public partial class PasswordDetailViewModel : BaseDetailViewModel<PasswordEntry>
{
    private readonly IPasswordGenerator _generator;
    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;
    private readonly ITotpService _totp;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Url { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? FaviconBase64 { get; set; }

    [ObservableProperty]
    public partial int PasswordStrengthScore { get; set; }

    [ObservableProperty]
    public partial string PasswordStrengthLabel { get; set; } = string.Empty;

    // ── TOTP / 2FA ───────────────────────────────────────────────────────────

    /// <summary>Base32 seed for the TOTP, or null when 2FA is not configured for this entry.</summary>
    [ObservableProperty]
    public partial string? TotpSecret { get; set; }

    /// <summary>HMAC algorithm (default SHA1, see <see cref="PasswordEntry.TotpAlgorithm"/>).</summary>
    [ObservableProperty]
    public partial string TotpAlgorithm { get; set; } = "SHA1";

    /// <summary>Number of digits (default 6, see <see cref="PasswordEntry.TotpDigits"/>).</summary>
    [ObservableProperty]
    public partial int TotpDigits { get; set; } = 6;

    /// <summary>Time-step in seconds (default 30, see <see cref="PasswordEntry.TotpPeriod"/>).</summary>
    [ObservableProperty]
    public partial int TotpPeriod { get; set; } = 30;

    /// <summary>The currently valid TOTP code formatted as a space-grouped string ("123 456"), or empty when no secret.</summary>
    [ObservableProperty]
    public partial string CurrentTotpCode { get; set; } = string.Empty;

    /// <summary>Seconds remaining before the current code rolls over.</summary>
    [ObservableProperty]
    public partial int TotpRemainingSeconds { get; set; }

    /// <summary>True when <see cref="TotpSecret"/> is non-empty — the View flips between empty/filled states off this.</summary>
    public bool HasTotp => !string.IsNullOrWhiteSpace(TotpSecret);

    // ── Inline validation (T5.6) ───────────────────────────────────────────────

    [ObservableProperty]
    public partial bool IsTitleEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsUsernameEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsPasswordEmpty { get; set; }

    public PasswordDetailViewModel(
        IVaultStateService vaultState,
        IPasswordGenerator generator,
        IDialogQueueService dialogQueue,
        IPasswordStrengthAnalyzer strengthAnalyzer,
        ITotpService totp)
        : base(vaultState, dialogQueue)
    {
        _generator = generator;
        _strengthAnalyzer = strengthAnalyzer;
        _totp = totp;
    }

    // ─── Template-method overrides ────────────────────────────────────────────

    protected override string GetPanelTitleForNew() => "Aggiungi password";
    protected override string GetPanelTitleForEdit() => "Modifica password";
    protected override string GetDeleteDisplayName(PasswordEntry entry) => entry.Title;

    protected override IList<PasswordEntry> GetVaultCollection(Vault vault) => vault.Passwords;

    protected override void ResetFieldsForNew()
    {
        Title = string.Empty;
        Url = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        Notes = string.Empty;
        FaviconBase64 = null;
        TotpSecret = null;
        TotpAlgorithm = "SHA1";
        TotpDigits = 6;
        TotpPeriod = 30;
        CurrentTotpCode = string.Empty;
        TotpRemainingSeconds = 0;
        IsTitleEmpty = true;
        IsUsernameEmpty = true;
        IsPasswordEmpty = true;
    }

    protected override void LoadFromEntry(PasswordEntry entry)
    {
        Title = entry.Title;
        Url = entry.Url;
        Username = entry.Username;
        Password = entry.Password;
        Notes = entry.Notes;
        FaviconBase64 = entry.FaviconBase64;
        TotpSecret = entry.TotpSecret;
        TotpAlgorithm = entry.TotpAlgorithm;
        TotpDigits = entry.TotpDigits;
        TotpPeriod = entry.TotpPeriod;
        RefreshTotpDisplay();
    }

    protected override PasswordEntry CreateNewEntry() => new()
    {
        Title = Title.Trim(),
        Url = Url.Trim(),
        Username = Username.Trim(),
        Password = Password,
        Notes = Notes.Trim(),
        FaviconBase64 = FaviconBase64,
        TotpSecret = string.IsNullOrWhiteSpace(TotpSecret) ? null : TotpSecret.Trim(),
        TotpAlgorithm = TotpAlgorithm,
        TotpDigits = TotpDigits,
        TotpPeriod = TotpPeriod,
    };

    protected override void ApplyToEntry(PasswordEntry entry)
    {
        entry.Title = Title.Trim();
        entry.Url = Url.Trim();
        entry.Username = Username.Trim();
        entry.Password = Password;
        entry.Notes = Notes.Trim();
        entry.FaviconBase64 = FaviconBase64;
        entry.TotpSecret = string.IsNullOrWhiteSpace(TotpSecret) ? null : TotpSecret.Trim();
        entry.TotpAlgorithm = TotpAlgorithm;
        entry.TotpDigits = TotpDigits;
        entry.TotpPeriod = TotpPeriod;
    }

    protected override void UpdateCanSave()
    {
        CanSave = !string.IsNullOrWhiteSpace(Title) &&
                  !string.IsNullOrWhiteSpace(Username) &&
                  !string.IsNullOrWhiteSpace(Password);
    }

    // ─── Property change handlers ─────────────────────────────────────────────

    partial void OnTitleChanged(string value)
    {
        IsTitleEmpty = string.IsNullOrWhiteSpace(value);
        UpdateCanSave();
    }

    partial void OnUsernameChanged(string value)
    {
        IsUsernameEmpty = string.IsNullOrWhiteSpace(value);
        UpdateCanSave();
    }

    partial void OnPasswordChanged(string value)
    {
        IsPasswordEmpty = string.IsNullOrWhiteSpace(value);
        UpdateCanSave();
        UpdatePasswordStrength();
    }

    partial void OnTotpSecretChanged(string? value)
    {
        OnPropertyChanged(nameof(HasTotp));
        RefreshTotpDisplay();
    }

    private void UpdatePasswordStrength()
    {
        if (string.IsNullOrEmpty(Password))
        {
            PasswordStrengthScore = 0;
            PasswordStrengthLabel = string.Empty;
            return;
        }
        var result = _strengthAnalyzer.Analyze(Password.AsSpan());
        PasswordStrengthScore = result.Score;
        PasswordStrengthLabel = result.Label;
    }

    // ─── TOTP helpers (called by the View timer) ──────────────────────────────

    /// <summary>
    /// Recomputes <see cref="CurrentTotpCode"/> and <see cref="TotpRemainingSeconds"/>
    /// from the current <see cref="TotpSecret"/>. Cheap — safe to call every second.
    /// </summary>
    public void RefreshTotpDisplay()
    {
        if (!HasTotp)
        {
            CurrentTotpCode = string.Empty;
            TotpRemainingSeconds = 0;
            return;
        }

        var probe = new PasswordEntry
        {
            TotpSecret = TotpSecret,
            TotpAlgorithm = TotpAlgorithm,
            TotpDigits = TotpDigits,
            TotpPeriod = TotpPeriod,
        };

        var raw = _totp.GenerateCode(probe);
        CurrentTotpCode = FormatCode(raw);
        TotpRemainingSeconds = _totp.RemainingSeconds(probe);
    }

    /// <summary>Returns the unformatted (no spaces) current code, ready to be copied to the clipboard.</summary>
    public string GetCurrentTotpCodeRaw()
    {
        if (!HasTotp) return string.Empty;
        var probe = new PasswordEntry
        {
            TotpSecret = TotpSecret,
            TotpAlgorithm = TotpAlgorithm,
            TotpDigits = TotpDigits,
            TotpPeriod = TotpPeriod,
        };
        return _totp.GenerateCode(probe);
    }

    /// <summary>
    /// Applies an <c>otpauth://</c> URI (typically the textual payload of a QR code)
    /// to this entry. Returns true on success.
    /// </summary>
    public bool ApplyOtpAuthUri(string uri)
    {
        var parsed = _totp.ParseOtpAuthUri(uri);
        if (parsed?.TotpSecret is not { Length: > 0 }) return false;

        TotpSecret = parsed.TotpSecret;
        TotpAlgorithm = parsed.TotpAlgorithm;
        TotpDigits = parsed.TotpDigits;
        TotpPeriod = parsed.TotpPeriod;

        // If the user is creating a fresh entry, pre-fill Title / Username from the URI
        // labels too — only when the user hasn't typed anything yet (don't clobber input).
        if (string.IsNullOrWhiteSpace(Title) && parsed.Title.Length > 0) Title = parsed.Title;
        if (string.IsNullOrWhiteSpace(Username) && parsed.Username.Length > 0) Username = parsed.Username;

        return true;
    }

    /// <summary>Applies a bare Base32 secret with the standard SHA1/6/30 parameters.</summary>
    public bool ApplyManualSeed(string base32)
    {
        if (!_totp.IsValidBase32(base32)) return false;
        TotpSecret = base32.Trim();
        TotpAlgorithm = "SHA1";
        TotpDigits = 6;
        TotpPeriod = 30;
        return true;
    }

    public void RemoveTotp()
    {
        TotpSecret = null;
        TotpAlgorithm = "SHA1";
        TotpDigits = 6;
        TotpPeriod = 30;
        CurrentTotpCode = string.Empty;
        TotpRemainingSeconds = 0;
    }

    /// <summary>Formats a numeric code like "123456" as "123 456" for easier reading.</summary>
    private static string FormatCode(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        if (raw.Length <= 4) return raw;
        var mid = raw.Length / 2;
        return raw[..mid] + " " + raw[mid..];
    }

    // ─── Type-specific command ────────────────────────────────────────────────

    [RelayCommand]
    private void GeneratePassword()
    {
        Password = _generator.Generate(new PasswordGeneratorOptions
        {
            Length = 20,
            IncludeUppercase = true,
            IncludeLowercase = true,
            IncludeDigits = true,
            IncludeSymbols = true
        });
    }
}
