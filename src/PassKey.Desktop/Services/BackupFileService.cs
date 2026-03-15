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

    public async Task WriteAutoBackupAsync(byte[] currentEncryptedBlob)
    {
        Directory.CreateDirectory(BackupDir);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        var autoBackupPath = Path.Combine(BackupDir, $"vault.{timestamp}.autobak");
        await File.WriteAllBytesAsync(autoBackupPath, currentEncryptedBlob);
    }
}
