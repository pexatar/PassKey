namespace PassKey.Desktop.Services;

/// <summary>
/// Handles low-level file I/O for vault backup blobs (<c>.pkbak</c>) and automatic backups.
/// Abstracts atomic write semantics (write-to-temp-then-move) from higher-level backup logic.
/// </summary>
public interface IBackupFileService
{
    /// <summary>
    /// Writes a backup blob to the specified path using an atomic write strategy
    /// (write to <c>.tmp</c>, then move) to prevent partial-write corruption.
    /// </summary>
    /// <param name="filePath">Destination path for the backup file.</param>
    /// <param name="blob">The encrypted backup blob produced by <c>IBackupService</c>.</param>
    /// <returns>A task that completes when the file has been written and moved.</returns>
    Task WriteBackupAsync(string filePath, byte[] blob);

    /// <summary>
    /// Reads and returns the raw backup blob from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the <c>.pkbak</c> file.</param>
    /// <returns>The raw encrypted backup blob.</returns>
    Task<byte[]> ReadBackupAsync(string filePath);

    /// <summary>
    /// Creates a timestamped automatic backup of the current encrypted vault blob
    /// in the application data directory (<c>%LOCALAPPDATA%\PassKey\vault.{timestamp}.autobak</c>).
    /// </summary>
    /// <param name="currentEncryptedBlob">The current encrypted vault blob to back up.</param>
    /// <returns>A task that completes when the auto-backup file has been written.</returns>
    Task WriteAutoBackupAsync(byte[] currentEncryptedBlob);
}
