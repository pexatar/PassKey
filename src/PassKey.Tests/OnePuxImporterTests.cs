using PassKey.Core.Services;

namespace PassKey.Tests;

public class OnePuxImporterTests
{
    private readonly OnePuxImporter _importer = new();

    [Fact]
    public void ParseOnePux_LoginItem_MapsToPasswordEntry()
    {
        var json = """
        {
          "accounts": [{
            "vaults": [{
              "items": [{
                "overview": {
                  "title": "GitHub",
                  "urls": [{ "url": "https://github.com" }]
                },
                "details": {
                  "notesPlain": "My notes",
                  "loginFields": [
                    { "designation": "username", "value": "dev@test.com" },
                    { "designation": "password", "value": "secret123" }
                  ]
                }
              }]
            }]
          }]
        }
        """;

        var vault = _importer.ParseOnePux(json);

        Assert.Single(vault.Passwords);
        Assert.Equal("GitHub", vault.Passwords[0].Title);
        Assert.Equal("dev@test.com", vault.Passwords[0].Username);
        Assert.Equal("secret123", vault.Passwords[0].Password);
        Assert.Equal("https://github.com", vault.Passwords[0].Url);
    }

    [Fact]
    public void ParseOnePux_CreditCardItem_MapsToCreditCardEntry()
    {
        var json = """
        {
          "accounts": [{
            "vaults": [{
              "items": [{
                "overview": { "title": "My Visa" },
                "details": {
                  "sections": [{
                    "fields": [
                      { "title": "cardholder name", "value": { "string": "John Doe" } },
                      { "title": "card number", "value": { "creditCardNumber": "4111111111111111" } },
                      { "title": "expiry date", "value": { "monthYear": 202712 } },
                      { "title": "CVV", "value": { "concealed": "123" } }
                    ]
                  }]
                }
              }]
            }]
          }]
        }
        """;

        var vault = _importer.ParseOnePux(json);

        Assert.Single(vault.CreditCards);
        var card = vault.CreditCards[0];
        Assert.Equal("My Visa", card.Label);
        Assert.Equal("John Doe", card.CardholderName);
        Assert.Equal("4111111111111111", card.CardNumber);
        Assert.Equal(2027, card.ExpiryYear);
        Assert.Equal(12, card.ExpiryMonth);
        Assert.Equal("123", card.Cvv);
    }

    [Fact]
    public void ParseOnePux_IdentityItem_MapsToIdentityEntry()
    {
        var json = """
        {
          "accounts": [{
            "vaults": [{
              "items": [{
                "overview": { "title": "My Identity" },
                "details": {
                  "sections": [{
                    "fields": [
                      { "title": "first name", "value": { "string": "Jane" } },
                      { "title": "last name", "value": { "string": "Smith" } },
                      { "title": "email", "value": { "email": "jane@test.com" } },
                      { "title": "phone", "value": { "phone": "+1234567890" } },
                      { "title": "address", "value": { "address": { "street": "123 Main St", "city": "Springfield", "state": "IL", "zip": "62704", "country": "US" } } }
                    ]
                  }]
                }
              }]
            }]
          }]
        }
        """;

        var vault = _importer.ParseOnePux(json);

        Assert.Single(vault.Identities);
        var id = vault.Identities[0];
        Assert.Equal("Jane", id.FirstName);
        Assert.Equal("Smith", id.LastName);
        Assert.Equal("jane@test.com", id.Email);
        Assert.Equal("123 Main St", id.Street);
        Assert.Equal("Springfield", id.City);
    }

    [Fact]
    public void ParseOnePux_SecureNoteItem_FallbackToSecureNote()
    {
        var json = """
        {
          "accounts": [{
            "vaults": [{
              "items": [{
                "overview": { "title": "My Secret Note" },
                "details": {
                  "notesPlain": "This is a secret note content."
                }
              }]
            }]
          }]
        }
        """;

        var vault = _importer.ParseOnePux(json);

        Assert.Single(vault.SecureNotes);
        Assert.Equal("My Secret Note", vault.SecureNotes[0].Title);
        Assert.Equal("This is a secret note content.", vault.SecureNotes[0].Content);
    }

    [Fact]
    public void ParseOnePux_EmptyExport_ReturnsEmptyVault()
    {
        var json = """{ "accounts": [] }""";
        var vault = _importer.ParseOnePux(json);

        Assert.Empty(vault.Passwords);
        Assert.Empty(vault.CreditCards);
        Assert.Empty(vault.Identities);
        Assert.Empty(vault.SecureNotes);
    }

    [Fact]
    public void ParseOnePux_NullAccounts_ReturnsEmptyVault()
    {
        var json = """{ }""";
        var vault = _importer.ParseOnePux(json);

        Assert.Empty(vault.Passwords);
    }

    [Fact]
    public void ParseOnePux_MultipleVaults_AllProcessed()
    {
        var json = """
        {
          "accounts": [{
            "vaults": [
              {
                "items": [{
                  "overview": { "title": "Login1" },
                  "details": {
                    "loginFields": [
                      { "designation": "username", "value": "u1" },
                      { "designation": "password", "value": "p1" }
                    ]
                  }
                }]
              },
              {
                "items": [{
                  "overview": { "title": "Login2" },
                  "details": {
                    "loginFields": [
                      { "designation": "username", "value": "u2" },
                      { "designation": "password", "value": "p2" }
                    ]
                  }
                }]
              }
            ]
          }]
        }
        """;

        var vault = _importer.ParseOnePux(json);

        Assert.Equal(2, vault.Passwords.Count);
    }
}
