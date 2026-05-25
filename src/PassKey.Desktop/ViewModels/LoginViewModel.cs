using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Login view. Handles vault unlock by verifying the master password
/// against the stored KDF parameters via <see cref="IVaultStateService.UnlockAsync"/>.
/// On success, navigates to <see cref="ShellViewModel"/>; on failure, exposes an error message.
/// </summary>
/// <remarks>
/// Dependencies injected via constructor: <see cref="IVaultStateService"/>, <see cref="INavigationStack"/>.
/// The password is handled as a <c>char[]</c> and cleared with <c>Array.Clear</c> after use
/// to minimize its lifetime in managed memory.
/// </remarks>
public partial class LoginViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly INavigationStack _navigation;
    private readonly IBackupService _backupService;
    private readonly IBackupFileService _backupFile;

    /// <summary>
    /// Gets or sets a value indicating whether a vault unlock operation is in progress.
    /// Used by the View to show a <c>ProgressRing</c> and disable the login button.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAuthenticating { get; set; }

    /// <summary>
    /// Gets or sets the error message to display when authentication fails.
    /// Contains a localization key string (e.g., "IncorrectPassword" or "UnlockFailed").
    /// </summary>
    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether an authentication error occurred on the last attempt.
    /// Controls visibility of the inline error panel in the View.
    /// </summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="LoginViewModel"/>.
    /// </summary>
    /// <param name="vaultState">Vault state service used to attempt vault unlock.</param>
    /// <param name="navigation">Navigation stack for replacing the current page with the Shell on success.</param>
    /// <param name="backupService">Backup crypto service used by the "restore backup" recovery path.</param>
    /// <param name="backupFile">Backup file reader used by the "restore backup" recovery path.</param>
    public LoginViewModel(
        IVaultStateService vaultState,
        INavigationStack navigation,
        IBackupService backupService,
        IBackupFileService backupFile)
    {
        _vaultState = vaultState;
        _navigation = navigation;
        _backupService = backupService;
        _backupFile = backupFile;
    }

    /// <summary>
    /// Attempts to unlock the vault with the provided master password.
    /// Navigates to <see cref="ShellViewModel"/> on success, or sets <see cref="HasError"/> on failure.
    /// </summary>
    [RelayCommand]
    private async Task LoginAsync(string password)
    {
        if (string.IsNullOrEmpty(password)) return;

        IsAuthenticating = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var chars = password.ToCharArray();
            // Run KDF + decrypt on the thread pool so the synchronous CPU-bound parts
            // of UnlockAsync (Argon2id / AES-GCM) do not freeze the UI thread.
            // Using Task.Run(Func<Task<T>>) — NOT Task.Run(async lambda) — to avoid the
            // anti-pattern of an async lambda just awaiting another async call.
            bool success;
            try
            {
                success = await Task.Run(() => _vaultState.UnlockAsync(chars));
            }
            finally
            {
                Array.Clear(chars);
            }

            if (success)
            {
                _navigation.Replace<ShellViewModel>();
            }
            else
            {
                HasError = true;
                ErrorMessage = "IncorrectPassword"; // Localization key
            }
        }
        catch (Exception ex)
        {
            // Generic catch — UI shows a localized "UnlockFailed" key without leaking the
            // underlying error to the user. Forward the original exception to Debug so it's
            // still visible to developers during local diagnostics.
            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Login failed: {ex.GetType().Name}: {ex.Message}");
            HasError = true;
            ErrorMessage = "UnlockFailed"; // Localization key
        }
        finally
        {
            IsAuthenticating = false;
        }
    }

    /// <summary>
    /// Recovery path: abandons the current (inaccessible) vault and routes to first-run setup.
    /// The existing vault data is overwritten only when the user completes setup — abandoning
    /// the setup screen leaves the old vault untouched, so this is non-destructive until then.
    /// </summary>
    public void StartNewVault() => _navigation.Replace<SetupViewModel>();

    /// <summary>
    /// Recovery path: restores a <c>.pkbak</c> backup as the new vault. The backup's own
    /// password becomes the master password, so the user logs straight in afterwards.
    /// </summary>
    /// <param name="path">Full path to the selected <c>.pkbak</c> file.</param>
    /// <param name="backupPassword">The password the backup was encrypted with.</param>
    /// <returns>
    /// True if the backup was decrypted and adopted as the new vault (navigation to the Shell
    /// has already happened); false if the password was wrong or the file was invalid.
    /// </returns>
    public async Task<bool> RestoreFromBackupAsync(string path, ReadOnlyMemory<char> backupPassword)
    {
        try
        {
            var blob = await _backupFile.ReadBackupAsync(path);

            // RestoreFromBlob runs Argon2id (CPU-bound, ~1 s) — keep it off the UI thread.
            var vault = await Task.Run(
                () => _backupService.RestoreFromBlob(blob, backupPassword.Span));

            await _vaultState.InitializeWithVaultAsync(backupPassword, vault);
            _navigation.Replace<ShellViewModel>();
            return true;
        }
        catch (Exception ex)
        {
            // Wrong backup password (GCM tag mismatch) or corrupt/invalid file — surface a
            // generic failure to the caller; details go to the debug listener only.
            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Restore failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
