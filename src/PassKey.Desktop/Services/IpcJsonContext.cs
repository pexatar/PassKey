using System.Text.Json.Serialization;

namespace PassKey.Desktop.Services;

/// <summary>
/// Source-generated JSON serializer context for all IPC request and response models.
/// Required for AOT (Ahead-of-Time) compatibility: the .NET AOT compiler cannot use
/// reflection-based JSON serialization, so all types that are serialized or deserialized
/// must be declared here so the source generator emits the necessary metadata at compile time.
/// Configured with camelCase property naming to match the browser extension JavaScript conventions.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(IpcRequest))]
[JsonSerializable(typeof(IpcResponse))]
[JsonSerializable(typeof(ExchangeKeysRequest))]
[JsonSerializable(typeof(ExchangeKeysResponse))]
[JsonSerializable(typeof(TestSessionRequest))]
[JsonSerializable(typeof(TestSessionResponse))]
[JsonSerializable(typeof(GetStatusResponse))]
[JsonSerializable(typeof(GetCredentialsRequest))]
[JsonSerializable(typeof(CredentialSummary))]
[JsonSerializable(typeof(GetCredentialsResponse))]
[JsonSerializable(typeof(GetCredentialPasswordRequest))]
[JsonSerializable(typeof(GetCredentialPasswordResponse))]
[JsonSerializable(typeof(UnlockVaultRequest))]
[JsonSerializable(typeof(GetAllCredentialsResponse))]
internal partial class IpcJsonContext : JsonSerializerContext
{
}
