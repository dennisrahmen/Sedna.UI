using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <see cref="SednaContract"/> lists exactly what the shipped stylesheet declares, so an app
/// that holds its markup to it is held to the real thing.
/// </summary>
/// <remarks>
/// Read independently of the class history: the names come from the stylesheet itself. A
/// class or a token added to the parts and missing from the contract — the history not
/// regenerated, or its extraction drifting — fails here rather than in an app's test.
/// </remarks>
public class SednaContractTests
{
    private static string Css => Assets.StripComments(Assets.Css);

    [Fact]
    public void Every_class_the_stylesheet_declares_is_in_the_contract_and_nothing_else_is()
    {
        var declared = Assets.ClassSelectors(Css);
        var listed = SednaContract.Classes.Keys.ToHashSet(StringComparer.Ordinal);

        Assert.Empty(declared.Except(listed, StringComparer.Ordinal));
        Assert.Empty(listed.Except(declared, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_token_the_stylesheet_declares_is_in_the_contract()
    {
        var declared = Assets.TokenBlocks(Css)
            .SelectMany(b => Regex.Matches(b.Body, @"(?<![\w-])(--[a-z0-9-]+)\s*:").Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(declared);
        Assert.Empty(declared.Except(SednaContract.Tokens.Keys, StringComparer.Ordinal));
    }

    [Fact]
    public void A_name_from_an_early_release_carries_that_release()
    {
        Assert.False(string.IsNullOrEmpty(SednaContract.LatestRelease));
        // .btn has been in every release; a null here means the history saw no tag at all.
        Assert.NotNull(SednaContract.Classes["btn"]);
        Assert.NotNull(SednaContract.Tokens["--brand"]);
    }
}
