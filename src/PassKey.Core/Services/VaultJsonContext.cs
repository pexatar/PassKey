using System.Text.Json.Serialization;
using PassKey.Core.Models;

namespace PassKey.Core.Services;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Vault))]
[JsonSerializable(typeof(PasswordEntry))]
[JsonSerializable(typeof(CreditCardEntry))]
[JsonSerializable(typeof(IdentityEntry))]
[JsonSerializable(typeof(SecureNoteEntry))]
[JsonSerializable(typeof(VaultMetadata))]
public partial class VaultJsonContext : JsonSerializerContext
{
}
