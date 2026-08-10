using Microsoft.AspNetCore.Components.Routing;

namespace Sedna.UI.Tests;

/// <summary>
/// The address matching that used to live inside NavItem. It is the one thing
/// hand-written navigation markup cannot express, which is why it survived the
/// components.
/// </summary>
public class ActiveLinkTests
{
    private static string Absolute(string relative) =>
        new Uri(new Uri("http://localhost/"), relative).AbsoluteUri;

    [Theory]
    // Exact match, and a trailing slash on either side, are one address.
    [InlineData("http://localhost/queue", "queue", NavLinkMatch.All, true)]
    [InlineData("http://localhost/queue/", "queue", NavLinkMatch.All, true)]
    [InlineData("http://localhost/queue", "queue/", NavLinkMatch.All, true)]
    // A query string or a fragment does not change which page you are on.
    [InlineData("http://localhost/queue?page=2", "queue", NavLinkMatch.All, true)]
    [InlineData("http://localhost/queue#top", "queue", NavLinkMatch.All, true)]
    // All matches only the page itself; Prefix also matches below it.
    [InlineData("http://localhost/queue/42", "queue", NavLinkMatch.All, false)]
    [InlineData("http://localhost/queue/42", "queue", NavLinkMatch.Prefix, true)]
    // The boundary check: a prefix must end on a path segment.
    [InlineData("http://localhost/queue-archive", "queue", NavLinkMatch.Prefix, false)]
    [InlineData("http://localhost/queued", "queue", NavLinkMatch.Prefix, false)]
    // The root link with the default Prefix is active everywhere, which is why it
    // needs Match="All" — the same trap the framework's NavLink has.
    [InlineData("http://localhost/queue", "", NavLinkMatch.Prefix, true)]
    [InlineData("http://localhost/queue", "", NavLinkMatch.All, false)]
    [InlineData("http://localhost/", "", NavLinkMatch.All, true)]
    public void Matches_follows_the_address(
        string current, string href, NavLinkMatch match, bool expected) =>
        Assert.Equal(expected, ActiveLink.Matches(current, Absolute(href), match));

    [Fact]
    public void Matching_is_case_insensitive()
    {
        // A URL path is case-sensitive in the spec and not in practice: ASP.NET
        // Core routing matches case-insensitively, so a link the router considers
        // current has to look current.
        Assert.True(ActiveLink.Matches("http://localhost/Queue", Absolute("queue")));
    }

    [Fact]
    public void Matches_rejects_a_null_address()
    {
        Assert.Throws<ArgumentNullException>(() => ActiveLink.Matches(null!, "http://localhost/"));
        Assert.Throws<ArgumentNullException>(() => ActiveLink.Matches("http://localhost/", null!));
    }
}
