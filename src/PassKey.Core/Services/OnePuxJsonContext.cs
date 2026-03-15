using System.Text.Json.Serialization;

namespace PassKey.Core.Services;

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
    public string? Email { get; set; }
    public string? Concealed { get; set; }
    public OnePuxAddress? Address { get; set; }
    public OnePuxDate? Date { get; set; }
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
