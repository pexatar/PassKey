# Browser Extension

PassKey includes browser extensions for Chrome and Firefox that let you autofill login forms with one click, without leaving the browser.

---

## How It Works

The browser extension does **not** store any vault data itself. Instead, it communicates with the PassKey Desktop app running on your computer:

```
Browser Extension  ←→  PassKey.BrowserHost  ←→  PassKey Desktop
    (popup/content)     (Native Messaging)       (vault + crypto)
```

1. The extension sends requests to **PassKey.BrowserHost**, a small bridge process that communicates via the browser's Native Messaging protocol.
2. The BrowserHost forwards requests to the PassKey Desktop app over a local Named Pipe.
3. All communication is encrypted with ephemeral ECDH P-256 + AES-256-GCM session keys — even though it never leaves your machine.

> **Important:** PassKey Desktop must be installed and running for the extension to work.

---

## Installing on Chrome

### Method A: Chrome Web Store *(coming soon)*

The extension will be available on the [Chrome Web Store](https://chrome.google.com/webstore) once approved.

### Method B: Manual Installation (Developer Mode)

1. Open Chrome and navigate to `chrome://extensions/`.
2. Enable **Developer mode** using the toggle in the top-right corner.
3. Click **Load unpacked**.
4. Navigate to your PassKey installation folder and select the `extensions/chrome` subfolder.
5. The PassKey extension icon should now appear in your toolbar.

> **Tip:** Pin the extension to your toolbar for quick access. Click the puzzle piece icon in Chrome's toolbar, then click the pin next to PassKey.

---

## Installing on Firefox

### Method A: Firefox Add-ons *(coming soon)*

The extension will be available on [Firefox Add-ons (AMO)](https://addons.mozilla.org) once approved.

### Method B: Temporary Installation (for testing)

1. Open Firefox and navigate to `about:debugging#/runtime/this-firefox`.
2. Click **Load Temporary Add-on...**.
3. Navigate to the `extensions/firefox` folder and select `manifest.json`.
4. The extension appears in the toolbar.

> **Note:** Temporary add-ons in Firefox are removed when the browser is closed. For permanent installation, use the AMO listing or sign the extension yourself.

---

## Native Messaging Host

The Native Messaging host is a small configuration that tells Chrome and Firefox where to find the PassKey.BrowserHost executable.

### Automatic Registration

If you installed PassKey using the **Installer (EXE)**, the Native Messaging host is registered automatically for both Chrome and Firefox. No action required.

### Manual Registration

If you are using the **Portable** version, run the registration script:

```powershell
.\scripts\register-native-host.ps1
```

This creates registry entries at:
- **Chrome:** `HKCU\SOFTWARE\Google\Chrome\NativeMessagingHosts\com.passkey.host`
- **Firefox:** `HKCU\SOFTWARE\Mozilla\NativeMessagingHosts\com.passkey.host`

Each entry points to the `PassKey.BrowserHost.NMH.json` manifest file in the installation folder.

---

## Using the Popup

Click the PassKey extension icon in your browser toolbar to open the popup. The popup has several states:

### Connecting

The extension is trying to reach PassKey Desktop. If this persists:
- Make sure PassKey Desktop is running.
- Check that the Native Messaging host is registered (see above).

### Vault Locked

The vault is locked. Enter your master password directly in the popup and click **Unlock**. You do not need to switch to the desktop app.

### No Credentials

The vault is unlocked but no credentials match the current website. You can:
- Switch to the **All passwords** tab to browse the full vault.
- Open PassKey Desktop to add a new entry with this website's URL.

### Credential List

Matching credentials are shown with:
- **Avatar** — The entry's icon (letter, glyph, or image)
- **Title** — The entry name
- **Username** — The login username

The popup has two tabs:
- **This site** — Credentials matching the current website's domain
- **All passwords** — Your full vault with a search box

#### Actions

Hover over an entry to reveal action buttons:
- **Copy username** — Copies the username to the clipboard
- **Copy password** — Copies the password to the clipboard
- **Fill form** — Fills the login form on the current page

Click an entry directly to autofill the login form immediately.

### Badge

The extension icon shows a **badge number** indicating how many credentials match the current website.

---

## Autofill

When you click an entry or press the Fill button, the extension's content script fills in the login form on the page.

### Supported Frameworks

The autofill engine detects and fills login forms built with:
- Standard HTML `<input>` elements
- **React** (synthetic events and controlled components)
- **Angular** (ngModel and reactive forms)
- **Vue** (v-model binding)

### Multi-Step Login Forms

Some websites split login into two steps (e.g., Google, Microsoft):
1. **Step 1** asks for your email/username only.
2. **Step 2** asks for your password.

PassKey handles this automatically:
- On step 1, it fills only the username/email field.
- On step 2, when the password field appears, it fills the password.

### What If Autofill Doesn't Work?

If the form is not filled correctly:
1. Try clicking the **Fill** button again.
2. Use the **Copy password** button and paste manually (Ctrl+V).
3. Some websites use non-standard form implementations that may not be detected. Open a [GitHub issue](https://github.com/pexatar/PassKey/issues) to report compatibility problems.

---

## Adding Credentials for a New Site

The browser extension does not support adding new entries directly. To add credentials for a new website:

1. Open PassKey Desktop.
2. Go to the Passwords section.
3. Press **Ctrl+N** to create a new entry.
4. Enter the website URL, username, and password.
5. Save the entry.

The new credentials will appear in the extension popup the next time you visit that website.

---

## Privacy and Security

- **Your data never leaves your computer.** All communication between the extension and the desktop app happens locally.
- **No telemetry.** The extension does not send any data to external servers.
- **Encrypted IPC.** Even local communication is encrypted with ephemeral ECDH P-256 session keys and AES-256-GCM.
- **Named Pipe ACL.** Only the Windows user account that owns the PassKey Desktop process can connect to the Named Pipe.

---

## Troubleshooting

### Popup stuck on "Connecting"

- Make sure PassKey Desktop is running.
- Restart PassKey Desktop.
- Check the Native Messaging host registration (see [Manual Registration](#manual-registration)).
- Open Chrome's extension page, remove and re-add the extension.

### Autofill does not work on a specific site

- Ensure the URL in the password entry matches the website domain.
- Try the **Copy password** button as a workaround.
- Report the site in a [GitHub issue](https://github.com/pexatar/PassKey/issues) so we can improve compatibility.

### Badge number does not appear

- The badge only appears when the vault is unlocked and there are matching credentials.
- Make sure the URL field of your password entries contains the correct domain.

### Extension shows errors in DevTools

1. Open `chrome://extensions/` (or `about:debugging` in Firefox).
2. Click **Inspect** on the PassKey extension.
3. Check the Console tab for error messages.
4. Include these messages when filing a bug report.
