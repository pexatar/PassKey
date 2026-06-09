using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using PassKey.Core.Constants;
using PassKey.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace PassKey.Desktop.Services;

/// <summary>
/// Named Pipe IPC server for browser extension communication.
/// Listens on <c>\\.\pipe\PassKey.IPC</c> with an ACL restricted to the current user SID only.
/// Handles 8 actions: exchange-keys, test-session, get-status, get-credentials,
/// get-credential-password, unlock-vault, get-all-credentials, show-window.
///
/// <para>
/// ECDH handshake pattern (exchange-keys → get-credential-password):
/// <list type="number">
///   <item>Browser generates an ECDH P-256 ephemeral key pair and sends its public key (SPKI Base64).</item>
///   <item>Desktop generates its own ECDH key pair, derives the shared secret, runs HKDF-SHA256
///         with info "PassKey-IPC-Session" to produce a 256-bit AES-GCM session key, and returns
///         its public key plus a session ID.</item>
///   <item>Browser imports the server public key, performs the same HKDF derivation, and stores
///         the resulting session key.</item>
///   <item>Subsequent get-credential-password responses encrypt the plaintext password with AES-GCM
///         using the session key; the browser decrypts it client-side.</item>
/// </list>
/// Sessions expire after 1 hour. Expired sessions are cleaned up after each connection cycle.
/// </para>
/// </summary>
internal sealed class BrowserIpcService : IBrowserIpcService
{
    private const string PipeName = "PassKey.IPC";
    private const int MaxMessageSize = 1024 * 1024; // 1 MB
    private const int LengthPrefixSize = 4;
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromHours(1);

    // Upper bound on concurrent ECDH sessions. A malicious same-user process could otherwise
    // spam exchange-keys to grow the dictionary unbounded (sessions are only pruned hourly).
    // When the cap is hit we evict the oldest session before accepting a new one.
    private const int MaxSessions = 32;

    // unlock-vault brute-force throttling. The KDF (Argon2id, 64 MiB) already makes guessing
    // slow, but we add an explicit lockout so a same-user process cannot hammer the pipe.
    private const int MaxUnlockFailures = 5;
    private static readonly TimeSpan UnlockLockout = TimeSpan.FromSeconds(30);

    private readonly IVaultStateService _vaultState;
    private readonly ICryptoService _crypto;
    private readonly IDialogQueueService _dialogQueue;
    private static readonly ResourceLoader Res = new();
    private readonly Dictionary<string, SessionInfo> _sessions = new();
    private readonly Lock _sessionsLock = new();

    // SEC-03 user-consent cache: clientIds for which the user chose "remember for this session".
    // Cleared on vault lock (OnVaultLocked) so consent never outlives an unlock session.
    private readonly HashSet<string> _consentRemembered = new(StringComparer.Ordinal);
    private readonly Lock _consentLock = new();

    private readonly Lock _unlockLock = new();
    private int _unlockFailedCount;
    private DateTime _unlockLockoutUntil = DateTime.MinValue;

    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    /// <summary>
    /// Gets a value indicating whether the server loop is currently running.
    /// </summary>
    public bool IsRunning => _serverTask is not null && !_serverTask.IsCompleted;

    /// <summary>
    /// Initializes a new instance of <see cref="BrowserIpcService"/>.
    /// </summary>
    /// <param name="vaultState">Vault state service for credential lookups and unlock operations.</param>
    /// <param name="crypto">Crypto service for AES-GCM encryption of password responses.</param>
    /// <param name="dialogQueue">Serial dialog pump used to show the SEC-03 user-consent prompt.</param>
    public BrowserIpcService(IVaultStateService vaultState, ICryptoService crypto, IDialogQueueService dialogQueue)
    {
        _vaultState = vaultState;
        _crypto = crypto;
        _dialogQueue = dialogQueue;

        // SEC-03 + hardening: drop remembered consent AND all ECDH sessions when the vault locks.
        _vaultState.VaultLocked += OnVaultLocked;
    }

    /// <summary>
    /// Starts the Named Pipe server loop on a background thread.
    /// Has no effect if the server is already running.
    /// </summary>
    /// <returns>A completed task (the server runs in the background).</returns>
    public Task StartAsync()
    {
        if (IsRunning) return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        _serverTask = Task.Run(() => ServerLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Signals the server loop to stop and awaits its completion.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_serverTask is not null)
            {
                try { await _serverTask; } catch (OperationCanceledException) { }
            }
            _cts.Dispose();
            _cts = null;
            _serverTask = null;
        }
    }

    /// <summary>
    /// Main server loop: creates a pipe, waits for a client, processes one message, closes, repeats.
    /// Uses single-connection mode (one client at a time).
    /// Individual connection errors are caught and silently ignored to keep the loop alive.
    /// </summary>
    /// <param name="ct">Cancellation token that stops the loop when cancelled.</param>
    private async Task ServerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = CreateSecurePipe();
                await pipe.WaitForConnectionAsync(ct);

                // Read request
                var requestJson = await ReadPipeMessageAsync(pipe, ct);
                if (requestJson is not null)
                {
                    // Process and write response
                    var responseJson = await ProcessMessageAsync(requestJson);
                    await WritePipeMessageAsync(pipe, responseJson, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Individual connection errors should not kill the server; surface to Debug
                // so diagnostics aren't lost while keeping the loop resilient.
                System.Diagnostics.Debug.WriteLine($"[BrowserIpcService] connection error: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (pipe is not null)
                {
                    try
                    {
                        if (pipe.IsConnected) pipe.Disconnect();
                    }
                    catch { /* ignore */ }
                    pipe.Dispose();
                }
            }

            // Periodic session cleanup
            CleanupExpiredSessions();
        }
    }

    /// <summary>
    /// Creates a Named Pipe server with an ACL restricted to the current user SID only.
    /// This prevents other Windows users or processes from connecting to the pipe.
    /// </summary>
    /// <returns>A configured <see cref="NamedPipeServerStream"/> awaiting connection.</returns>
    private static NamedPipeServerStream CreateSecurePipe()
    {
        var pipeSecurity = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                currentUser,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
        }

        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            1, // maxConnections: single client
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity);
    }

    /// <summary>
    /// Routes an incoming JSON request to the appropriate handler and returns the JSON response.
    /// The <c>unlock-vault</c> action is handled asynchronously; all others are synchronous.
    /// Returns a JSON error response for malformed input or unknown actions.
    /// </summary>
    /// <param name="requestJson">UTF-8 JSON string of the IPC request envelope.</param>
    /// <returns>UTF-8 JSON string of the IPC response envelope.</returns>
    internal async Task<string> ProcessMessageAsync(string requestJson)
    {
        IpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(requestJson, IpcJsonContext.Default.IpcRequest);
        }
        catch
        {
            return SerializeResponse(new IpcResponse
            {
                Action = "unknown",
                Success = false,
                Error = "invalid-json"
            });
        }

        if (request is null)
        {
            return SerializeResponse(new IpcResponse
            {
                Action = "unknown",
                Success = false,
                Error = "empty-request"
            });
        }

        try
        {
            IpcResponse response;

            // Actions that may await a UI consent prompt (SEC-03) or the KDF (unlock) run async.
            response = request.Action switch
            {
                "unlock-vault"            => await HandleUnlockVaultAsync(request),
                "get-credential-password" => await HandleGetCredentialPasswordAsync(request),
                "get-all-credentials"     => HandleGetAllCredentials(request),
                "exchange-keys"           => HandleExchangeKeys(request),
                "test-session"            => HandleTestSession(request),
                "get-status"              => HandleGetStatus(request),
                "get-credentials"         => HandleGetCredentials(request),
                "show-window"             => HandleShowWindow(request),
                _ => new IpcResponse
                {
                    Action = request.Action,
                    RequestId = request.RequestId,
                    Success = false,
                    Error = "unknown-action"
                }
            };

            return SerializeResponse(response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BrowserIpcService] handler '{request.Action}' failed: {ex.GetType().Name}: {ex.Message}");
            return SerializeResponse(new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "internal-error"
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Action Handlers
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles the <c>exchange-keys</c> action: generates a server ECDH P-256 key pair,
    /// derives the shared secret with the client's public key, runs HKDF-SHA256 to produce
    /// a 256-bit AES-GCM session key, stores it in <see cref="_sessions"/>, and returns
    /// the server's public key (SPKI Base64) plus a new session ID.
    /// </summary>
    private IpcResponse HandleExchangeKeys(IpcRequest request)
    {
        ExchangeKeysRequest? payload = null;
        if (request.Payload.HasValue)
        {
            payload = JsonSerializer.Deserialize(
                request.Payload.Value.GetRawText(),
                IpcJsonContext.Default.ExchangeKeysRequest);
        }

        if (payload is null || string.IsNullOrEmpty(payload.PublicKey))
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "missing-public-key"
            };
        }

        try
        {
            // Generate server ECDH key pair
            using var serverEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var serverPublicKey = serverEcdh.ExportSubjectPublicKeyInfo();

            // Import client public key and derive shared secret
            var clientPublicKeyBytes = Convert.FromBase64String(payload.PublicKey);
            using var clientEcdh = ECDiffieHellman.Create();
            clientEcdh.ImportSubjectPublicKeyInfo(clientPublicKeyBytes, out _);

            // Derive session key via HKDF
            var sharedSecret = serverEcdh.DeriveRawSecretAgreement(clientEcdh.PublicKey);
            var sessionKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                sharedSecret,
                CryptoConstants.KeySizeBytes,
                info: Encoding.UTF8.GetBytes("PassKey-IPC-Session"));

            // Zero shared secret
            CryptographicOperations.ZeroMemory(sharedSecret);

            // Create session
            var sessionId = Guid.NewGuid().ToString();
            var session = new SessionInfo
            {
                SessionId = sessionId,
                SessionKey = sessionKey,
                ClientId = request.ClientId
            };

            lock (_sessionsLock)
            {
                // Bound the session table: drop expired entries first, then if still at the
                // cap evict the oldest session so a flood of exchange-keys can't exhaust memory.
                if (_sessions.Count >= MaxSessions)
                {
                    PruneExpiredSessionsNoLock();
                    if (_sessions.Count >= MaxSessions)
                    {
                        var oldest = _sessions.Values.MinBy(s => s.CreatedAt);
                        if (oldest is not null)
                        {
                            CryptographicOperations.ZeroMemory(oldest.SessionKey);
                            _sessions.Remove(oldest.SessionId);
                        }
                    }
                }

                _sessions[sessionId] = session;
            }

            // Build response payload
            var responsePayload = new ExchangeKeysResponse(
                Convert.ToBase64String(serverPublicKey),
                sessionId);

            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = true,
                Payload = JsonSerializer.SerializeToElement(responsePayload, IpcJsonContext.Default.ExchangeKeysResponse)
            };
        }
        catch
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "key-exchange-failed"
            };
        }
    }

    /// <summary>
    /// Handles the <c>test-session</c> action: verifies that the given session ID exists
    /// and has not expired, and reports whether the vault is currently unlocked.
    /// </summary>
    private IpcResponse HandleTestSession(IpcRequest request)
    {
        TestSessionRequest? payload = null;
        if (request.Payload.HasValue)
        {
            payload = JsonSerializer.Deserialize(
                request.Payload.Value.GetRawText(),
                IpcJsonContext.Default.TestSessionRequest);
        }

        bool valid;
        lock (_sessionsLock)
        {
            valid = payload is not null
                    && _sessions.TryGetValue(payload.SessionId, out var session)
                    && DateTime.UtcNow - session.CreatedAt < SessionTimeout;
        }

        var responsePayload = new TestSessionResponse(valid, _vaultState.IsUnlocked);

        return new IpcResponse
        {
            Action = request.Action,
            RequestId = request.RequestId,
            Success = true,
            Payload = JsonSerializer.SerializeToElement(responsePayload, IpcJsonContext.Default.TestSessionResponse)
        };
    }

    /// <summary>
    /// Handles the <c>get-status</c> action: returns whether the vault is unlocked
    /// and the total count of stored password entries.
    /// </summary>
    private IpcResponse HandleGetStatus(IpcRequest request)
    {
        var entryCount = _vaultState.CurrentVault?.Passwords.Count ?? 0;
        var responsePayload = new GetStatusResponse(_vaultState.IsUnlocked, entryCount);

        return new IpcResponse
        {
            Action = request.Action,
            RequestId = request.RequestId,
            Success = true,
            Payload = JsonSerializer.SerializeToElement(responsePayload, IpcJsonContext.Default.GetStatusResponse)
        };
    }

    /// <summary>
    /// Handles the <c>get-credentials</c> action: returns credential summaries
    /// matching the supplied URL. Requires the vault to be unlocked.
    /// </summary>
    private IpcResponse HandleGetCredentials(IpcRequest request)
    {
        if (!_vaultState.IsUnlocked)
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "vault-locked"
            };
        }

        GetCredentialsRequest? payload = null;
        if (request.Payload.HasValue)
        {
            payload = JsonSerializer.Deserialize(
                request.Payload.Value.GetRawText(),
                IpcJsonContext.Default.GetCredentialsRequest);
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Url))
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "missing-url"
            };
        }

        var matches = _vaultState.FindCredentialsByUrl(payload.Url);
        var summaries = matches
            .Select(p => new CredentialSummary(p.Id, p.Title, p.Username, !string.IsNullOrEmpty(p.Password)))
            .ToList();

        var responsePayload = new GetCredentialsResponse(summaries);

        return new IpcResponse
        {
            Action = request.Action,
            RequestId = request.RequestId,
            Success = true,
            Payload = JsonSerializer.SerializeToElement(responsePayload, IpcJsonContext.Default.GetCredentialsResponse)
        };
    }

    /// <summary>
    /// Handles the <c>get-credential-password</c> action: retrieves a single password entry
    /// by GUID. If a valid ECDH session exists, the password is AES-GCM encrypted with the
    /// session key and returned as <c>{ encryptedPassword, nonce }</c>. Without a session,
    /// the password is returned Base64-encoded in plaintext (less secure fallback).
    /// Requires the vault to be unlocked.
    /// </summary>
    private async Task<IpcResponse> HandleGetCredentialPasswordAsync(IpcRequest request)
    {
        if (!_vaultState.IsUnlocked)
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "vault-locked"
            };
        }

        GetCredentialPasswordRequest? payload = null;
        if (request.Payload.HasValue)
        {
            payload = JsonSerializer.Deserialize(
                request.Payload.Value.GetRawText(),
                IpcJsonContext.Default.GetCredentialPasswordRequest);
        }

        if (payload is null || payload.Id == Guid.Empty)
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "missing-id"
            };
        }

        // Require a valid ECDH session BOUND to this request — by session ID (SEC-01) AND by the
        // exact ClientId that performed the handshake (SEC-02). The password is always AES-GCM
        // encrypted with that session's key: there is no plaintext fallback, and a session
        // established by one client cannot be reused by another even if its ID leaks.
        SessionInfo? session = null;
        if (!string.IsNullOrEmpty(payload.SessionId) && !string.IsNullOrEmpty(request.ClientId))
        {
            lock (_sessionsLock)
            {
                if (_sessions.TryGetValue(payload.SessionId, out var s)
                    && DateTime.UtcNow - s.CreatedAt < SessionTimeout
                    && string.Equals(s.ClientId, request.ClientId, StringComparison.Ordinal))
                {
                    session = s;
                }
            }
        }

        if (session is null)
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "ecdh-session-required"
            };
        }

        var entry = _vaultState.GetCredentialById(payload.Id);
        if (entry is null)
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "credential-not-found"
            };
        }

        // SEC-03: explicit user consent before releasing a password (unless remembered this session).
        var consentBody = string.Format(Res.GetString("IpcConsentBodyPassword"), entry.Title);
        if (!await EnsureConsentAsync(request.ClientId, consentBody))
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "consent-denied"
            };
        }

        var passwordBytes = Encoding.UTF8.GetBytes(entry.Password);
        var encryptedBlob = _crypto.Encrypt(passwordBytes, session.SessionKey);
        CryptographicOperations.ZeroMemory(passwordBytes);

        // Split blob into nonce + ciphertext+tag for the response
        var nonce = Convert.ToBase64String(encryptedBlob.AsSpan(0, CryptoConstants.NonceSizeBytes));
        var encryptedData = Convert.ToBase64String(encryptedBlob.AsSpan(CryptoConstants.NonceSizeBytes));

        var responsePayload = new GetCredentialPasswordResponse(encryptedData, nonce);

        return new IpcResponse
        {
            Action = request.Action,
            RequestId = request.RequestId,
            Success = true,
            Payload = JsonSerializer.SerializeToElement(responsePayload, IpcJsonContext.Default.GetCredentialPasswordResponse)
        };
    }

    /// <summary>
    /// Handles the <c>unlock-vault</c> action: passes the master password from the request
    /// payload to <see cref="IVaultStateService.UnlockAsync"/>. The master password char array
    /// is zeroed after use to minimize time in memory.
    /// </summary>
    private async Task<IpcResponse> HandleUnlockVaultAsync(IpcRequest request)
    {
        UnlockVaultRequest? payload = null;
        if (request.Payload.HasValue)
        {
            payload = JsonSerializer.Deserialize(
                request.Payload.Value.GetRawText(),
                IpcJsonContext.Default.UnlockVaultRequest);
        }

        if (payload is null || string.IsNullOrEmpty(payload.MasterPassword))
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "missing-master-password"
            };
        }

        // Brute-force throttle: reject unlock attempts while a lockout window is active.
        lock (_unlockLock)
        {
            if (DateTime.UtcNow < _unlockLockoutUntil)
            {
                return new IpcResponse
                {
                    Action = request.Action,
                    RequestId = request.RequestId,
                    Success = false,
                    Error = "too-many-attempts"
                };
            }
        }

        // Copy to char array so we can zero it after use
        var masterChars = payload.MasterPassword.ToCharArray();
        try
        {
            var ok = await _vaultState.UnlockAsync(masterChars.AsMemory());

            lock (_unlockLock)
            {
                if (ok)
                {
                    _unlockFailedCount = 0;
                    _unlockLockoutUntil = DateTime.MinValue;
                }
                else if (++_unlockFailedCount >= MaxUnlockFailures)
                {
                    _unlockLockoutUntil = DateTime.UtcNow + UnlockLockout;
                    _unlockFailedCount = 0; // reset the counter; the lockout window now applies
                }
            }

            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = ok,
                Error = ok ? null : "invalid-master-password"
            };
        }
        finally
        {
            Array.Clear(masterChars, 0, masterChars.Length);
        }
    }

    /// <summary>
    /// Handles the <c>get-all-credentials</c> action: returns credential summaries (title +
    /// username, NO passwords) for every entry, sorted alphabetically by title. Requires the vault
    /// to be unlocked. No consent prompt here: this is metadata shown in the popup the user opened;
    /// the SEC-03 consent gate applies only to <c>get-credential-password</c> (where a secret leaves).
    /// </summary>
    private IpcResponse HandleGetAllCredentials(IpcRequest request)
    {
        if (!_vaultState.IsUnlocked)
        {
            return new IpcResponse
            {
                Action = request.Action,
                RequestId = request.RequestId,
                Success = false,
                Error = "vault-locked"
            };
        }

        var all = (_vaultState.CurrentVault?.Passwords ?? [])
            .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .Select(p => new CredentialSummary(p.Id, p.Title, p.Username, !string.IsNullOrEmpty(p.Password)))
            .ToList();

        var responsePayload = new GetAllCredentialsResponse(all);

        return new IpcResponse
        {
            Action = request.Action,
            RequestId = request.RequestId,
            Success = true,
            Payload = JsonSerializer.SerializeToElement(responsePayload, IpcJsonContext.Default.GetAllCredentialsResponse)
        };
    }

    /// <summary>
    /// Handles the <c>show-window</c> action: brings the PassKey Desktop main window to the
    /// foreground by calling <see cref="Microsoft.UI.Xaml.Window.Activate"/> on the UI thread.
    /// Activation failure is silently ignored (non-critical).
    /// </summary>
    private static IpcResponse HandleShowWindow(IpcRequest request)
    {
        try
        {
            App.MainWindow?.DispatcherQueue?.TryEnqueue(() => App.MainWindow?.Activate());
        }
        catch
        {
            // Non-critical: ignore if window cannot be activated
        }

        return new IpcResponse
        {
            Action = request.Action,
            RequestId = request.RequestId,
            Success = true
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // Pipe I/O helpers (same wire format as Native Messaging)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Reads a length-prefixed message from the pipe.
    /// Wire format: [4-byte little-endian length][UTF-8 JSON payload].
    /// Returns null if the stream closes before a complete message is read or the message
    /// exceeds <c>MaxMessageSize</c> (1 MB).
    /// </summary>
    private static async Task<string?> ReadPipeMessageAsync(Stream pipe, CancellationToken ct)
    {
        var lengthBuffer = new byte[LengthPrefixSize];
        var bytesRead = await ReadExactAsync(pipe, lengthBuffer, ct);
        if (bytesRead < LengthPrefixSize)
            return null;

        var messageLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        if (messageLength <= 0 || messageLength > MaxMessageSize)
            return null;

        var messageBuffer = new byte[messageLength];
        bytesRead = await ReadExactAsync(pipe, messageBuffer, ct);
        if (bytesRead < messageLength)
            return null;

        return Encoding.UTF8.GetString(messageBuffer);
    }

    /// <summary>
    /// Writes a length-prefixed message to the pipe and flushes.
    /// Wire format: [4-byte little-endian length][UTF-8 JSON payload].
    /// </summary>
    private static async Task WritePipeMessageAsync(Stream pipe, string json, CancellationToken ct)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var lengthBuffer = new byte[LengthPrefixSize];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, jsonBytes.Length);

        await pipe.WriteAsync(lengthBuffer, ct);
        await pipe.WriteAsync(jsonBytes, ct);
        await pipe.FlushAsync(ct);
    }

    /// <summary>
    /// Reads exactly <c>buffer.Length</c> bytes from <paramref name="stream"/>, handling partial reads.
    /// Returns the total bytes read (may be less than requested on EOF).
    /// </summary>
    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0) return totalRead;
            totalRead += read;
        }
        return totalRead;
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Serializes an <see cref="IpcResponse"/> to a JSON string using the source-generated context.
    /// </summary>
    private static string SerializeResponse(IpcResponse response) =>
        JsonSerializer.Serialize(response, IpcJsonContext.Default.IpcResponse);

    /// <summary>
    /// Removes all sessions whose <see cref="SessionInfo.CreatedAt"/> exceeds
    /// <see cref="SessionTimeout"/> and zeros their session keys before removal.
    /// </summary>
    private void CleanupExpiredSessions()
    {
        lock (_sessionsLock)
        {
            PruneExpiredSessionsNoLock();
        }
    }

    /// <summary>
    /// Removes and zeroes all expired sessions. The caller MUST already hold
    /// <see cref="_sessionsLock"/>.
    /// </summary>
    private void PruneExpiredSessionsNoLock()
    {
        var expiredKeys = _sessions
            .Where(kv => DateTime.UtcNow - kv.Value.CreatedAt >= SessionTimeout)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            if (_sessions.TryGetValue(key, out var session))
            {
                CryptographicOperations.ZeroMemory(session.SessionKey);
                _sessions.Remove(key);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SEC-03 — User consent
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Clears remembered consent and zeroes/removes every ECDH session when the vault locks,
    /// so neither consent nor session keys outlive an unlock session.
    /// </summary>
    private void OnVaultLocked()
    {
        lock (_consentLock)
        {
            _consentRemembered.Clear();
        }
        lock (_sessionsLock)
        {
            foreach (var session in _sessions.Values)
                CryptographicOperations.ZeroMemory(session.SessionKey);
            _sessions.Clear();
        }
    }

    /// <summary>
    /// Returns true if the caller may receive sensitive data: either the <paramref name="clientId"/>
    /// was already granted "remember for this session", or the user approves the consent prompt now.
    /// A null/empty clientId always prompts and is never remembered.
    /// </summary>
    private async Task<bool> EnsureConsentAsync(string? clientId, string body)
    {
        var key = clientId ?? string.Empty;
        if (key.Length > 0)
        {
            lock (_consentLock)
            {
                if (_consentRemembered.Contains(key)) return true;
            }
        }

        var (granted, remember) = await RequestConsentAsync(body);
        if (granted && remember && key.Length > 0)
        {
            lock (_consentLock)
            {
                _consentRemembered.Add(key);
            }
        }
        return granted;
    }

    /// <summary>
    /// Shows the SEC-03 consent <see cref="ContentDialog"/> (with a "remember for this session"
    /// checkbox) on the UI thread and awaits the user's choice. Fails secure (denies) when no UI
    /// is available. The default button is "Deny".
    /// </summary>
    private async Task<(bool granted, bool remember)> RequestConsentAsync(string body)
    {
        var dispatcher = App.MainWindow?.DispatcherQueue;
        if (dispatcher is null) return (false, false); // fail-secure: cannot ask the user

        var tcs = new TaskCompletionSource<(bool granted, bool remember)>();

        var enqueued = dispatcher.TryEnqueue(() =>
        {
            try
            {
                // Capture the checkbox state via its events (which fire on the UI thread) into a
                // plain bool. CRITICAL: never read a UI element (rememberBox.IsChecked) from the
                // ContinueWith below — that runs on a thread-pool thread and accessing a WinUI
                // object off the UI thread throws RPC_E_WRONG_THREAD, which would leave `tcs`
                // never completed and DEADLOCK the IPC server awaiting the consent result.
                var remember = false;
                var rememberBox = new CheckBox { Content = Res.GetString("IpcConsentRemember") };
                rememberBox.Checked   += (_, _) => remember = true;
                rememberBox.Unchecked += (_, _) => remember = false;

                var panel = new StackPanel { Spacing = 12 };
                panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.WrapWholeWords });
                panel.Children.Add(rememberBox);

                _ = _dialogQueue.EnqueueAndWait(() =>
                {
                    var dialog = new ContentDialog
                    {
                        Title = Res.GetString("IpcConsentTitle"),
                        Content = panel,
                        PrimaryButtonText = Res.GetString("IpcConsentAllow"),
                        CloseButtonText = Res.GetString("IpcConsentDeny"),
                        DefaultButton = ContentDialogButton.Close, // default = Deny (fail-safe)
                        XamlRoot = _dialogQueue.XamlRootAccessor?.Invoke(),
                    };
                    return dialog.ShowAsync().AsTask();
                }).ContinueWith(t =>
                {
                    // Reads only plain values (dialog result enum + captured bool) → thread-safe.
                    var granted = t.Status == TaskStatus.RanToCompletion && t.Result == ContentDialogResult.Primary;
                    tcs.TrySetResult((granted, granted && remember));
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrowserIpcService] consent dialog failed: {ex.Message}");
                tcs.TrySetResult((false, false));
            }
        });

        if (!enqueued) return (false, false); // fail-secure
        return await tcs.Task;
    }

    /// <summary>
    /// Cancels the server loop, unsubscribes from vault events, and zeros all active session keys
    /// before releasing resources.
    /// </summary>
    public void Dispose()
    {
        _vaultState.VaultLocked -= OnVaultLocked;

        _cts?.Cancel();
        _cts?.Dispose();

        lock (_consentLock)
        {
            _consentRemembered.Clear();
        }

        // Zero all session keys
        lock (_sessionsLock)
        {
            foreach (var session in _sessions.Values)
                CryptographicOperations.ZeroMemory(session.SessionKey);
            _sessions.Clear();
        }
    }
}
