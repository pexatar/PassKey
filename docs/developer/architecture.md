# Architecture

This document describes PassKey's solution structure, dependency graph, and key design patterns.

---

## Solution Structure

```
PassKey.sln
├── src/
│   ├── PassKey.Core/              # Domain models, crypto, services
│   ├── PassKey.Desktop/           # WinUI 3 app (MVVM, DI)
│   ├── PassKey.BrowserHost/       # Native Messaging bridge
│   └── PassKey.Tests/             # xUnit test suite
└── extensions/
    ├── chrome/                    # Chrome Manifest V3 extension
    └── firefox/                   # Firefox Manifest V3 extension
```

---

## Projects

### PassKey.Core

- **Type:** Class library (.NET 10, AOT-ready)
- **Purpose:** All domain logic with zero UI dependency
- **Contains:** Models, crypto service, vault service, importers, backup service, password analyser, URL matcher, card type detector
- **Dependencies:** `Konscious.Security.Cryptography.Argon2`, `System.Security.Cryptography`, `Microsoft.Data.Sqlite` (via interface)

### PassKey.Desktop

- **Type:** WinUI 3 application (Windows App SDK 1.8, unpackaged, self-contained x64)
- **Purpose:** User interface and platform integration
- **Contains:** Views (XAML + code-behind), ViewModels, Services, Controls, Converters
- **Dependencies:** PassKey.Core, CommunityToolkit.Mvvm, CommunityToolkit.WinUI, Microsoft.WindowsAppSDK

### PassKey.BrowserHost

- **Type:** Console application (.NET 10, single-file publish, AOT-ready)
- **Purpose:** Bridge between browser extensions and PassKey Desktop
- **Protocol:** Reads/writes Native Messaging format (4-byte length prefix + JSON) on stdin/stdout; connects to PassKey Desktop via Named Pipe
- **Dependencies:** PassKey.Core (models only)

### PassKey.Tests

- **Type:** xUnit test project
- **Purpose:** 167+ deterministic tests covering crypto, vault, importers, URL matching, card detection
- **Dependencies:** PassKey.Core (with `InternalsVisibleTo`), PassKey.BrowserHost (with `InternalsVisibleTo`), Moq

---

## Dependency Graph

```
PassKey.Desktop ────► PassKey.Core
                            ▲
PassKey.BrowserHost ────────┘
                            ▲
PassKey.Tests ──────────────┘
```

`PassKey.Core` has no dependency on any UI framework. `PassKey.Desktop` and `PassKey.BrowserHost` both depend on Core. `PassKey.Tests` references Core and BrowserHost.

---

## Key Design Patterns

### MVVM ViewModel-First

PassKey uses a ViewModel-first navigation pattern:

1. `ShellViewModel` manages the current page via a `CurrentViewModel` property.
2. Navigation sets `CurrentViewModel` to a new ViewModel instance.
3. `MainWindow` uses a `ContentControl` bound to `CurrentViewModel`, with `DataTemplate` selectors mapping each ViewModel type to its corresponding View.
4. Views receive their ViewModel via `SetViewModel()` in code-behind and set `DataContext`.

Benefits:
- ViewModels are fully unit-testable (no XAML dependency).
- Navigation logic lives in ViewModels, not code-behind.

### Dependency Injection (Constructor Only)

All services are registered in the DI container at startup (`App.xaml.cs`). Dependencies are injected via constructor parameters.

**Rules:**
- No Service Locator pattern (never resolve from `App.Current` or a static container).
- No `[Inject]` attributes — constructor parameters only.
- Scoped/transient where appropriate, singletons for long-lived services.

### Dialog Queue (Serial Pump)

WinUI 3 does not allow multiple `ContentDialog` instances to be open simultaneously. PassKey uses a `DialogQueueService` with a `Queue<Func<Task>>` and a serial pump:

1. Code enqueues a dialog request (a `Func<Task>` that shows and awaits a `ContentDialog`).
2. The pump dequeues and executes one at a time.
3. The next dialog is shown only after the previous one is dismissed.

> **Why not SemaphoreSlim?** Using `SemaphoreSlim.WaitAsync()` on the UI thread causes deadlocks because the dialog's dismissal callback cannot run while the UI thread is blocked.

### INavigationStack

ViewModels can push/pop sub-pages via `INavigationStack`:
- `Push(viewModel)` — Navigate to a child page.
- `Pop()` — Return to the previous page.
- Used for detail views (e.g., password list → password detail → back to list).

---

## Cryptographic Flow

```
User enters master password
        │
        ▼
 KDF (Argon2id or PBKDF2) + salt from VaultMetadata
        │
        ▼
 KEK (32 bytes) ── AES-GCM Decrypt ──► DEK (32 bytes)
                                            │
                              Stored in PinnedSecureBuffer
                                            │
                              AES-GCM Decrypt vault blob
                                            │
                                            ▼
                                    Vault (JSON → objects)
```

On save:
```
Vault objects → JSON → AES-GCM Encrypt with DEK → blob → VaultData table
```

On master password change:
```
New password → KDF → new KEK → AES-GCM Encrypt DEK → update VaultMetadata
(vault blob is NOT re-encrypted)
```

---

## Browser Extension Architecture

```
┌─────────────────┐    Native Messaging     ┌──────────────────┐    Named Pipe    ┌─────────────────┐
│  Browser        │  (stdio: 4-byte len +   │  PassKey         │   (local IPC)    │  PassKey        │
│  Extension      │   JSON payload)         │  BrowserHost     │                  │  Desktop        │
│                 │◄───────────────────────►│                  │◄────────────────►│                 │
│  - popup.js     │                         │  - stdin/stdout  │                  │  - BrowserIpc   │
│  - content.js   │                         │  - pipe client   │                  │    Service      │
│  - background.js│                         │                  │                  │                 │
└─────────────────┘                         └──────────────────┘                  └─────────────────┘
```

### Session Encryption

1. Extension generates ephemeral ECDH P-256 key pair.
2. BrowserHost generates ephemeral ECDH P-256 key pair.
3. Public keys exchanged → shared secret → HKDF-SHA256 → 32-byte session key.
4. All subsequent messages encrypted with AES-256-GCM (unique nonce per message).

### Message Types

| Message | Direction | Description |
|---------|-----------|-------------|
| `get-credentials` | Extension → Desktop | Get credentials matching a URL |
| `get-all-credentials` | Extension → Desktop | Get all vault credentials |
| `unlock-vault` | Extension → Desktop | Unlock vault with master password |
| `show-window` | Extension → Desktop | Bring Desktop app to foreground |
