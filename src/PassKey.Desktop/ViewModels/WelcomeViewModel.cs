using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Welcome view shown immediately after first-run vault creation.
/// Offers quick-start actions to the new user before entering the main shell, including
/// restoring an existing .pkbak backup into the freshly-created vault.
/// </summary>
/// <remarks>
/// The three "quick action" commands (add password / import / settings) currently just enter
/// the shell; deeper routing is planned for a later phase. <see cref="RestoreBackupCommand"/>
/// is fully functional: it picks a backup file, decrypts it, and replaces the vault content.
/// </remarks>
public partial class WelcomeViewModel : ObservableObject
{
    private readonly INavigationStack _navigation;
    private readonly IFilePickerService _filePicker;
    private readonly IBackupService _backup;
    private readonly IBackupFileService _backupFile;
    private readonly IVaultStateService _vaultState;

    // Restore dialogs are delegated to the code-behind via these callbacks.
    public event Func<Task<bool>>? RestoreWarningRequested;
    public event Func<Task<(string password, bool confirmed)>>? RestorePasswordRequested;
    public event Func<Task>? RestoreCompleted;
    public event Func<string, Task>? OperationError;

    public WelcomeViewModel(
        INavigationStack navigation,
        IFilePickerService filePicker,
        IBackupService backup,
        IBackupFileService backupFile,
        IVaultStateService vaultState)
    {
        _navigation = navigation;
        _filePicker = filePicker;
        _backup = backup;
        _backupFile = backupFile;
        _vaultState = vaultState;
    }

    /// <summary>Navigates to the Shell (direct password-page routing planned for a future phase).</summary>
    [RelayCommand]
    private Task AddPasswordAsync()
    {
        _navigation.Replace<ShellViewModel>();
        return Task.CompletedTask;
    }

    /// <summary>Navigates to the Shell (direct Settings → Import routing planned for a future phase).</summary>
    [RelayCommand]
    private Task ImportDataAsync()
    {
        _navigation.Replace<ShellViewModel>();
        return Task.CompletedTask;
    }

    /// <summary>Navigates to the Shell (direct Settings routing planned for a future phase).</summary>
    [RelayCommand]
    private Task OpenSettingsAsync()
    {
        _navigation.Replace<ShellViewModel>();
        return Task.CompletedTask;
    }

    /// <summary>Navigates to the Shell and begins the normal authenticated session.</summary>
    [RelayCommand]
    private Task ContinueAsync()
    {
        _navigation.Replace<ShellViewModel>();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Restores an existing <c>.pkbak</c> backup into the freshly-created vault, then enters
    /// the shell. The vault's master password (chosen during setup) is preserved — only the
    /// vault content is replaced. The current (empty) vault is auto-backed-up first.
    /// </summary>
    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (!_vaultState.IsUnlocked) return;

        try
        {
            // 1. Warning dialog.
            if (RestoreWarningRequested is null) return;
            var warningConfirmed = await RestoreWarningRequested.Invoke();
            if (!warningConfirmed) return;

            // 2. Pick the backup file.
            var path = await _filePicker.PickOpenFileAsync(".pkbak", "PassKey Backup");
            if (path is null) return;

            // 3. Read the backup blob.
            var blob = await _backupFile.ReadBackupAsync(path);

            // 4. Ask for the backup password.
            if (RestorePasswordRequested is null) return;
            var (password, confirmed) = await RestorePasswordRequested.Invoke();
            if (!confirmed) return;

            // 5. Auto-backup the current vault before replacing it.
            var currentBlob = await _vaultState.GetEncryptedBlobAsync();
            if (currentBlob is not null)
                await _backupFile.WriteAutoBackupAsync(currentBlob);

            // 6. Decrypt the backup (KDF is slow → off the UI thread).
            var passwordChars = password.ToCharArray();
            Core.Models.Vault restoredVault;
            try
            {
                restoredVault = await Task.Run(() => _backup.RestoreFromBlob(blob, passwordChars));
            }
            finally
            {
                Array.Clear(passwordChars);
            }

            // 7. Replace the vault content and enter the shell.
            await _vaultState.RestoreVaultAsync(restoredVault);

            if (RestoreCompleted is not null)
                await RestoreCompleted.Invoke();

            _navigation.Replace<ShellViewModel>();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            if (OperationError is not null) await OperationError.Invoke("WRONG_PASSWORD");
        }
        catch (System.IO.InvalidDataException)
        {
            if (OperationError is not null) await OperationError.Invoke("INVALID_FILE");
        }
        catch (Exception ex)
        {
            if (OperationError is not null) await OperationError.Invoke(ex.Message);
        }
    }
}
