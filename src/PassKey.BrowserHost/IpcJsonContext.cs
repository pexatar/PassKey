using System.Text.Json.Serialization;
using PassKey.BrowserHost.Models;

namespace PassKey.BrowserHost;

/// <summary>
/// Source-generated JSON serializer context for the BrowserHost IPC models.
/// Source generation is required for AOT (Ahead-of-Time) compilation compatibility:
/// the .NET AOT compiler strips reflection metadata at publish time, so standard
/// reflection-based <see cref="System.Text.Json.JsonSerializer"/> calls would fail at runtime.
/// By declaring all serialized types here, the Roslyn source generator emits all required
/// type metadata at compile time, enabling AOT-safe serialization without reflection.
/// Configured with camelCase property naming to match the browser extension JavaScript conventions.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(IpcRequest))]
[JsonSerializable(typeof(IpcResponse))]
internal partial class IpcJsonContext : JsonSerializerContext
{
}
