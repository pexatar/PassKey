/**
 * PassKey Chrome Extension - Service Worker (Background Script)
 *
 * Manages:
 * - Native Messaging connection to BrowserHost.exe via chrome.runtime.connectNative
 * - ECDH session establishment for encrypted password retrieval
 * - Message routing between popup/content script and native host
 *
 * The native messaging connection keeps the service worker alive (MV3 behavior).
 * On disconnection, the session is re-established on the next request.
 */

// Load shared libraries (non-module service worker)
importScripts('lib/messages.js', 'lib/crypto.js');

const NATIVE_HOST = 'com.passkey.host';
const REQUEST_TIMEOUT_MS = 5000;

// ─── State (lost on service worker restart — re-established automatically) ────

let port = null;
let sessionId = null;
let sessionKey = null; // CryptoKey (AES-GCM, non-extractable)
let keyPair = null;    // ECDH key pair
const pendingRequests = new Map(); // requestId → { resolve, reject, timeoutId }

// ─── Native Messaging ─────────────────────────────────────────────────────────

/**
 * Ensures a Native Messaging port to BrowserHost.exe is open.
 * Creates a new port if none exists. Sets up onMessage and onDisconnect handlers.
 * onMessage dispatches responses to the matching pending request by requestId.
 * onDisconnect rejects all pending requests and resets session state.
 *
 * @returns {chrome.runtime.Port|null} The active port, or null if the host is unavailable.
 */
function ensurePort() {
  if (port) return port;

  try {
    port = chrome.runtime.connectNative(NATIVE_HOST);
  } catch (e) {
    return null;
  }

  port.onMessage.addListener((message) => {
    const pending = pendingRequests.get(message.requestId);
    if (pending) {
      clearTimeout(pending.timeoutId);
      pendingRequests.delete(message.requestId);
      pending.resolve(message);
    }
  });

  port.onDisconnect.addListener(() => {
    const error = chrome.runtime.lastError?.message || 'native-disconnected';
    port = null;
    sessionId = null;
    sessionKey = null;
    keyPair = null;

    // Reject all pending requests
    for (const [id, pending] of pendingRequests) {
      clearTimeout(pending.timeoutId);
      pending.reject(new Error(error));
    }
    pendingRequests.clear();
  });

  return port;
}

/**
 * Sends a message to the native host and returns the response as a Promise.
 * Registers the request in pendingRequests with a 5-second timeout.
 * The promise is resolved by the onMessage handler when the matching requestId arrives.
 *
 * @param {object} message - IPC request envelope (from buildRequest)
 * @returns {Promise<object>} IPC response envelope
 * @throws {Error} If the Desktop is not running ('desktop-not-running') or the request times out ('timeout').
 */
function sendNativeMessage(message) {
  return new Promise((resolve, reject) => {
    const p = ensurePort();
    if (!p) {
      reject(new Error('desktop-not-running'));
      return;
    }

    const timeoutId = setTimeout(() => {
      pendingRequests.delete(message.requestId);
      reject(new Error('timeout'));
    }, REQUEST_TIMEOUT_MS);

    pendingRequests.set(message.requestId, { resolve, reject, timeoutId });

    try {
      p.postMessage(message);
    } catch (e) {
      clearTimeout(timeoutId);
      pendingRequests.delete(message.requestId);
      reject(e);
    }
  });
}

// ─── ECDH Session ─────────────────────────────────────────────────────────────

/**
 * Ensures a valid ECDH session exists with the Desktop app.
 * If a session ID and key are already in memory, tests the session via 'test-session'.
 * If the session is invalid or missing, performs a full ECDH key exchange:
 *   1. Generates an ephemeral ECDH P-256 key pair (generateKeyPair).
 *   2. Exports the public key in SPKI Base64 format.
 *   3. Sends 'exchange-keys' to Desktop with the public key.
 *   4. Desktop generates its own key pair, derives the shared secret, runs HKDF-SHA256
 *      with info "PassKey-IPC-Session", and returns its public key + session ID.
 *   5. Client derives the same session key using deriveSessionKey.
 * The resulting AES-256-GCM session key (non-extractable) is stored in sessionKey.
 *
 * @returns {Promise<boolean>} true if session is valid and ready for use.
 * @throws {Error} If key exchange fails.
 */
async function ensureSession() {
  // Test existing session
  if (sessionId && sessionKey) {
    try {
      const testReq = buildRequest('test-session', { sessionId });
      const testResp = await sendNativeMessage(testReq);
      if (testResp.success && testResp.payload?.valid) {
        return true;
      }
    } catch {
      // Session invalid or connection lost — fall through to re-exchange
    }
    sessionId = null;
    sessionKey = null;
  }

  // Generate new ECDH key pair
  keyPair = await generateKeyPair();
  const pubKeyBase64 = await exportPublicKeySpki(keyPair.publicKey);

  // Exchange keys with Desktop
  const exchangeReq = buildRequest('exchange-keys', { publicKey: pubKeyBase64 });
  const exchangeResp = await sendNativeMessage(exchangeReq);

  if (!exchangeResp.success) {
    throw new Error(exchangeResp.error || 'key-exchange-failed');
  }

  // Derive session key from ECDH shared secret
  sessionId = exchangeResp.payload.sessionId;
  sessionKey = await deriveSessionKey(
    keyPair.privateKey,
    exchangeResp.payload.publicKey
  );

  return true;
}

// ─── Message Handlers ─────────────────────────────────────────────────────────

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  handleMessage(msg, sender)
    .then(sendResponse)
    .catch(err => sendResponse({ success: false, error: err.message || 'internal-error' }));
  return true; // Will respond asynchronously
});

/**
 * Routes messages from popup and content scripts to appropriate handlers.
 * Supported message types: get-status, get-credentials, get-all-credentials,
 * fill-credential, copy-credential, unlock-vault, show-window, update-badge, detect-forms.
 *
 * @param {object} msg - Message object with a 'type' field.
 * @param {chrome.runtime.MessageSender} sender - Sender context (tab ID, etc.).
 * @returns {Promise<object>} Handler result.
 */
async function handleMessage(msg, sender) {
  switch (msg.type) {
    case 'get-status':
      return handleGetStatus();

    case 'get-credentials':
      return handleGetCredentials(msg.url);

    case 'get-all-credentials':
      return handleGetAllCredentials();

    case 'fill-credential':
      return handleFillCredential(msg.id, msg.username, msg.tabId ?? sender.tab?.id);

    case 'copy-credential':
      return handleCopyCredential(msg.id);

    case 'unlock-vault':
      return handleUnlockVault(msg.masterPassword);

    case 'show-window':
      return handleShowWindow();

    case 'update-badge':
      // Sent by content.js on page load — sender.tab.id available without 'tabs' permission
      updateBadgeForTab(sender.tab?.id, msg.url).catch(() => {});
      return { success: true };

    case 'detect-forms':
      return chrome.tabs.sendMessage(msg.tabId, { type: 'detect-forms' });

    default:
      return { success: false, error: 'unknown-message-type' };
  }
}

/**
 * Gets vault status (locked/unlocked state and entry count) from Desktop.
 *
 * @returns {Promise<object>} IPC response with payload { unlocked, entryCount }.
 */
async function handleGetStatus() {
  try {
    const req = buildRequest('get-status');
    const resp = await sendNativeMessage(req);
    return resp;
  } catch (err) {
    return { success: false, error: err.message || 'desktop-not-running' };
  }
}

/**
 * Gets matching credentials for a URL from Desktop.
 * Returns credential summaries (id, title, username, hasPassword) — no plaintext passwords.
 *
 * @param {string} url - The current page URL to match credentials against.
 * @returns {Promise<object>} IPC response with payload { credentials: CredentialSummary[] }.
 */
async function handleGetCredentials(url) {
  try {
    const req = buildRequest('get-credentials', { url });
    const resp = await sendNativeMessage(req);
    return resp;
  } catch (err) {
    return { success: false, error: err.message || 'desktop-not-running' };
  }
}

/**
 * Gets all credentials from Desktop (no URL filter), sorted alphabetically by title.
 *
 * @returns {Promise<object>} IPC response with payload { credentials: CredentialSummary[] }.
 */
async function handleGetAllCredentials() {
  try {
    const req = buildRequest('get-all-credentials');
    const resp = await sendNativeMessage(req);
    return resp;
  } catch (err) {
    return { success: false, error: err.message || 'desktop-not-running' };
  }
}

/**
 * Retrieves a password with ECDH decryption and returns the plaintext for clipboard copy.
 * Calls ensureSession() first to guarantee a session key is available.
 * If the server response includes a nonce, decrypts with AES-GCM; otherwise decodes Base64 plaintext.
 *
 * @param {string} credentialId - Entry GUID.
 * @returns {Promise<{success: boolean, password?: string, error?: string}>}
 */
async function handleCopyCredential(credentialId) {
  try {
    await ensureSession();
    const req = buildRequest('get-credential-password', { id: credentialId });
    const resp = await sendNativeMessage(req);
    if (!resp.success) return resp;

    let password;
    if (resp.payload.nonce && resp.payload.nonce.length > 0) {
      password = await decryptPassword(sessionKey, resp.payload.nonce, resp.payload.encryptedPassword);
    } else {
      password = new TextDecoder().decode(base64ToUint8Array(resp.payload.encryptedPassword));
    }
    return { success: true, password };
  } catch (err) {
    return { success: false, error: err.message || 'copy-failed' };
  }
}

/**
 * Sends the master password to Desktop to unlock the vault without leaving the browser.
 * The master password is included in the IPC request payload and zeroed by the Desktop after use.
 *
 * @param {string} masterPassword - The master password entered in the popup unlock form.
 * @returns {Promise<object>} IPC response with success true/false.
 */
async function handleUnlockVault(masterPassword) {
  try {
    const req = buildRequest('unlock-vault', { masterPassword });
    const resp = await sendNativeMessage(req);
    return resp;
  } catch (err) {
    return { success: false, error: err.message || 'unlock-failed' };
  }
}

/**
 * Requests Desktop to bring its main window to the foreground.
 *
 * @returns {Promise<object>} IPC response with success true/false.
 */
async function handleShowWindow() {
  try {
    const req = buildRequest('show-window');
    const resp = await sendNativeMessage(req);
    return resp;
  } catch (err) {
    return { success: false, error: err.message || 'show-window-failed' };
  }
}

// ─── Extension badge: credential count for current tab ────────────────────────

/**
 * Updates the extension toolbar badge with the number of matching credentials for a URL.
 * Clears the badge for non-HTTP URLs. Sets the badge background color to PassKey blue (#0078d4)
 * when credentials are found.
 *
 * @param {number|undefined} tabId - Tab ID to update the badge for.
 * @param {string|undefined} url - Current tab URL.
 * @returns {Promise<void>}
 */
async function updateBadgeForTab(tabId, url) {
  if (!url?.startsWith('http')) {
    chrome.action.setBadgeText({ text: '', tabId }).catch(() => {});
    return;
  }
  try {
    const resp = await handleGetCredentials(url);
    const count = resp?.payload?.credentials?.length ?? 0;
    chrome.action.setBadgeText({ text: count > 0 ? String(count) : '', tabId }).catch(() => {});
    if (count > 0) {
      chrome.action.setBadgeBackgroundColor({ color: '#0078d4', tabId }).catch(() => {});
    }
  } catch {
    chrome.action.setBadgeText({ text: '', tabId }).catch(() => {});
  }
}

// Badge is updated via 'update-badge' message from content.js (no 'tabs' permission needed)
// and from popup.js after loading credentials for the current site.

/**
 * Retrieves a password with ECDH decryption and sends it to the content script for autofill.
 * Calls ensureSession() first, then requests the encrypted password from Desktop,
 * decrypts it client-side, and forwards username + password to the content script.
 *
 * @param {string} credentialId - Entry GUID.
 * @param {string} username - Username to fill (from get-credentials response, not re-fetched).
 * @param {number} tabId - Tab to inject credentials into via content script message.
 * @returns {Promise<{success: boolean, error?: string}>}
 */
async function handleFillCredential(credentialId, username, tabId) {
  if (!tabId) {
    return { success: false, error: 'no-tab' };
  }

  try {
    // Ensure ECDH session for password decryption
    await ensureSession();

    // Request encrypted password
    const req = buildRequest('get-credential-password', { id: credentialId });
    const resp = await sendNativeMessage(req);

    if (!resp.success) {
      return resp;
    }

    // Decrypt password using session key
    let password;
    if (resp.payload.nonce && resp.payload.nonce.length > 0) {
      password = await decryptPassword(
        sessionKey,
        resp.payload.nonce,
        resp.payload.encryptedPassword
      );
    } else {
      // No session encryption — decode Base64 plaintext (fallback)
      password = new TextDecoder().decode(base64ToUint8Array(resp.payload.encryptedPassword));
    }

    // Send credentials to content script for form filling
    await chrome.tabs.sendMessage(tabId, {
      type: 'fill-fields',
      username: username,
      password: password
    });

    return { success: true };
  } catch (err) {
    return { success: false, error: err.message || 'fill-failed' };
  }
}
