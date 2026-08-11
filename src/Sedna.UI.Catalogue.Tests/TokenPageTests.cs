using System.Text.Json;
using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The Tokens page shows every token the library declares, and nothing it does not.
/// </summary>
/// <remarks>
/// <para>
/// <c>TokenGroups</c>'s own summary has claimed since it was written that this test
/// existed. It did not — and 45 of the 145 exported tokens were missing from the page
/// as a result: the whole spacing scale, the whole type scale, every radius, every
/// duration. <c>docs/getting-started.md</c> calls that page "the full token list", so
/// the omission was not a matter of taste.
/// </para>
/// <para>
/// Compared against the token <b>export</b> rather than against a regex over the
/// stylesheet, because the export is the same artefact the MCP server and any design
/// tool read, and it already contains only <c>:root</c> declarations — a
/// component-local custom property like <c>--avatar-size</c> is not a token and must
/// not be listed as one.
/// </para>
/// </remarks>
public class TokenPageTests
{
    private static ISet<string> Exported()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Assets.TokensPath));

        return document.RootElement.GetProperty("blocks").EnumerateArray()
            .SelectMany(b => b.GetProperty("tokens").EnumerateObject().Select(t => t.Name))
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void Every_declared_token_is_on_the_page()
    {
        var missing = Exported()
            .Except(TokenGroups.AllTokens, StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"{missing.Count} tokens ship but are on no group of the Tokens page, which the docs "
            + $"call the full token list: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_token_on_the_page_is_one_the_library_declares()
    {
        // The other direction. A renamed token leaves the old name behind, and the page
        // then shows a row reading "not declared" — which looks like a library bug.
        var stale = TokenGroups.AllTokens
            .Except(Exported(), StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            $"The Tokens page lists tokens the library does not declare: {string.Join(", ", stale)}");
    }

    [Fact]
    public void No_token_is_listed_twice()
    {
        var duplicates = TokenGroups.AllTokens
            .GroupBy(t => t, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0,
            $"Listed under more than one heading: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void The_page_has_groups_and_they_all_have_tokens()
    {
        // The vacuity guard, and a real one: an empty group renders a heading over
        // nothing.
        Assert.True(TokenGroups.All.Count >= 10);

        foreach (var group in TokenGroups.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(group.Name));
            Assert.NotEmpty(group.Tokens);
        }
    }
}
