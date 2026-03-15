using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class BackupServiceTests
{
    private readonly CryptoService _crypto = new();
    private readonly BackupService _backup;

    public BackupServiceTests()
    {
        _backup = new BackupService(_crypto);
    }

    [Fact]
    public void CreateAndRestore_RoundTrip_VaultPreserved()
    {
        var vault = CreateSampleVault();
        var blob = _backup.CreateBackupBlob(vault, "BackupP@ss1!".AsSpan());
        var restored = _backup.RestoreFromBlob(blob, "BackupP@ss1!".AsSpan());

        Assert.Single(restored.Passwords);
        Assert.Equal("GitHub", restored.Passwords[0].Title);
        Assert.Equal("dev@test.com", restored.Passwords[0].Username);
        Assert.Single(restored.CreditCards);
        Assert.Equal("4111111111111111", restored.CreditCards[0].CardNumber);
        Assert.Single(restored.Identities);
        Assert.Equal("John", restored.Identities[0].FirstName);
        Assert.Single(restored.SecureNotes);
        Assert.Equal("WiFi Note", restored.SecureNotes[0].Title);
    }

    [Fact]
    public void CreateBackupBlob_HasCorrectMagicHeader()
    {
        var vault = new Vault();
        var blob = _backup.CreateBackupBlob(vault, "TestP@ss1!".AsSpan());

        Assert.True(blob.Length > 5);
        Assert.Equal(0x50, blob[0]); // 'P'
        Assert.Equal(0x4B, blob[1]); // 'K'
        Assert.Equal(0x42, blob[2]); // 'B'
        Assert.Equal(0x4B, blob[3]); // 'K'
        Assert.Equal(0x01, blob[4]); // version
    }

    [Fact]
    public void CreateBackupBlob_DifferentSaltEachTime()
    {
        var vault = new Vault();
        var blob1 = _backup.CreateBackupBlob(vault, "TestP@ss1!".AsSpan());
        var blob2 = _backup.CreateBackupBlob(vault, "TestP@ss1!".AsSpan());

        // Salt is at bytes 5-36
        var salt1 = blob1[5..37];
        var salt2 = blob2[5..37];
        Assert.NotEqual(salt1, salt2);
    }

    [Fact]
    public void RestoreFromBlob_WrongPassword_ThrowsCryptographicException()
    {
        var vault = new Vault();
        var blob = _backup.CreateBackupBlob(vault, "CorrectP@ss1!".AsSpan());

        Assert.ThrowsAny<Exception>(() =>
            _backup.RestoreFromBlob(blob, "WrongP@ss!".AsSpan()));
    }

    [Fact]
    public void RestoreFromBlob_CorruptedBlob_Throws()
    {
        var vault = new Vault();
        var blob = _backup.CreateBackupBlob(vault, "TestP@ss1!".AsSpan());

        // Corrupt the payload
        blob[^5] ^= 0xFF;

        Assert.ThrowsAny<Exception>(() =>
            _backup.RestoreFromBlob(blob, "TestP@ss1!".AsSpan()));
    }

    [Fact]
    public void RestoreFromBlob_TooSmall_ThrowsInvalidData()
    {
        var tinyBlob = new byte[] { 0x50, 0x4B, 0x42, 0x4B, 0x01, 0x00 };

        Assert.Throws<InvalidDataException>(() =>
            _backup.RestoreFromBlob(tinyBlob, "test".AsSpan()));
    }

    [Fact]
    public void RestoreFromBlob_BadMagic_ThrowsInvalidData()
    {
        var blob = new byte[200];
        blob[0] = 0xFF; // Wrong magic
        blob[4] = 0x01;

        Assert.Throws<InvalidDataException>(() =>
            _backup.RestoreFromBlob(blob, "test".AsSpan()));
    }

    [Fact]
    public void RestoreFromBlob_UnsupportedVersion_ThrowsNotSupported()
    {
        var vault = new Vault();
        var blob = _backup.CreateBackupBlob(vault, "TestP@ss1!".AsSpan());

        // Change version to 0xFF
        blob[4] = 0xFF;

        Assert.Throws<NotSupportedException>(() =>
            _backup.RestoreFromBlob(blob, "TestP@ss1!".AsSpan()));
    }

    [Fact]
    public void CreateAndRestore_EmptyVault_RoundTrips()
    {
        var vault = new Vault();
        var blob = _backup.CreateBackupBlob(vault, "EmptyP@ss1!".AsSpan());
        var restored = _backup.RestoreFromBlob(blob, "EmptyP@ss1!".AsSpan());

        Assert.Empty(restored.Passwords);
        Assert.Empty(restored.CreditCards);
        Assert.Empty(restored.Identities);
        Assert.Empty(restored.SecureNotes);
    }

    private static Vault CreateSampleVault() => new()
    {
        Passwords = [new() { Title = "GitHub", Username = "dev@test.com", Password = "ghp_secret123" }],
        CreditCards = [new() { Label = "Visa", CardNumber = "4111111111111111", CardholderName = "John Doe", ExpiryMonth = 12, ExpiryYear = 2027 }],
        Identities = [new() { Label = "Main", FirstName = "John", LastName = "Doe", Email = "john@test.com" }],
        SecureNotes = [new() { Title = "WiFi Note", Content = "SSID: Home, Pass: abc123" }]
    };
}
