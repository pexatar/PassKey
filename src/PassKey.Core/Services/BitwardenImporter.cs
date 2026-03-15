using System.Text.Json;
using PassKey.Core.Constants;
using PassKey.Core.Models;

namespace PassKey.Core.Services;

public sealed class BitwardenImporter : IBitwardenImporter
{
    public Vault ParseBitwarden(string jsonContent)
    {
        ArgumentNullException.ThrowIfNull(jsonContent);

        var vault = new Vault();
        var export = JsonSerializer.Deserialize(jsonContent, BitwardenJsonContext.Default.BitwardenExport);

        if (export?.Items is null) return vault;

        foreach (var item in export.Items)
        {
            switch (item.Type)
            {
                case 1: // Login
                    vault.Passwords.Add(MapLogin(item));
                    break;
                case 2: // SecureNote
                    vault.SecureNotes.Add(MapSecureNote(item));
                    break;
                case 3: // Card
                    vault.CreditCards.Add(MapCard(item));
                    break;
                case 4: // Identity
                    vault.Identities.Add(MapIdentity(item));
                    break;
            }
        }

        vault.LastModified = DateTime.UtcNow;
        return vault;
    }

    private static PasswordEntry MapLogin(BitwardenItem item)
    {
        var login = item.Login;
        return new PasswordEntry
        {
            Id = Guid.NewGuid(),
            Title = item.Name ?? string.Empty,
            Username = login?.Username ?? string.Empty,
            Password = login?.Password ?? string.Empty,
            Url = login?.Uris?.FirstOrDefault()?.Uri ?? string.Empty,
            Notes = BuildNotes(item.Notes, login?.Totp),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    private static string BuildNotes(string? notes, string? totp)
    {
        if (string.IsNullOrEmpty(totp)) return notes ?? string.Empty;
        var result = notes ?? string.Empty;
        if (!string.IsNullOrEmpty(result)) result += "\n";
        result += $"TOTP: {totp}";
        return result;
    }

    private static SecureNoteEntry MapSecureNote(BitwardenItem item)
    {
        return new SecureNoteEntry
        {
            Id = Guid.NewGuid(),
            Title = item.Name ?? string.Empty,
            Content = item.Notes ?? string.Empty,
            Category = NoteCategory.General,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    private static CreditCardEntry MapCard(BitwardenItem item)
    {
        var card = item.Card;
        int.TryParse(card?.ExpMonth, out var expMonth);
        int.TryParse(card?.ExpYear, out var expYear);

        var entry = new CreditCardEntry
        {
            Id = Guid.NewGuid(),
            Label = item.Name ?? string.Empty,
            CardholderName = card?.CardholderName ?? string.Empty,
            CardNumber = card?.Number ?? string.Empty,
            ExpiryMonth = expMonth,
            ExpiryYear = expYear,
            Cvv = card?.Code ?? string.Empty,
            Notes = item.Notes ?? string.Empty,
            Category = CardCategory.Personal,
            AccentColor = CardColor.Default,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        // Auto-detect card type via CardTypeDetector pattern
        entry.CardType = CardTypeDetector.Detect(entry.CardNumber);

        return entry;
    }

    private static IdentityEntry MapIdentity(BitwardenItem item)
    {
        var id = item.Identity;
        return new IdentityEntry
        {
            Id = Guid.NewGuid(),
            Label = item.Name ?? string.Empty,
            FirstName = id?.FirstName ?? string.Empty,
            LastName = id?.LastName ?? string.Empty,
            Email = id?.Email ?? string.Empty,
            Phone = id?.Phone ?? string.Empty,
            Street = CombineAddress(id?.Address1, id?.Address2),
            City = id?.City ?? string.Empty,
            Province = id?.State ?? string.Empty,
            PostalCode = id?.PostalCode ?? string.Empty,
            Country = id?.Country ?? string.Empty,
            Notes = item.Notes ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    private static string CombineAddress(string? address1, string? address2)
    {
        if (string.IsNullOrEmpty(address2)) return address1 ?? string.Empty;
        if (string.IsNullOrEmpty(address1)) return address2;
        return $"{address1}, {address2}";
    }
}
