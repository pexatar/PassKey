# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | ✅ Yes    |

## Reporting a Vulnerability

**Please do NOT report security vulnerabilities through public GitHub issues.**

Report vulnerabilities privately via [GitHub Security Advisories](https://github.com/pexatar/PassKey/security/advisories/new).

### Response Timeline
- **Acknowledgement:** within 48 hours
- **Status update:** within 7 days
- **Fix / mitigation:** within 90 days of disclosure

### Scope — In Scope
- Cryptographic weaknesses in vault encryption (AES-256-GCM, PBKDF2, Argon2id)
- Key derivation vulnerabilities
- Native Messaging / IPC channel vulnerabilities
- Vault data exposure bugs
- Authentication bypass in vault unlock

### Scope — Out of Scope
- Denial-of-service on the local machine
- Physical access attacks
- Bugs in third-party dependencies (report to them directly)
- Issues requiring malware already running with admin privileges

---

## Security Design

### Cryptographic Primitives

| Component | Algorithm | Parameters |
|-----------|-----------|------------|
| Vault encryption | AES-256-GCM | 256-bit key, 96-bit nonce, 128-bit auth tag |
| Key derivation (new vaults) | Argon2id | Memory: 64 MB, Iterations: 3, Parallelism: 4 |
| Key derivation (legacy vaults) | PBKDF2-SHA256 | 600,000 iterations |
| IPC session key | ECDH P-256 + HKDF-SHA256 | Ephemeral, per-connection |
| IPC message encryption | AES-256-GCM | Unique nonce per message |

All parameters meet or exceed OWASP 2023 recommendations for password hashing and authenticated encryption.

### Two-Tier Key Architecture

PassKey uses a Key Encryption Key (KEK) / Data Encryption Key (DEK) split:

1. **Master password + salt → KEK** (via Argon2id or PBKDF2-SHA256)
2. **KEK decrypts the wrapped DEK** (stored in the VaultMetadata table as an AES-GCM blob)
3. **DEK decrypts the vault blob** (stored in the VaultData table)

Changing the master password re-wraps the DEK with a new KEK and a new random salt, without re-encrypting the vault blob. This makes master password changes fast regardless of vault size.

### Key Management in Memory

- The DEK is stored in a `PinnedSecureBuffer` — a managed byte array pinned in memory using `GCHandle.Alloc(Pinned)` to prevent GC relocation.
- On vault lock or app shutdown, `CryptographicOperations.ZeroMemory` is called before the GC pin is released, ensuring the DEK bytes are overwritten even if the JIT would optimise away a plain `Array.Clear`.
- The master password is passed as `ReadOnlySpan<char>` or `char[]` and cleared with `Array.Clear` immediately after the KDF computation.

### Vault Storage

- SQLite database stored at `%LOCALAPPDATA%\PassKey\vault.db`.
- Three tables: `VaultMetadata` (plaintext KDF parameters), `VaultData` (single AES-GCM encrypted blob), `ActivityLog` (plaintext audit trail).
- Blob format: `[nonce (12 bytes) || ciphertext || authentication tag (16 bytes)]`.

### Backup Format (`.pkbak`)

- Magic header: `PKBK` (4 bytes) + version byte (1 byte)
- Argon2id salt (32 bytes)
- AES-256-GCM nonce (12 bytes)
- Encrypted payload (vault blob)

The backup password may differ from the vault master password.

### Browser Extension IPC

- Communication between the browser extension and PassKey Desktop uses the Native Messaging protocol (a local Named Pipe).
- Each connection establishes an ephemeral ECDH P-256 key pair, performs a key exchange, derives a 32-byte session key via HKDF-SHA256, and uses AES-256-GCM with a unique nonce per message.
- The Named Pipe ACL restricts connections to the same Windows user account that owns the PassKey Desktop process.
- No data traverses any network interface.

### Clipboard Security

- Passwords copied to the clipboard are automatically cleared after 30 seconds.
- `ClipboardContentOptions.IsAllowedInHistory = false` suppresses the entry from Windows clipboard history (requires Windows 10 version 1809+).

---

## What PassKey Does NOT Protect Against

PassKey is designed to be secure against remote attackers and offline vault theft. It does **not** protect against:

- **Malware** running on the same machine with sufficient privileges to read process memory or inject keystrokes.
- **Keyloggers** active when the master password is typed into the unlock screen.
- **An unlocked vault left unattended** with the screen visible to others.
- **Physical access** to the machine while the vault is unlocked.
- **Weak master passwords** — choose a long passphrase (4+ random words or 16+ characters).

---

## Known Limitations

- PassKey v1.0 is distributed with a **self-signed certificate**. Windows SmartScreen will display an "Unknown publisher" warning on first installation. This is expected behaviour and will be resolved with a commercial EV code-signing certificate in a future release.

---

## Past Security Advisories

None. This is the initial public release (v1.0.0).
