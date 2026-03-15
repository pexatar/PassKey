using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Desktop.Services;

/// <summary>
/// Manages vault state in-memory: locked/unlocked status, the Data Encryption Key (DEK),
/// and the currently loaded <see cref="Vault"/> object.
/// The DEK is held in a <see cref="PinnedSecureBuffer"/> whose memory is pinned in the
/// managed heap and zeroed on disposal, preventing the GC from copying sensitive key material.
/// Uses <see cref="IVaultRepository"/> via constructor DI (not Service Locator).
/// </summary>
public sealed class VaultStateService : IVaultStateService, IDisposable
{
    private readonly IVaultService _vaultService;
    private readonly IVaultRepository _repository;
    private PinnedSecureBuffer? _dek;

    /// <summary>
    /// Gets a value indicating whether the vault is currently unlocked (DEK available in memory).
    /// </summary>
    public bool IsUnlocked => _dek is not null;

    /// <summary>
    /// Gets the decrypted vault currently held in memory, or null when locked.
    /// </summary>
    public Vault? CurrentVault { get; private set; }

    /// <summary>Raised after a successful unlock or initialization.</summary>
    public event Action? VaultUnlocked;

    /// <summary>Raised after the vault is locked and the DEK is zeroed.</summary>
    public event Action? VaultLocked;

    /// <summary>
    /// Initializes a new instance of <see cref="VaultStateService"/>.
    /// </summary>
    /// <param name="vaultService">Core vault service for encryption and KDF operations.</param>
    /// <param name="repository">Repository for persisting metadata and encrypted blobs.</param>
    public VaultStateService(IVaultService vaultService, IVaultRepository repository)
    {
        _vaultService = vaultService;
        _repository = repository;
    }

    /// <summary>
    /// Creates a new vault from the provided master password, persists metadata and an empty
    /// encrypted blob, and transitions the service to the unlocked state.
    /// </summary>
    /// <param name="masterPassword">The master password used to derive the KEK.</param>
    /// <returns>Always true on success.</returns>
    public async Task<bool> InitializeAsync(ReadOnlyMemory<char> masterPassword)
    {
        var (metadata, dek) = _vaultService.InitializeVault(masterPassword.Span);
        _dek = dek;

        CurrentVault = new Vault();
        var encrypted = _vaultService.EncryptVault(CurrentVault, _dek.ReadOnlySpan);

        await _repository.SaveMetadataAsync(metadata);
        await _repository.SaveEncryptedVaultAsync(encrypted);

        VaultUnlocked?.Invoke();
        return true;
    }

    /// <summary>
    /// Attempts to unlock the vault using the provided master password.
    /// Derives the KEK, unwraps the DEK, and decrypts the vault blob from the repository.
    /// </summary>
    /// <param name="masterPassword">The master password to verify.</param>
    /// <returns>True if the password is correct and the vault is now unlocked; false otherwise.</returns>
    public async Task<bool> UnlockAsync(ReadOnlyMemory<char> masterPassword)
    {
        var metadata = await _repository.LoadMetadataAsync();
        if (metadata is null) return false;

        try
        {
            _dek = _vaultService.UnlockVault(masterPassword.Span, metadata);
        }
        catch
        {
            return false;
        }

        var encryptedBlob = await _repository.LoadEncryptedVaultAsync();
        if (encryptedBlob is null)
        {
            CurrentVault = new Vault();
        }
        else
        {
            CurrentVault = _vaultService.DecryptVault(encryptedBlob, _dek.ReadOnlySpan);
        }

        VaultUnlocked?.Invoke();
        return true;
    }

    /// <summary>
    /// Locks the vault by zeroing the DEK via <see cref="PinnedSecureBuffer.Dispose"/>
    /// and clearing the in-memory vault object.
    /// </summary>
    public void Lock()
    {
        CurrentVault = null;
        _dek?.Dispose();
        _dek = null;
        VaultLocked?.Invoke();
    }

    /// <summary>
    /// Re-encrypts the current vault with the active DEK and writes the blob to the repository.
    /// Updates <see cref="Vault.LastModified"/> to the current UTC time before saving.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the vault is not unlocked.</exception>
    public async Task SaveVaultAsync()
    {
        if (_dek is null || CurrentVault is null)
            throw new InvalidOperationException("Vault is not unlocked.");

        CurrentVault.LastModified = DateTime.UtcNow;
        var encrypted = _vaultService.EncryptVault(CurrentVault, _dek.ReadOnlySpan);
        await _repository.SaveEncryptedVaultAsync(encrypted);
    }

    /// <summary>
    /// Changes the master password by re-wrapping the existing DEK with a new KEK derived
    /// from <paramref name="newPassword"/>. The DEK itself remains unchanged (SC-10).
    /// Verifies the current password before applying the change.
    /// </summary>
    /// <param name="currentPassword">The current master password for verification.</param>
    /// <param name="newPassword">The new master password.</param>
    /// <returns>True if the password was changed successfully; false if verification failed or vault is locked.</returns>
    public async Task<bool> ChangeMasterPasswordAsync(ReadOnlyMemory<char> currentPassword, ReadOnlyMemory<char> newPassword)
    {
        if (_dek is null || CurrentVault is null)
            return false;

        var metadata = await _repository.LoadMetadataAsync();
        if (metadata is null)
            return false;

        // Verify current password
        PinnedSecureBuffer? verifyDek = null;
        try
        {
            verifyDek = _vaultService.UnlockVault(currentPassword.Span, metadata);
        }
        catch
        {
            return false;
        }
        finally
        {
            verifyDek?.Dispose();
        }

        // Re-wrap DEK with new KEK derived from new password (SC-10: DEK unchanged)
        var newMetadata = _vaultService.ChangeMasterPassword(newPassword.Span, _dek.ReadOnlySpan, metadata);
        await _repository.SaveMetadataAsync(newMetadata);

        return true;
    }

    /// <summary>
    /// Searches the current vault for password entries whose URL matches the given URL.
    /// Returns an empty list when the vault is locked.
    /// </summary>
    /// <param name="url">The page URL to match credentials against.</param>
    /// <returns>Matching <see cref="PasswordEntry"/> objects, or an empty list.</returns>
    public List<PasswordEntry> FindCredentialsByUrl(string url)
    {
        if (!IsUnlocked || CurrentVault is null)
            return [];

        return UrlMatcher.FindMatchingCredentials(CurrentVault.Passwords, url);
    }

    /// <summary>
    /// Looks up a single password entry by its unique identifier.
    /// Returns null when the vault is locked or the ID is not found.
    /// </summary>
    /// <param name="id">The GUID of the entry to retrieve.</param>
    /// <returns>The matching <see cref="PasswordEntry"/>, or null.</returns>
    public PasswordEntry? GetCredentialById(Guid id)
    {
        if (!IsUnlocked || CurrentVault is null)
            return null;

        return CurrentVault.Passwords.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Replaces the current in-memory vault with <paramref name="restoredVault"/>,
    /// re-encrypts it with the active DEK, and persists it to the repository.
    /// Used during backup restore operations.
    /// </summary>
    /// <param name="restoredVault">The vault object to restore.</param>
    /// <exception cref="InvalidOperationException">Thrown if the vault is not unlocked.</exception>
    public async Task RestoreVaultAsync(Vault restoredVault)
    {
        if (_dek is null)
            throw new InvalidOperationException("Vault is not unlocked.");

        CurrentVault = restoredVault;
        CurrentVault.LastModified = DateTime.UtcNow;
        var encrypted = _vaultService.EncryptVault(CurrentVault, _dek.ReadOnlySpan);
        await _repository.SaveEncryptedVaultAsync(encrypted);
    }

    /// <summary>
    /// Returns the raw encrypted vault blob from the repository without decrypting it.
    /// Used for backup export operations.
    /// </summary>
    /// <returns>The encrypted blob bytes, or null if no vault data exists.</returns>
    public async Task<byte[]?> GetEncryptedBlobAsync()
    {
        return await _repository.LoadEncryptedVaultAsync();
    }

    /// <summary>
    /// Zeros and releases the DEK buffer.
    /// </summary>
    public void Dispose()
    {
        _dek?.Dispose();
    }
}
