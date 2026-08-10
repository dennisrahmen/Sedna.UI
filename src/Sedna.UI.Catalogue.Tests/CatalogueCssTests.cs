using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// <c>wwwroot/catalogue.css</c> is the docs' own chrome and may only style
/// <c>.cat-*</c> and <c>.ex-*</c>.
/// </summary>
/// <remarks>
/// <para>
/// The repository's own <c>CLAUDE.md</c> and the catalogue's have both said a test
/// enforces this since the file existed. Neither was true, which is the more
/// interesting failure: a documented guard that does not run is worse than no guard,
/// because it is the reason nobody checks by hand.
/// </para>
/// <para>
/// What it protects is not tidiness. An example that looks better here than in the app
/// that copies it is a lie told by the documentation — and the copy has no
/// <c>catalogue.css</c> to explain the difference. The tokens are the deliberate
/// exception: every colour here is a library token, which is what makes the docs follow
/// the theme toggles.
/// </para>
/// </remarks>
public class CatalogueCssTests
{
    [Fact]
    public void The_catalogue_stylesheet_only_styles_its_own_chrome()
    {
        var css = Assets.StripComments(File.ReadAllText(CatalogueAssets.CatalogueCssPath));

        var offenders = new List<string>();

        foreach (var selector in TopLevelSelectors(css))
        {
            // Two conditions, and both matter. The selector has to be anchored in
            // chrome at all — a bare `h2 { }` here would restyle every heading in every
            // example. And its SUBJECT, the last compound, is the element the rule
            // actually paints: `.cat-note code` styling a <code> inside a note is fine
            // and can never reach an example, while `.ex-demo .modal` would paint a
            // .modal, which is precisely the markup a reader copies.
            if (!Classes(selector).Any(IsChrome))
            {
                offenders.Add($"{selector}   (not scoped to .cat-* / .ex-*)");
                continue;
            }

            var subject = selector.Split([' ', '>', '+', '~'], StringSplitOptions.RemoveEmptyEntries)[^1];
            var foreign = Classes(subject).Where(c => !IsChrome(c)).ToList();

            if (foreign.Count > 0)
                offenders.Add($"{selector}   (paints .{string.Join(", .", foreign)})");
        }

        Assert.True(offenders.Count == 0,
            "catalogue.css may only style .cat-* and .ex-*. These rules land on markup a reader "
            + "copies, so an example would look better here than in the app that copied it:"
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Fact]
    public void Every_colour_in_the_catalogue_stylesheet_is_a_library_token()
    {
        // The same rule the library holds itself to, for the same reason: the docs have
        // to follow the theme toggles, and a hex here is a value that stays put when
        // the palette moves.
        var css = Assets.StripComments(File.ReadAllText(CatalogueAssets.CatalogueCssPath));

        var offenders = System.Text.RegularExpressions.Regex
            .Matches(css, @"#[0-9a-f]{3,8}\b|\brgba?\(|\bhsla?\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Use a library token: {string.Join(", ", offenders.Distinct())}");
    }

    [Fact]
    public void The_scan_sees_the_stylesheet()
    {
        // The vacuity guard. Both tests above pass on an empty file.
        Assert.True(TopLevelSelectors(
            Assets.StripComments(File.ReadAllText(CatalogueAssets.CatalogueCssPath))).Count >= 20);
    }

    /// <summary>Every class name in a selector — the tokens that follow a dot.</summary>
    private static IEnumerable<string> Classes(string selector) =>
        System.Text.RegularExpressions.Regex
            .Matches(selector, @"\.(-?[A-Za-z][\w-]*)")
            .Select(m => m.Groups[1].Value);

    private static bool IsChrome(string className) =>
        className.StartsWith("cat-", StringComparison.Ordinal)
        || className.StartsWith("ex-", StringComparison.Ordinal);

    /// <summary>
    /// Every individual selector in the file, media blocks included, one per entry.
    /// </summary>
    private static List<string> TopLevelSelectors(string css)
    {
        var bodies = new List<string> { css };
        bodies.AddRange(Assets.MediaBlocks(css).Select(m => m.Body));

        return bodies
            .SelectMany(Assets.TopLevelRules)
            .Select(r => r.Selector)
            // @media preludes survive as a "selector" when the outer pass reaches
            // them; the inner pass is what reads their contents.
            .Where(s => !s.TrimStart().StartsWith('@'))
            .SelectMany(s => s.Split(','))
            .Select(Assets.Squash)
            .Where(s => s.Length > 0)
            .ToList();
    }
}
