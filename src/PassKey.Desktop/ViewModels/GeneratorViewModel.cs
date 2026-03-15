using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Password Generator page.
/// Two-column layout: generator controls (left) + strength info (right).
/// Settings persisted via ISettingsService. History in-memory, cleared on lock.
/// </summary>
public partial class GeneratorViewModel : ObservableObject
{
    private readonly IPasswordGenerator _generator;
    private readonly IPasswordStrengthAnalyzer _analyzer;
    private readonly IClipboardService _clipboard;
    private readonly ISettingsService _settings;
    private readonly IVaultStateService _vaultState;

    [ObservableProperty]
    public partial string GeneratedPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Length { get; set; } = 16;

    [ObservableProperty]
    public partial bool IncludeUppercase { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeLowercase { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeDigits { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeSymbols { get; set; } = true;

    [ObservableProperty]
    public partial bool ExcludeAmbiguous { get; set; }

    [ObservableProperty]
    public partial PasswordStrengthResult? StrengthResult { get; set; }

    [ObservableProperty]
    public partial bool ShowCopiedFeedback { get; set; }

    public List<HistoryEntry> History { get; } = [];
    public const int MaxHistoryCount = 5;

    public GeneratorViewModel(
        IPasswordGenerator generator,
        IPasswordStrengthAnalyzer analyzer,
        IClipboardService clipboard,
        ISettingsService settings,
        IVaultStateService vaultState)
    {
        _generator = generator;
        _analyzer = analyzer;
        _clipboard = clipboard;
        _settings = settings;
        _vaultState = vaultState;

        // Load persisted settings
        Length = _settings.PasswordGeneratorLength;
        IncludeUppercase = _settings.PasswordGeneratorUppercase;
        IncludeLowercase = _settings.PasswordGeneratorLowercase;
        IncludeDigits = _settings.PasswordGeneratorDigits;
        IncludeSymbols = _settings.PasswordGeneratorSymbols;
        ExcludeAmbiguous = _settings.PasswordGeneratorExcludeAmbiguous;

        // Clear history on vault lock
        _vaultState.VaultLocked += OnVaultLocked;
    }

    /// <summary>
    /// Called when the page is displayed. Generates initial password.
    /// </summary>
    public void Initialize()
    {
        Generate();
    }

    [RelayCommand]
    private void Generate()
    {
        // Ensure at least one character set is enabled
        if (!IncludeUppercase && !IncludeLowercase && !IncludeDigits && !IncludeSymbols)
        {
            IncludeLowercase = true;
        }

        var options = new PasswordGeneratorOptions
        {
            Length = Length,
            IncludeUppercase = IncludeUppercase,
            IncludeLowercase = IncludeLowercase,
            IncludeDigits = IncludeDigits,
            IncludeSymbols = IncludeSymbols,
            ExcludeAmbiguous = ExcludeAmbiguous
        };

        GeneratedPassword = _generator.Generate(options);
        StrengthResult = _analyzer.Analyze(GeneratedPassword.AsSpan());

        // Persist settings
        SaveSettings();
    }

    [RelayCommand]
    private void CopyPassword()
    {
        if (string.IsNullOrEmpty(GeneratedPassword))
            return;

        _clipboard.Copy(GeneratedPassword, CopyType.Sensitive);
        ShowCopiedFeedback = true;
    }

    [RelayCommand]
    private void GenerateAndCopy()
    {
        Generate();
        CopyPassword();
    }

    [RelayCommand]
    private void CopyHistoryEntry(HistoryEntry? entry)
    {
        if (entry is null || string.IsNullOrEmpty(entry.Password))
            return;

        _clipboard.Copy(entry.Password, CopyType.Sensitive);
    }

    /// <summary>
    /// Adds the current password to history before generating a new one.
    /// Called from the Regenerate button (which first saves current, then generates new).
    /// </summary>
    public void AddToHistory()
    {
        if (string.IsNullOrEmpty(GeneratedPassword))
            return;

        var score = StrengthResult?.Score ?? 0;
        History.Insert(0, new HistoryEntry(GeneratedPassword, DateTime.Now, score));

        // Keep only last 5
        while (History.Count > MaxHistoryCount)
            History.RemoveAt(History.Count - 1);

        OnPropertyChanged(nameof(History));
    }

    partial void OnLengthChanged(int value)
    {
        // Clamp to valid range
        if (value < 8) Length = 8;
        else if (value > 128) Length = 128;
    }

    private void SaveSettings()
    {
        _settings.PasswordGeneratorLength = Length;
        _settings.PasswordGeneratorUppercase = IncludeUppercase;
        _settings.PasswordGeneratorLowercase = IncludeLowercase;
        _settings.PasswordGeneratorDigits = IncludeDigits;
        _settings.PasswordGeneratorSymbols = IncludeSymbols;
        _settings.PasswordGeneratorExcludeAmbiguous = ExcludeAmbiguous;
        _settings.Save();
    }

    private void OnVaultLocked()
    {
        History.Clear();
        OnPropertyChanged(nameof(History));
        GeneratedPassword = string.Empty;
        StrengthResult = null;
    }

    public sealed record HistoryEntry(string Password, DateTime GeneratedAt, int Score)
    {
        /// <summary>
        /// Display-friendly truncated password (first 12 chars + ...)
        /// </summary>
        public string DisplayPassword => Password.Length > 12
            ? string.Concat(Password.AsSpan(0, 12), "...")
            : Password;
    }
}
