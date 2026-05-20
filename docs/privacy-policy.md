# PassKey Privacy Policy

**Last updated:** 2026-05-20

---

## Summary

PassKey does not collect, store, or transmit any personal data to external servers. All data remains exclusively on your device.

---

## Data Storage

- All vault data is stored locally in an encrypted SQLite database.
- Default location: `%LOCALAPPDATA%\PassKey\vault.db`
- Encryption: AES-256-GCM with keys derived from your master password via Argon2id or PBKDF2-SHA256.
- PassKey never stores your master password anywhere — not on disk, not in memory longer than necessary.

---

## Network Activity

By default, PassKey makes **zero** network connections. The only local communication that occurs is:

- **Browser extension to PassKey Desktop app** — via the Native Messaging protocol over a local Named Pipe. This communication never leaves your computer and is encrypted with ephemeral ECDH P-256 + AES-256-GCM session keys.

There is no analytics, no telemetry, no crash reporting, and no update checking.

The **only** circumstance in which PassKey contacts an external server is the optional, opt-in
compromised-password check described in the next section. It is disabled by default and never
runs unless you explicitly enable it.

---

## Compromised Password Check (optional, opt-in)

PassKey can tell you whether any of your stored passwords have appeared in a known data breach.
This feature is **disabled by default** and is only ever activated when you explicitly turn on
the **"Check for compromised passwords"** setting in *Settings → Security*.

### How it works — k-anonymity

When enabled, PassKey queries the [Have I Been Pwned](https://haveibeenpwned.com/) Pwned Passwords
service using the **k-anonymity model**, which is designed so that **your password is never sent**:

1. PassKey computes the SHA-1 hash of a password locally, on your device.
2. Only the **first 5 hexadecimal characters** of that hash are sent to `api.pwnedpasswords.com`.
3. The service returns the list of all breached-hash suffixes that share those 5 characters.
4. PassKey compares that list against the remaining hash characters **locally** — the server
   never learns which password, or even which full hash, you were checking.

This means the breach service cannot identify your passwords, your account, or you. No API key
is required and no personal identifier is transmitted.

### What is and isn't sent

| Sent to the breach service | Never sent |
|----------------------------|------------|
| The first 5 characters of a password's SHA-1 hash | The password itself |
| | The full SHA-1 hash |
| | Usernames, URLs, or entry titles |
| | Any device or account identifier |

### Your control

- The feature is **off until you turn it on**, and turning it off immediately stops all such requests.
- Requests are made over HTTPS with a short timeout; if the service is unreachable, the check
  fails gracefully and no data is retried or queued.
- No results are shared with anyone — breach findings are displayed only inside the app.

---

## Browser Extension

The PassKey browser extension (Chrome and Firefox) communicates exclusively with the locally installed PassKey Desktop application via the Native Messaging API. No data is sent to any external server.

The extension requests only the minimum permissions required:

| Permission | Purpose |
|------------|---------|
| `nativeMessaging` | Communicate with PassKey Desktop via Native Messaging |
| `activeTab` | Read the current tab's URL to match credentials |
| `tabs` | Inject autofill into the active tab and keep the popup's tab reference current |

---

## Data Sharing

PassKey does not share any data with third parties. Ever.

---

## Backups

Encrypted backups (`.pkbak` files) are stored locally at a location you choose. Backups are protected with AES-256-GCM encryption using an Argon2id-derived key from a password you provide at backup time.

---

## Open Source

PassKey is open-source software licensed under GPLv3. You can audit the entire codebase at [github.com/pexatar/PassKey](https://github.com/pexatar/PassKey).

---

## Contact

- **Security issues:** Report privately via [GitHub Security Advisories](https://github.com/pexatar/PassKey/security/advisories/new)
- **General issues:** [GitHub Issues](https://github.com/pexatar/PassKey/issues)
