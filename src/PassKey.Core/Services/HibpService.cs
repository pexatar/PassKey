using System.Security.Cryptography;
using System.Text;

namespace PassKey.Core.Services;

/// <summary>
/// k-anonymity client for the Have I Been Pwned "Pwned Passwords" API.
/// </summary>
/// <remarks>
/// <para>Wire protocol:</para>
/// <list type="number">
///   <item>Compute <c>SHA1(password)</c> → 40 hex chars (uppercase, no separators).</item>
///   <item>Send the first <b>5</b> hex chars to <c>https://api.pwnedpasswords.com/range/{prefix}</c>.</item>
///   <item>Server returns plain text — one line per hash with that prefix in the form
///         <c>{suffix35}:{count}</c>.</item>
///   <item>Search the response locally for the 35-char suffix of our hash. The matching
///         line's count is the number of breaches; absence means 0.</item>
/// </list>
/// <para>The full password (and its full SHA-1) never leaves the device.</para>
/// </remarks>
public sealed class HibpService : IHibpService
{
    private const string BaseAddress = "https://api.pwnedpasswords.com/range/";
    private const int PrefixLength = 5;

    // Static singleton HttpClient — same pattern as UpdateService. HttpClient is
    // designed to be reused across calls (DNS / TCP pooling, thread-safe). Disposing
    // per-call would exhaust ephemeral ports under load.
    private static readonly HttpClient DefaultClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private readonly HttpClient _http;

    /// <summary>Production constructor — uses the shared singleton client.</summary>
    public HibpService() : this(DefaultClient)
    {
    }

    /// <summary>Test seam — injects a <see cref="HttpClient"/> with a mock handler.</summary>
    public HibpService(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        // Identify the client per HIBP courtesy guidance. This is the only header sent.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("PassKey/2.0 (+https://pass-key.it)");
        }
    }

    /// <inheritdoc/>
    public async Task<int> CheckPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        var hash = Sha1Hex(password);
        var prefix = hash[..PrefixLength];
        var suffix = hash[PrefixLength..];

        var url = BaseAddress + prefix;
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseBreachCount(body, suffix);
    }

    /// <summary>Parses the line "<c>suffix:count</c>" matching the supplied suffix and returns the count.</summary>
    internal static int ParseBreachCount(string body, string suffix)
    {
        // Iterate lines without allocating arrays for each token. The HIBP response
        // averages ~500 lines (~30 KB) — small but worth avoiding the LINQ overhead.
        var span = body.AsSpan();
        while (true)
        {
            var newline = span.IndexOf('\n');
            ReadOnlySpan<char> line = newline < 0 ? span : span[..newline];
            if (line.Length > 0 && line[^1] == '\r') line = line[..^1];

            var colon = line.IndexOf(':');
            if (colon == suffix.Length && line[..colon].Equals(suffix, StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(line[(colon + 1)..], out var count))
                    return count;
            }

            if (newline < 0) break;
            span = span[(newline + 1)..];
        }
        return 0;
    }

    /// <summary>Uppercase, no-separator SHA-1 hex digest of <paramref name="text"/> (UTF-8 bytes).</summary>
    internal static string Sha1Hex(string text)
    {
        Span<byte> hash = stackalloc byte[20]; // SHA-1 is 160 bits
        SHA1.HashData(Encoding.UTF8.GetBytes(text), hash);
        return Convert.ToHexString(hash); // uppercase by contract
    }
}
