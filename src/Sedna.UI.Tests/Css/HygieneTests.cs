using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The library ships nothing app-specific, fetches nothing, and lets an app change typeface.
/// </summary>
public class HygieneTests
{
    [Fact]
    public void No_application_specific_naming_leaked_into_the_library()
    {
        // The library was extracted from one app's stylesheet. These names are
        // that app's; if one reappears in a selector, something app-specific came
        // along with it. Comments are stripped first — describing where a rule
        // came from is fine, shipping the app's classes is not.
        string[] forbidden =
        [
            "athene", "zbx", "sn-journal", "queue-grid", "topics-grid", "calls-grid",
            "queue-group-header", "guide-", "chooser-", "tour-pop", "tour-spot", "claim-overlay",
            "gsearch"
        ];

        var css = Assets.StripComments(Assets.Css);
        var found = forbidden
            .Where(f => css.Contains(f, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(found.Count == 0,
            "App-specific naming does not belong in the shared library — these stay in the app " +
            $"that owns them: {string.Join(", ", found)}");
    }

    /// <summary>
    /// The markup snippets in the parts' comments are demo content, and they ship.
    /// </summary>
    /// <remarks>
    /// The guard above strips comments on purpose — saying which app a rule came from
    /// is allowed. But a comment in a part is copied verbatim into
    /// <c>wwwroot/css/Sedna.UI.css</c>, which every installing app serves publicly, so
    /// a snippet written into one is published exactly as an example file is. A first
    /// name used as demo content sat in <c>69-timeline.css</c> that way. This is the
    /// same list of real things, matched against the text with its comments intact.
    /// </remarks>
    [Fact]
    public void No_real_name_is_used_as_demo_content_in_a_comment()
    {
        string[] forbidden = ["dennis", "rahmen", "netpoint", "servicenow"];

        var found = forbidden
            .Where(f => Assets.Css.Contains(f, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(found.Count == 0,
            "A comment in a part is served by every app that installs the package, so a "
            + "name in one is as published as a name in an example. Use the demo identity "
            + $"or a role instead: {string.Join(", ", found)}");
    }

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
