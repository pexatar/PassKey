using PassKey.Core.Models;

namespace PassKey.Core.Services;

public interface IBackupService
{
    /// <summary>
    /// Encrypts a Vault into a .pkbak byte blob using a backup-specific password.
    /// Format: [magic 4B "PKBK"][version 1B][salt 32B][nonce 12B || ciphertext || tag 16B]
    /// </summary>
    byte[] CreateBackupBlob(Vault vault, ReadOnlySpan<char> backupPassword);

    /// <summary>
    /// Decrypts a .pkbak blob and returns the Vault.
    /// Throws CryptographicException on wrong password, InvalidDataException on corrupt blob.
    /// </summary>
    Vault RestoreFromBlob(ReadOnlySpan<byte> blob, ReadOnlySpan<char> backupPassword);
}
