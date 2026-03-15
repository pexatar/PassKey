# Passwords

PassKey's primary function is storing and managing your login credentials securely. This guide covers everything you need to know about password entries.

---

## Viewing Your Passwords

The **Passwords** section is the default view after unlocking. It shows all saved password entries in a scrollable list, each displaying:

- **Icon** — A letter avatar (first character of the title), a Segoe MDL2 glyph, or a custom uploaded image
- **Title** — The name you gave the entry (e.g., "Gmail", "GitHub")
- **Username** — The login username or email

Use **Ctrl+F** to focus the search box and filter entries by title, URL, or username.

---

## Adding a Password

1. Press **Ctrl+N** or click the **+** button.
2. Fill in the fields:

| Field | Description | Required |
|-------|-------------|----------|
| Title | A descriptive name for the entry | Yes |
| URL | The website address (used for browser extension matching) | No |
| Username | Your login username or email | No |
| Password | Your login password | No |
| Notes | Any additional information | No |

3. Optionally set a custom icon (see [Icons](#icons) below).
4. Click **Save**.

> **Tip:** Always fill in the URL field if you plan to use the browser extension. The extension matches credentials to websites based on this URL.

---

## Editing a Password

- Select an entry from the list and press **F2**, or
- Click the entry to open the detail panel, then click the edit button.

Make your changes and click **Save**.

---

## Deleting a Password

- Select an entry and press **Delete**, or
- Open the detail panel and click the delete button.

A confirmation dialog will appear. The default button is **Cancel** to prevent accidental deletions.

---

## Icons

Each password entry can have one of three icon types:

### Letter Avatar (Default)
Automatically generated from the first character of the title. Displayed as an uppercase letter on a coloured circle.

### Segoe MDL2 Glyph
Choose from 24 built-in glyphs (lock, globe, key, etc.) via the icon picker flyout in the detail view.

### Custom Image
Upload a PNG, JPG, or ICO file (maximum 64 KB). The image is stored as Base64 in the vault and displayed as a circular thumbnail.

To change the icon, click the avatar area in the detail view and select your preferred option.

---

## Password Generator

PassKey includes a built-in password generator accessible from the password detail view:

1. Click the **Generate** button next to the password field.
2. Configure the generator:

| Option | Range | Default |
|--------|-------|---------|
| Length | 8–128 characters | 16 |
| Uppercase (A–Z) | On/Off | On |
| Lowercase (a–z) | On/Off | On |
| Digits (0–9) | On/Off | On |
| Symbols (!@#$...) | On/Off | On |

3. The generated password and its entropy (in bits) are displayed in real time.
4. Click **Use** to apply the generated password to the entry.

> **Tip:** For maximum security, use a password of at least 20 characters with all character sets enabled. This provides over 130 bits of entropy.

---

## Password Strength Indicator

When you type or paste a password, a strength indicator appears below the field showing:

- **Score** — 0 (very weak) to 4 (very strong), displayed as a coloured bar
- **Estimated crack time** — How long it would take to crack the password with current hardware
- **Suggestions** — Actionable tips to improve the password (e.g., "Add more characters", "Avoid common patterns")

| Score | Label | Colour |
|-------|-------|--------|
| 0 | Very weak | Red |
| 1 | Weak | Orange |
| 2 | Fair | Yellow |
| 3 | Strong | Light green |
| 4 | Very strong | Green |

---

## Copying Credentials

- **Copy username** — Click the copy button next to the username, or use the context action
- **Copy password** — Click the copy button next to the password

When you copy a password:
1. The password is placed on the clipboard.
2. The entry is **excluded from Windows clipboard history** (the clipboard history panel will not show it).
3. After **30 seconds**, the clipboard is automatically cleared if it still contains the copied password.

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+N | Create new password entry |
| Ctrl+F | Focus the search box |
| F2 | Edit the selected entry |
| Delete | Delete the selected entry (with confirmation) |
| Esc | Close the detail panel |
| Ctrl+1 | Navigate to Passwords section |
