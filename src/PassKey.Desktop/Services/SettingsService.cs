using System.Text.Json;

namespace PassKey.Desktop.Services;

/// <summary>
/// Reads and writes application settings to <c>%LOCALAPPDATA%\PassKey\settings.json</c>.
/// Settings are loaded automatically in the constructor and can be reloaded at any time via <see cref="Load"/>.
/// Persists all public properties as a flat JSON object using a source-generated serializer context
/// for AOT compatibility.
///
/// <para>
/// <b>Recursion guard:</b> <see cref="System.Text.Json.JsonSerializer.Deserialize{T}"/> instantiates
/// a new <see cref="SettingsService"/> which triggers the constructor again. The static
/// <c>_isLoading</c> flag prevents infinite recursion.
/// </para>
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PassKey");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    // Guard against infinite recursion: JsonSerializer.Deserialize creates a new SettingsService
    // instance which calls the constructor → Load() → Deserialize → constructor → StackOverflow
    private static bool _isLoading;

    /// <summary>Gets or sets the UI theme. One of "Light", "Dark", or "System". Default is "System".</summary>
    public string Theme { get; set; } = "System";

    /// <summary>
    /// Gets or sets the application language override.
    /// "auto" uses the system locale; other values are BCP-47 language tags (e.g., "en-GB", "it-IT").
    /// Default is "auto".
    /// </summary>
    public string Language { get; set; } = "auto";

    /// <summary>Gets or sets whether the app launches automatically with Windows. Default is false.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Gets or sets whether the app starts minimized to the system tray. Default is false.</summary>
    public bool StartMinimized { get; set; }

    /// <summary>
    /// Gets or sets the inactivity period in seconds after which the vault is automatically locked.
    /// Default is 300 (5 minutes).
    /// </summary>
    public int AutoLockSeconds { get; set; } = 300; // 5 minutes

    /// <summary>Gets or sets the default generated password length. Default is 16.</summary>
    public int PasswordGeneratorLength { get; set; } = 16;

    /// <summary>Gets or sets whether uppercase letters are included in generated passwords. Default is true.</summary>
    public bool PasswordGeneratorUppercase { get; set; } = true;

    /// <summary>Gets or sets whether lowercase letters are included in generated passwords. Default is true.</summary>
    public bool PasswordGeneratorLowercase { get; set; } = true;

    /// <summary>Gets or sets whether digits are included in generated passwords. Default is true.</summary>
    public bool PasswordGeneratorDigits { get; set; } = true;

    /// <summary>Gets or sets whether symbols are included in generated passwords. Default is true.</summary>
    public bool PasswordGeneratorSymbols { get; set; } = true;

    /// <summary>Gets or sets whether ambiguous characters (0, O, l, 1, I) are excluded from generated passwords. Default is false.</summary>
    public bool PasswordGeneratorExcludeAmbiguous { get; set; }

    /// <summary>Gets or sets whether favicon downloading is enabled for password entries. Default is false.</summary>
    public bool FaviconDownloadEnabled { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsService"/> and loads persisted settings
    /// from disk. If the settings file does not exist, all properties retain their default values.
    /// </summary>
    public SettingsService()
    {
        if (!_isLoading)
        {
            Load();
        }
    }

    /// <summary>
    /// Serializes the current settings to <c>settings.json</c> and writes the file atomically.
    /// Creates the directory if it does not exist.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(this, SettingsJsonContext.Default.SettingsService);
        File.WriteAllText(SettingsPath, json);
    }

    /// <summary>
    /// Reads and deserializes <c>settings.json</c>, copying all recognized properties onto this
    /// instance. Silently falls back to defaults if the file is absent or its JSON is corrupted.
    /// </summary>
    public void Load()
    {
        if (!File.Exists(SettingsPath)) return;
        if (_isLoading) return;

        _isLoading = true;
        try
        {
            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.SettingsService);
            if (loaded is null) return;

            Theme = loaded.Theme;
            Language = loaded.Language;
            StartWithWindows = loaded.StartWithWindows;
            StartMinimized = loaded.StartMinimized;
            AutoLockSeconds = loaded.AutoLockSeconds;
            PasswordGeneratorLength = loaded.PasswordGeneratorLength;
            PasswordGeneratorUppercase = loaded.PasswordGeneratorUppercase;
            PasswordGeneratorLowercase = loaded.PasswordGeneratorLowercase;
            PasswordGeneratorDigits = loaded.PasswordGeneratorDigits;
            PasswordGeneratorSymbols = loaded.PasswordGeneratorSymbols;
            PasswordGeneratorExcludeAmbiguous = loaded.PasswordGeneratorExcludeAmbiguous;
            FaviconDownloadEnabled = loaded.FaviconDownloadEnabled;
        }
        catch
        {
            // Corrupted settings — use defaults
        }
        finally
        {
            _isLoading = false;
        }
    }
}

/// <summary>
/// Source-generated JSON serializer context for <see cref="SettingsService"/> (AOT-safe).
/// </summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(SettingsService))]
internal partial class SettingsJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
