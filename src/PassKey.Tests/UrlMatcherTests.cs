using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class UrlMatcherTests
{
    // ═══════════════════════════════════════════════════════════════
    // ExtractDomain
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("https://www.example.com/login", "example.com")]
    [InlineData("https://example.com", "example.com")]
    [InlineData("http://example.com/path?q=1", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("www.example.com", "example.com")]
    [InlineData("https://login.example.com", "login.example.com")]
    [InlineData("https://example.com:8443/path", "example.com")]
    [InlineData("https://WWW.EXAMPLE.COM", "example.com")]
    public void ExtractDomain_VariousFormats_ReturnsCorrectDomain(string url, string expected)
    {
        Assert.Equal(expected, UrlMatcher.ExtractDomain(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractDomain_EmptyOrNull_ReturnsEmpty(string? url)
    {
        Assert.Equal(string.Empty, UrlMatcher.ExtractDomain(url!));
    }

    // ═══════════════════════════════════════════════════════════════
    // IsMatch
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("https://example.com", "https://example.com/login", true)]
    [InlineData("https://www.example.com", "https://example.com", true)]
    [InlineData("example.com", "https://www.example.com/path", true)]
    [InlineData("https://login.example.com", "https://example.com", true)] // subdomain match
    [InlineData("https://example.com", "https://different.com", false)]
    [InlineData("https://example.com", "https://notexample.com", false)]
    public void IsMatch_VariousPairs_ReturnsExpected(string entryUrl, string requestUrl, bool expected)
    {
        Assert.Equal(expected, UrlMatcher.IsMatch(entryUrl, requestUrl));
    }

    [Fact]
    public void IsMatch_EmptyUrls_ReturnsFalse()
    {
        Assert.False(UrlMatcher.IsMatch("", "https://example.com"));
        Assert.False(UrlMatcher.IsMatch("https://example.com", ""));
        Assert.False(UrlMatcher.IsMatch("", ""));
    }

    // ═══════════════════════════════════════════════════════════════
    // FindMatchingCredentials
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FindMatchingCredentials_ExactMatch_ReturnsEntry()
    {
        var entries = new List<PasswordEntry>
        {
            new() { Title = "Example", Url = "https://example.com", Username = "user1" },
            new() { Title = "Other", Url = "https://other.com", Username = "user2" }
        };

        var result = UrlMatcher.FindMatchingCredentials(entries, "https://example.com/login");
        Assert.Single(result);
        Assert.Equal("user1", result[0].Username);
    }

    [Fact]
    public void FindMatchingCredentials_WwwStripping_Matches()
    {
        var entries = new List<PasswordEntry>
        {
            new() { Title = "Example", Url = "https://www.example.com", Username = "user1" }
        };

        var result = UrlMatcher.FindMatchingCredentials(entries, "https://example.com");
        Assert.Single(result);
    }

    [Fact]
    public void FindMatchingCredentials_SubdomainMatch_ReturnedAfterExact()
    {
        var entries = new List<PasswordEntry>
        {
            new() { Title = "Login", Url = "https://login.example.com", Username = "subdomain" },
            new() { Title = "Main", Url = "https://example.com", Username = "exact" }
        };

        var result = UrlMatcher.FindMatchingCredentials(entries, "https://example.com/path");

        Assert.Equal(2, result.Count);
        // Exact match should be first
        Assert.Equal("exact", result[0].Username);
        Assert.Equal("subdomain", result[1].Username);
    }

    [Fact]
    public void FindMatchingCredentials_NoMatch_ReturnsEmpty()
    {
        var entries = new List<PasswordEntry>
        {
            new() { Title = "Example", Url = "https://example.com", Username = "user1" }
        };

        var result = UrlMatcher.FindMatchingCredentials(entries, "https://different.com");
        Assert.Empty(result);
    }

    [Fact]
    public void FindMatchingCredentials_EmptyUrl_ReturnsEmpty()
    {
        var entries = new List<PasswordEntry>
        {
            new() { Title = "Example", Url = "https://example.com", Username = "user1" }
        };

        var result = UrlMatcher.FindMatchingCredentials(entries, "");
        Assert.Empty(result);
    }

    [Fact]
    public void FindMatchingCredentials_EntriesWithEmptyUrls_SkipsGracefully()
    {
        var entries = new List<PasswordEntry>
        {
            new() { Title = "No URL", Url = "", Username = "nourl" },
            new() { Title = "Null URL", Url = null!, Username = "nullurl" },
            new() { Title = "Example", Url = "https://example.com", Username = "valid" }
        };

        var result = UrlMatcher.FindMatchingCredentials(entries, "https://example.com");
        Assert.Single(result);
        Assert.Equal("valid", result[0].Username);
    }

    [Fact]
    public void FindMatchingCredentials_MultipleExactMatches_ReturnsAll()
    {
        var entries = new List<PasswordEntry>
        {
            new() { Title = "Account 1", Url = "https://example.com", Username = "user1" },
            new() { Title = "Account 2", Url = "https://example.com/other", Username = "user2" }
        };

        var result = UrlMatcher.FindMatchingCredentials(entries, "https://example.com/login");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FindMatchingCredentials_CaseInsensitive_Matches()
    {
        var entries = new List<PasswordEntry>
        {
            new() { Title = "Example", Url = "https://EXAMPLE.COM", Username = "user1" }
        };

        var result = UrlMatcher.FindMatchingCredentials(entries, "https://example.com");
        Assert.Single(result);
    }
}
