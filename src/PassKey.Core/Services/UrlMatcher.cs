using PassKey.Core.Models;

namespace PassKey.Core.Services;

/// <summary>
/// URL matching logic for browser extension credential lookup.
/// Matches entries by domain, handling www prefixes, subdomains, and malformed URLs.
/// </summary>
public static class UrlMatcher
{
    /// <summary>
    /// Finds all password entries whose URL matches the requested URL.
    /// Returns exact domain matches first, then subdomain matches.
    /// </summary>
    public static List<PasswordEntry> FindMatchingCredentials(IEnumerable<PasswordEntry> entries, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return [];

        var requestDomain = ExtractDomain(url);
        if (string.IsNullOrEmpty(requestDomain))
            return [];

        var exactMatches = new List<PasswordEntry>();
        var domainMatches = new List<PasswordEntry>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Url))
                continue;

            var entryDomain = ExtractDomain(entry.Url);
            if (string.IsNullOrEmpty(entryDomain))
                continue;

            if (string.Equals(entryDomain, requestDomain, StringComparison.OrdinalIgnoreCase))
            {
                exactMatches.Add(entry);
            }
            else if (IsSubdomainMatch(entryDomain, requestDomain))
            {
                domainMatches.Add(entry);
            }
        }

        // Exact matches first, then subdomain matches
        exactMatches.AddRange(domainMatches);
        return exactMatches;
    }

    /// <summary>
    /// Checks if two URLs match by domain.
    /// </summary>
    public static bool IsMatch(string entryUrl, string requestUrl)
    {
        if (string.IsNullOrWhiteSpace(entryUrl) || string.IsNullOrWhiteSpace(requestUrl))
            return false;

        var entryDomain = ExtractDomain(entryUrl);
        var requestDomain = ExtractDomain(requestUrl);

        if (string.IsNullOrEmpty(entryDomain) || string.IsNullOrEmpty(requestDomain))
            return false;

        return string.Equals(entryDomain, requestDomain, StringComparison.OrdinalIgnoreCase)
               || IsSubdomainMatch(entryDomain, requestDomain);
    }

    /// <summary>
    /// Extracts the effective domain from a URL.
    /// Strips protocol, www prefix, path, port, query, and fragment.
    /// </summary>
    public static string ExtractDomain(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        // Try to parse as URI
        var normalized = url.Trim();

        // Add scheme if missing (required for Uri.TryCreate)
        if (!normalized.Contains("://"))
            normalized = "https://" + normalized;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            return StripWwwPrefix(host).ToLowerInvariant();
        }

        // Fallback: manual extraction for malformed URLs
        return ExtractDomainFallback(url);
    }

    /// <summary>
    /// Checks if one domain is a subdomain of the other.
    /// e.g., "login.example.com" matches "example.com"
    /// </summary>
    private static bool IsSubdomainMatch(string domain1, string domain2)
    {
        // Check if domain1 is a subdomain of domain2 or vice versa
        if (domain1.EndsWith("." + domain2, StringComparison.OrdinalIgnoreCase))
            return true;
        if (domain2.EndsWith("." + domain1, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Strips "www." prefix from a hostname.
    /// </summary>
    private static string StripWwwPrefix(string host)
    {
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && host.Length > 4)
            return host[4..];
        return host;
    }

    /// <summary>
    /// Fallback domain extraction for malformed URLs.
    /// Handles cases where Uri.TryCreate fails.
    /// </summary>
    private static string ExtractDomainFallback(string url)
    {
        var text = url.Trim();

        // Strip protocol
        var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0)
            text = text[(schemeEnd + 3)..];

        // Strip path
        var pathStart = text.IndexOf('/');
        if (pathStart >= 0)
            text = text[..pathStart];

        // Strip port
        var portStart = text.IndexOf(':');
        if (portStart >= 0)
            text = text[..portStart];

        // Strip query (should be gone with path, but just in case)
        var queryStart = text.IndexOf('?');
        if (queryStart >= 0)
            text = text[..queryStart];

        return StripWwwPrefix(text).ToLowerInvariant();
    }
}
