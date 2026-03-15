using System.Text.Json;
using PassKey.Core.Constants;
using PassKey.Core.Models;

namespace PassKey.Core.Services;

public sealed class VaultService : IVaultService
{
    private readonly ICryptoService _crypto;

    public VaultService(ICryptoService crypto)
    {
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
    }

    public (VaultMetadata Metadata, PinnedSecureBuffer Dek) InitializeVault(ReadOnlySpan<char> masterPassword)
    {
        // Generate random DEK
        var dekBytes = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);
        var dek = new PinnedSecureBuffer(CryptoConstants.KeySizeBytes);
        dek.Write(dekBytes);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(dekBytes);

        // Generate salt and derive KEK from master password using Argon2id
        var salt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);
        using var kek = _crypto.DeriveKeyFromPassword(masterPassword, salt, 0, CryptoConstants.KdfAlgorithmArgon2Id);

        // Wrap DEK with KEK (encrypt DEK using AES-GCM with KEK)
        var wrappedDek = _crypto.Encrypt(dek.ReadOnlySpan, kek.ReadOnlySpan);

        var metadata = new VaultMetadata
        {
            KekSalt = salt,
            EncryptedDek = wrappedDek,
            KdfIterations = CryptoConstants.Argon2TimeCost,
            KdfAlgorithm = CryptoConstants.KdfAlgorithmArgon2Id,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };

        return (metadata, dek);
    }

    public PinnedSecureBuffer UnlockVault(ReadOnlySpan<char> masterPassword, VaultMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        // Derive KEK from master password + stored salt (rispetta l'algoritmo del vault)
        using var kek = _crypto.DeriveKeyFromPassword(masterPassword, metadata.KekSalt, metadata.KdfIterations, metadata.KdfAlgorithm);

        // Unwrap DEK
        var dekBytes = _crypto.Decrypt(metadata.EncryptedDek, kek.ReadOnlySpan);
        var dek = new PinnedSecureBuffer(CryptoConstants.KeySizeBytes);
        try
        {
            dek.Write(dekBytes);
            return dek;
        }
        catch
        {
            dek.Dispose();
            throw;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(dekBytes);
        }
    }

    public VaultMetadata ChangeMasterPassword(ReadOnlySpan<char> newPassword, ReadOnlySpan<byte> currentDek, VaultMetadata currentMetadata)
    {
        ArgumentNullException.ThrowIfNull(currentMetadata);

        // Generate new salt and derive new KEK, upgrading to Argon2id
        var newSalt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);
        using var newKek = _crypto.DeriveKeyFromPassword(newPassword, newSalt, 0, CryptoConstants.KdfAlgorithmArgon2Id);

        // Re-wrap DEK with new KEK (DEK itself unchanged)
        var wrappedDek = _crypto.Encrypt(currentDek, newKek.ReadOnlySpan);

        return new VaultMetadata
        {
            KekSalt = newSalt,
            EncryptedDek = wrappedDek,
            KdfIterations = CryptoConstants.Argon2TimeCost,
            KdfAlgorithm = CryptoConstants.KdfAlgorithmArgon2Id,
            Version = currentMetadata.Version,
            CreatedAt = currentMetadata.CreatedAt
        };
    }

    public byte[] EncryptVault(Vault vault, ReadOnlySpan<byte> dek)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(vault, VaultJsonContext.Default.Vault);
        try
        {
            return _crypto.Encrypt(jsonBytes, dek);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(jsonBytes);
        }
    }

    public Vault DecryptVault(ReadOnlySpan<byte> encryptedBlob, ReadOnlySpan<byte> dek)
    {
        var jsonBytes = _crypto.Decrypt(encryptedBlob, dek);
        try
        {
            return JsonSerializer.Deserialize(jsonBytes, VaultJsonContext.Default.Vault)
                   ?? throw new InvalidOperationException("Failed to deserialize vault.");
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(jsonBytes);
        }
    }
}
