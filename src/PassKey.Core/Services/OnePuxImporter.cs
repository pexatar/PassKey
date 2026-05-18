using System.Text.Json;
using PassKey.Core.Constants;
using PassKey.Core.Models;

namespace PassKey.Core.Services;

public sealed class OnePuxImporter : IOnePuxImporter
{
    private readonly ITotpService? _totp;

    /// <summary>
    /// Initialises the importer. Pass an <see cref="ITotpService"/> to enable
    /// structured TOTP import for login items (extracting <c>otpauth://</c> URIs
    /// from item sections); without it the URIs remain only in the notes blob.
    /// </summary>
    public OnePuxImporter(ITotpService? totp = null)
    {
        _totp = totp;
    }

    public Vault ParseOnePux(string exportDataJson)
    {
        ArgumentNullException.ThrowIfNull(exportDataJson);

        var vault = new Vault();
        var export = JsonSerializer.Deserialize(exportDataJson, OnePuxJsonContext.Default.OnePuxExport);

        if (export?.Accounts is null) return vault;

        foreach (var account in export.Accounts)
        {
            if (account.Vaults is null) continue;
            foreach (var onepuxVault in account.Vaults)
            {
                if (onepuxVault.Items is null) continue;
                foreach (var item in onepuxVault.Items)
                {
                    MapItem(vault, item);
                }
            }
        }

        vault.LastModified = DateTime.UtcNow;
        return vault;
    }

    private void MapItem(Vault vault, OnePuxItem item)
    {
        var title = item.Overview?.Title ?? string.Empty;
        var notes = item.Details?.NotesPlain ?? string.Empty;
        var url = GetPrimaryUrl(item.Overview);

        // Determine category from login fields and sections
        var loginFields = item.Details?.LoginFields;
        var sections = item.Details?.Sections;

        // Check if it's a login (has username/password fields)
        if (HasLoginFields(loginFields))
        {
            var pw = MapToPassword(title, url, notes, loginFields!);
            // Look in sections for a TOTP one-time password URI and promote it to
            // the structured fields when found (otpauth:// form preferred).
            ApplyTotpFromSections(pw, sections);
            vault.Passwords.Add(pw);
            return;
        }

        // Check sections for credit card or identity data
        if (sections is { Length: > 0 })
        {
            if (HasCreditCardSection(sections))
            {
                vault.CreditCards.Add(MapToCreditCard(title, notes, sections));
                return;
            }

            if (HasIdentitySection(sections))
            {
                vault.Identities.Add(MapToIdentity(title, notes, sections));
                return;
            }
        }

        // Default: treat as secure note
        vault.SecureNotes.Add(new SecureNoteEntry
        {
            Id = Guid.NewGuid(),
            Title = title,
            Content = notes,
            Category = NoteCategory.General,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        });
    }

    private static string GetPrimaryUrl(OnePuxOverview? overview)
    {
        if (overview?.Urls is { Length: > 0 })
            return overview.Urls[0].Url ?? string.Empty;
        return overview?.Url ?? string.Empty;
    }

    private static bool HasLoginFields(OnePuxLoginField[]? fields)
    {
        if (fields is null) return false;
        return fields.Any(f =>
            string.Equals(f.Designation, "username", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.Designation, "password", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Searches the item's sections for a one-time password (TOTP) URI and applies it
    /// to <paramref name="entry"/>. Supports three field shapes seen in 1Password 1pux
    /// exports:
    /// <list type="bullet">
    ///   <item><c>value.totp</c> with an <c>otpauth://</c> URI (newest exports);</item>
    ///   <item><c>value.string</c> or <c>value.concealed</c> with an <c>otpauth://</c>
    ///   URI and the field title matching "one-time password" or "totp" (older exports).</item>
    /// </list>
    /// Falls back to a no-op when no <see cref="ITotpService"/> was injected (the URI,
    /// if any, remains visible in the entry notes blob).
    /// </summary>
    private void ApplyTotpFromSections(PasswordEntry entry, OnePuxSection[]? sections)
    {
        if (_totp is null || sections is null) return;

        foreach (var section in sections)
        {
            if (section.Fields is null) continue;
            foreach (var field in section.Fields)
            {
                var candidate = ExtractTotpCandidate(field);
                if (string.IsNullOrWhiteSpace(candidate)) continue;

                var parsed = _totp.ParseOtpAuthUri(candidate);
                if (parsed?.TotpSecret is { Length: > 0 })
                {
                    entry.TotpSecret = parsed.TotpSecret;
                    entry.TotpAlgorithm = parsed.TotpAlgorithm;
                    entry.TotpDigits = parsed.TotpDigits;
                    entry.TotpPeriod = parsed.TotpPeriod;
                    return; // first match wins
                }
            }
        }
    }

    private static string? ExtractTotpCandidate(OnePuxSectionField field)
    {
        var value = field.Value;
        if (value is null) return null;

        if (!string.IsNullOrWhiteSpace(value.Totp)) return value.Totp;

        var titleLower = field.Title?.ToLowerInvariant() ?? string.Empty;
        if (titleLower.Contains("one-time password") || titleLower.Contains("totp"))
        {
            if (value.String is { Length: > 0 } s && s.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
                return s;
            if (value.Concealed is { Length: > 0 } c && c.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    private static PasswordEntry MapToPassword(string title, string url, string notes, OnePuxLoginField[] fields)
    {
        string username = string.Empty, password = string.Empty;

        foreach (var field in fields)
        {
            if (string.Equals(field.Designation, "username", StringComparison.OrdinalIgnoreCase))
                username = field.Value ?? string.Empty;
            else if (string.Equals(field.Designation, "password", StringComparison.OrdinalIgnoreCase))
                password = field.Value ?? string.Empty;
        }

        return new PasswordEntry
        {
            Id = Guid.NewGuid(),
            Title = title,
            Username = username,
            Password = password,
            Url = url,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    private static bool HasCreditCardSection(OnePuxSection[] sections)
    {
        return sections.Any(s => s.Fields?.Any(f =>
            f.Value?.CreditCardNumber is not null ||
            f.Title?.Contains("card", StringComparison.OrdinalIgnoreCase) == true) == true);
    }

    private static CreditCardEntry MapToCreditCard(string title, string notes, OnePuxSection[] sections)
    {
        var entry = new CreditCardEntry
        {
            Id = Guid.NewGuid(),
            Label = title,
            Notes = notes,
            Category = CardCategory.Personal,
            AccentColor = CardColor.Default,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        foreach (var section in sections)
        {
            if (section.Fields is null) continue;
            foreach (var field in section.Fields)
            {
                var fieldTitle = field.Title?.ToLowerInvariant() ?? string.Empty;
                var val = field.Value;

                if (val?.CreditCardNumber is not null)
                {
                    entry.CardNumber = val.CreditCardNumber;
                    entry.CardType = CardTypeDetector.Detect(entry.CardNumber);
                }
                else if (fieldTitle.Contains("cardholder") || fieldTitle.Contains("holder") || fieldTitle.Contains("name"))
                {
                    entry.CardholderName = val?.String ?? string.Empty;
                }
                else if (fieldTitle.Contains("cvv") || fieldTitle.Contains("verification"))
                {
                    entry.Cvv = val?.Concealed ?? val?.String ?? string.Empty;
                }
                else if (val?.MonthYear is int monthYear and > 0)
                {
                    // MonthYear format: YYYYMM
                    entry.ExpiryYear = monthYear / 100;
                    entry.ExpiryMonth = monthYear % 100;
                }
                else if (fieldTitle.Contains("pin"))
                {
                    entry.Pin = val?.Concealed ?? val?.String ?? string.Empty;
                }
            }
        }

        return entry;
    }

    private static bool HasIdentitySection(OnePuxSection[] sections)
    {
        return sections.Any(s => s.Fields?.Any(f =>
        {
            var t = f.Title?.ToLowerInvariant() ?? string.Empty;
            return t.Contains("first name") || t.Contains("last name") ||
                   t.Contains("address") || f.Value?.Address is not null;
        }) == true);
    }

    private static IdentityEntry MapToIdentity(string title, string notes, OnePuxSection[] sections)
    {
        var entry = new IdentityEntry
        {
            Id = Guid.NewGuid(),
            Label = title,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        foreach (var section in sections)
        {
            if (section.Fields is null) continue;
            foreach (var field in section.Fields)
            {
                var fieldTitle = field.Title?.ToLowerInvariant() ?? string.Empty;
                var val = field.Value;

                if (val?.Address is { } addr)
                {
                    entry.Street = addr.Street ?? string.Empty;
                    entry.City = addr.City ?? string.Empty;
                    entry.Province = addr.State ?? string.Empty;
                    entry.PostalCode = addr.Zip ?? string.Empty;
                    entry.Country = addr.Country ?? string.Empty;
                }
                else if (fieldTitle.Contains("first name"))
                    entry.FirstName = val?.String ?? string.Empty;
                else if (fieldTitle.Contains("last name"))
                    entry.LastName = val?.String ?? string.Empty;
                else if (fieldTitle.Contains("email") || val?.Email is not null)
                    entry.Email = val?.Email ?? val?.String ?? string.Empty;
                else if (fieldTitle.Contains("phone") || val?.Phone is not null)
                    entry.Phone = val?.Phone ?? val?.String ?? string.Empty;
                else if (val?.Date is { } date)
                    entry.BirthDate = $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}";
            }
        }

        return entry;
    }
}
