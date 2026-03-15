/**
 * URL utility functions for domain extraction.
 *
 * Mirrors the logic of UrlMatcher.ExtractDomain in
 * PassKey.Core.Services.UrlMatcher.cs so that credential matching
 * in the browser extension uses the same normalization rules as the Desktop.
 *
 * Both implementations:
 *   - Strip protocol prefix (http://, https://)
 *   - Remove the 'www.' subdomain prefix
 *   - Discard path, port, query string, and fragment
 *   - Return a lowercase domain string
 */

// ─── Domain Extraction ────────────────────────────────────────────────────────

/**
 * Extracts the effective domain from a URL string using the URL Web API.
 * Handles bare hostnames (without a protocol) by prepending 'https://'
 * before parsing, ensuring compatibility with partial URLs.
 *
 * Falls back to extractDomainFallback for URLs that cause URL() to throw
 * (e.g., malformed inputs, non-standard schemes).
 *
 * @param {string} url - The URL to extract the domain from. May include
 *   protocol, path, port, query string, and fragment.
 * @returns {string} Lowercase effective domain (e.g., "example.com"),
 *   or an empty string if the input is null, non-string, or unparseable.
 */
function extractDomain(url) {
  if (!url || typeof url !== 'string') return '';
  try {
    let normalized = url.trim();
    if (!normalized.includes('://')) normalized = 'https://' + normalized;
    const parsed = new URL(normalized);
    let host = parsed.hostname.toLowerCase();
    if (host.startsWith('www.') && host.length > 4) host = host.substring(4);
    return host;
  } catch {
    // Fallback for malformed URLs
    return extractDomainFallback(url);
  }
}

/**
 * Fallback domain extraction for URLs that cannot be parsed by the URL constructor.
 * Uses string operations to strip common URL components without relying on
 * browser URL parsing, making it resilient to non-standard or malformed inputs.
 *
 * Strips (in order): protocol scheme, path, port, query string, 'www.' prefix.
 *
 * @param {string} url - The malformed or non-standard URL string to process.
 * @returns {string} Lowercase domain string, or an empty/partial string if
 *   the input does not contain recognizable URL structure.
 */
function extractDomainFallback(url) {
  let text = url.trim();
  const schemeEnd = text.indexOf('://');
  if (schemeEnd >= 0) text = text.substring(schemeEnd + 3);
  const pathStart = text.indexOf('/');
  if (pathStart >= 0) text = text.substring(0, pathStart);
  const portStart = text.indexOf(':');
  if (portStart >= 0) text = text.substring(0, portStart);
  const queryStart = text.indexOf('?');
  if (queryStart >= 0) text = text.substring(0, queryStart);
  if (text.startsWith('www.') && text.length > 4) text = text.substring(4);
  return text.toLowerCase();
}
