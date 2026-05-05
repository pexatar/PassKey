# Changelog

All notable changes to PassKey will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.12] - 2026-05-05

### Changed
- **Installer follows system theme**: The setup wizard now automatically switches between
  light and dark appearance based on the Windows system theme (`WizardStyle=modern dynamic`
  in Inno Setup 6.6+).

### Fixed
- **".NET must be installed" startup failure**: PassKey v1.0.11 showed "You must install
  or update .NET to run this application" because the bundled `.NET 10.0.7` installer
  was failing silently during setup (likely due to a pre-existing .NET 8/9 installation
  triggering an incorrect skip condition). The fix switches `PassKey.Desktop` back to
  **self-contained** publishing: the .NET runtime is now bundled directly in the
  application folder, so no separate .NET installer is required. The `.NET 10.0.7`
  redistributable has been removed from the installer entirely.
  Windows App Runtime 1.8.260416003 (introduced in v1.0.11) is still bundled and
  continues to address the Windows 11 25H2 STATUS_INVALID_IMAGE_HASH crash.

## [1.0.11] - 2026-05-04

### Fixed
- **Windows 11 25H2 crash (STATUS_INVALID_IMAGE_HASH — Windows App Runtime 1.8.260101001)**:
  PassKey v1.0.10 crashed on Windows 11 25H2 (Build 26200+) with exception code `0xc000027b`
  in `Microsoft.UI.Xaml.dll` even with the system-installed Windows App Runtime 1.8.
  The root cause is a compatibility bug in Windows App Runtime **1.8.260101001** with
  Windows 11 25H2. The fix upgrades the bundled Windows App Runtime installer and the NuGet SDK
  from `1.8.260101001` to **1.8.260416003** (released 21 April 2026).

## [1.0.10] - 2026-05-04

### Fixed
- **Windows 11 25H2 crash (STATUS_INVALID_IMAGE_HASH — self-contained .NET)**: PassKey v1.0.9
  crashed on Windows 11 25H2 (Build 26200+) because the self-contained .NET 10 apphost
  triggers a WinRT activation incompatibility with the Windows App Runtime framework package:
  `Microsoft.UI.Xaml.dll` throws `STATUS_INVALID_IMAGE_HASH` internally. The same crash
  was reproducible on clean VirtualBox VMs with no third-party security software, confirming
  the root cause is a Windows 11 25H2-specific behaviour.

  The fix switches `PassKey.Desktop` to **framework-dependent** publishing
  (`--self-contained false`). The installer now bundles and silently installs the
  **.NET 10.0.7 Runtime** (29 MB, from `builds.dotnet.microsoft.com`) before launching
  PassKey, so end users still do not need to install .NET manually.

  The `BrowserHost` component remains self-contained (single-file executable, unaffected
  by the WinRT issue).

## [1.0.9] - 2026-05-04

### Fixed
- **HVCI crash (STATUS_INVALID_IMAGE_HASH)**: PassKey v1.0.7 and v1.0.8 crashed silently on
  every PC with Hypervisor-Protected Code Integrity (HVCI) enabled — the majority of modern
  Windows 11 systems with TPM. Windows Code Integrity rejected the NuGet-bundled
  `Microsoft.UI.Xaml.dll` with exception code `0xc000027b` because NuGet copies are not
  registered in the Windows system catalog. No window ever appeared and no crash log was
  written (the exception is a native SEH fault that bypasses .NET `catch`).

  The fix removes `WindowsAppSDKSelfContained` from the build (the publish folder no longer
  contains `Microsoft.UI.Xaml.dll`), adds an explicit `Bootstrap.Initialize(1.8)` call in
  `Program.cs`, and bundles the official **Windows App Runtime 1.8** installer
  (`WindowsAppRuntimeInstall-x64.exe`) inside the PassKey installer. The runtime is installed
  silently during setup; its DLLs are fully trusted by Windows Code Integrity.

  A user-visible error dialog (Win32 `MessageBoxW`) is shown on the rare occasion that
  `Bootstrap.Initialize` fails (e.g. corrupt runtime installation), replacing the previous
  silent disappearance.

## [1.0.8] - 2026-05-04

### Fixed
- Single-instance protection: launching PassKey when it is already running in the system tray
  now brings the existing window to the foreground instead of silently starting (and immediately
  hiding) a second invisible process. This was the primary cause of the "app installs but
  doesn't start" symptom reported on v1.0.7.
- Startup crash log: if the application fails to initialise before the main window is created,
  a diagnostic file is written to `%LOCALAPPDATA%\PassKey\startup-crash.log`. This makes
  silent startup failures diagnosable without attaching a debugger.

## [1.0.7] - 2026-05-02

### Fixed
- PassKey.Desktop is now published as a truly self-contained executable: the .NET 10 runtime is
  bundled inside the installer and the portable ZIP. Users no longer need to install .NET separately.
  The README claim "No .NET runtime required" is now accurate.

## [1.0.6] - 2026-05-01

### Changed

#### UI / UX
- Empty state screens (Passwords, Credit Cards, Identities, Secure Notes) no longer show a duplicate add button at the centre of the page — the action is already available in the top-right toolbar
- Empty state subtitle text no longer wraps to a second line
- Passwords view: column headers (Title / Username / URL / Modified) are now hidden when the list is empty, keeping the empty state vertically centred
- All four empty state icons are now vertically aligned at the same height across sections

## [1.0.5] - 2026-05-01

### Added

#### Auto-updater
- Silent update check at startup: compares the running version against the latest GitHub Release every 24 hours (throttled, no check on every launch)
- Non-invasive InfoBar notification in the main shell when a newer version is detected — shown above the content area, does not interrupt workflow
- **Download & install** button inside the InfoBar: downloads the installer in the background with a live progress bar, then launches it and exits the app cleanly
- **What's new** button: opens the GitHub Release page in the default browser
- Skip version: dismissing the InfoBar records the skipped version in `settings.json` — the notification will not reappear for that version
- Manual update check in **Settings → Updates**: a "Check now" button with spinner and a localised last-check timestamp (e.g. "Checked 3 min ago")
- Toggle in Settings to disable automatic update checks at startup
- All update-related strings fully localised in 6 languages (it-IT, en-GB, fr-FR, de-DE, es-ES, pt-PT)

### Technical
- `IUpdateService` / `UpdateService` singleton: GitHub Releases API via `HttpClient` (10 s timeout, `User-Agent: PassKey-Desktop-Updater/1.0`), AOT-safe `UpdateJsonContext` (System.Text.Json source generation)
- Streaming installer download with `HttpCompletionOption.ResponseHeadersRead` and 80 KB buffer; temporary file cleaned up on failure
- `ISettingsService` extended with `AutoUpdateCheckEnabled`, `LastUpdateCheckUtc` (`DateTime?`, ISO 8601), `SkippedUpdateVersion`

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

[Unreleased]: https://github.com/pexatar/PassKey/compare/v1.0.12...HEAD
[1.0.12]: https://github.com/pexatar/PassKey/compare/v1.0.11...v1.0.12
[1.0.11]: https://github.com/pexatar/PassKey/compare/v1.0.10...v1.0.11
[1.0.10]: https://github.com/pexatar/PassKey/compare/v1.0.9...v1.0.10
[1.0.9]: https://github.com/pexatar/PassKey/compare/v1.0.8...v1.0.9
[1.0.8]: https://github.com/pexatar/PassKey/compare/v1.0.7...v1.0.8
[1.0.7]: https://github.com/pexatar/PassKey/compare/v1.0.6...v1.0.7
[1.0.6]: https://github.com/pexatar/PassKey/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/pexatar/PassKey/compare/v1.0.0...v1.0.5
[1.0.0]: https://github.com/pexatar/PassKey/releases/tag/v1.0.0
