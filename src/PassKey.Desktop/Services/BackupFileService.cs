namespace PassKey.Desktop.Services;

/// <summary>
/// Default implementation of <see cref="IBackupFileService"/>.
/// Writes backup files atomically and stores automatic backups in <c>%LOCALAPPDATA%\PassKey\</c>.
/// </summary>
public sealed class BackupFileService : IBackupFileService
{
    private static readonly string BackupDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PassKey");

    public async Task WriteBackupAsync(string filePath, byte[] blob)
    {
        // Atomic write: write to .tmp, then move (decision F4)
        var tmpPath = filePath + ".tmp";
        await File.WriteAllBytesAsync(tmpPath, blob);
        File.Move(tmpPath, filePath, overwrite: true);
    }

    public async Task<byte[]> ReadBackupAsync(string filePath)
    {
        return await File.ReadAllBytesAsync(filePath);
    }

    /// <summary>Number of most-recent automatic backups to retain; older ones are pruned.</summary>
    private const int MaxAutoBackups = 10;

    public async Task WriteAutoBackupAsync(byte[] currentEncryptedBlob)
    {
        Directory.CreateDirectory(BackupDir);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        var autoBackupPath = Path.Combine(BackupDir, $"vault.{timestamp}.autobak");
        await File.WriteAllBytesAsync(autoBackupPath, currentEncryptedBlob);

        PruneOldAutoBackups();
    }

    /// <summary>
    /// Keeps only the <see cref="MaxAutoBackups"/> most recent <c>vault.*.autobak</c> files,
    /// deleting older ones. Prevents the automatic-backup folder from growing without bound.
    /// Deletion failures are ignored (a locked/transient file must not break the backup flow).
    /// </summary>
    private static void PruneOldAutoBackups()
    {
        try
        {
            var oldBackups = Directory.GetFiles(BackupDir, "vault.*.autobak")
                .OrderByDescending(path => path, StringComparer.Ordinal) // timestamped name sorts chronologically
                .Skip(MaxAutoBackups);

            foreach (var path in oldBackups)
            {
                try { File.Delete(path); }
                catch { /* ignore individual deletion failures */ }
            }
        }
        catch
        {
            // Enumeration failure must never break the backup operation itself.
        }
    }
}
