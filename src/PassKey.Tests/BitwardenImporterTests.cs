using PassKey.Core.Services;

namespace PassKey.Tests;

public class BitwardenImporterTests
{
    private readonly BitwardenImporter _importer = new();

    [Fact]
    public void ParseBitwarden_LoginItem_MapsToPasswordEntry()
    {
        var json = """
        {
          "items": [{
            "type": 1,
            "name": "GitHub",
            "notes": "My notes",
            "login": {
              "username": "dev@test.com",
              "password": "secret123",
              "uris": [{ "uri": "https://github.com" }],
              "totp": null
            }
          }]
        }
        """;

        var vault = _importer.ParseBitwarden(json);

        Assert.Single(vault.Passwords);
        Assert.Equal("GitHub", vault.Passwords[0].Title);
        Assert.Equal("dev@test.com", vault.Passwords[0].Username);
        Assert.Equal("secret123", vault.Passwords[0].Password);
        Assert.Equal("https://github.com", vault.Passwords[0].Url);
        Assert.Equal("My notes", vault.Passwords[0].Notes);
    }

    [Fact]
    public void ParseBitwarden_CardItem_MapsToCreditCardEntry()
    {
        var json = """
        {
          "items": [{
            "type": 3,
            "name": "My Visa",
            "notes": null,
            "card": {
              "cardholderName": "John Doe",
              "number": "4111111111111111",
              "expMonth": "12",
              "expYear": "2027",
              "code": "123"
            }
          }]
        }
        """;

        var vault = _importer.ParseBitwarden(json);

        Assert.Single(vault.CreditCards);
        var card = vault.CreditCards[0];
        Assert.Equal("My Visa", card.Label);
        Assert.Equal("John Doe", card.CardholderName);
        Assert.Equal("4111111111111111", card.CardNumber);
        Assert.Equal(12, card.ExpiryMonth);
        Assert.Equal(2027, card.ExpiryYear);
        Assert.Equal("123", card.Cvv);
    }

    [Fact]
    public void ParseBitwarden_IdentityItem_MapsToIdentityEntry()
    {
        var json = """
        {
          "items": [{
            "type": 4,
            "name": "Main Identity",
            "notes": null,
            "identity": {
              "firstName": "Jane",
              "lastName": "Smith",
              "email": "jane@test.com",
              "phone": "+1234567890",
              "address1": "123 Main St",
              "address2": "Apt 4",
              "city": "Springfield",
              "state": "IL",
              "postalCode": "62704",
              "country": "US"
            }
          }]
        }
        """;

        var vault = _importer.ParseBitwarden(json);

        Assert.Single(vault.Identities);
        var id = vault.Identities[0];
        Assert.Equal("Jane", id.FirstName);
        Assert.Equal("Smith", id.LastName);
        Assert.Equal("jane@test.com", id.Email);
        Assert.Equal("123 Main St, Apt 4", id.Street);
        Assert.Equal("Springfield", id.City);
    }

    [Fact]
    public void ParseBitwarden_SecureNoteItem_MapsToSecureNoteEntry()
    {
        var json = """
        {
          "items": [{
            "type": 2,
            "name": "WiFi Passwords",
            "notes": "SSID: Home\nPass: abc123"
          }]
        }
        """;

        var vault = _importer.ParseBitwarden(json);

        Assert.Single(vault.SecureNotes);
        Assert.Equal("WiFi Passwords", vault.SecureNotes[0].Title);
        Assert.Contains("SSID: Home", vault.SecureNotes[0].Content);
    }

    [Fact]
    public void ParseBitwarden_UnknownType_Skipped()
    {
        var json = """
        {
          "items": [{
            "type": 99,
            "name": "Unknown"
          }]
        }
        """;

        var vault = _importer.ParseBitwarden(json);

        Assert.Empty(vault.Passwords);
        Assert.Empty(vault.CreditCards);
        Assert.Empty(vault.Identities);
        Assert.Empty(vault.SecureNotes);
    }

    [Fact]
    public void ParseBitwarden_EmptyJson_ReturnsEmptyVault()
    {
        var json = """{ "items": [] }""";
        var vault = _importer.ParseBitwarden(json);

        Assert.Empty(vault.Passwords);
    }

    [Fact]
    public void ParseBitwarden_NullItems_ReturnsEmptyVault()
    {
        var json = """{ }""";
        var vault = _importer.ParseBitwarden(json);

        Assert.Empty(vault.Passwords);
    }

    [Fact]
    public void ParseBitwarden_LoginWithTotp_AppendedToNotes()
    {
        var json = """
        {
          "items": [{
            "type": 1,
            "name": "With TOTP",
            "notes": "Some notes",
            "login": {
              "username": "user",
              "password": "pass",
              "totp": "otpauth://totp/Test?secret=ABC123"
            }
          }]
        }
        """;

        var vault = _importer.ParseBitwarden(json);

        Assert.Single(vault.Passwords);
        Assert.Contains("Some notes", vault.Passwords[0].Notes);
        Assert.Contains("TOTP:", vault.Passwords[0].Notes);
    }

    [Fact]
    public void ParseBitwarden_MixedTypes_AllCategorized()
    {
        var json = """
        {
          "items": [
            { "type": 1, "name": "Login1", "login": { "username": "u", "password": "p" } },
            { "type": 2, "name": "Note1", "notes": "content" },
            { "type": 3, "name": "Card1", "card": { "number": "4111111111111111", "cardholderName": "J", "expMonth": "1", "expYear": "2030", "code": "111" } },
            { "type": 4, "name": "Id1", "identity": { "firstName": "A", "lastName": "B", "email": "a@b.com" } }
          ]
        }
        """;

        var vault = _importer.ParseBitwarden(json);

        Assert.Single(vault.Passwords);
        Assert.Single(vault.SecureNotes);
        Assert.Single(vault.CreditCards);
        Assert.Single(vault.Identities);
    }
}
