# Getting Started

This guide walks you through installing PassKey, creating your first vault, and understanding the basics.

---

## System Requirements

| Requirement | Minimum |
|-------------|---------|
| Operating system | Windows 10 version 1809 (build 17763) or Windows 11 |
| Architecture | x64 (64-bit) processor |
| RAM | 4 GB |
| Disk space | ~150 MB |
| .NET runtime | **Not required** — PassKey is fully self-contained |

---

## Download

Download the latest release from the [GitHub Releases page](https://github.com/pexatar/PassKey/releases).

Two options are available:

| Package | Description |
|---------|-------------|
| **PassKey-Setup-x64.exe** | Installer — installs to Program Files, creates Start Menu shortcuts, registers the browser extension Native Messaging host automatically |
| **PassKey-Portable-x64.zip** | Portable — extract anywhere and run. No installation required. You will need to register the Native Messaging host manually if you want to use the browser extension (see [Browser Extension](06-browser-extension.md)) |

---

## Installation (Installer)

1. Download `PassKey-Setup-x64.exe` from the Releases page.
2. Double-click the installer to launch it.

   > **Note:** Windows SmartScreen may display an "Unknown publisher" warning because PassKey v1.0 uses a self-signed certificate. Click **More info**, then **Run anyway** to proceed. This is expected and will be resolved with a commercial code-signing certificate in a future release.

3. Follow the setup wizard:
   - Accept the licence agreement (GPLv3).
   - Choose the installation folder (default: `C:\Program Files\PassKey`).
   - Optionally create a desktop shortcut.
   - Optionally start PassKey at Windows startup.
4. Click **Install** and wait for the process to complete.
5. Click **Finish** to launch PassKey.

The installer automatically registers the Native Messaging host for both Chrome and Firefox, so the browser extension will work immediately.

---

## Installation (Portable)

1. Download `PassKey-Portable-x64.zip` from the Releases page.
2. Extract the ZIP to a folder of your choice (e.g., `C:\Tools\PassKey`).
3. Run `PassKey.Desktop.exe` from the extracted folder.

> **Tip:** If you plan to use the browser extension with the portable version, run the included `register-native-host.ps1` script once to register the Native Messaging host. See the [Browser Extension guide](06-browser-extension.md) for details.

---

## First Launch — Creating Your Vault

When you run PassKey for the first time, you will see the **Setup** screen.

### Step 1: Choose Your Master Password

The master password is the single key that protects all your data. Choose it carefully:

- **Use a long passphrase** — 4 or more random words (e.g., "correct horse battery staple") or at least 16 characters.
- **Make it unique** — do not reuse a password from any other service.
- **Memorise it** — PassKey uses a zero-knowledge design. Your master password is never stored anywhere. If you forget it, there is no way to recover your data.

> **Warning:** There is no "Forgot password" feature. If you lose your master password, your vault cannot be recovered. Consider writing it down and storing it in a physically secure location (e.g., a locked drawer or a safe) until you have memorised it.

### Step 2: Confirm Your Master Password

Type the master password a second time to confirm. The two entries must match.

### Step 3: Vault Created

PassKey will:
1. Generate a random 256-bit Data Encryption Key (DEK).
2. Derive a Key Encryption Key (KEK) from your master password using Argon2id (64 MB memory, 3 iterations, 4 parallel threads).
3. Wrap (encrypt) the DEK with the KEK using AES-256-GCM.
4. Store the encrypted DEK and KDF parameters in the local vault database.

You are now taken to the **Welcome** screen, which provides a brief overview of the app's sections.

---

## Unlocking Your Vault

Every time you launch PassKey (or after the vault has been locked), you will see the **Login** screen:

1. Enter your master password.
2. Press **Enter** or click the unlock button.

If the password is correct, the vault unlocks and you see the Dashboard. If incorrect, an error message is displayed.

---

## Locking Your Vault

You can lock the vault at any time:

- Click **Lock Vault** at the top of the navigation panel, or
- Press **Ctrl+L**.

When locked, the DEK is securely erased from memory and the vault data becomes inaccessible until you enter the master password again.

---

## Vault Storage Location

Your encrypted vault is stored at:

```
%LOCALAPPDATA%\PassKey\vault.db
```

This is typically `C:\Users\<YourName>\AppData\Local\PassKey\vault.db`.

The database contains three tables:
- **VaultMetadata** — KDF parameters and the encrypted DEK (plaintext metadata, no secrets).
- **VaultData** — The AES-256-GCM encrypted vault blob containing all your entries.
- **ActivityLog** — A plaintext audit trail of actions (add, edit, delete) for the Dashboard.

---

## Next Steps

- [Passwords](02-passwords.md) — Learn how to add and manage password entries
- [Browser Extension](06-browser-extension.md) — Set up one-click autofill in your browser
- [Settings](07-settings.md) — Configure language, backups, and more
