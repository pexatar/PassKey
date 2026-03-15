using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class VaultLifecycleTests
{
    private readonly CryptoService _crypto = new();
    private readonly VaultService _vaultService;

    public VaultLifecycleTests()
    {
        _vaultService = new VaultService(_crypto);
    }

    [Fact]
    public void FullLifecycle_Create_AddAllTypes_Encrypt_Decrypt_Verify()
    {
        // 1. Initialize vault
        var masterPassword = "TestM@sterP4ss!";
        var (metadata, dek) = _vaultService.InitializeVault(masterPassword.AsSpan());

        using (dek)
        {
            // 2. Populate vault with all 4 entry types (every field set)
            var vault = new Vault
            {
                Passwords =
                [
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Title = "GitHub",
                        Username = "dev@example.com",
                        Password = "gh_token_abc123!@#",
                        Url = "https://github.com",
                        Notes = "Personal account",
                        FaviconBase64 = "iVBORw0KGgo=",
                        CreatedAt = DateTime.UtcNow,
                        ModifiedAt = DateTime.UtcNow
                    }
                ],
                CreditCards =
                [
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Label = "Personal Visa",
                        Category = CardCategory.Personal,
                        AccentColor = CardColor.Blue,
                        CardholderName = "John Doe",
                        CardNumber = "4111111111111111",
                        ExpiryMonth = 12,
                        ExpiryYear = 2028,
                        Cvv = "123",
                        Pin = "4567",
                        CardType = CardType.Visa,
                        Notes = "Primary card",
                        CreatedAt = DateTime.UtcNow,
                        ModifiedAt = DateTime.UtcNow
                    }
                ],
                Identities =
                [
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Label = "Main Identity",
                        FirstName = "John",
                        LastName = "Doe",
                        BirthDate = "1990-05-15",
                        Email = "john.doe@example.com",
                        Phone = "+39 333 1234567",
                        Street = "Via Roma 42",
                        City = "Milano",
                        PostalCode = "20121",
                        Province = "MI",
                        Region = "Lombardia",
                        Country = "Italia",
                        IdCardNumber = "CA12345AB",
                        HealthCardNumber = "RSSMRA90E15F205X",
                        DrivingLicenseNumber = "MI1234567A",
                        PassportNumber = "YA1234567",
                        Notes = "Primary identity",
                        CreatedAt = DateTime.UtcNow,
                        ModifiedAt = DateTime.UtcNow
                    }
                ],
                SecureNotes =
                [
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Title = "WiFi Credentials",
                        Content = "Network: HomeNet\nPassword: abc123secure!",
                        Category = NoteCategory.Personal,
                        CreatedAt = DateTime.UtcNow,
                        ModifiedAt = DateTime.UtcNow
                    }
                ]
            };

            // 3. Encrypt
            var encrypted = _vaultService.EncryptVault(vault, dek.ReadOnlySpan);
            Assert.True(encrypted.Length > 0);

            // 4. Simulate "lock" — dispose DEK, re-derive from password
            var dekBytes = dek.ReadOnlySpan.ToArray();

            // 5. "Unlock" — derive DEK again from master password
            using var unlockedDek = _vaultService.UnlockVault(masterPassword.AsSpan(), metadata);
            Assert.Equal(dekBytes, unlockedDek.ReadOnlySpan.ToArray());

            // 6. Decrypt and verify every field
            var decrypted = _vaultService.DecryptVault(encrypted, unlockedDek.ReadOnlySpan);

            // Passwords
            Assert.Single(decrypted.Passwords);
            var pw = decrypted.Passwords[0];
            Assert.Equal("GitHub", pw.Title);
            Assert.Equal("dev@example.com", pw.Username);
            Assert.Equal("gh_token_abc123!@#", pw.Password);
            Assert.Equal("https://github.com", pw.Url);
            Assert.Equal("Personal account", pw.Notes);
            Assert.Equal("iVBORw0KGgo=", pw.FaviconBase64);

            // Credit Cards
            Assert.Single(decrypted.CreditCards);
            var card = decrypted.CreditCards[0];
            Assert.Equal("Personal Visa", card.Label);
            Assert.Equal(CardCategory.Personal, card.Category);
            Assert.Equal(CardColor.Blue, card.AccentColor);
            Assert.Equal("John Doe", card.CardholderName);
            Assert.Equal("4111111111111111", card.CardNumber);
            Assert.Equal(12, card.ExpiryMonth);
            Assert.Equal(2028, card.ExpiryYear);
            Assert.Equal("123", card.Cvv);
            Assert.Equal("4567", card.Pin);
            Assert.Equal(CardType.Visa, card.CardType);

            // Identities
            Assert.Single(decrypted.Identities);
            var id = decrypted.Identities[0];
            Assert.Equal("Main Identity", id.Label);
            Assert.Equal("John", id.FirstName);
            Assert.Equal("Doe", id.LastName);
            Assert.Equal("john.doe@example.com", id.Email);
            Assert.Equal("+39 333 1234567", id.Phone);
            Assert.Equal("Via Roma 42", id.Street);
            Assert.Equal("Milano", id.City);
            Assert.Equal("20121", id.PostalCode);
            Assert.Equal("MI", id.Province);
            Assert.Equal("Lombardia", id.Region);
            Assert.Equal("Italia", id.Country);
            Assert.Equal("CA12345AB", id.IdCardNumber);
            Assert.Equal("RSSMRA90E15F205X", id.HealthCardNumber);
            Assert.Equal("MI1234567A", id.DrivingLicenseNumber);
            Assert.Equal("YA1234567", id.PassportNumber);

            // Secure Notes
            Assert.Single(decrypted.SecureNotes);
            var note = decrypted.SecureNotes[0];
            Assert.Equal("WiFi Credentials", note.Title);
            Assert.Equal("Network: HomeNet\nPassword: abc123secure!", note.Content);
            Assert.Equal(NoteCategory.Personal, note.Category);
        }
    }

    [Fact]
    public void ChangeMasterPassword_OldFails_NewSucceeds_DataPreserved()
    {
        var oldPassword = "OldM@ster1!";
        var newPassword = "NewM@ster2!";

        // Initialize and populate
        var (metadata, dek) = _vaultService.InitializeVault(oldPassword.AsSpan());
        var vault = new Vault();
        vault.Passwords.Add(new PasswordEntry { Title = "Test1", Username = "user1", Password = "pass1" });
        vault.Passwords.Add(new PasswordEntry { Title = "Test2", Username = "user2", Password = "pass2" });
        vault.CreditCards.Add(new CreditCardEntry { Label = "Card1", CardNumber = "4111111111111111" });
        vault.SecureNotes.Add(new SecureNoteEntry { Title = "Note1", Content = "Secret content" });

        var encrypted = _vaultService.EncryptVault(vault, dek.ReadOnlySpan);

        // Change master password
        var newMetadata = _vaultService.ChangeMasterPassword(newPassword.AsSpan(), dek.ReadOnlySpan, metadata);
        dek.Dispose();

        // Old password should fail
        Assert.ThrowsAny<Exception>(() =>
            _vaultService.UnlockVault(oldPassword.AsSpan(), newMetadata));

        // New password should work
        using var newDek = _vaultService.UnlockVault(newPassword.AsSpan(), newMetadata);

        // Data should be preserved
        var decrypted = _vaultService.DecryptVault(encrypted, newDek.ReadOnlySpan);
        Assert.Equal(2, decrypted.Passwords.Count);
        Assert.Equal("Test1", decrypted.Passwords[0].Title);
        Assert.Equal("Test2", decrypted.Passwords[1].Title);
        Assert.Single(decrypted.CreditCards);
        Assert.Single(decrypted.SecureNotes);
    }

    [Fact]
    public void StressTest_100PerType_RoundTrip()
    {
        var (_, dek) = _vaultService.InitializeVault("StressT3st!Pass".AsSpan());
        using (dek)
        {
            var vault = new Vault();

            for (var i = 0; i < 100; i++)
            {
                vault.Passwords.Add(new PasswordEntry
                {
                    Title = $"Password_{i}",
                    Username = $"user{i}@example.com",
                    Password = $"P@ss{i:D4}!",
                    Url = $"https://site{i}.example.com"
                });
                vault.CreditCards.Add(new CreditCardEntry
                {
                    Label = $"Card_{i}",
                    CardNumber = $"411111111111{i:D4}",
                    CardholderName = $"User {i}"
                });
                vault.Identities.Add(new IdentityEntry
                {
                    Label = $"Identity_{i}",
                    FirstName = $"First{i}",
                    LastName = $"Last{i}",
                    Email = $"id{i}@example.com"
                });
                vault.SecureNotes.Add(new SecureNoteEntry
                {
                    Title = $"Note_{i}",
                    Content = $"Content for note {i} with some padding text to make it realistic."
                });
            }

            var encrypted = _vaultService.EncryptVault(vault, dek.ReadOnlySpan);
            var decrypted = _vaultService.DecryptVault(encrypted, dek.ReadOnlySpan);

            Assert.Equal(100, decrypted.Passwords.Count);
            Assert.Equal(100, decrypted.CreditCards.Count);
            Assert.Equal(100, decrypted.Identities.Count);
            Assert.Equal(100, decrypted.SecureNotes.Count);

            // Spot-check first and last
            Assert.Equal("Password_0", decrypted.Passwords[0].Title);
            Assert.Equal("Password_99", decrypted.Passwords[99].Title);
            Assert.Equal("user99@example.com", decrypted.Passwords[99].Username);
            Assert.Equal("Card_50", decrypted.CreditCards[50].Label);
            Assert.Equal("id25@example.com", decrypted.Identities[25].Email);
        }
    }

    [Fact]
    public void EmptyVault_EncryptDecrypt_RoundTrip()
    {
        var (_, dek) = _vaultService.InitializeVault("EmptyV@ult1!".AsSpan());
        using (dek)
        {
            var vault = new Vault(); // No entries

            var encrypted = _vaultService.EncryptVault(vault, dek.ReadOnlySpan);
            var decrypted = _vaultService.DecryptVault(encrypted, dek.ReadOnlySpan);

            Assert.Empty(decrypted.Passwords);
            Assert.Empty(decrypted.CreditCards);
            Assert.Empty(decrypted.Identities);
            Assert.Empty(decrypted.SecureNotes);
        }
    }

    [Fact]
    public void LargeNote_50KChars_Preserved()
    {
        var (_, dek) = _vaultService.InitializeVault("LargeN0te!Pass".AsSpan());
        using (dek)
        {
            var largeContent = new string('A', 50_000);
            var vault = new Vault
            {
                SecureNotes =
                [
                    new() { Title = "Huge Note", Content = largeContent }
                ]
            };

            var encrypted = _vaultService.EncryptVault(vault, dek.ReadOnlySpan);
            var decrypted = _vaultService.DecryptVault(encrypted, dek.ReadOnlySpan);

            Assert.Single(decrypted.SecureNotes);
            Assert.Equal(50_000, decrypted.SecureNotes[0].Content.Length);
            Assert.Equal(largeContent, decrypted.SecureNotes[0].Content);
        }
    }
}
