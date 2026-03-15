using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Microsoft.UI.Xaml;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Settings view. Manages application preferences including theme,
/// auto-lock timeout, language selection, startup behaviour, backup/restore, and vault import.
/// </summary>
/// <remarks>
/// Dependencies injected via constructor: ISettingsService, IVaultStateService,
/// IBackupService, IBackupFileService, IFilePickerService, IMergeService, IImportOrchestrator.
/// UI-specific dialogs are delegated back to the code-behind via event callbacks
/// (e.g. <see cref="BackupPasswordRequested"/>, <see cref="MergeStrategyRequested"/>).
/// Language changes require a process restart; the view shows an InfoBar with a "Restart now" button.
/// </remarks>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IVaultStateService _vaultState;
    private readonly IBackupService _backup;
    private readonly IBackupFileService _backupFile;
    private readonly IFilePickerService _filePicker;
    private readonly IMergeService _merge;
    private readonly IImportOrchestrator _importOrchestrator;
    private bool _initializing;

    private static readonly int[] AutoLockValues = [30, 60, 300, 600, 0];

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedAutoLockIndex { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool StartMinimized { get; set; }

    [ObservableProperty]
    public partial int SelectedLanguageIndex { get; set; }

    [ObservableProperty]
    public partial string AppVersion { get; set; } = string.Empty;

    // Existing events
    public event Action<ElementTheme>? ThemeChangeRequested;

    // Backup/Restore/Import events (code-behind subscribes, handles dialogs)
    public event Func<Task<(string password, bool confirmed)>>? BackupPasswordRequested;
    public event Func<Task<bool>>? RestoreWarningRequested;
    public event Func<Task<(string password, bool confirmed)>>? RestorePasswordRequested;
    public event Func<Task<(ImportFormat format, bool confirmed)>>? ImportFormatRequested;
    public event Func<ImportFormat, Task<(string password, bool confirmed)>>? ImportPasswordRequested;
    public event Func<int, int, int, int, Task<(ImportMergeStrategy strategy, bool confirmed)>>? MergeStrategyRequested;
    public event Func<ImportResult, Task>? ImportCompleted;
    public event Func<string, Task>? BackupCompleted;
    public event Func<Task>? RestoreCompleted;
    public event Func<string, Task>? OperationError;

    public SettingsViewModel(
        ISettingsService settings,
        IVaultStateService vaultState,
        IBackupService backup,
        IBackupFileService backupFile,
        IFilePickerService filePicker,
        IMergeService merge,
        IImportOrchestrator importOrchestrator)
    {
        _settings = settings;
        _vaultState = vaultState;
        _backup = backup;
        _backupFile = backupFile;
        _filePicker = filePicker;
        _merge = merge;
        _importOrchestrator = importOrchestrator;
    }

    public void Initialize()
    {
        _initializing = true;

        // Theme
        SelectedThemeIndex = _settings.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0 // "System"
        };

        // AutoLock
        SelectedAutoLockIndex = Array.IndexOf(AutoLockValues, _settings.AutoLockSeconds);
        if (SelectedAutoLockIndex < 0) SelectedAutoLockIndex = 2; // Default 5 min

        // Startup
        StartWithWindows = _settings.StartWithWindows;
        StartMinimized = _settings.StartMinimized;

        // Language (index 0 = auto/system, 1-6 = explicit languages)
        SelectedLanguageIndex = _settings.Language switch
        {
            "it-IT" => 1,
            "en-GB" => 2,
            "fr-FR" => 3,
            "es-ES" => 4,
            "pt-PT" => 5,
            "de-DE" => 6,
            _ => 0 // "auto" → Sistema
        };

        // Version
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        _initializing = false;
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (_initializing) return;

        _settings.Theme = value switch
        {
            1 => "Light",
            2 => "Dark",
            _ => "System"
        };
        _settings.Save();

        var theme = value switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        ThemeChangeRequested?.Invoke(theme);
    }

    partial void OnSelectedAutoLockIndexChanged(int value)
    {
        if (_initializing) return;

        if (value >= 0 && value < AutoLockValues.Length)
        {
            _settings.AutoLockSeconds = AutoLockValues[value];
            _settings.Save();
        }
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_initializing) return;

        _settings.StartWithWindows = value;
        _settings.Save();

        UpdateRegistryStartup(value);

        if (!value)
        {
            StartMinimized = false;
        }
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        if (_initializing) return;

        _settings.StartMinimized = value;
        _settings.Save();
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (_initializing) return;

        _settings.Language = value switch
        {
            1 => "it-IT",
            2 => "en-GB",
            3 => "fr-FR",
            4 => "es-ES",
            5 => "pt-PT",
            6 => "de-DE",
            _ => "auto"
        };
        _settings.Save();
    }

    public async Task<bool> PerformChangeMasterPasswordAsync(string currentPassword, string newPassword)
    {
        var currentChars = currentPassword.ToCharArray();
        var newChars = newPassword.ToCharArray();

        try
        {
            return await Task.Run(async () =>
                await _vaultState.ChangeMasterPasswordAsync(
                    new ReadOnlyMemory<char>(currentChars),
                    new ReadOnlyMemory<char>(newChars)));
        }
        finally
        {
            Array.Clear(currentChars);
            Array.Clear(newChars);
        }
    }

    [RelayCommand]
    public async Task BackupVaultAsync()
    {
        if (!_vaultState.IsUnlocked || _vaultState.CurrentVault is null) return;

        try
        {
            // 1. Ask for backup password
            if (BackupPasswordRequested is null) return;
            var (password, confirmed) = await BackupPasswordRequested.Invoke();
            if (!confirmed) return;

            // 2. Pick save location
            var path = await _filePicker.PickSaveFileAsync("PassKey_Backup", ".pkbak", "PassKey Backup");
            if (path is null) return;

            // 3. Create encrypted backup blob (KDF is slow → Task.Run)
            var passwordChars = password.ToCharArray();
            try
            {
                var blob = await Task.Run(() =>
                    _backup.CreateBackupBlob(_vaultState.CurrentVault, passwordChars));

                // 4. Write atomically
                await _backupFile.WriteBackupAsync(path, blob);
            }
            finally
            {
                Array.Clear(passwordChars);
            }

            if (BackupCompleted is not null)
                await BackupCompleted.Invoke(path);
        }
        catch (Exception ex)
        {
            if (OperationError is not null)
                await OperationError.Invoke(ex.Message);
        }
    }

    [RelayCommand]
    public async Task RestoreVaultAsync()
    {
        if (!_vaultState.IsUnlocked) return;

        try
        {
            // 1. Warning dialog
            if (RestoreWarningRequested is null) return;
            var warningConfirmed = await RestoreWarningRequested.Invoke();
            if (!warningConfirmed) return;

            // 2. Pick backup file
            var path = await _filePicker.PickOpenFileAsync(".pkbak", "PassKey Backup");
            if (path is null) return;

            // 3. Read backup
            var blob = await _backupFile.ReadBackupAsync(path);

            // 4. Ask for backup password
            if (RestorePasswordRequested is null) return;
            var (password, confirmed) = await RestorePasswordRequested.Invoke();
            if (!confirmed) return;

            // 5. Auto-backup current vault before replacing
            var currentBlob = await _vaultState.GetEncryptedBlobAsync();
            if (currentBlob is not null)
                await _backupFile.WriteAutoBackupAsync(currentBlob);

            // 6. Decrypt backup (KDF slow → Task.Run)
            var passwordChars = password.ToCharArray();
            Core.Models.Vault restoredVault;
            try
            {
                restoredVault = await Task.Run(() =>
                    _backup.RestoreFromBlob(blob, passwordChars));
            }
            finally
            {
                Array.Clear(passwordChars);
            }

            // 7. Replace current vault
            await _vaultState.RestoreVaultAsync(restoredVault);

            if (RestoreCompleted is not null)
                await RestoreCompleted.Invoke();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            if (OperationError is not null)
                await OperationError.Invoke("WRONG_PASSWORD");
        }
        catch (InvalidDataException)
        {
            if (OperationError is not null)
                await OperationError.Invoke("INVALID_FILE");
        }
        catch (Exception ex)
        {
            if (OperationError is not null)
                await OperationError.Invoke(ex.Message);
        }
    }

    [RelayCommand]
    public async Task ImportDataAsync()
    {
        if (!_vaultState.IsUnlocked || _vaultState.CurrentVault is null) return;

        try
        {
            // 1. Ask for format
            if (ImportFormatRequested is null) return;
            var (format, formatConfirmed) = await ImportFormatRequested.Invoke();
            if (!formatConfirmed) return;

            // 2. For KDBX, ask for password
            string? importPassword = null;
            if (format == ImportFormat.Kdbx)
            {
                if (ImportPasswordRequested is null) return;
                var (pw, pwConfirmed) = await ImportPasswordRequested.Invoke(format);
                if (!pwConfirmed) return;
                importPassword = pw;
            }

            // 3. Pick file
            var extension = format switch
            {
                ImportFormat.Csv => ".csv",
                ImportFormat.Kdbx => ".kdbx",
                ImportFormat.OnePux => ".1pux",
                ImportFormat.Bitwarden => ".json",
                _ => ".*"
            };
            var description = format switch
            {
                ImportFormat.Csv => "CSV",
                ImportFormat.Kdbx => "KeePass Database",
                ImportFormat.OnePux => "1Password Export",
                ImportFormat.Bitwarden => "Bitwarden JSON",
                _ => "All Files"
            };
            var path = await _filePicker.PickOpenFileAsync(extension, description);
            if (path is null) return;

            // 4. Parse file
            var importedVault = await _importOrchestrator.ParseFileAsync(path, format, importPassword);

            // 5. Show counts and ask for merge strategy
            if (MergeStrategyRequested is null) return;
            var (strategy, mergeConfirmed) = await MergeStrategyRequested.Invoke(
                importedVault.Passwords.Count,
                importedVault.CreditCards.Count,
                importedVault.Identities.Count,
                importedVault.SecureNotes.Count);
            if (!mergeConfirmed) return;

            // 6. Merge
            var result = _merge.MergeInto(_vaultState.CurrentVault, importedVault, strategy);

            // 7. Save
            await _vaultState.SaveVaultAsync();

            // 8. Show summary
            if (ImportCompleted is not null)
                await ImportCompleted.Invoke(result);
        }
        catch (Exception ex)
        {
            if (OperationError is not null)
                await OperationError.Invoke(ex.Message);
        }
    }

    private static void UpdateRegistryStartup(bool enable)
    {
        const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string appName = "PassKey";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key is null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (exePath is not null)
                    key.SetValue(appName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch
        {
            // Registry access may fail — silently ignore
        }
    }
}
