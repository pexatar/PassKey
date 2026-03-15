# Build and Test

This document covers everything you need to build PassKey from source and run the test suite.

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| Windows | 10 version 1809+ or Windows 11 | x64 only |
| Visual Studio 2022 | 17.x Preview | "Windows application development" workload |
| .NET SDK | 10.0 | `dotnet --version` should report `10.x.x` |
| Windows App SDK | 1.8 | Installed with the VS workload |
| Git | Any recent version | |

**VS Code** is supported with the C# Dev Kit extension, but Visual Studio is recommended for WinUI 3 XAML designer support.

---

## Clone and Restore

```bash
git clone https://github.com/pexatar/PassKey.git
cd PassKey
dotnet restore PassKey.sln
```

---

## Build Commands

### Full Solution

```bash
dotnet build PassKey.sln -p:Platform=x64
```

> **Important:** Always pass `-p:Platform=x64` when building `PassKey.Desktop`. The Windows App SDK requires an explicit target architecture for self-contained unpackaged builds.

### Individual Projects

| Project | Command |
|---------|---------|
| Core | `dotnet build src/PassKey.Core/PassKey.Core.csproj` |
| Desktop | `dotnet build src/PassKey.Desktop/PassKey.Desktop.csproj -p:Platform=x64` |
| BrowserHost | `dotnet build src/PassKey.BrowserHost/PassKey.BrowserHost.csproj -r win-x64` |
| Tests | `dotnet build src/PassKey.Tests/PassKey.Tests.csproj` |

### Release Build

```bash
dotnet build PassKey.sln -p:Platform=x64 -c Release
```

---

## Running Tests

```bash
dotnet test src/PassKey.Tests/PassKey.Tests.csproj --verbosity normal
```

The test suite includes 167+ deterministic tests covering:
- Cryptographic operations (AES-GCM encrypt/decrypt, PBKDF2, Argon2id)
- Vault lifecycle (init, unlock, lock, master password change)
- Password strength analysis
- URL matching for browser extension
- Credit card BIN detection and Luhn validation
- Import from CSV, Bitwarden, 1Password
- Merge conflict resolution
- Backup and restore

All tests run without external dependencies — no network, no file system writes beyond temp directories.

---

## Publishing

### Desktop (self-contained x64)

```bash
dotnet publish src/PassKey.Desktop/PassKey.Desktop.csproj ^
  -c Release -p:Platform=x64 -r win-x64 ^
  --self-contained true ^
  -o publish/
```

### BrowserHost (single file)

```bash
dotnet publish src/PassKey.BrowserHost/PassKey.BrowserHost.csproj ^
  -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -o publish/
```

---

## Troubleshooting

### XAML Compiler Errors (MSB3073)

If `dotnet build` fails with a cryptic `MSB3073` exit code during the XAML compiler pass, the actual error is hidden. Use the full-framework MSBuild to get the real error message:

```bash
"C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\MSBuild.exe" ^
  src\PassKey.Desktop\PassKey.Desktop.csproj -restore -t:Build -p:Platform=x64
```

### Common Issues

| Problem | Solution |
|---------|----------|
| Build fails without `-p:Platform=x64` | Always specify the platform for Desktop builds |
| `MVVMTK0045` warning | Use `partial property` syntax for `[ObservableProperty]`, not field-based |
| `SystemAccentColorBrush` runtime crash | Use `AccentFillColorDefaultBrush` instead (WinUI 3, not UWP) |
| `FocusVisualKind` on `Application` | Set it on `FrameworkElement` (e.g., root Grid), not `Application` |
| `PrimaryLanguageOverride` fails at runtime | Must be set in `App()` constructor before `InitializeComponent()` |
| `PasswordBox` crashes AOT | Use `SecureInputBox` custom control instead |
| `DatePicker` crashes AOT | Use `ComboBox` (MM/YY) or `CalendarDatePicker` instead |
| `SemaphoreSlim` deadlocks UI | Use `Queue<Func<Task>>` serial pump pattern instead |
