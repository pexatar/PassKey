using System.Text.Json;
using System.Text.Json.Serialization;

namespace PassKey.Core.Services;

/// <summary>
/// Tolerates both shapes of the 1Password 1pux <c>email</c> field value (FU7a):
/// the legacy plain string (<c>"email": "user@host"</c>) and the current object form
/// (<c>"email": { "email_address": "user@host", "provider": null }</c>). The default
/// <see cref="string"/> deserialiser throws on the object form, which aborts the entire
/// import — every recent 1Password export contains the object form in its default
/// "Starter Kit" identity, so without this converter 1PUX import is broken out of the box.
/// </summary>
public sealed class OnePuxEmailConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.StartObject:
                string? email = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    var prop = reader.GetString();
                    reader.Read();
                    if (string.Equals(prop, "email_address", StringComparison.OrdinalIgnoreCase)
                        && reader.TokenType == JsonTokenType.String)
                        email = reader.GetString();
                    else
                        reader.Skip();
                }
                return email;

            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

// --- 1Password 1PUX Export DTOs ---

public sealed class OnePuxExport
{
    public OnePuxAccount[]? Accounts { get; set; }
}

public sealed class OnePuxAccount
{
    public OnePuxVault[]? Vaults { get; set; }
}

public sealed class OnePuxVault
{
    public OnePuxItem[]? Items { get; set; }
}

public sealed class OnePuxItem
{
    public string? Uuid { get; set; }
    public OnePuxOverview? Overview { get; set; }
    public OnePuxDetails? Details { get; set; }
}

public sealed class OnePuxOverview
{
    public string? Title { get; set; }
    public string? Url { get; set; }
    public OnePuxUrl[]? Urls { get; set; }
    public string[]? Tags { get; set; }
}

public sealed class OnePuxUrl
{
    public string? Url { get; set; }
}

public sealed class OnePuxDetails
{
    public string? NotesPlain { get; set; }
    public OnePuxLoginField[]? LoginFields { get; set; }
    public OnePuxSection[]? Sections { get; set; }
}

public sealed class OnePuxLoginField
{
    public string? Designation { get; set; }
    public string? Value { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
}

public sealed class OnePuxSection
{
    public string? Title { get; set; }
    public OnePuxSectionField[]? Fields { get; set; }
}

public sealed class OnePuxSectionField
{
    /// <summary>
    /// Language-independent, stable field identifier (e.g. "firstname", "lastname",
    /// "email", "ccnum", "cvv"). Preferred over <see cref="Title"/> for mapping because
    /// the title is localized in the user's 1Password language (FU7b).
    /// </summary>
    public string? Id { get; set; }

    public string? Title { get; set; }
    public OnePuxFieldValue? Value { get; set; }
}

public sealed class OnePuxFieldValue
{
    // Different field types store values in different properties
    public string? String { get; set; }
    public string? CreditCardNumber { get; set; }
    public string? CreditCardType { get; set; }
    public int? MonthYear { get; set; }
    public string? Phone { get; set; }

    [JsonConverter(typeof(OnePuxEmailConverter))]
    public string? Email { get; set; }

    public string? Concealed { get; set; }
    public OnePuxAddress? Address { get; set; }
    public OnePuxDate? Date { get; set; }

    /// <summary>
    /// Some 1Password 1pux exports use a dedicated <c>totp</c> field for one-time
    /// password URIs (full <c>otpauth://...</c> form). Older exports embed the URI
    /// in <see cref="String"/> or <see cref="Concealed"/> with the field title set
    /// to "one-time password" — the importer handles both shapes.
    /// </summary>
    public string? Totp { get; set; }
}

public sealed class OnePuxAddress
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }
}

public sealed class OnePuxDate
{
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OnePuxExport))]
public partial class OnePuxJsonContext : JsonSerializerContext
{
}
