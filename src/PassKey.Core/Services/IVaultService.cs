using PassKey.Core.Models;

namespace PassKey.Core.Services;

/// <summary>
/// Defines the high-level vault lifecycle operations: initialisation, unlock, master-password change,
/// and vault encryption/decryption. This layer sits above <see cref="ICryptoService"/> and manages
/// the two-tier key architecture (KEK → DEK → vault blob).
/// </summary>
public interface IVaultService
{
    /// <summary>
    /// Creates a new vault for the given master password using Argon2id key derivation.
    /// Generates a random DEK and salt, derives the KEK from the password, and wraps the DEK with AES-GCM.
    /// </summary>
    /// <param name="masterPassword">The user's chosen master password. Cleared by the caller after use.</param>
    /// <returns>
    /// A tuple containing the plaintext <see cref="VaultMetadata"/> (to be saved to the database)
    /// and a <see cref="PinnedSecureBuffer"/> holding the 32-byte DEK (owned and disposed by the caller).
    /// </returns>
    (VaultMetadata Metadata, PinnedSecureBuffer Dek) InitializeVault(ReadOnlySpan<char> masterPassword);

    /// <summary>
    /// Unlocks an existing vault by deriving the KEK from the master password and unwrapping the DEK.
    /// </summary>
    /// <param name="masterPassword">The master password to verify. Cleared by the caller after use.</param>
    /// <param name="metadata">The plaintext metadata loaded from the VaultMetadata database table.</param>
    /// <returns>A <see cref="PinnedSecureBuffer"/> holding the 32-byte DEK (owned and disposed by the caller).</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown if the master password is incorrect (AES-GCM authentication tag mismatch).
    /// </exception>
    PinnedSecureBuffer UnlockVault(ReadOnlySpan<char> masterPassword, VaultMetadata metadata);

    /// <summary>
    /// Changes the master password without re-encrypting the vault blob.
    /// Derives a new KEK from <paramref name="newPassword"/> using Argon2id,
    /// generates a new salt, and re-wraps the existing DEK.
    /// </summary>
    /// <param name="newPassword">The new master password. Cleared by the caller after use.</param>
    /// <param name="currentDek">The currently active 32-byte DEK.</param>
    /// <param name="currentMetadata">The current vault metadata (used for the schema version).</param>
    /// <returns>Updated <see cref="VaultMetadata"/> with new salt, new encrypted DEK, and Argon2id algorithm.</returns>
    VaultMetadata ChangeMasterPassword(ReadOnlySpan<char> newPassword, ReadOnlySpan<byte> currentDek, VaultMetadata currentMetadata);

    /// <summary>
    /// Serialises a <see cref="Vault"/> to JSON and encrypts it with AES-256-GCM using the DEK.
    /// </summary>
    /// <param name="vault">The vault to encrypt.</param>
    /// <param name="dek">The 32-byte Data Encryption Key.</param>
    /// <returns>A self-contained encrypted blob: <c>[nonce (12 B) || ciphertext || auth tag (16 B)]</c>.</returns>
    byte[] EncryptVault(Vault vault, ReadOnlySpan<byte> dek);

    /// <summary>
    /// Decrypts an AES-256-GCM blob and deserialises the result into a <see cref="Vault"/>.
    /// </summary>
    /// <param name="encryptedBlob">The encrypted vault blob from the VaultData database table.</param>
    /// <param name="dek">The 32-byte Data Encryption Key.</param>
    /// <returns>The decrypted and deserialised <see cref="Vault"/>.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown if decryption fails (wrong DEK or corrupted blob).
    /// </exception>
    Vault DecryptVault(ReadOnlySpan<byte> encryptedBlob, ReadOnlySpan<byte> dek);
}
