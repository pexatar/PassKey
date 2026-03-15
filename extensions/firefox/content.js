/**
 * PassKey Content Script — Form Detection and Autofill Injection
 *
 * Injected into all pages at document_idle.
 * Detects login forms (password fields + associated username fields).
 * Receives fill commands from the background script.
 *
 * Uses native value setter + event dispatch for React/Angular/Vue compatibility.
 *
 * Firefox difference from Chrome: the onMessage listener returns Promise.resolve()
 * instead of using the sendResponse callback, as required by the Firefox
 * WebExtensions message passing API.
 */

// ═══════════════════════════════════════════════════════════════
// Form Detection
// ═══════════════════════════════════════════════════════════════

/**
 * Detects login forms on the current page by finding all visible password fields
 * and pairing each with its associated username/email field.
 * Skips aria-hidden and invisible fields.
 *
 * @returns {Array<{passwordField: HTMLInputElement, usernameField: HTMLInputElement|null}>}
 *          Array of detected form pairs. Empty if no login forms are found.
 */
function detectLoginForms() {
  const passwordFields = document.querySelectorAll(
    'input[type="password"]:not([aria-hidden="true"])'
  );
  const forms = [];

  for (const pwField of passwordFields) {
    if (!isVisible(pwField)) continue;

    const container = pwField.closest('form') || pwField.parentElement;
    if (!container) continue;

    const usernameField = findUsernameField(container, pwField);

    forms.push({
      passwordField: pwField,
      usernameField: usernameField
    });
  }
  return forms;
}

/**
 * Finds the most likely username or email input associated with a given password field.
 * Uses a three-priority heuristic:
 *   1. autocomplete="username" or autocomplete="email" attribute.
 *   2. name/id/autocomplete/placeholder containing user|email|login|account|ident|mail.
 *   3. First visible text or email input in the same container.
 *
 * @param {HTMLElement} container - The form element or nearest parent containing both fields.
 * @param {HTMLInputElement} passwordField - The password field to associate with.
 * @returns {HTMLInputElement|null} The best candidate username field, or null if none found.
 */
function findUsernameField(container, passwordField) {
  // Priority 1: autocomplete attribute
  const autoComplete = container.querySelector(
    'input[autocomplete="username"], input[autocomplete="email"]'
  );
  if (autoComplete && autoComplete !== passwordField && isVisible(autoComplete)) {
    return autoComplete;
  }

  // Priority 2: name/id/placeholder pattern matching
  const inputs = container.querySelectorAll(
    'input[type="text"], input[type="email"], input:not([type])'
  );
  for (const input of inputs) {
    if (input === passwordField || !isVisible(input)) continue;
    const identifier = (
      (input.name || '') +
      (input.id || '') +
      (input.getAttribute('autocomplete') || '') +
      (input.placeholder || '')
    ).toLowerCase();
    if (/user|email|login|account|ident|mail/.test(identifier)) {
      return input;
    }
  }

  // Priority 3: first visible text/email input before the password field
  for (const input of inputs) {
    if (input === passwordField) continue;
    if (isVisible(input)) return input;
  }

  return null;
}

/**
 * Checks whether a DOM element is visible and occupies space in the layout.
 * An element is considered visible when its computed display is not 'none',
 * visibility is not 'hidden', opacity is not '0', and it has non-zero dimensions.
 *
 * @param {Element|null} el - The element to check.
 * @returns {boolean} True if the element is visible.
 */
function isVisible(el) {
  if (!el) return false;
  const style = window.getComputedStyle(el);
  return style.display !== 'none'
    && style.visibility !== 'hidden'
    && style.opacity !== '0'
    && el.offsetWidth > 0
    && el.offsetHeight > 0;
}

// ═══════════════════════════════════════════════════════════════
// Autofill
// ═══════════════════════════════════════════════════════════════

// React and Angular intercept DOM changes via their own property descriptors.
// Directly setting .value doesn't trigger synthetic events. We must obtain
// the original native setter from the HTMLInputElement prototype and invoke it,
// then dispatch both 'input' and 'change' events to simulate real user input.
const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
  window.HTMLInputElement.prototype, 'value'
)?.set;

/**
 * Sets a form field's value using the native HTMLInputElement value setter to bypass
 * React/Angular/Vue controlled component interception, then dispatches 'input', 'change',
 * and 'blur' events to trigger framework change detection.
 *
 * @param {HTMLInputElement} field - The input element to fill.
 * @param {string} value - The value to set.
 */
function setFieldValue(field, value) {
  if (!field || !value) return;

  // Focus the field first (some sites require this)
  field.focus();

  // Use native setter to bypass framework interception
  if (nativeInputValueSetter) {
    nativeInputValueSetter.call(field, value);
  } else {
    field.value = value;
  }

  // Dispatch events to trigger framework change detection
  field.dispatchEvent(new Event('input', { bubbles: true }));
  field.dispatchEvent(new Event('change', { bubbles: true }));
  field.dispatchEvent(new Event('blur', { bubbles: true }));
}

/**
 * Fills the first detected login form with the given credentials.
 * Falls back to single-field detection for multi-step logins where the password
 * field is not yet visible (e.g., Google's email-first flow).
 *
 * @param {string|null} username - The username or email to fill.
 * @param {string|null} password - The password to fill.
 * @returns {boolean} True if at least one field was successfully filled.
 */
function fillForm(username, password) {
  // Normal path: form with visible password field
  const forms = detectLoginForms();
  if (forms.length > 0) {
    const { usernameField, passwordField } = forms[0];
    if (usernameField && username) setFieldValue(usernameField, username);
    if (passwordField && password) setFieldValue(passwordField, password);
    return true;
  }

  // Fallback: multi-step login showing only email/username field (no password yet)
  if (username) {
    const usernameOnly =
      findUsernameField(document.body, null) ||
      document.querySelector('input[type="email"]:not([aria-hidden="true"])');
    if (usernameOnly && isVisible(usernameOnly)) {
      setFieldValue(usernameOnly, username);
      return true;
    }
  }

  return false;
}

// ═══════════════════════════════════════════════════════════════
// Message Handler
// ═══════════════════════════════════════════════════════════════

// Firefox requires the listener to return a Promise rather than call sendResponse.
browser.runtime.onMessage.addListener((msg, sender) => {
  if (msg.type === 'fill-fields') {
    const success = fillForm(msg.username, msg.password);
    return Promise.resolve({ success, error: success ? null : 'no-form-detected' });
  }

  if (msg.type === 'detect-forms') {
    const forms = detectLoginForms();
    return Promise.resolve({
      hasForms: forms.length > 0,
      formCount: forms.length
    });
  }
});

// Notify background to update the extension badge for this tab's URL.
// Uses content script sender (no 'tabs' permission required).
browser.runtime.sendMessage({ type: 'update-badge', url: location.href }).catch(() => {});
