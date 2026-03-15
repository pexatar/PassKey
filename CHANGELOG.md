# Changelog

All notable changes to PassKey will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-03-15

### Added

#### Core Security
- Password vault with AES-256-GCM encryption (256-bit key, 96-bit nonce, 128-bit authentication tag)
- Dual KDF support: Argon2id (64 MB, 3 iterations, 4 threads — OWASP 2023) for new vault creation and master password changes; PBKDF2-SHA256 (600,000 iterations) for backward-compatible vault unlock
- Two-tier key architecture: KEK derived from master password wraps the DEK; changing the master password re-wraps the DEK without re-encrypting the vault blob
- `PinnedSecureBuffer` — DEK held in GC-pinned managed memory, zeroed with `CryptographicOperations.ZeroMemory` on dispose
- Master password handled as `char[]`/`ReadOnlySpan<char>`, cleared immediately after KDF computation
- Encrypted backup and restore (`.pkbak` format: 4-byte magic `PKBK` + version + Argon2id salt + AES-GCM nonce + encrypted payload)
- Auto-clear clipboard after 30 seconds with Windows clipboard history suppression (`ClipboardContentOptions.IsAllowedInHistory = false`)

#### Vault Features
- Password entries: title, URL, username, password, notes, and custom icon (letter avatar / Segoe MDL2 Assets glyph / uploaded PNG/JPG/ICO image ≤ 64 KB)
- Credit card management: BIN-based network detection (Visa, Mastercard, Amex, Discover, JCB, Maestro, Diners Club), real-time Luhn validation, 10 colour swatches
- Identity profiles: personal data (name, birth date, email, phone), postal address, and four document types (national ID, health card, driver's licence, passport)
- Secure notes: 10 categories with Fluent icons, pastel colour palette
- Password strength analyser: 0–4 score, estimated crack-time, actionable suggestions
- Password verifier: checks against known breach patterns
- Password generator: configurable length (8–128 chars), charset options (uppercase/lowercase/digits/symbols), real-time entropy display
- Dashboard: vault statistics (entry counts by type, recent activity log)

#### Browser Extension
- Chrome extension (Manifest V3): service worker, content script, popup
- Firefox extension (Manifest V3): background scripts, content script, popup
- `PassKey.BrowserHost`: self-contained single-file Native Messaging bridge
- Ephemeral ECDH P-256 + HKDF-SHA256 + AES-256-GCM session encryption for all IPC messages
- Named Pipe with ACL: only the PassKey Desktop process owner can accept connections
- In-extension vault unlock: master password entered directly in the browser popup (no tab switch required)
- One-click autofill for username and password fields
- Framework-aware field detection: standard HTML forms, React (synthetic events), Angular, Vue virtual DOM
- Multi-step login form support (email-only step 1 → fills username; password fill happens on step 2)
- Dual-view popup: "This site" tab shows matching credentials; "All passwords" tab shows full vault with search
- Badge on the extension icon: number of credentials matching the current domain

#### Platform Integration
- `passkey://` URL scheme handler (registered in HKCU at first launch, no admin required)
- `passkey://unlock` deep link: brings the app to the foreground and prompts for unlock
- Native Messaging Host auto-registration in HKCU for both Chrome and Firefox at first launch

#### Import
- Import from generic CSV (column auto-detection for title/URL/username/password/notes)
- Import from Bitwarden JSON export (v2 format)
- Import from 1Password `.1pux` archive

#### Localization
- Full 6-language support: Italian (it-IT), English (en-GB), French (fr-FR), German (de-DE), Spanish (es-ES), Portuguese (pt-PT)
- Language switching via process restart (required for Windows MRT Core resource loading in unpackaged apps)
- Language preference persisted in `settings.json` and applied in `App()` constructor before `InitializeComponent()`
- All 6 languages applied to both the desktop app and browser extension popup

#### Accessibility
- WCAG AA compliant throughout the application
- All interactive elements have descriptive `AutomationProperties.Name` values
- Custom `AutomationPeer` implementations for `SecureInputBox` and `CreditCardControl`
- Live regions (`AutomationProperties.LiveSetting`) for dynamic feedback messages
- Full keyboard navigation: `Ctrl+N` (new), `Ctrl+F` (search), `Ctrl+L` (lock vault), `F2` (edit selected), `Del` (delete selected with confirmation), `Esc` (close detail panel), `Ctrl+1–7` (navigate to vault sections)
- Focus rings on all interactive elements; `FocusVisualKind.Reveal` set on the root element

#### UI & UX
- Fluent Design (WinUI 3, Windows App SDK 1.8), light theme
- Unpackaged app (`WindowsPackageType=None`), self-contained x64 — no MSIX required
- `SecureInputBox` custom control (replaces the AOT-incompatible native `PasswordBox`)
- Press-and-hold eye icon to reveal password (pointer-based, not toggle)
- `CreditCardControl` skeuomorphic card rendering with colour-coded network icon
- `EmptyStateControl` placeholder shown in all empty list views with a primary action button
- `NavigationView` shell with vault section icons, "Lock Vault" top-of-list entry, separator, "Help" and "Settings" items at the bottom

### Authors

- **Giuseppe Imperato** — concept, design, product decisions
- **[Claude](https://www.anthropic.com/claude) by Anthropic** — architecture, implementation, documentation

[Unreleased]: https://github.com/pexatar/PassKey/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/pexatar/PassKey/releases/tag/v1.0.0
