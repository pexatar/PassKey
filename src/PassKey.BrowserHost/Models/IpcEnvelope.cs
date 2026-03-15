using System.Text.Json;

namespace PassKey.BrowserHost.Models;

/// <summary>
/// IPC request envelope sent from the browser extension via BrowserHost to the Desktop app.
/// Deserialized from the JSON payload received on stdin using the Native Messaging protocol.
/// JSON property names are camelCase per <see cref="IpcJsonContext"/> configuration.
/// </summary>
internal sealed record IpcRequest
{
    /// <summary>Gets the protocol version for future compatibility. Currently always 1.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets the action identifier (e.g., "get-credentials", "unlock-vault", "exchange-keys").</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Gets a UUID string used to correlate this request with its response.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Gets the optional client identifier (typically <c>chrome.runtime.id</c> of the extension).</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the optional action-specific payload as a raw JSON element.</summary>
    public JsonElement? Payload { get; init; }
}

/// <summary>
/// IPC response envelope sent from the Desktop app back through BrowserHost to the browser extension.
/// Serialized to JSON and written to stdout using the Native Messaging protocol.
/// </summary>
internal sealed record IpcResponse
{
    /// <summary>Gets the protocol version. Currently always 1.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets the action identifier echoed from the corresponding request.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Gets the request ID echoed from the request for client-side correlation.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the requested action succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the optional action-specific response payload as a raw JSON element.</summary>
    public JsonElement? Payload { get; init; }

    /// <summary>Gets an optional machine-readable error code when <see cref="Success"/> is false.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a generic error response for the given action and request ID.
    /// </summary>
    /// <param name="action">The action from the original request.</param>
    /// <param name="requestId">The request ID from the original request.</param>
    /// <param name="error">A machine-readable error code string.</param>
    /// <returns>An <see cref="IpcResponse"/> with <see cref="Success"/> set to false.</returns>
    public static IpcResponse CreateError(string action, string requestId, string error) => new()
    {
        Action = action,
        RequestId = requestId,
        Success = false,
        Error = error
    };

    /// <summary>
    /// Creates a standardized error response indicating the PassKey Desktop app is not running.
    /// </summary>
    /// <param name="action">The action from the original request.</param>
    /// <param name="requestId">The request ID from the original request.</param>
    /// <returns>An <see cref="IpcResponse"/> with error code "desktop-not-running".</returns>
    public static IpcResponse DesktopNotRunning(string action, string requestId) =>
        CreateError(action, requestId, "desktop-not-running");
}
