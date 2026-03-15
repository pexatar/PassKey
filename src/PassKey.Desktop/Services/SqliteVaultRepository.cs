using Microsoft.Data.Sqlite;
using PassKey.Core.Constants;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;

namespace PassKey.Desktop.Services;

/// <summary>
/// SQLite implementation of <see cref="IVaultRepository"/>.
/// Uses three tables: VaultMetadata (plaintext KDF parameters and salt),
/// VaultData (single encrypted blob containing all vault entries),
/// and ActivityLog (append-only audit trail).
/// All connections are opened per-operation and disposed immediately (no pooling).
/// </summary>
public sealed class SqliteVaultRepository : IVaultRepository
{
    private readonly IDatabaseService _db;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteVaultRepository"/>.
    /// </summary>
    /// <param name="db">Database service providing connection factory and path.</param>
    public SqliteVaultRepository(IDatabaseService db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns true if a vault record exists in the VaultMetadata table (Id = 1).
    /// </summary>
    /// <returns>True if the vault has been initialized; false otherwise.</returns>
    public async Task<bool> VaultExistsAsync()
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM VaultMetadata WHERE Id = 1";
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }

    /// <summary>
    /// Loads the vault metadata record (KDF parameters, salt, encrypted DEK) from VaultMetadata.
    /// </summary>
    /// <returns>The <see cref="VaultMetadata"/> record, or null if no vault has been initialized.</returns>
    public async Task<VaultMetadata?> LoadMetadataAsync()
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT KekSalt, EncryptedDek, DekNonce, KdfIterations, KdfAlgorithm, Version, CreatedAt FROM VaultMetadata WHERE Id = 1";

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new VaultMetadata
        {
            KekSalt = (byte[])reader["KekSalt"],
            EncryptedDek = (byte[])reader["EncryptedDek"],
            DekNonce = reader["DekNonce"] is DBNull ? [] : (byte[])reader["DekNonce"],
            KdfIterations = reader.GetInt32(3),
            KdfAlgorithm = reader.GetString(4),
            Version = reader.GetInt32(5),
            CreatedAt = DateTime.Parse(reader.GetString(6))
        };
    }

    /// <summary>
    /// Saves (upserts) the vault metadata record into VaultMetadata (Id = 1).
    /// Called during vault initialization and master password change.
    /// </summary>
    /// <param name="metadata">Metadata to persist, including KDF parameters and encrypted DEK.</param>
    public async Task SaveMetadataAsync(VaultMetadata metadata)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO VaultMetadata (Id, KekSalt, EncryptedDek, DekNonce, KdfIterations, KdfAlgorithm, Version, CreatedAt)
            VALUES (1, @salt, @dek, @nonce, @iter, @alg, @ver, @created)
            """;
        command.Parameters.AddWithValue("@salt", metadata.KekSalt);
        command.Parameters.AddWithValue("@dek", metadata.EncryptedDek);
        command.Parameters.AddWithValue("@nonce", metadata.DekNonce.Length > 0 ? metadata.DekNonce : DBNull.Value);
        command.Parameters.AddWithValue("@iter", metadata.KdfIterations);
        command.Parameters.AddWithValue("@alg", metadata.KdfAlgorithm);
        command.Parameters.AddWithValue("@ver", metadata.Version);
        command.Parameters.AddWithValue("@created", metadata.CreatedAt.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Loads the encrypted vault blob from VaultData (Id = 1).
    /// The blob has the format <c>[nonce || ciphertext || tag]</c> as produced by AES-GCM.
    /// </summary>
    /// <returns>The raw encrypted bytes, or null if no vault data has been written yet.</returns>
    public async Task<byte[]?> LoadEncryptedVaultAsync()
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT EncryptedBlob FROM VaultData WHERE Id = 1";
        var result = await command.ExecuteScalarAsync();
        return result is DBNull or null ? null : (byte[])result;
    }

    /// <summary>
    /// Saves (upserts) the encrypted vault blob into VaultData (Id = 1).
    /// Updates ModifiedAt to the current UTC timestamp.
    /// </summary>
    /// <param name="encryptedBlob">AES-GCM encrypted vault bytes in <c>[nonce || ciphertext || tag]</c> format.</param>
    public async Task SaveEncryptedVaultAsync(byte[] encryptedBlob)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO VaultData (Id, EncryptedBlob, ModifiedAt)
            VALUES (1, @blob, @modified)
            """;
        command.Parameters.AddWithValue("@blob", encryptedBlob);
        command.Parameters.AddWithValue("@modified", DateTime.UtcNow.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Appends an entry to the ActivityLog table.
    /// </summary>
    /// <param name="entry">The audit log entry describing the action performed.</param>
    public async Task LogActivityAsync(ActivityLogEntry entry)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ActivityLog (EntityType, EntityId, Action, Timestamp)
            VALUES (@type, @id, @action, @ts)
            """;
        command.Parameters.AddWithValue("@type", entry.EntityType);
        command.Parameters.AddWithValue("@id", entry.EntityId.ToString());
        command.Parameters.AddWithValue("@action", entry.Action);
        command.Parameters.AddWithValue("@ts", entry.Timestamp.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Returns the most recent activity log entries, ordered by timestamp descending.
    /// </summary>
    /// <param name="count">Maximum number of entries to return.</param>
    /// <returns>List of <see cref="ActivityLogEntry"/> ordered newest first.</returns>
    public async Task<List<ActivityLogEntry>> GetRecentActivityAsync(int count)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, EntityType, EntityId, Action, Timestamp FROM ActivityLog ORDER BY Timestamp DESC LIMIT @count";
        command.Parameters.AddWithValue("@count", count);

        var entries = new List<ActivityLogEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new ActivityLogEntry
            {
                Id = reader.GetInt64(0),
                EntityType = reader.GetString(1),
                EntityId = Guid.Parse(reader.GetString(2)),
                Action = reader.GetString(3),
                Timestamp = DateTime.Parse(reader.GetString(4))
            });
        }

        return entries;
    }
}
