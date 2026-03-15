# Contributing to PassKey

Thank you for your interest in contributing to PassKey! This document explains how to set up your development environment, build the project, run tests, and submit contributions.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Development Environment](#development-environment)
- [Building the Project](#building-the-project)
- [Running Tests](#running-tests)
- [Architecture Overview](#architecture-overview)
- [Pull Request Process](#pull-request-process)
- [Code Style Guidelines](#code-style-guidelines)
- [Commit Message Format](#commit-message-format)
- [Testing Requirements](#testing-requirements)

---

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behaviour to the project maintainer via GitHub.

---

## Development Environment

### Requirements

| Tool | Version | Notes |
|------|---------|-------|
| Windows | 10 version 1809+ or Windows 11 | x64 only |
| Visual Studio 2022 | 17.x Preview | "Windows application development" workload required |
| .NET SDK | 10.0 | `dotnet --version` should show `10.x.x` |
| Windows App SDK | 1.8 | Installed automatically with the VS workload |
| Git | Any recent version | |

**VS Code** is also supported with the C# Dev Kit extension, but Visual Studio is recommended for WinUI 3 XAML designer support.

### Clone and Set Up

```bash
git clone https://github.com/pexatar/PassKey.git
cd PassKey
dotnet restore PassKey.sln
```

---

## Building the Project

The solution contains four projects:

| Project | Type | Build Command |
|---------|------|---------------|
| `PassKey.Core` | Class library (AOT-ready) | `dotnet build src/PassKey.Core/PassKey.Core.csproj` |
| `PassKey.Desktop` | WinUI 3 app (requires x64) | `dotnet build src/PassKey.Desktop/PassKey.Desktop.csproj -p:Platform=x64` |
| `PassKey.BrowserHost` | Native Messaging console exe | `dotnet build src/PassKey.BrowserHost/PassKey.BrowserHost.csproj -r win-x64` |
| `PassKey.Tests` | xUnit test project | `dotnet test src/PassKey.Tests/PassKey.Tests.csproj` |

> **Important:** Always pass `-p:Platform=x64` when building `PassKey.Desktop`. The Windows App SDK requires an explicit target architecture for self-contained unpackaged builds.

### Full Solution Build

```bash
dotnet build PassKey.sln -p:Platform=x64
```

### Diagnostic Build (when XAML errors are unclear)

If `dotnet build` fails with a cryptic MSB3073 exit code during the XAML compiler pass, use the full-framework MSBuild to get the actual error message:

```bash
"C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\MSBuild.exe" `
  src\PassKey.Desktop\PassKey.Desktop.csproj -restore -t:Build -p:Platform=x64
```

---

## Running Tests

```bash
dotnet test src/PassKey.Tests/PassKey.Tests.csproj --verbosity normal
```

The test suite has 167+ deterministic tests covering cryptographic services, vault operations, URL matching, card detection, importers, and more. All tests run without external dependencies (no network, no file system writes beyond temp).

---

## Architecture Overview

See [docs/developer/architecture.md](docs/developer/architecture.md) for a full description.

**Short summary:**
- `PassKey.Core` — Domain models, crypto services, importers, backup. No UI dependency. AOT-compatible.
- `PassKey.Desktop` — WinUI 3 app using MVVM ViewModel-first pattern. DI via constructor injection only.
- `PassKey.BrowserHost` — Tiny console exe that bridges browser extensions to the desktop app via Named Pipe.
- `PassKey.Tests` — xUnit tests referencing Core and BrowserHost internals (`InternalsVisibleTo`).

---

## Pull Request Process

1. Fork the repository and create a branch from `main`.
2. Make your changes following the [Code Style Guidelines](#code-style-guidelines).
3. Add tests for any new functionality (see [Testing Requirements](#testing-requirements)).
4. Ensure all existing tests pass: `dotnet test`.
5. Ensure the solution builds without warnings: `dotnet build PassKey.sln -p:Platform=x64`.
6. Submit a pull request against `main` with a clear description of the change.
7. A maintainer will review and may request changes before merging.

---

## Code Style Guidelines

- **Language:** C# with `LangVersion=preview`. Latest C# features are permitted.
- **Nullable:** `enable` — all nullable reference type annotations must be correct. No `!` suppression without justification.
- **Formatting:** Follow the existing code style: 4-space indentation, no tabs, braces on new lines for type/method declarations.
- **XML Documentation:** Every `public` type and member must have `/// <summary>` documentation in **English**.
- **No Service Locator:** Dependency injection via constructor only. Never resolve from `App`, `Application.Current`, or static containers.
- **Async/Await:** Use `async`/`await` correctly throughout. Never call `.Result` or `.Wait()` on the UI thread.
- **UI Thread:** For updates from background tasks, use `DispatcherQueue.TryEnqueue`.
- **ThemeResource only:** In XAML, never use hardcoded colours or `#RRGGBB` values. Always reference theme brushes via `{ThemeResource}`.
- **CommunityToolkit.Mvvm:** Use `partial property` syntax for `[ObservableProperty]` (not field-based — MVVMTK0045 is an AOT incompatibility).

---

## Commit Message Format

This project uses [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short summary in imperative mood>

[optional body — explain WHY, not WHAT]
[optional footer — breaking change note or issue reference]
```

**Types:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `ci`, `perf`

**Examples:**
```
feat(vault): add Argon2id KDF for new vault creation
fix(extension): remove unused storage permission from manifest
docs(api): add XML documentation to CryptoService public methods
test(vault): add unlock tests for Argon2id-derived keys
chore(ci): add GitHub Actions workflow for automated builds
```

---

## Testing Requirements

Every pull request that adds or changes behaviour must include tests:

| Change area | Where to add tests |
|-------------|-------------------|
| Crypto primitives | `CryptoServiceTests.cs` |
| Vault operations | `VaultServiceTests.cs` |
| New import format | New `*ImporterTests.cs` file |
| URL matching | `UrlMatcherTests.cs` |
| Card detection / Luhn | `CardTypeDetectorTests.cs` |

**Rules for tests:**
- Use **xUnit** (already referenced).
- Use **Moq** for interface mocks (already referenced).
- Tests must be **deterministic** — no `Thread.Sleep`, no random without a fixed seed, no network calls, no external file dependencies.
- Each test method name should clearly describe what is being tested and what the expected outcome is.
