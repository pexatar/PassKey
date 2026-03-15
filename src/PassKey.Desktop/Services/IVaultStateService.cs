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
    Task<bool> UnlockAsync(ReadOnlyMemory<char> masterPassword);
    void Lock();
    Task SaveVaultAsync();
    Task<bool> ChangeMasterPasswordAsync(ReadOnlyMemory<char> currentPassword, ReadOnlyMemory<char> newPassword);

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
