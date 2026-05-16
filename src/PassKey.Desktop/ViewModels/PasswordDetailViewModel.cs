using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels.Base;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Password detail ViewModel for add/edit panel.
/// Fields: Title, URL, Username, Password, Notes.
/// Shared add/edit/save/delete plumbing is provided by <see cref="BaseDetailViewModel{TEntry}"/>.
/// </summary>
public partial class PasswordDetailViewModel : BaseDetailViewModel<PasswordEntry>
{
    private readonly IPasswordGenerator _generator;
    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;

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

    public PasswordDetailViewModel(
        IVaultStateService vaultState,
        IPasswordGenerator generator,
        IDialogQueueService dialogQueue,
        IPasswordStrengthAnalyzer strengthAnalyzer)
        : base(vaultState, dialogQueue)
    {
        _generator = generator;
        _strengthAnalyzer = strengthAnalyzer;
    }

    // ─── Template-method overrides ────────────────────────────────────────────

    protected override string GetPanelTitleForNew() => "Aggiungi password";
    protected override string GetPanelTitleForEdit() => "Modifica password";
    protected override string GetDeleteDialogTitle() => "Elimina password";
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
    }

    protected override void LoadFromEntry(PasswordEntry entry)
    {
        Title = entry.Title;
        Url = entry.Url;
        Username = entry.Username;
        Password = entry.Password;
        Notes = entry.Notes;
        FaviconBase64 = entry.FaviconBase64;
    }

    protected override PasswordEntry CreateNewEntry() => new()
    {
        Title = Title.Trim(),
        Url = Url.Trim(),
        Username = Username.Trim(),
        Password = Password,
        Notes = Notes.Trim(),
        FaviconBase64 = FaviconBase64
    };

    protected override void ApplyToEntry(PasswordEntry entry)
    {
        entry.Title = Title.Trim();
        entry.Url = Url.Trim();
        entry.Username = Username.Trim();
        entry.Password = Password;
        entry.Notes = Notes.Trim();
        entry.FaviconBase64 = FaviconBase64;
    }

    protected override void UpdateCanSave()
    {
        CanSave = !string.IsNullOrWhiteSpace(Title) &&
                  !string.IsNullOrWhiteSpace(Username) &&
                  !string.IsNullOrWhiteSpace(Password);
    }

    // ─── Property change handlers ─────────────────────────────────────────────

    partial void OnTitleChanged(string value) => UpdateCanSave();
    partial void OnUsernameChanged(string value) => UpdateCanSave();
    partial void OnPasswordChanged(string value)
    {
        UpdateCanSave();
        UpdatePasswordStrength();
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
