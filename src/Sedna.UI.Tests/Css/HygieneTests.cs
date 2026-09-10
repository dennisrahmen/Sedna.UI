using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The library fetches nothing and lets an app change typeface.
/// </summary>
/// <remarks>
/// There used to be a deny-list of application and organisation names here, and there is
/// deliberately no longer one: a literal list is itself a published list of the names it
/// forbids, in a public repository. What replaces it is the <c>No real names</c> section of
/// the repo's <c>CLAUDE.md</c> as a review rule, and the SHAPE guards in
/// <c>ScriptContractTests</c>, which match a ticket format, a company's legal form or an
/// internal DNS label without naming anybody to do it.
/// </remarks>
public class HygieneTests
{
    [Fact]
    public void Fonts_ride_tokens_so_an_app_can_change_typeface()
    {
        var css = Assets.StripComments(Assets.Css);

        // font-family: inherit is fine — a control adopting its host's font.
        var offenders = Assets.LinesOutsideTokenBlocks(css)
            .Where(x => Regex.IsMatch(x.Line, @"font-family\s*:"))
            .Where(x => !x.Line.Contains("var(--font-", StringComparison.Ordinal))
            .Where(x => !Regex.IsMatch(x.Line, @"font-family\s*:\s*inherit"))
            .Select(x => $"line {x.Number}: {x.Line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "Use var(--font-sans) / var(--font-mono) so a consuming app can change typeface: " +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Fact]
    public void The_stylesheet_neither_loads_nor_inlines_anything()
    {
        // Two rules in one. `url(` would either fetch at runtime — which no customer
        // site may depend on — or point at a file that has to be packed and version-
        // matched. `data:` is the sneakier half: an inlined SVG is still a shipped
        // asset, and it smuggles a colour past the three colour patterns above,
        // because a percent-encoded `%23fff` carries no literal `#` and base64
        // carries nothing recognisable at all.
        var css = Assets.StripComments(Assets.Css);

        var offenders = css.Split('\n')
            .Select((line, index) => (Line: line.Trim(), Number: index + 1))
            .Where(x => x.Line.Contains("url(", StringComparison.OrdinalIgnoreCase)
                     || x.Line.Contains("data:", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"line {x.Number}: {x.Line}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "The stylesheet must reference no external file and inline no asset. Draw the mark in " +
            "CSS, or use a glyph from the bundled icon font on a pseudo-element. An inlined data: " +
            $"URI also hard-codes a colour the token contract forbids:{Environment.NewLine}" +
            string.Join(Environment.NewLine, offenders));
    }
}
