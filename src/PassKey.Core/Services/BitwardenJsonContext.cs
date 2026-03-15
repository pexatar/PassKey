using System.Text.Json.Serialization;

namespace PassKey.Core.Services;

// --- Bitwarden Export DTOs ---

public sealed class BitwardenExport
{
    public BitwardenItem[]? Items { get; set; }
}

public sealed class BitwardenItem
{
    public int Type { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public BitwardenLogin? Login { get; set; }
    public BitwardenCard? Card { get; set; }
    public BitwardenIdentity? Identity { get; set; }
}

public sealed class BitwardenLogin
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public BitwardenUri[]? Uris { get; set; }
    public string? Totp { get; set; }
}

public sealed class BitwardenUri
{
    public string? Uri { get; set; }
}

public sealed class BitwardenCard
{
    public string? CardholderName { get; set; }
    public string? Number { get; set; }
    public string? ExpMonth { get; set; }
    public string? ExpYear { get; set; }
    public string? Code { get; set; }
    public string? Brand { get; set; }
}

public sealed class BitwardenIdentity
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BitwardenExport))]
public partial class BitwardenJsonContext : JsonSerializerContext
{
}
