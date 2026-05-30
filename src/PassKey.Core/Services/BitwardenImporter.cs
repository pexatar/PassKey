using System.Text.Json;
using PassKey.Core.Constants;
using PassKey.Core.Models;

namespace PassKey.Core.Services;

public sealed class BitwardenImporter : IBitwardenImporter
{
    private readonly ITotpService? _totp;

    /// <summary>
    /// Initialises the importer. Pass an <see cref="ITotpService"/> to enable
    /// structured TOTP import from the Bitwarden <c>login.totp</c> field; otherwise
    /// the importer falls back to the legacy v1.x behaviour and appends the raw
    /// TOTP string to the entry notes.
    /// </summary>
    public BitwardenImporter(ITotpService? totp = null)
    {
        _totp = totp;
    }

    public Vault ParseBitwarden(string jsonContent)
    {
        ArgumentNullException.ThrowIfNull(jsonContent);

        var vault = new Vault();
        var export = JsonSerializer.Deserialize(jsonContent, BitwardenJsonContext.Default.BitwardenExport);

        // FU3: an encrypted export has no plaintext items — surface a clear message
        // instead of silently importing an empty vault.
        if (export?.Encrypted == true)
            throw new ImportFileException(
                "Questo file Bitwarden è cifrato e non può essere importato. " +
                "Esporta i dati in formato non cifrato (.json) da Bitwarden e riprova.");

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

    private PasswordEntry MapLogin(BitwardenItem item)
    {
        var login = item.Login;
        var entry = new PasswordEntry
        {
            Id = Guid.NewGuid(),
            Title = item.Name ?? string.Empty,
            Username = login?.Username ?? string.Empty,
            Password = login?.Password ?? string.Empty,
            Url = login?.Uris?.FirstOrDefault()?.Uri ?? string.Empty,
            Notes = item.Notes ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        ApplyBitwardenTotp(entry, login?.Totp);
        return entry;
    }

    /// <summary>
    /// Bitwarden stores TOTP either as a raw Base32 secret or as an <c>otpauth://</c> URI
    /// in the <c>login.totp</c> field. PassKey 2.0 parses both shapes into the structured
    /// <see cref="PasswordEntry.TotpSecret"/> family of fields so the user gets a live code
    /// in the UI instead of a string in the notes blob.
    /// </summary>
    /// <remarks>
    /// When no <see cref="ITotpService"/> was injected (legacy callers, future tests),
    /// the value is appended to the notes — preserving the original v1.x behaviour.
    /// Steam Guard tokens (<c>steam://</c>) are not standard TOTP and are kept as notes.
    /// </remarks>
    private void ApplyBitwardenTotp(PasswordEntry entry, string? totp)
    {
        if (string.IsNullOrWhiteSpace(totp)) return;

        if (_totp is not null)
        {
            // 1. otpauth:// URI → parse all parameters.
            if (totp.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = _totp.ParseOtpAuthUri(totp);
                if (parsed?.TotpSecret is { Length: > 0 })
                {
                    entry.TotpSecret = parsed.TotpSecret;
                    entry.TotpAlgorithm = parsed.TotpAlgorithm;
                    entry.TotpDigits = parsed.TotpDigits;
                    entry.TotpPeriod = parsed.TotpPeriod;
                    return;
                }
            }

            // 2. Bare Base32 secret → use defaults (SHA1, 6 digits, 30 s).
            if (_totp.IsValidBase32(totp))
            {
                entry.TotpSecret = totp.Trim();
                return;
            }
        }

        // 3. Unrecognised (e.g. steam://) or no TotpService — preserve as notes.
        if (!string.IsNullOrEmpty(entry.Notes)) entry.Notes += "\n";
        entry.Notes += $"TOTP: {totp}";
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
