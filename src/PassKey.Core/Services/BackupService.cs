using System.Security.Cryptography;
using System.Text.Json;
using PassKey.Core.Constants;
using PassKey.Core.Models;

namespace PassKey.Core.Services;

public sealed class BackupService : IBackupService
{
    private static readonly byte[] Magic = [0x50, 0x4B, 0x42, 0x4B]; // "PKBK"
    private const byte Version = 0x01;
    private const int HeaderSize = 5; // magic(4) + version(1)

    private readonly ICryptoService _crypto;

    public BackupService(ICryptoService crypto)
    {
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
    }

    public byte[] CreateBackupBlob(Vault vault, ReadOnlySpan<char> backupPassword)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(vault, VaultJsonContext.Default.Vault);
        try
        {
            var salt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);
            using var key = _crypto.DeriveKeyFromPassword(backupPassword, salt, CryptoConstants.DefaultKdfIterations);

            var encryptedPayload = _crypto.Encrypt(jsonBytes, key.ReadOnlySpan);

            var blob = new byte[HeaderSize + CryptoConstants.SaltSizeBytes + encryptedPayload.Length];
            Magic.CopyTo(blob, 0);
            blob[4] = Version;
            salt.CopyTo(blob.AsSpan(HeaderSize));
            encryptedPayload.CopyTo(blob.AsSpan(HeaderSize + CryptoConstants.SaltSizeBytes));

            return blob;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(jsonBytes);
        }
    }

    public Vault RestoreFromBlob(ReadOnlySpan<byte> blob, ReadOnlySpan<char> backupPassword)
    {
        var minSize = HeaderSize + CryptoConstants.SaltSizeBytes
                      + CryptoConstants.NonceSizeBytes + CryptoConstants.TagSizeBytes + 1;

        if (blob.Length < minSize)
            throw new InvalidDataException("Blob too small to be a valid PassKey backup.");

        if (!blob[..4].SequenceEqual(Magic))
            throw new InvalidDataException("Not a valid PassKey backup file (bad magic).");

        if (blob[4] != Version)
            throw new NotSupportedException($"Unsupported backup version: {blob[4]}.");

        var salt = blob.Slice(HeaderSize, CryptoConstants.SaltSizeBytes).ToArray();
        var payload = blob[(HeaderSize + CryptoConstants.SaltSizeBytes)..];

        using var key = _crypto.DeriveKeyFromPassword(backupPassword, salt, CryptoConstants.DefaultKdfIterations);
        var jsonBytes = _crypto.Decrypt(payload, key.ReadOnlySpan);
        try
        {
            return JsonSerializer.Deserialize(jsonBytes, VaultJsonContext.Default.Vault)
                   ?? throw new InvalidDataException("Backup contains empty vault.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(jsonBytes);
        }
    }
}
