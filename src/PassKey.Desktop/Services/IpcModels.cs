using System.Text.Json;

namespace PassKey.Desktop.Services;

// ═══════════════════════════════════════════════════════════════════
// IPC Envelope (mirrors BrowserHost.Models.IpcEnvelope)
// Separate records because they are in different assemblies.
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// IPC request envelope received from the browser extension via the Named Pipe.
/// Mirrors <c>PassKey.BrowserHost.Models.IpcRequest</c> (separate assembly copy).
/// JSON property names are camelCase per <see cref="IpcJsonContext"/> configuration.
/// </summary>
internal sealed record IpcRequest
{
    /// <summary>Gets the protocol version. Currently always 1.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets the action identifier (e.g., "get-credentials", "unlock-vault").</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Gets a unique request ID (UUID) used to correlate responses with requests.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Gets the optional client identifier (chrome.runtime.id of the extension).</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets the optional action-specific payload as a raw JSON element.</summary>
    public JsonElement? Payload { get; init; }
}

/// <summary>
/// IPC response envelope sent back to the browser extension via the Named Pipe.
/// Mirrors <c>PassKey.BrowserHost.Models.IpcResponse</c> (separate assembly copy).
/// </summary>
internal sealed record IpcResponse
{
    /// <summary>Gets the protocol version. Currently always 1.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets the action identifier echoed from the request.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Gets the request ID echoed from the request for correlation.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the action succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the optional action-specific response payload as a raw JSON element.</summary>
    public JsonElement? Payload { get; init; }

    /// <summary>Gets an optional machine-readable error code when <see cref="Success"/> is false.</summary>
    public string? Error { get; init; }
}

// ═══════════════════════════════════════════════════════════════════
// Action-specific payload models
// ═══════════════════════════════════════════════════════════════════

// --- exchange-keys ---

/// <summary>Request payload for <c>exchange-keys</c>: the client's ECDH P-256 public key in SPKI Base64 format.</summary>
internal sealed record ExchangeKeysRequest(string PublicKey);

/// <summary>Response payload for <c>exchange-keys</c>: the server's SPKI public key and a new session ID.</summary>
internal sealed record ExchangeKeysResponse(string PublicKey, string SessionId);

// --- test-session ---

/// <summary>Request payload for <c>test-session</c>: the session ID to validate.</summary>
internal sealed record TestSessionRequest(string SessionId);

/// <summary>Response payload for <c>test-session</c>: whether the session is valid and whether the vault is unlocked.</summary>
internal sealed record TestSessionResponse(bool Valid, bool VaultUnlocked);

// --- get-status ---

/// <summary>Response payload for <c>get-status</c>: vault lock state and total entry count.</summary>
internal sealed record GetStatusResponse(bool Unlocked, int EntryCount);

// --- get-credentials ---

/// <summary>Request payload for <c>get-credentials</c>: the page URL to match credentials against.</summary>
internal sealed record GetCredentialsRequest(string Url);

/// <summary>A minimal credential summary returned in list responses (no password plaintext).</summary>
internal sealed record CredentialSummary(Guid Id, string Title, string Username, bool HasPassword);

/// <summary>Response payload for <c>get-credentials</c>: matching credential summaries for the requested URL.</summary>
internal sealed record GetCredentialsResponse(List<CredentialSummary> Credentials);

// --- get-credential-password ---

/// <summary>Request payload for <c>get-credential-password</c>: the GUID of the entry whose password to retrieve.</summary>
internal sealed record GetCredentialPasswordRequest(Guid Id);

/// <summary>
/// Response payload for <c>get-credential-password</c>.
/// When an ECDH session is active: <see cref="EncryptedPassword"/> is Base64(ciphertext+tag)
/// and <see cref="Nonce"/> is Base64(12-byte AES-GCM nonce).
/// Without a session: <see cref="EncryptedPassword"/> is Base64(plaintext) and <see cref="Nonce"/> is empty.
/// </summary>
internal sealed record GetCredentialPasswordResponse(string EncryptedPassword, string Nonce);

// --- unlock-vault ---

/// <summary>Request payload for <c>unlock-vault</c>: the master password supplied by the user in the extension popup.</summary>
internal sealed record UnlockVaultRequest(string MasterPassword);

// --- get-all-credentials ---

/// <summary>Response payload for <c>get-all-credentials</c>: all credential summaries, sorted alphabetically by title.</summary>
internal sealed record GetAllCredentialsResponse(List<CredentialSummary> Credentials);

// --- Session state ---

/// <summary>
/// In-memory ECDH session state. Holds the derived AES-GCM session key and the time the session
/// was created. Sessions expire after one hour and are cleaned up between connection cycles.
/// </summary>
internal sealed class SessionInfo
{
    /// <summary>Gets the unique session identifier returned to the browser extension.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the 256-bit AES-GCM session key derived via ECDH + HKDF-SHA256.</summary>
    public required byte[] SessionKey { get; init; }

    /// <summary>Gets the UTC timestamp when this session was established.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
