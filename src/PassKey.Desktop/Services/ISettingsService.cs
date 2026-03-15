namespace PassKey.Desktop.Services;

/// <summary>
/// Persists and retrieves user preferences from <c>settings.json</c>
/// in <c>%LOCALAPPDATA%\PassKey\</c>. All properties are kept in memory
/// and flushed to disk only when <see cref="Save"/> is called.
/// </summary>
public interface ISettingsService
{
    /// <summary>Gets or sets the UI theme. Valid values: <c>"System"</c>, <c>"Light"</c>, <c>"Dark"</c>.</summary>
    string Theme { get; set; }

    /// <summary>Gets or sets the BCP-47 language tag override (e.g. <c>"it-IT"</c>, <c>"en-GB"</c>).
    /// An empty string means "follow the system locale".</summary>
    string Language { get; set; }

    /// <summary>Gets or sets whether PassKey should launch automatically at Windows login.</summary>
    bool StartWithWindows { get; set; }

    /// <summary>Gets or sets whether PassKey starts in a minimised state to the system tray.</summary>
    bool StartMinimized { get; set; }

    /// <summary>Gets or sets the auto-lock timeout in seconds. <c>0</c> means never auto-lock.</summary>
    int AutoLockSeconds { get; set; }

    /// <summary>Gets or sets the default password length for the generator.</summary>
    int PasswordGeneratorLength { get; set; }

    /// <summary>Gets or sets whether the generator includes uppercase letters (A–Z).</summary>
    bool PasswordGeneratorUppercase { get; set; }

    /// <summary>Gets or sets whether the generator includes lowercase letters (a–z).</summary>
    bool PasswordGeneratorLowercase { get; set; }

    /// <summary>Gets or sets whether the generator includes digits (0–9).</summary>
    bool PasswordGeneratorDigits { get; set; }

    /// <summary>Gets or sets whether the generator includes symbols (!@#$ etc.).</summary>
    bool PasswordGeneratorSymbols { get; set; }

    /// <summary>Gets or sets whether the generator excludes visually ambiguous characters (0/O, 1/l/I).</summary>
    bool PasswordGeneratorExcludeAmbiguous { get; set; }

    /// <summary>Gets or sets whether favicon downloading from site URLs is enabled.</summary>
    bool FaviconDownloadEnabled { get; set; }

    /// <summary>Serialises all current settings to <c>settings.json</c> on disk.</summary>
    void Save();

    /// <summary>Deserialises settings from <c>settings.json</c> into memory, applying defaults for missing keys.</summary>
    void Load();
}
