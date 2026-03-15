# Credit Cards

PassKey can securely store your credit and debit card details with automatic network detection and visual card rendering.

---

## Viewing Your Cards

The **Credit Cards** section displays your saved cards. You can toggle between two views using the button in the toolbar:

- **Card view** — Skeuomorphic card rendering showing the card number, cardholder name, expiry date, and network logo on a coloured background
- **List view** — Compact list with card label, masked number, and network icon

---

## Adding a Card

1. Press **Ctrl+N** or click the **+** button.
2. Fill in the card details:

| Field | Description | Required |
|-------|-------------|----------|
| Label | A name for the card (e.g., "Personal Visa") | Yes |
| Card number | The 13–19 digit number on the front of the card | Yes |
| Cardholder name | Name as printed on the card | No |
| Expiry date | Month and year (MM/YY) via dropdown selectors | No |
| CVV | The 3 or 4-digit security code | No |
| PIN | The card's PIN code | No |
| Notes | Any additional information | No |

3. Choose a card colour from the **10 available swatches**.
4. Click **Save**.

---

## Automatic Network Detection

As you type the card number, PassKey automatically detects the card network based on the BIN (Bank Identification Number — the first 6–8 digits):

| Network | BIN Prefix |
|---------|------------|
| Visa | 4xxx |
| Mastercard | 51xx–55xx, 2221–2720 |
| American Express | 34xx, 37xx |
| Discover | 6011, 644–649, 65xx |
| JCB | 3528–3589 |
| Maestro | 5018, 5020, 5038, 6304, 6759, 6761, 6763 |
| Diners Club | 300–305, 36xx, 38xx |

The detected network logo is displayed on the card preview in real time.

---

## Luhn Validation

PassKey validates card numbers using the [Luhn algorithm](https://en.wikipedia.org/wiki/Luhn_algorithm) in real time:

- A **checkmark icon** appears when the number passes validation
- A **cross icon** appears when the number is invalid

This helps you catch typos before saving.

---

## Card Colours

Each card can be assigned one of 10 colour swatches. The colour is applied to the card's visual background in card view. To change the colour:

1. Open the card detail view.
2. Click a colour swatch from the row of 10 options.
3. The card preview updates immediately.

---

## Copying Card Details

Click the copy button next to the card number to copy it to the clipboard. The same 30-second auto-clear and clipboard history suppression rules apply as with passwords.

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+N | Create new card entry |
| Ctrl+F | Focus the search box |
| F2 | Edit the selected card |
| Delete | Delete the selected card (with confirmation) |
| Esc | Close the detail panel |
| Ctrl+2 | Navigate to Credit Cards section |
