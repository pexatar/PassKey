namespace PassKey.Desktop.Services;

/// <summary>
/// Manages the SQLite database lifecycle: schema initialisation, migrations,
/// and connection factory for the three-table vault schema
/// (VaultMetadata, VaultData, ActivityLog).
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Gets the absolute path to the SQLite database file
    /// (typically <c>%LOCALAPPDATA%\PassKey\vault.db</c>).
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Creates the database file and applies the schema if it does not yet exist,
    /// or runs any pending migrations for existing databases.
    /// Must be awaited before any vault operations are performed.
    /// </summary>
    /// <returns>A task that completes when the database is ready.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Opens and returns a new <see cref="Microsoft.Data.Sqlite.SqliteConnection"/>
    /// to the vault database. The caller is responsible for disposing the connection.
    /// </summary>
    /// <returns>An open <see cref="Microsoft.Data.Sqlite.SqliteConnection"/>.</returns>
    Microsoft.Data.Sqlite.SqliteConnection CreateConnection();
}
