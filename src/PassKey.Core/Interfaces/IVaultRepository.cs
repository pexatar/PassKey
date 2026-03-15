using PassKey.Core.Models;

namespace PassKey.Core.Interfaces;

/// <summary>
/// Defines the persistence contract for the encrypted vault database.
/// Implementations are responsible for reading and writing vault data to a durable store
/// (e.g., SQLite) without knowing anything about cryptography or application business logic.
/// </summary>
public interface IVaultRepository
{
    /// <summary>
    /// Determines whether a vault database already exists for the current user.
    /// </summary>
    /// <returns><c>true</c> if the vault has been initialised; <c>false</c> otherwise.</returns>
    Task<bool> VaultExistsAsync();

    /// <summary>
    /// Loads the plaintext vault metadata from the VaultMetadata table.
    /// </summary>
    /// <returns>
    /// The <see cref="VaultMetadata"/> record, or <c>null</c> if the vault has not been initialised yet.
    /// </returns>
    Task<VaultMetadata?> LoadMetadataAsync();

    /// <summary>
    /// Persists the vault metadata to the VaultMetadata table.
    /// Overwrites any existing record (there is exactly one row in this table).
    /// </summary>
    /// <param name="metadata">The metadata to save.</param>
    Task SaveMetadataAsync(VaultMetadata metadata);

    /// <summary>
    /// Loads the AES-GCM encrypted vault blob from the VaultData table.
    /// </summary>
    /// <returns>
    /// The raw encrypted bytes (<c>[nonce || ciphertext || tag]</c>),
    /// or <c>null</c> if no blob has been saved yet.
    /// </returns>
    Task<byte[]?> LoadEncryptedVaultAsync();

    /// <summary>
    /// Persists the AES-GCM encrypted vault blob to the VaultData table.
    /// Overwrites any existing blob (there is exactly one row in this table).
    /// </summary>
    /// <param name="encryptedBlob">The encrypted bytes to save.</param>
    Task SaveEncryptedVaultAsync(byte[] encryptedBlob);

    /// <summary>
    /// Appends an entry to the ActivityLog table.
    /// </summary>
    /// <param name="entry">The log entry to append.</param>
    Task LogActivityAsync(ActivityLogEntry entry);

    /// <summary>
    /// Retrieves the most recent activity log entries, ordered by descending timestamp.
    /// </summary>
    /// <param name="count">The maximum number of entries to return.</param>
    /// <returns>A list of at most <paramref name="count"/> <see cref="ActivityLogEntry"/> records.</returns>
    Task<List<ActivityLogEntry>> GetRecentActivityAsync(int count);
}
