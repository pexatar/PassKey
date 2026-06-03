using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Desktop.Services;

public interface IVaultStateService
{
    bool IsUnlocked { get; }
    Vault? CurrentVault { get; }
    event Action? VaultUnlocked;
    event Action? VaultLocked;

    Task<bool> InitializeAsync(ReadOnlyMemory<char> masterPassword);

    /// <summary>
    /// Creates a brand-new vault keyed to <paramref name="masterPassword"/> but pre-populated
    /// with <paramref name="vault"/> instead of an empty vault. Overwrites any existing vault
    /// metadata and data. Used by the "restore backup" path on the login screen, where the
    /// backup's password becomes the new master password.
    /// </summary>
    /// <param name="masterPassword">The master password for the new vault (the backup's password).</param>
    /// <param name="vault">The decrypted vault content to persist.</param>
    /// <returns>Always true on success.</returns>
    Task<bool> InitializeWithVaultAsync(ReadOnlyMemory<char> masterPassword, Vault vault);

    Task<bool> UnlockAsync(ReadOnlyMemory<char> masterPassword);
    void Lock();
    Task SaveVaultAsync();
    Task<bool> ChangeMasterPasswordAsync(ReadOnlyMemory<char> currentPassword, ReadOnlyMemory<char> newPassword);

    /// <summary>
    /// Verifies that the supplied password matches the vault's master password, without
    /// altering any state. Used to gate destructive actions such as "clear vault".
    /// </summary>
    /// <param name="password">The password to verify.</param>
    /// <returns><see langword="true"/> if the password is correct; otherwise <see langword="false"/>.</returns>
    Task<bool> VerifyMasterPasswordAsync(ReadOnlyMemory<char> password);

    /// <summary>
    /// Finds password entries matching the given URL (for browser extension IPC).
    /// Returns empty list if vault is locked.
    /// </summary>
    List<PasswordEntry> FindCredentialsByUrl(string url);

    /// <summary>
    /// Gets a specific password entry by ID (for browser extension IPC).
    /// Returns null if vault is locked or entry not found.
    /// </summary>
    PasswordEntry? GetCredentialById(Guid id);

    /// <summary>
    /// Replaces the current vault with a restored one and saves to repository.
    /// Used by backup restore to avoid exposing DEK to the ViewModel.
    /// </summary>
    Task RestoreVaultAsync(Vault restoredVault);

    /// <summary>
    /// Returns the raw encrypted vault blob from the repository (for auto-backup before restore).
    /// </summary>
    Task<byte[]?> GetEncryptedBlobAsync();
}
