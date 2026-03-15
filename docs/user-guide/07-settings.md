# Settings

The Settings page lets you configure PassKey's behaviour, manage backups, import data from other password managers, and change your master password.

---

## Language

PassKey supports 6 languages:

| Language | Code |
|----------|------|
| Italian | it-IT |
| English | en-GB |
| French | fr-FR |
| German | de-DE |
| Spanish | es-ES |
| Portuguese | pt-PT |

To change the language:

1. Open **Settings**.
2. Select a language from the dropdown.
3. An info bar appears: "Language changed. Restart required."
4. Click **Restart now**.

> **Why a restart?** WinUI 3 unpackaged apps load language resources (MRT Core) at process startup. Changing the language at runtime is not supported by the framework, so a process restart is required.

---

## Backup

### Creating a Backup

1. Open **Settings**.
2. Click **Create backup**.
3. Choose a save location and file name (`.pkbak` extension).
4. Enter a **backup password** — this can be different from your master password.
5. Click **Save**.

The backup file is encrypted with AES-256-GCM using a key derived from the backup password via Argon2id. The file format is:

```
[Magic "PKBK" (4 bytes)] [Version (1 byte)] [Salt (32 bytes)] [Nonce (12 bytes)] [Encrypted payload]
```

> **Tip:** Store backups on a separate drive, USB stick, or cloud storage. Since the backup is fully encrypted, it is safe to store on cloud services.

### Restoring from Backup

1. Open **Settings**.
2. Click **Restore from backup**.
3. Select a `.pkbak` file.
4. Enter the backup password.
5. PassKey decrypts and imports the vault data.

> **Warning:** Restoring a backup replaces your current vault. Make sure to create a backup of your current vault first if you have data you want to keep.

---

## Import

PassKey can import passwords from other password managers:

### CSV (Generic)

1. Export your passwords from the source application as a CSV file.
2. In PassKey Settings, click **Import from CSV**.
3. Select the CSV file.
4. PassKey auto-detects columns for title, URL, username, password, and notes.
5. Review the import preview and click **Import**.

### Bitwarden (JSON)

1. In Bitwarden, go to **Settings > Export Vault** and choose **JSON** format.
2. In PassKey Settings, click **Import from Bitwarden**.
3. Select the exported `.json` file.
4. Review and click **Import**.

### 1Password (.1pux)

1. In 1Password, go to **File > Export** and choose the `.1pux` format.
2. In PassKey Settings, click **Import from 1Password**.
3. Select the `.1pux` file.
4. Review and click **Import**.

> **Note:** Imported entries are merged with your existing vault. Duplicates (matched by title + URL + username) are skipped.

---

## Change Master Password

1. Open **Settings**.
2. Click **Change master password**.
3. Enter your **current** master password.
4. Enter and confirm your **new** master password.
5. Click **Change**.

What happens behind the scenes:
- A new KEK is derived from the new password using Argon2id with a fresh random salt.
- The existing DEK is re-wrapped with the new KEK.
- The vault blob itself is **not** re-encrypted — only the DEK wrapper changes.
- This makes the operation fast regardless of vault size.

> **Warning:** After changing the master password, update the password in any location where you may have stored it (e.g., a secure written backup).

---

## Help

The **Help** section (accessible from the navigation panel) provides:

- **Keyboard shortcuts** — A complete reference table
- **Usage guide** — Expandable sections covering each feature page-by-page
- **FAQ** — Answers to common questions
- **About** — App version and credits
