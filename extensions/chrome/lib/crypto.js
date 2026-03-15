/**
 * ECDH key exchange + AES-GCM decryption via Web Crypto API.
 *
 * Compatibility with .NET server (BrowserIpcService.cs):
 * - ECDH P-256 with SPKI DER format (ExportSubjectPublicKeyInfo / ImportSubjectPublicKeyInfo)
 * - HKDF-SHA256 with empty salt (32 zero bytes) and info "PassKey-IPC-Session"
 * - AES-256-GCM: nonce 12 bytes, tag 128 bits appended to ciphertext
 *
 * .NET blob format from CryptoService.Encrypt: [nonce 12B || ciphertext || tag 16B]
 * Server splits it as: nonce = blob[0..12], encryptedPassword = blob[12..] (ct+tag)
 */

// ─── Cryptographic Pipeline ───────────────────────────────────────────────────
// ECDH P-256 (ephemeral key pair) → shared secret → HKDF-SHA256 (32-byte key)
// → AES-256-GCM (unique nonce per message)

const HKDF_INFO = 'PassKey-IPC-Session';
const HKDF_SALT_LENGTH = 32; // SHA-256 hash length — .NET HKDF null salt = zeros of hash length
const AES_KEY_BITS = 256;
const AES_TAG_BITS = 128;

// ─── Key Generation ───────────────────────────────────────────────────────────

/**
 * Generates an ephemeral ECDH P-256 key pair for a single key exchange session.
 * The key pair is extractable so the public key can be exported in SPKI format
 * and sent to the Desktop service. Usage is limited to 'deriveBits'.
 *
 * @returns {Promise<CryptoKeyPair>} An ECDH P-256 key pair with extractable public key.
 */
async function generateKeyPair() {
  return crypto.subtle.generateKey(
    { name: 'ECDH', namedCurve: 'P-256' },
    true, // extractable — needed to export public key
    ['deriveBits']
  );
}

/**
 * Exports a CryptoKey public key in SPKI DER format encoded as Base64.
 * The SPKI format matches the .NET ECDiffieHellman.ExportSubjectPublicKeyInfo()
 * output expected by BrowserIpcService.HandleExchangeKeys on the server side.
 *
 * @param {CryptoKey} publicKey - The ECDH public key to export.
 * @returns {Promise<string>} Base64-encoded SPKI DER representation of the public key.
 */
async function exportPublicKeySpki(publicKey) {
  const spkiBuffer = await crypto.subtle.exportKey('spki', publicKey);
  return arrayBufferToBase64(spkiBuffer);
}

// ─── Session Key Derivation ───────────────────────────────────────────────────

/**
 * Derives a non-extractable AES-256-GCM session key from the ECDH shared secret
 * using HKDF-SHA256. The derivation matches the server-side implementation in
 * BrowserIpcService.cs (ECDiffieHellmanCng + HKDF with info "PassKey-IPC-Session").
 *
 * Derivation steps:
 *   1. Import the server's Base64 SPKI public key as an ECDH CryptoKey.
 *   2. Compute the ECDH shared secret via deriveBits (raw agreement, 256 bits).
 *   3. Import the shared secret as HKDF key material.
 *   4. Apply HKDF-SHA256 with salt=32 zero bytes (matching .NET null-salt convention)
 *      and info="PassKey-IPC-Session" to produce 256 bits of key material.
 *   5. Import the result as a non-extractable AES-GCM decrypt key.
 *
 * @param {CryptoKey} privateKey - The client's ephemeral ECDH private key.
 * @param {string} serverPublicKeyBase64 - The server's SPKI public key encoded as Base64.
 * @returns {Promise<CryptoKey>} A non-extractable AES-256-GCM key usable for decryption.
 * @throws {Error} If the server public key cannot be imported or HKDF derivation fails.
 */
async function deriveSessionKey(privateKey, serverPublicKeyBase64) {
  // 1. Import server SPKI public key
  const spkiBytes = base64ToArrayBuffer(serverPublicKeyBase64);
  const serverPublicKey = await crypto.subtle.importKey(
    'spki',
    spkiBytes,
    { name: 'ECDH', namedCurve: 'P-256' },
    false,
    []
  );

  // 2. Derive raw shared secret (ECDH agreement)
  const sharedBits = await crypto.subtle.deriveBits(
    { name: 'ECDH', public: serverPublicKey },
    privateKey,
    AES_KEY_BITS
  );

  // 3. Import shared secret as HKDF key material
  const hkdfKey = await crypto.subtle.importKey(
    'raw',
    sharedBits,
    { name: 'HKDF' },
    false,
    ['deriveBits']
  );

  // 4. HKDF derive: SHA-256, salt = 32 zero bytes (matches .NET null salt), info = "PassKey-IPC-Session"
  const salt = new Uint8Array(HKDF_SALT_LENGTH); // All zeros
  const info = new TextEncoder().encode(HKDF_INFO);

  const derivedBits = await crypto.subtle.deriveBits(
    { name: 'HKDF', hash: 'SHA-256', salt: salt, info: info },
    hkdfKey,
    AES_KEY_BITS
  );

  // 5. Import as AES-GCM key (non-extractable for security)
  return crypto.subtle.importKey(
    'raw',
    derivedBits,
    { name: 'AES-GCM' },
    false,
    ['decrypt']
  );
}

// ─── Decryption ───────────────────────────────────────────────────────────────

/**
 * Decrypts a password that was encrypted by the Desktop service using AES-256-GCM.
 *
 * The server response format from BrowserIpcService.HandleGetCredentialPassword:
 *   - nonce: Base64-encoded 12-byte AES-GCM IV (randomly generated per encryption).
 *   - encryptedPassword: Base64-encoded concatenation of [ciphertext || 16-byte tag].
 *
 * Web Crypto AES-GCM automatically handles the authentication tag appended to the
 * ciphertext buffer (standard behavior when tagLength is specified).
 *
 * @param {CryptoKey} sessionKey - The AES-256-GCM session key from deriveSessionKey.
 * @param {string} nonceBase64 - Base64-encoded 12-byte AES-GCM initialization vector.
 * @param {string} encryptedBase64 - Base64-encoded ciphertext with appended 16-byte GCM tag.
 * @returns {Promise<string>} The decrypted password as a UTF-8 string.
 * @throws {Error} If decryption fails due to an invalid key, tampered ciphertext, or bad tag.
 */
async function decryptPassword(sessionKey, nonceBase64, encryptedBase64) {
  const nonce = base64ToUint8Array(nonceBase64);
  const encryptedData = base64ToUint8Array(encryptedBase64);

  const decrypted = await crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: nonce, tagLength: AES_TAG_BITS },
    sessionKey,
    encryptedData
  );

  return new TextDecoder().decode(decrypted);
}

// ─── Base64 Helpers ───────────────────────────────────────────────────────────

/**
 * Encodes an ArrayBuffer as a standard Base64 string.
 * Used to encode binary key material for transmission over JSON.
 *
 * @param {ArrayBuffer} buffer - The binary data to encode.
 * @returns {string} Base64-encoded string representation of the buffer.
 */
function arrayBufferToBase64(buffer) {
  const bytes = new Uint8Array(buffer);
  let binary = '';
  for (let i = 0; i < bytes.length; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

/**
 * Decodes a Base64 string into an ArrayBuffer.
 * Used to decode SPKI-encoded public keys received from the server.
 *
 * @param {string} base64 - The Base64-encoded string to decode.
 * @returns {ArrayBuffer} The decoded binary data.
 */
function base64ToArrayBuffer(base64) {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes.buffer;
}

/**
 * Decodes a Base64 string into a Uint8Array.
 * Used to decode nonces and ciphertext for AES-GCM decryption.
 * Returns a Uint8Array (not ArrayBuffer) for direct use with SubtleCrypto APIs.
 *
 * @param {string} base64 - The Base64-encoded string to decode.
 * @returns {Uint8Array} The decoded binary data as a typed array.
 */
function base64ToUint8Array(base64) {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}
