using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// One idea ships under one name.
/// </summary>
/// <remarks>
/// <para>
/// The rule is in <c>docs/releasing.md</c>: do not ship the same idea twice under two
/// names because renaming the first would break someone. <c>.user-menu-*</c> was
/// removed rather than left beside <c>.menu-*</c> for exactly that reason.
/// </para>
/// <para>
/// The case this guards is the one that already happened. <c>.card-span-full</c> was
/// the only way to make a grid child take the whole row, so a form field in a
/// <c>.field-grid</c> wore a card's class — and the obvious repair, adding
/// <c>.field-span-full</c> beside it, would have been one declaration under two names,
/// with two catalogue entries and two chances to diverge. It was renamed instead.
/// </para>
/// </remarks>
public class OneIdeaOneNameTests
{
    [Fact]
    public void Only_one_class_takes_the_whole_row_of_a_grid()
    {
        var css = Assets.StripComments(Assets.Css);

        var selectors = Regex.Matches(
                css,
                @"(?<selector>[^{}]+)\{(?<body>[^{}]*grid-column\s*:\s*1\s*/\s*-1[^{}]*)\}",
                RegexOptions.Compiled)
            .Select(m => Assets.Squash(m.Groups["selector"].Value))
            .ToList();

        Assert.True(
            selectors.Count == 1 && selectors[0] == ".sedna-span-full",
            "`grid-column: 1 / -1` is one idea and takes one name, `.sedna-span-full`, so a card "
            + "grid, a field grid and an app's own grid all reach for the same class. These "
            + $"selectors declare it: {string.Join(" / ", selectors)}");
    }
}
