using Microsoft.Data.Sqlite;

namespace PassKey.Desktop.Services;

/// <summary>
/// Initializes the SQLite database schema and provides connection factory access.
/// The database file is located at <c>%LOCALAPPDATA%\PassKey\passkey.db</c>.
/// Creates three tables on first run: VaultMetadata, VaultData, and ActivityLog.
/// </summary>
public sealed class DatabaseService : IDatabaseService
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PassKey");

    /// <summary>
    /// Gets the full path to the SQLite database file.
    /// </summary>
    public string DatabasePath { get; } = Path.Combine(DataDir, "passkey.db");

    /// <summary>
    /// Creates the database directory and initializes all tables if they do not already exist.
    /// Safe to call on every application start (uses CREATE TABLE IF NOT EXISTS).
    /// </summary>
    /// <remarks>
    /// Tables created:
    /// <list type="bullet">
    ///   <item><term>VaultMetadata</term><description>Single row (Id=1) holding KDF parameters and the encrypted DEK.</description></item>
    ///   <item><term>VaultData</term><description>Single row (Id=1) holding the AES-GCM encrypted vault blob.</description></item>
    ///   <item><term>ActivityLog</term><description>Append-only audit trail with AUTOINCREMENT primary key.</description></item>
    /// </list>
    /// </remarks>
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(DataDir);

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS VaultMetadata (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                KekSalt BLOB NOT NULL,
                EncryptedDek BLOB NOT NULL,
                DekNonce BLOB,
                KdfIterations INTEGER NOT NULL DEFAULT 600000,
                KdfAlgorithm TEXT NOT NULL DEFAULT 'PBKDF2-SHA256',
                Version INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS VaultData (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                EncryptedBlob BLOB NOT NULL,
                ModifiedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ActivityLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EntityType TEXT NOT NULL,
                EntityId TEXT NOT NULL,
                Action TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Creates and returns a new <see cref="SqliteConnection"/> for the PassKey database.
    /// The caller is responsible for opening and disposing the connection.
    /// </summary>
    /// <returns>An unopened <see cref="SqliteConnection"/> pointed at <see cref="DatabasePath"/>.</returns>
    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={DatabasePath}");
    }
}
