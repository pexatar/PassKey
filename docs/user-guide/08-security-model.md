# Security Model

This document describes PassKey's cryptographic design, threat model, and security properties in detail. It is intended for security-conscious users and auditors.

For a shorter overview, see [SECURITY.md](../../SECURITY.md) in the repository root.

---

## Zero-Knowledge Design

PassKey is a zero-knowledge system:

- Your **master password** is never stored on disk or in memory beyond the brief moment it takes to derive the KEK.
- The master password is handled as `char[]` or `ReadOnlySpan<char>` and cleared with `Array.Clear` immediately after the KDF computation.
- There is no "forgot password" mechanism, no recovery key, and no server-side backup. If you forget your master password, your data is irrecoverable.

---

## Cryptographic Primitives

| Component | Algorithm | Parameters |
|-----------|-----------|------------|
| Vault encryption | AES-256-GCM | 256-bit key, 96-bit random nonce, 128-bit authentication tag |
| Key derivation (new vaults) | Argon2id | Memory: 64 MB, Iterations: 3, Parallelism: 4 |
| Key derivation (legacy vaults) | PBKDF2-SHA256 | 600,000 iterations |
| IPC session key | ECDH P-256 + HKDF-SHA256 | Ephemeral key pair per connection |
| IPC message encryption | AES-256-GCM | Unique nonce per message |

All parameters meet or exceed [OWASP 2023 recommendations](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html) for password hashing and authenticated encryption.

---

## Two-Tier Key Architecture

PassKey uses a Key Encryption Key (KEK) / Data Encryption Key (DEK) split:

```
Master Password + Salt
        │
        ▼
    ┌────────┐
    │  KDF   │  (Argon2id or PBKDF2-SHA256)
    └────────┘
        │
        ▼
       KEK  (Key Encryption Key — 32 bytes, ephemeral)
        │
        ▼
    ┌─────────────┐
    │ AES-GCM     │  Unwrap
    │ Decrypt     │──────────►  DEK  (Data Encryption Key — 32 bytes)
    └─────────────┘                     │
                                        ▼
                                   ┌─────────────┐
                                   │ AES-GCM     │  Decrypt
                                   │ Decrypt     │──────────►  Vault (JSON)
                                   └─────────────┘
```

1. **Master password + random salt** are fed into the KDF (Argon2id for new vaults, PBKDF2-SHA256 for legacy vaults) to produce the 32-byte **KEK**.
2. The **KEK** decrypts the wrapped DEK stored in the `VaultMetadata` database table.
3. The **DEK** decrypts the vault blob stored in the `VaultData` database table.

### Why Two Tiers?

- **Changing the master password** only re-wraps the DEK with a new KEK. The vault blob (which can be large) does not need to be re-encrypted. This makes password changes fast regardless of vault size.
- **The DEK is random** (not derived from user input), which means the vault's encryption strength does not degrade if the user chooses a weak master password — an attacker must still brute-force the KDF.

---

## Key Management in Memory

The DEK is stored in a `PinnedSecureBuffer`:

1. A 32-byte managed array is allocated.
2. The array is **pinned** in memory using `GCHandle.Alloc(array, GCHandleType.Pinned)` to prevent the garbage collector from copying it to a new location (which would leave the old copy in memory).
3. When the vault is locked or the app shuts down, `CryptographicOperations.ZeroMemory` is called on the array before the pin is released. This method is guaranteed by the .NET runtime not to be optimised away by the JIT compiler, unlike `Array.Clear`.

The KEK is never stored. It exists only as a local variable during the unlock operation and is eligible for GC immediately after.

---

## Vault Storage

The vault is stored in a SQLite database at `%LOCALAPPDATA%\PassKey\vault.db` with three tables:

| Table | Contents | Encrypted? |
|-------|----------|-----------|
| `VaultMetadata` | KDF algorithm, salt, iteration count, encrypted DEK blob | DEK blob is encrypted; metadata fields are plaintext |
| `VaultData` | Single AES-GCM encrypted blob containing the entire vault | Yes |
| `ActivityLog` | Audit trail (add/edit/delete actions with timestamps) | No |

### Encrypted Blob Format

```
[Nonce (12 bytes)] [Ciphertext (variable)] [Authentication Tag (16 bytes)]
```

- A **fresh random nonce** is generated for every encryption operation.
- The **authentication tag** ensures integrity — any modification to the ciphertext or nonce will cause decryption to fail.

---

## Backup Format (`.pkbak`)

```
Offset  Length  Description
0       4       Magic bytes: "PKBK" (0x504B424B)
4       1       Format version (currently 0x01)
5       32      Argon2id salt
37      12      AES-GCM nonce
49      var     Encrypted vault payload (ciphertext + 16-byte auth tag)
```

The backup password (which may differ from the vault master password) is fed through Argon2id with the embedded salt to derive a 32-byte key, which decrypts the payload.

---

## Browser Extension IPC

Communication between the browser extension and PassKey Desktop follows this path:

```
Extension  ──(Native Messaging / stdio)──►  BrowserHost  ──(Named Pipe)──►  Desktop
```

### Session Establishment

1. On each new connection, the extension generates an **ephemeral ECDH P-256 key pair**.
2. The BrowserHost generates its own ephemeral ECDH P-256 key pair.
3. Public keys are exchanged in plaintext (since this is a local connection, MITM is not a practical threat).
4. Both sides compute the shared secret via ECDH and derive a 32-byte session key using **HKDF-SHA256**.
5. All subsequent messages are encrypted with **AES-256-GCM** using the session key and a unique nonce per message.

### Named Pipe Security

- The Named Pipe is created with an ACL that restricts connections to the **same Windows user account** that owns the PassKey Desktop process.
- No other user on the system can connect to the pipe.
- The pipe is local-only — no data traverses any network interface.

---

## Clipboard Security

When a password is copied to the clipboard:

1. The password is placed on the clipboard using `DataPackage`.
2. `ClipboardContentOptions.IsAllowedInHistory` is set to `false`, which prevents the entry from appearing in Windows clipboard history (Win+V). Requires Windows 10 version 1809+.
3. `ClipboardContentOptions.IsRoamable` is set to `false`, preventing clipboard sync across devices.
4. A 30-second timer starts. When it expires, PassKey checks if the clipboard still contains the same content (by hash) and clears it if so.

---

## What PassKey Does NOT Protect Against

PassKey is designed to be secure against **remote attackers** and **offline vault theft**. It does **not** protect against:

| Threat | Why |
|--------|-----|
| **Malware with admin/kernel privileges** | Can read process memory, intercept keystrokes, or modify the app binary |
| **Keyloggers** | Can capture the master password as it is typed |
| **Physical access while unlocked** | The vault is decrypted in memory; anyone with screen access can read it |
| **Weak master passwords** | While the DEK itself is random, a weak master password allows brute-forcing the KEK via KDF |
| **Screen capture while unlocked** | The revealed password is visible in plaintext in the UI |

### Mitigations You Can Apply

- Use a **long, unique master password** (4+ random words or 16+ characters).
- **Lock the vault** (Ctrl+L) whenever you step away from your computer.
- Keep your operating system and antivirus **up to date**.
- Enable **full-disk encryption** (BitLocker) to protect the vault file at rest.
- Do not run PassKey on shared or untrusted computers.
