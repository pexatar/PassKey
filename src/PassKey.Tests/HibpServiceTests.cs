using System.Net;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class HibpServiceTests
{
    // ─── ParseBreachCount (pure-function unit tests) ─────────────────────────

    [Fact]
    public void ParseBreachCount_KnownSuffix_ReturnsCount()
    {
        // Format: SUFFIX:COUNT — body uses CRLF as HIBP's real responses do.
        var body =
            "1E4C9B93F3F0682250B6CF8331B7EE68FD8:3730471\r\n" +
            "FC1E80FB44A9C9C90DFF7C2B72D8FCC4ED1:7\r\n" +
            "ABCDEF0123456789ABCDEF0123456789ABC:42";

        var n = HibpService.ParseBreachCount(body, "FC1E80FB44A9C9C90DFF7C2B72D8FCC4ED1");

        Assert.Equal(7, n);
    }

    [Fact]
    public void ParseBreachCount_UnknownSuffix_ReturnsZero()
    {
        var body = "AAA0000000000000000000000000000000A:5";
        Assert.Equal(0, HibpService.ParseBreachCount(body, "BBB0000000000000000000000000000000B"));
    }

    [Fact]
    public void ParseBreachCount_IsCaseInsensitive()
    {
        // The HIBP API returns uppercase hex. Make sure we tolerate a lowercase
        // local hash by chance (Convert.ToHexString returns uppercase, so this
        // is belt + braces).
        var body = "1E4C9B93F3F0682250B6CF8331B7EE68FD8:11\r\n";
        Assert.Equal(11, HibpService.ParseBreachCount(body, "1e4c9b93f3f0682250b6cf8331b7ee68fd8"));
    }

    [Fact]
    public void ParseBreachCount_BodyWithLfOnly_StillParses()
    {
        // Defensive — accept either CRLF or LF line endings.
        var body = "AAA0:1\nBBB0:2\nCCC0:3";
        Assert.Equal(2, HibpService.ParseBreachCount(body, "BBB0"));
    }

    [Fact]
    public void ParseBreachCount_NoTrailingNewline_StillParses()
    {
        var body = "ABCD:99";
        Assert.Equal(99, HibpService.ParseBreachCount(body, "ABCD"));
    }

    [Fact]
    public void Sha1Hex_KnownVector_MatchesRfc3174()
    {
        // RFC 3174 test vector: SHA1("abc") == A9993E364706816ABA3E25717850C26C9CD0D89D
        Assert.Equal("A9993E364706816ABA3E25717850C26C9CD0D89D", HibpService.Sha1Hex("abc"));
    }

    [Fact]
    public void Sha1Hex_EmptyString_MatchesRfc3174()
    {
        // RFC 3174: SHA1("") == DA39A3EE5E6B4B0D3255BFEF95601890AFD80709
        Assert.Equal("DA39A3EE5E6B4B0D3255BFEF95601890AFD80709", HibpService.Sha1Hex(string.Empty));
    }

    // ─── Network behaviour (HttpClient mocked with a fake handler) ───────────

    [Fact]
    public async Task CheckPasswordAsync_KAnonymity_SendsOnlyFiveCharPrefix()
    {
        // Prove that the URL request only carries 5 hex chars of the hash — the
        // foundational privacy invariant of this whole feature.
        string? observedUrl = null;
        var handler = StubHandler.Sync((req, _) =>
        {
            observedUrl = req.RequestUri?.AbsoluteUri;
            // Return a body that matches "secret" → suffix not present (i.e. count 0).
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("0000000000000000000000000000000000A:1\r\n"),
            };
        });
        var service = new HibpService(new HttpClient(handler));

        await service.CheckPasswordAsync("hello");

        Assert.NotNull(observedUrl);
        Assert.StartsWith("https://api.pwnedpasswords.com/range/", observedUrl);
        var prefix = observedUrl!["https://api.pwnedpasswords.com/range/".Length..];
        Assert.Equal(5, prefix.Length);
        // Real SHA1("hello")[0..5] is "AAF4C" — assert that, to confirm we don't
        // accidentally send the WHOLE hash.
        Assert.Equal("AAF4C", prefix);
    }

    [Fact]
    public async Task CheckPasswordAsync_KnownBreachedPassword_ReturnsBreachCount()
    {
        // SHA1("password") = 5BAA61E4C9B93F3F0682250B6CF8331B7EE68FD8
        // Prefix "5BAA6", suffix "1E4C9B93F3F0682250B6CF8331B7EE68FD8"
        var handler = StubHandler.Sync((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("1E4C9B93F3F0682250B6CF8331B7EE68FD8:9659365\r\n"),
        });
        var service = new HibpService(new HttpClient(handler));

        var count = await service.CheckPasswordAsync("password");

        Assert.Equal(9659365, count);
    }

    [Fact]
    public async Task CheckPasswordAsync_NotBreached_ReturnsZero()
    {
        // Return a single non-matching line.
        var handler = StubHandler.Sync((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF:1\r\n"),
        });
        var service = new HibpService(new HttpClient(handler));

        var count = await service.CheckPasswordAsync("a-very-unlikely-passphrase-q9p4r2x7z");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CheckPasswordAsync_NetworkError_PropagatesException()
    {
        var handler = StubHandler.Sync((_, _) => throw new HttpRequestException("simulated DNS failure"));
        var service = new HibpService(new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.CheckPasswordAsync("anything"));
    }

    [Fact]
    public async Task CheckPasswordAsync_NonSuccessStatus_Throws()
    {
        var handler = StubHandler.Sync((_, _) =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var service = new HibpService(new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.CheckPasswordAsync("anything"));
    }

    [Fact]
    public async Task CheckPasswordAsync_EmptyPassword_ReturnsZeroWithoutHttpCall()
    {
        var called = false;
        var handler = StubHandler.Sync((_, _) =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty),
            };
        });
        var service = new HibpService(new HttpClient(handler));

        var count = await service.CheckPasswordAsync(string.Empty);

        Assert.Equal(0, count);
        Assert.False(called, "Empty password must short-circuit before issuing any HTTP request.");
    }

    [Fact]
    public async Task CheckPasswordAsync_Cancellation_PropagatesTaskCanceled()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = new HibpService(new HttpClient(handler));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(20);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CheckPasswordAsync("anything", cts.Token));
    }

    // ─── Helper: minimal HttpMessageHandler stub ─────────────────────────────

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public static StubHandler Sync(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
            => new((req, ct) => Task.FromResult(handler(req, ct)));

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
