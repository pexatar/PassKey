# PassKey Privacy Policy

**Last updated:** 2026-03-15

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

PassKey makes **zero** network connections. The only local communication that occurs is:

- **Browser extension to PassKey Desktop app** — via the Native Messaging protocol over a local Named Pipe. This communication never leaves your computer and is encrypted with ephemeral ECDH P-256 + AES-256-GCM session keys.

There is no analytics, no telemetry, no crash reporting, and no update checking.

---

## Browser Extension

The PassKey browser extension (Chrome and Firefox) communicates exclusively with the locally installed PassKey Desktop application via the Native Messaging API. No data is sent to any external server.

The extension requests only the minimum permissions required:

| Permission | Purpose |
|------------|---------|
| `nativeMessaging` | Communicate with PassKey Desktop via Native Messaging |
| `activeTab` | Read the current tab's URL to match credentials |

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
