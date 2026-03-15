/**
 * IPC message envelope builder and parser.
 *
 * Mirrors the IpcRequest / IpcResponse records defined in
 * PassKey.Desktop.Services.IpcModels.cs. JSON property names use camelCase
 * to match the .NET IpcJsonContext configured with JsonKnownNamingPolicy.CamelCase.
 *
 * Protocol version: 1. Responses with a different version number are rejected
 * by parseResponse with error code 'unsupported-version'.
 *
 * Firefox variant: clientId is resolved from browser.runtime (WebExtensions API)
 * instead of chrome.runtime used in the Chrome variant.
 */

// ─── Request Builder ──────────────────────────────────────────────────────────

/**
 * Builds an IPC request envelope ready to send to the native host via
 * browser.runtime.connectNative / port.postMessage.
 *
 * Generates a new UUID for requestId on every call so that the background
 * event page can correlate async responses in the pendingRequests Map.
 * The clientId is set to browser.runtime.id when running inside the extension;
 * falls back to 'unknown' in non-extension contexts (e.g., unit tests).
 *
 * @param {string} action - IPC action identifier. Supported values:
 *   'exchange-keys', 'test-session', 'get-status', 'get-credentials',
 *   'get-credential-password', 'unlock-vault', 'get-all-credentials', 'show-window'.
 * @param {object|null} [payload=null] - Action-specific request payload object,
 *   or null for actions that require no parameters (e.g., 'get-status').
 * @returns {object} A complete IPC request envelope with version, action,
 *   requestId, clientId, and payload fields.
 */
function buildRequest(action, payload = null) {
  return {
    version: 1,
    action: action,
    requestId: crypto.randomUUID(),
    clientId: typeof browser !== 'undefined' && browser.runtime ? browser.runtime.id : 'unknown',
    payload: payload
  };
}

// ─── Response Parser ──────────────────────────────────────────────────────────

/**
 * Validates and normalizes an IPC response received from the native host.
 *
 * Returns the response unchanged if it is a valid version-1 envelope.
 * Returns a synthetic error envelope for null/non-object responses or
 * responses with an unsupported protocol version.
 *
 * @param {object} response - Raw response object received from the native host.
 * @returns {object} Validated response with fields: version, action, requestId,
 *   success, payload, error. On validation failure, returns
 *   { success: false, error: 'invalid-response'|'unsupported-version', payload: null }.
 */
function parseResponse(response) {
  if (!response || typeof response !== 'object') {
    return { success: false, error: 'invalid-response', payload: null };
  }
  if (response.version !== 1) {
    return { success: false, error: 'unsupported-version', payload: null };
  }
  return response;
}
