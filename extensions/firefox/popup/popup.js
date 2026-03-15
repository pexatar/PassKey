/**
 * PassKey Popup — state machine with in-extension unlock + dual view.
 * States: loading | disconnected | unlock | empty | list
 * Views (in list state): 'site' (filtered by URL) | 'all' (full vault)
 *
 * Firefox version: uses browser.* API (Promise-based) instead of chrome.*
 */

// ─── SVG constants (inline, no external deps, CSP-safe) ──────────────────────

const COPY_SVG = `<svg width="15" height="15" viewBox="0 0 15 15" fill="none" aria-hidden="true">
  <rect x="5" y="1" width="9" height="11" rx="1.5" stroke="currentColor" stroke-width="1.4"/>
  <rect x="1" y="4" width="9" height="11" rx="1.5" fill="var(--pk-bg)" stroke="currentColor" stroke-width="1.4"/>
</svg>`;

const USER_SVG = `<svg width="15" height="15" viewBox="0 0 15 15" fill="none" aria-hidden="true">
  <circle cx="7.5" cy="5" r="2.5" stroke="currentColor" stroke-width="1.4"/>
  <path d="M2 13c0-3 2.5-5 5.5-5s5.5 2 5.5 5" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/>
</svg>`;

const LOCK_SVG = `<svg width="15" height="15" viewBox="0 0 15 15" fill="none" aria-hidden="true">
  <rect x="3" y="7" width="9" height="6" rx="1.5" stroke="currentColor" stroke-width="1.4"/>
  <path d="M5 7V5a2.5 2.5 0 0 1 5 0v2" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/>
</svg>`;

const LOCK_SM_SVG = `<svg width="12" height="12" viewBox="0 0 15 15" fill="none" aria-hidden="true">
  <rect x="3" y="7" width="9" height="6" rx="1.5" stroke="currentColor" stroke-width="1.4"/>
  <path d="M5 7V5a2.5 2.5 0 0 1 5 0v2" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/>
</svg>`;

const FILL_SVG = `<svg width="15" height="15" viewBox="0 0 15 15" fill="none" aria-hidden="true">
  <path d="M10.5 2.5l2 2L5 12H3v-2L10.5 2.5z" stroke="currentColor" stroke-width="1.4" stroke-linejoin="round"/>
</svg>`;

const CHECK_SVG = `<svg width="15" height="15" viewBox="0 0 15 15" fill="none" aria-hidden="true">
  <path d="M2.5 8l4 4 6-7" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>
</svg>`;

const SPIN_SVG = `<svg class="pk-spin" width="15" height="15" viewBox="0 0 15 15" fill="none" aria-hidden="true">
  <circle cx="7.5" cy="7.5" r="6" stroke="currentColor" stroke-width="1.8"
          stroke-dasharray="24 12" stroke-linecap="round"/>
</svg>`;

// ─── State ────────────────────────────────────────────────────────────────────

const STATES = ['loading', 'disconnected', 'unlock', 'empty', 'list'];
let currentView = 'site'; // 'site' | 'all'
let siteCredentials = [];
let allCredentials  = [];
let activeTabId     = null;
let isHttpUrl       = false;

// ─── DOM references ───────────────────────────────────────────────────────────

const $ = id => document.getElementById(id);

const statusDot         = $('status-dot');
const domainBadge       = $('domain-badge');
const domainText        = $('domain-text');
const copyFeedback      = $('copy-feedback');
const loadingText       = $('loading-text');
const disconnectedTitle = $('disconnected-title');
const disconnectedSub   = $('disconnected-sub');
const btnRetry          = $('btn-retry');
const unlockTitle       = $('unlock-title');
const unlockSub         = $('unlock-sub');
const pwInput           = $('master-password-input');
const btnUnlock         = $('btn-unlock');
const unlockError       = $('unlock-error');
const emptyTitle        = $('empty-title');
const emptySub          = $('empty-sub');
const tabPills          = $('tab-pills');
const tabSite           = $('tab-site');
const tabAll            = $('tab-all');
const searchInput       = $('search-input');
const btnClear          = $('btn-clear-search');
const resultsCount      = $('results-count');
const credList          = $('credentials-list');
const btnOpenApp        = $('btn-open-app');
const footerVersion     = $('footer-version');

// ─── Init UI strings ──────────────────────────────────────────────────────────

/**
 * Applies all localized strings from window.t to the popup DOM.
 * window.t is set by lib/i18n.js which is loaded before popup.js.
 */
function initStrings() {
  const t = window.t;
  loadingText.textContent       = t.loadingText;
  disconnectedTitle.textContent = t.disconnectedTitle;
  disconnectedSub.textContent   = t.disconnectedSub;
  btnRetry.textContent          = t.retryBtn;
  unlockTitle.textContent       = t.unlockTitle;
  unlockSub.textContent         = t.unlockSub;
  pwInput.placeholder           = t.unlockPlaceholder;
  btnUnlock.textContent         = t.unlockBtn;
  emptyTitle.textContent        = t.emptyTitle;
  emptySub.textContent          = t.emptySub;
  tabSite.textContent           = t.tabThisSite;
  tabAll.textContent            = t.tabAll;
  searchInput.placeholder       = t.searchPlaceholder;
  btnClear.setAttribute('aria-label', t.clearSearch);
  credList.setAttribute('aria-label', t.tabAll);
  btnOpenApp.textContent        = t.openApp;
}

// ─── State machine ────────────────────────────────────────────────────────────

/**
 * Shows one popup state panel and hides all others.
 * Each state corresponds to a DOM element with id="state-{state}".
 *
 * @param {'loading'|'disconnected'|'unlock'|'empty'|'list'} state - The state to activate.
 */
function setState(state) {
  for (const s of STATES) {
    const el = $(`state-${s}`);
    if (el) el.hidden = (s !== state);
  }
}

/**
 * Updates the connection status dot CSS class and aria-label.
 *
 * @param {string} type - Status class suffix: '', 'connected', 'locked', 'disconnected'.
 */
function setStatus(type) {
  statusDot.className = `pk-status ${type}`;
  statusDot.setAttribute('aria-label', window.t[`status${type.charAt(0).toUpperCase()+type.slice(1)}`] || type);
}

// ─── Main init ────────────────────────────────────────────────────────────────

/**
 * Initializes the popup: applies i18n strings, reads the active tab, queries vault status,
 * and transitions to the appropriate state (disconnected / unlock / list / empty).
 * Focuses the first actionable element in each state for keyboard accessibility.
 *
 * @returns {Promise<void>}
 */
async function init() {
  initStrings();
  setState('loading');
  setStatus('');

  // Footer version
  const manifest = browser.runtime.getManifest();
  footerVersion.textContent = `v${manifest.version}`;

  // Active tab
  const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
  activeTabId = tab?.id ?? null;
  isHttpUrl   = tab?.url?.startsWith('http') ?? false;
  updateDomainBadge(tab?.url);

  // Get vault status
  let statusResp;
  try {
    statusResp = await browser.runtime.sendMessage({ type: 'get-status' });
  } catch {
    statusResp = null;
  }

  if (!statusResp?.success) {
    setStatus('disconnected');
    setState('disconnected');
    btnRetry.focus();
    return;
  }

  if (!statusResp.payload?.unlocked) {
    setStatus('locked');
    setState('unlock');
    pwInput.focus();
    return;
  }

  setStatus('connected');
  await loadCredentialsAndShow(tab);
}

/**
 * Fetches site-specific and all-vault credentials in parallel, updates the extension badge,
 * and transitions to the 'list' or 'empty' state.
 * Auto-selects the 'site' view when there are matching site credentials,
 * otherwise falls back to the 'all' view.
 *
 * @param {browser.tabs.Tab} tab - The active tab object.
 * @returns {Promise<void>}
 */
async function loadCredentialsAndShow(tab) {
  // Load both lists in parallel
  const [siteResp, allResp] = await Promise.all([
    isHttpUrl
      ? browser.runtime.sendMessage({ type: 'get-credentials', url: tab.url }).catch(() => null)
      : Promise.resolve(null),
    browser.runtime.sendMessage({ type: 'get-all-credentials' }).catch(() => null)
  ]);

  siteCredentials = siteResp?.payload?.credentials ?? [];
  allCredentials  = allResp?.payload?.credentials  ?? [];

  // Update extension badge with site credential count
  const badgeCount = siteCredentials.length;
  browser.action.setBadgeText({ text: badgeCount > 0 ? String(badgeCount) : '' }).catch(() => {});
  if (badgeCount > 0) {
    browser.action.setBadgeBackgroundColor({ color: '#0078d4' }).catch(() => {});
  }

  // Show / hide tab pills
  if (isHttpUrl) {
    tabPills.hidden = false;
  } else {
    tabPills.hidden = true;
  }

  if (siteCredentials.length > 0) {
    switchView('site');
    setState('list');
  } else if (allCredentials.length > 0) {
    switchView('all');
    setState('list');
  } else {
    setState('empty');
    return;
  }
  searchInput.focus();
}

// ─── Domain badge ─────────────────────────────────────────────────────────────

/**
 * Updates the domain badge in the popup header with the current page's hostname.
 * Hides the badge for non-HTTP pages (new tab, extension pages, etc.).
 *
 * @param {string|undefined} url - The active tab URL.
 */
function updateDomainBadge(url) {
  if (!url?.startsWith('http')) {
    domainBadge.hidden = true;
    return;
  }
  try {
    const domain = new URL(url).hostname.replace(/^www\./, '');
    domainText.textContent = domain;
    domainBadge.hidden = false;
  } catch {
    domainBadge.hidden = true;
  }
}

// ─── Tab switching ────────────────────────────────────────────────────────────

/**
 * Switches between the 'site' and 'all' credential views.
 * Updates tab pill active states, aria-selected attributes, clears the search,
 * and re-renders the credential list.
 *
 * @param {'site'|'all'} view - The view to activate.
 */
function switchView(view) {
  currentView = view;
  tabSite.classList.toggle('pk-tab--active', view === 'site');
  tabAll.classList.toggle('pk-tab--active',  view === 'all');
  tabSite.setAttribute('aria-selected', String(view === 'site'));
  tabAll.setAttribute('aria-selected',  String(view === 'all'));

  // Clear search
  searchInput.value = '';
  btnClear.hidden   = true;
  resultsCount.hidden = true;

  renderList(view === 'site' ? siteCredentials : allCredentials);
}

tabSite.addEventListener('click', () => switchView('site'));
tabAll.addEventListener('click',  () => switchView('all'));

// ─── Credential rendering ─────────────────────────────────────────────────────

/**
 * Clears and re-renders the credential list from the given array.
 *
 * @param {Array<{id: string, title: string, username: string, hasPassword: boolean}>} creds
 */
function renderList(creds) {
  credList.innerHTML = '';
  for (const cred of creds) {
    credList.appendChild(buildItem(cred));
  }
}

/**
 * Creates a credential list item element with avatar, title, username, and action buttons.
 * Action buttons (copy username, copy password, fill form) are revealed on hover.
 * Row click triggers autofill. Keyboard: ArrowUp/Down to navigate, Enter/Space to fill, Ctrl+C to copy.
 * Data attributes (data-title, data-username) enable client-side search filtering.
 *
 * @param {{id: string, title: string, username: string, hasPassword: boolean}} cred - Credential summary.
 * @returns {HTMLLIElement} The constructed list item element.
 */
function buildItem(cred) {
  const li = document.createElement('li');
  li.className = 'pk-cred-item';
  li.setAttribute('role', 'listitem');
  li.setAttribute('tabindex', '0');
  li.setAttribute('aria-label', `${cred.title}, ${cred.username}`);
  li.dataset.id       = cred.id;
  li.dataset.title    = (cred.title    || '').toLowerCase();
  li.dataset.username = (cred.username || '').toLowerCase();

  // Avatar
  const avatar = document.createElement('div');
  avatar.className = 'pk-avatar';
  avatar.textContent = cred.title ? cred.title[0].toUpperCase() : '?';
  avatar.setAttribute('aria-hidden', 'true');

  // Body
  const body = document.createElement('div');
  body.className = 'pk-cred-body';

  const titleEl = document.createElement('div');
  titleEl.className = 'pk-cred-title';
  titleEl.textContent = cred.title || '';

  const subEl = document.createElement('div');
  subEl.className = 'pk-cred-sub';
  subEl.textContent = cred.username || '';

  body.appendChild(titleEl);
  body.appendChild(subEl);

  // Actions (revealed on hover — progressive disclosure)
  const actions = document.createElement('div');
  actions.className = 'pk-cred-actions';

  const btnCopyUser = makeActionBtn(USER_SVG, window.t.copyUsername, 'copy-username');
  const btnCopyPw   = makeActionBtn(LOCK_SVG, window.t.copyPassword, 'copy-password');
  const btnFill     = makeActionBtn(FILL_SVG, window.t.fillForm,     'fill');

  actions.appendChild(btnCopyUser);
  actions.appendChild(btnCopyPw);
  actions.appendChild(btnFill);

  // Password presence indicator (visible at rest, fades on hover)
  const pwIndicator = document.createElement('span');
  pwIndicator.className = 'pk-pw-indicator';
  if (window.t.hasPassword) pwIndicator.setAttribute('aria-label', window.t.hasPassword);
  pwIndicator.innerHTML = LOCK_SM_SVG;

  li.appendChild(avatar);
  li.appendChild(body);
  if (cred.hasPassword !== false) li.appendChild(pwIndicator);
  li.appendChild(actions);

  // Row click = fill (primary action, NordPass pattern)
  li.style.cursor = 'pointer';
  li.addEventListener('click', e => {
    if (!e.target.closest('.pk-cred-actions')) {
      onFill(cred, null);
    }
  });

  // Item-level keyboard handler
  li.addEventListener('keydown', e => {
    const items = [...credList.querySelectorAll('.pk-cred-item')];
    const idx   = items.indexOf(li);
    if (e.key === 'ArrowDown' && idx < items.length - 1) {
      e.preventDefault(); items[idx + 1].focus();
    } else if (e.key === 'ArrowUp' && idx > 0) {
      e.preventDefault(); items[idx - 1].focus();
    } else if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault(); onFill(cred, btnFill);
    } else if (e.ctrlKey && e.key === 'c') {
      e.preventDefault(); onCopyPassword(cred, btnCopyPw);
    }
  });

  btnCopyUser.addEventListener('click', e => { e.stopPropagation(); onCopyUsername(cred, btnCopyUser); });
  btnCopyPw.addEventListener(  'click', e => { e.stopPropagation(); onCopyPassword(cred, btnCopyPw); });
  btnFill.addEventListener(    'click', e => { e.stopPropagation(); onFill(cred, btnFill); });

  return li;
}

/**
 * Creates an icon-only action button (used internally by buildItem for legacy compatibility).
 *
 * @param {string} svgHtml - SVG markup for the button icon.
 * @param {string} label - Accessible label (aria-label).
 * @param {string} action - Data attribute value for the button's action type.
 * @returns {HTMLButtonElement}
 */
function makeIconBtn(svgHtml, label, action) {
  const btn = document.createElement('button');
  btn.className = 'pk-icon-btn';
  btn.setAttribute('aria-label', label);
  btn.setAttribute('data-action', action);
  btn.setAttribute('tabindex', '-1');
  btn.type = 'button';
  btn.innerHTML = svgHtml;
  return btn;
}

/**
 * Creates a labeled action button with an icon and text label for use in credential items.
 * tabindex="-1" keeps buttons out of the tab order (the row itself is focusable).
 *
 * @param {string} svgHtml - SVG markup for the button icon.
 * @param {string} label - Accessible label and visible text.
 * @param {string} action - Data attribute value for the button's action type.
 * @returns {HTMLButtonElement}
 */
function makeActionBtn(svgHtml, label, action) {
  const btn = document.createElement('button');
  btn.className = 'pk-action-btn';
  btn.setAttribute('aria-label', label);
  btn.setAttribute('title', label);
  btn.setAttribute('data-action', action);
  btn.setAttribute('tabindex', '-1');
  btn.type = 'button';

  const icon = document.createElement('span');
  icon.className = 'pk-action-icon';
  icon.setAttribute('aria-hidden', 'true');
  icon.innerHTML = svgHtml;

  const text = document.createElement('span');
  text.className = 'pk-action-label';
  text.textContent = label;

  btn.appendChild(icon);
  btn.appendChild(text);
  return btn;
}

// ─── Actions ──────────────────────────────────────────────────────────────────

/**
 * Copies the credential's username to the clipboard and shows button feedback.
 *
 * @param {{username: string}} cred - Credential object.
 * @param {HTMLButtonElement} btn - The button to show feedback on.
 */
async function onCopyUsername(cred, btn) {
  try {
    await navigator.clipboard.writeText(cred.username || '');
    showBtnFeedback(btn, CHECK_SVG, true);
  } catch {
    showBtnFeedback(btn, CHECK_SVG, false, true);
  }
}

/**
 * Requests the decrypted password from the background event page (via ECDH),
 * copies it to the clipboard, and shows button feedback.
 *
 * @param {{id: string}} cred - Credential object with GUID.
 * @param {HTMLButtonElement} btn - The button to show feedback on.
 */
async function onCopyPassword(cred, btn) {
  const iconTarget = btn.querySelector('.pk-action-icon') ?? btn;
  iconTarget.innerHTML = SPIN_SVG;
  btn.disabled = true;

  try {
    const resp = await browser.runtime.sendMessage({ type: 'copy-credential', id: cred.id });
    if (resp.success) {
      await navigator.clipboard.writeText(resp.password);
      showBtnFeedback(btn, CHECK_SVG, true, false, LOCK_SVG);
    } else {
      showBtnFeedback(btn, CHECK_SVG, false, true, LOCK_SVG);
    }
  } catch {
    showBtnFeedback(btn, CHECK_SVG, false, true, LOCK_SVG);
  }
}

/**
 * Sends a fill-credential message to the background to autofill the active tab's login form,
 * then closes the popup. Shows a spinner on the button while the operation is in progress.
 *
 * @param {{id: string, username: string}} cred - Credential to fill.
 * @param {HTMLButtonElement|null} btn - The fill button (may be null for row-click fills).
 */
async function onFill(cred, btn) {
  if (btn) {
    const iconTarget = btn.querySelector('.pk-action-icon') ?? btn;
    iconTarget.innerHTML = SPIN_SVG;
    btn.disabled = true;
  }
  try {
    await browser.runtime.sendMessage({
      type: 'fill-credential',
      id: cred.id,
      username: cred.username,
      tabId: activeTabId
    });
  } catch { /* ignore */ }
  window.close();
}

/**
 * Shows temporary visual feedback on an action button (success check or error state).
 * Restores the original icon after 1.5 seconds.
 *
 * @param {HTMLButtonElement} btn - The button to update.
 * @param {string} iconSvg - SVG markup for the feedback icon (usually CHECK_SVG).
 * @param {boolean} ok - True for success, false for failure.
 * @param {boolean} [isError=false] - True to apply the error CSS class.
 * @param {string|null} [restoreIcon=null] - SVG to restore after the timeout; defaults to CHECK_SVG or original icon.
 */
function showBtnFeedback(btn, iconSvg, ok, isError = false, restoreIcon = null) {
  // Support both icon-only (.pk-icon-btn) and labeled (.pk-action-btn) buttons
  const iconTarget = btn.querySelector('.pk-action-icon') ?? btn;
  iconTarget.innerHTML = iconSvg;
  btn.classList.toggle('success', ok && !isError);
  btn.classList.toggle('error',   isError);
  btn.disabled = false;

  const msgKey = isError ? 'copyError' : 'copied';
  copyFeedback.textContent = window.t[msgKey];

  setTimeout(() => {
    iconTarget.innerHTML = restoreIcon ?? (ok ? CHECK_SVG : iconSvg);
    btn.classList.remove('success', 'error');
    copyFeedback.textContent = '';
  }, 1500);
}

// ─── Unlock flow ──────────────────────────────────────────────────────────────

btnUnlock.addEventListener('click', doUnlock);
pwInput.addEventListener('keydown', e => { if (e.key === 'Enter') doUnlock(); });

/**
 * Reads the master password from the input, sends an 'unlock-vault' message,
 * clears the input immediately, and transitions to the list or shows an error.
 *
 * @returns {Promise<void>}
 */
async function doUnlock() {
  const pw = pwInput.value;
  if (!pw) return;

  unlockError.hidden = true;
  btnUnlock.innerHTML = SPIN_SVG;
  btnUnlock.disabled  = true;

  let result;
  try {
    result = await browser.runtime.sendMessage({ type: 'unlock-vault', masterPassword: pw });
  } catch {
    result = null;
  } finally {
    pwInput.value = ''; // clear immediately
  }

  if (result?.success) {
    setStatus('connected');
    const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
    await loadCredentialsAndShow(tab);
  } else {
    unlockError.textContent = window.t.wrongPassword;
    unlockError.hidden = false;
    btnUnlock.innerHTML = window.t.unlockBtn;
    btnUnlock.disabled  = false;
    pwInput.focus();
  }
}

// ─── Retry / reconnect ────────────────────────────────────────────────────────

btnRetry.addEventListener('click', init);

// ─── Search / filter ──────────────────────────────────────────────────────────

searchInput.addEventListener('input', () => {
  const q = searchInput.value.trim().toLowerCase();
  btnClear.hidden = !q;

  const items   = [...credList.querySelectorAll('.pk-cred-item')];
  let   visible = 0;

  for (const item of items) {
    const match = !q || item.dataset.title.includes(q) || item.dataset.username.includes(q);
    item.hidden = !match;
    if (match) visible++;
  }

  if (q) {
    const total = currentView === 'site' ? siteCredentials.length : allCredentials.length;
    resultsCount.textContent = window.t.resultsCount(visible, total);
    resultsCount.hidden = false;
  } else {
    resultsCount.hidden = true;
  }
});

btnClear.addEventListener('click', () => {
  searchInput.value = '';
  searchInput.dispatchEvent(new Event('input'));
  searchInput.focus();
});

// ─── Footer: open PassKey app ─────────────────────────────────────────────────

btnOpenApp.addEventListener('click', async () => {
  await browser.runtime.sendMessage({ type: 'show-window' }).catch(() => {});
});

// ─── Start ────────────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', init);
