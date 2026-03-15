using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class VaultServiceTests
{
    private readonly CryptoService _crypto = new();
    private readonly VaultService _vaultService;

    public VaultServiceTests()
    {
        _vaultService = new VaultService(_crypto);
    }

    [Fact]
    public void InitializeVault_CreatesMetadataAndDek()
    {
        var (metadata, dek) = _vaultService.InitializeVault("StrongP@ssw0rd!".AsSpan());
        using (dek)
        {
            Assert.NotNull(metadata);
            Assert.Equal(32, metadata.KekSalt.Length);
            Assert.True(metadata.EncryptedDek.Length > 0);
            Assert.Equal(CryptoConstants.Argon2TimeCost, metadata.KdfIterations);
            Assert.Equal(CryptoConstants.KdfAlgorithmArgon2Id, metadata.KdfAlgorithm);
            Assert.Equal(32, dek.Length);
        }
    }

    [Fact]
    public void InitializeVault_UsesArgon2id()
    {
        var (metadata, dek) = _vaultService.InitializeVault("AnyPassword1!".AsSpan());
        dek.Dispose();

        Assert.Equal(CryptoConstants.KdfAlgorithmArgon2Id, metadata.KdfAlgorithm);
    }

    [Fact]
    public void UnlockVault_WithCorrectPassword_ReturnsDek()
    {
        var password = "MyStr0ngP@ss!";
        var (metadata, originalDek) = _vaultService.InitializeVault(password.AsSpan());
        var originalDekBytes = originalDek.ReadOnlySpan.ToArray();
        originalDek.Dispose();

        using var unlockedDek = _vaultService.UnlockVault(password.AsSpan(), metadata);

        Assert.Equal(originalDekBytes, unlockedDek.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void UnlockVault_WithWrongPassword_Throws()
    {
        var (metadata, dek) = _vaultService.InitializeVault("CorrectPassword1!".AsSpan());
        dek.Dispose();

        Assert.ThrowsAny<Exception>(() =>
            _vaultService.UnlockVault("WrongPassword!".AsSpan(), metadata));
    }

    [Fact]
    public void UnlockVault_LegacyPbkdf2Vault_StillWorks()
    {
        // Simula un vault esistente creato con PBKDF2 (prima della migrazione Argon2id)
        var password = "LegacyVaultP@ss!";
        var salt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);
        using var kek = _crypto.DeriveKeyFromPassword(password.AsSpan(), salt, 1_000, CryptoConstants.KdfAlgorithmPbkdf2);

        var dekBytes = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);
        var wrappedDek = _crypto.Encrypt(dekBytes, kek.ReadOnlySpan);

        var legacyMetadata = new VaultMetadata
        {
            KekSalt      = salt,
            EncryptedDek = wrappedDek,
            KdfAlgorithm = CryptoConstants.KdfAlgorithmPbkdf2,
            KdfIterations = 1_000,
            Version      = 1,
            CreatedAt    = DateTime.UtcNow
        };

        using var unlockedDek = _vaultService.UnlockVault(password.AsSpan(), legacyMetadata);

        Assert.Equal(dekBytes, unlockedDek.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void ChangeMasterPassword_UpgradesToArgon2id()
    {
        // Crea vault legacy PBKDF2
        var password = "OldPbkdf2P@ss!";
        var salt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);
        using var kek = _crypto.DeriveKeyFromPassword(password.AsSpan(), salt, 1_000, CryptoConstants.KdfAlgorithmPbkdf2);
        var dekBytes = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);
        var wrappedDek = _crypto.Encrypt(dekBytes, kek.ReadOnlySpan);

        var pbkdf2Metadata = new VaultMetadata
        {
            KekSalt       = salt,
            EncryptedDek  = wrappedDek,
            KdfAlgorithm  = CryptoConstants.KdfAlgorithmPbkdf2,
            KdfIterations = 1_000,
            Version       = 1,
            CreatedAt     = DateTime.UtcNow
        };

        // Cambia password → deve migrare a Argon2id
        var newPassword = "NewArgon2idP@ss!";
        var newMetadata = _vaultService.ChangeMasterPassword(newPassword.AsSpan(), dekBytes, pbkdf2Metadata);

        Assert.Equal(CryptoConstants.KdfAlgorithmArgon2Id, newMetadata.KdfAlgorithm);
        Assert.Equal(CryptoConstants.Argon2TimeCost, newMetadata.KdfIterations);

        // Sblocca con nuova password → DEK invariato
        using var unlockedDek = _vaultService.UnlockVault(newPassword.AsSpan(), newMetadata);
        Assert.Equal(dekBytes, unlockedDek.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void ChangeMasterPassword_DekRemainsUsable()
    {
        var oldPassword = "OldP@ssword1!";
        var newPassword = "NewP@ssword2!";

        var (metadata, dek) = _vaultService.InitializeVault(oldPassword.AsSpan());
        var dekBytes = dek.ReadOnlySpan.ToArray();

        // Encrypt a vault with the original DEK
        var vault = new Vault();
        vault.Passwords.Add(new PasswordEntry { Title = "Test", Username = "user", Password = "pass" });
        var encrypted = _vaultService.EncryptVault(vault, dek.ReadOnlySpan);

        // Change master password (re-wrap DEK)
        var newMetadata = _vaultService.ChangeMasterPassword(newPassword.AsSpan(), dek.ReadOnlySpan, metadata);
        dek.Dispose();

        // Unlock with new password
        using var newDek = _vaultService.UnlockVault(newPassword.AsSpan(), newMetadata);

        // DEK should be the same
        Assert.Equal(dekBytes, newDek.ReadOnlySpan.ToArray());

        // Should still decrypt the vault
        var decrypted = _vaultService.DecryptVault(encrypted, newDek.ReadOnlySpan);
        Assert.Single(decrypted.Passwords);
        Assert.Equal("Test", decrypted.Passwords[0].Title);
    }

    [Fact]
    public void EncryptVault_DecryptVault_RoundTrip()
    {
        var (_, dek) = _vaultService.InitializeVault("P@ssword1!".AsSpan());
        using (dek)
        {
            var vault = new Vault
            {
                Passwords =
                [
                    new() { Title = "GitHub", Username = "dev@example.com", Password = "gh_token_123" }
                ],
                CreditCards =
                [
                    new() { Label = "Personal", CardholderName = "John Doe", CardNumber = "4111111111111111" }
                ],
                Identities =
                [
                    new() { Label = "Main", FirstName = "John", LastName = "Doe", Email = "john@example.com" }
                ],
                SecureNotes =
                [
                    new() { Title = "WiFi", Content = "Network: HomeNet, Pass: abc123" }
                ]
            };

            var encrypted = _vaultService.EncryptVault(vault, dek.ReadOnlySpan);
            var decrypted = _vaultService.DecryptVault(encrypted, dek.ReadOnlySpan);

            Assert.Single(decrypted.Passwords);
            Assert.Equal("GitHub", decrypted.Passwords[0].Title);
            Assert.Single(decrypted.CreditCards);
            Assert.Equal("4111111111111111", decrypted.CreditCards[0].CardNumber);
            Assert.Single(decrypted.Identities);
            Assert.Equal("John", decrypted.Identities[0].FirstName);
            Assert.Single(decrypted.SecureNotes);
            Assert.Equal("WiFi", decrypted.SecureNotes[0].Title);
        }
    }
}
