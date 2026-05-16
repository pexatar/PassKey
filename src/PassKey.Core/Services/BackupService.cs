using System.Security.Cryptography;
using System.Text.Json;
using PassKey.Core.Constants;
using PassKey.Core.Models;

namespace PassKey.Core.Services;

public sealed class BackupService : IBackupService
{
    private static readonly byte[] Magic = [0x50, 0x4B, 0x42, 0x4B]; // "PKBK"

    /// <summary>Legacy backup format derived with PBKDF2-SHA256 (600k iterations).</summary>
    private const byte VersionPbkdf2 = 0x01;

    /// <summary>Current backup format derived with Argon2id (OWASP 2023 parameters).</summary>
    private const byte VersionArgon2Id = 0x02;

    private const int HeaderSize = 5; // magic(4) + version(1)

    private readonly ICryptoService _crypto;

    public BackupService(ICryptoService crypto)
    {
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
    }

    /// <summary>
    /// Serialises and encrypts the supplied <paramref name="vault"/> to a self-describing
    /// <c>.pkbak</c> blob. New backups always use Argon2id (version byte 0x02); the
    /// <see cref="RestoreFromBlob"/> path still accepts legacy 0x01 (PBKDF2) blobs for
    /// backward compatibility with v1.x backups produced before this change.
    /// </summary>
    public byte[] CreateBackupBlob(Vault vault, ReadOnlySpan<char> backupPassword)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(vault, VaultJsonContext.Default.Vault);
        try
        {
            var salt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);
            // Argon2id for new backups — restores symmetry with the main vault KEK,
            // which has been Argon2id-derived since v1.0.x.
            // IMPORTANT: pass 0 as `iterations` so CryptoService falls back to the
            // Argon2-tuned default (CryptoConstants.Argon2TimeCost = 3). Forwarding
            // CryptoConstants.DefaultKdfIterations (600,000 — PBKDF2-shaped) to Argon2id
            // would cost ~600,000 × 64 MB memory passes and never complete.
            using var key = _crypto.DeriveKeyFromPassword(
                backupPassword,
                salt,
                iterations: 0,
                CryptoConstants.KdfAlgorithmArgon2Id);

            var encryptedPayload = _crypto.Encrypt(jsonBytes, key.ReadOnlySpan);

            var blob = new byte[HeaderSize + CryptoConstants.SaltSizeBytes + encryptedPayload.Length];
            Magic.CopyTo(blob, 0);
            blob[4] = VersionArgon2Id;
            salt.CopyTo(blob.AsSpan(HeaderSize));
            encryptedPayload.CopyTo(blob.AsSpan(HeaderSize + CryptoConstants.SaltSizeBytes));

            return blob;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(jsonBytes);
        }
    }

    /// <summary>
    /// Restores a vault from a <c>.pkbak</c> blob, dispatching to the appropriate KDF
    /// based on the version byte: <see cref="VersionPbkdf2"/> (legacy v1.x backups,
    /// PBKDF2-SHA256) or <see cref="VersionArgon2Id"/> (current, Argon2id).
    /// </summary>
    public Vault RestoreFromBlob(ReadOnlySpan<byte> blob, ReadOnlySpan<char> backupPassword)
    {
        var minSize = HeaderSize + CryptoConstants.SaltSizeBytes
                      + CryptoConstants.NonceSizeBytes + CryptoConstants.TagSizeBytes + 1;

        if (blob.Length < minSize)
            throw new InvalidDataException("Blob too small to be a valid PassKey backup.");

        if (!blob[..4].SequenceEqual(Magic))
            throw new InvalidDataException("Not a valid PassKey backup file (bad magic).");

        var version = blob[4];
        var kdfAlgorithm = version switch
        {
            VersionPbkdf2   => CryptoConstants.KdfAlgorithmPbkdf2,
            VersionArgon2Id => CryptoConstants.KdfAlgorithmArgon2Id,
            _ => throw new NotSupportedException($"Unsupported backup version: {version}."),
        };

        var salt = blob.Slice(HeaderSize, CryptoConstants.SaltSizeBytes).ToArray();
        var payload = blob[(HeaderSize + CryptoConstants.SaltSizeBytes)..];

        // Iterations is only meaningful for PBKDF2 (legacy v0x01 backups); for Argon2id
        // CryptoService ignores positive non-default iterations only when the value is 0,
        // so we pass 0 for the Argon2id path. For PBKDF2 we honour the historic count.
        var iterations = kdfAlgorithm == CryptoConstants.KdfAlgorithmArgon2Id
            ? 0
            : CryptoConstants.DefaultKdfIterations;

        using var key = _crypto.DeriveKeyFromPassword(
            backupPassword,
            salt,
            iterations,
            kdfAlgorithm);
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
