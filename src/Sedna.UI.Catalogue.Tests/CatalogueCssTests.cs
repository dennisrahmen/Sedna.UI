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
            // actually paints: `.cat-tile-text strong` styling a <strong> inside a
            // tile is fine and can never reach an example, while `.ex-demo .modal`
            // would paint a .modal, which is precisely the markup a reader copies.
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

    /// <summary>
    /// Inside a container that holds a rendered example, a rule whose subject is a bare
    /// element must reach it with the child combinator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The test above checks the subject's CLASSES, so a subject with no class at all
    /// walks straight past it — and <c>.cat-main h3</c> did, for as long as the file
    /// existed. It set <c>margin: 26px 0 10px</c> and <c>font-size: 14px</c> on every
    /// <c>h3</c> inside <c>.cat-main</c>, which includes the <c>.modal-header</c>,
    /// <c>.drawer-header</c>, <c>.markdown-body</c> and <c>.prose</c> headings of every
    /// example the page renders. A modal header measured 111px here against the 61px it
    /// is in an app.
    /// </para>
    /// <para>
    /// The reason it wins is not specificity: this file is UNLAYERED and the library is
    /// entirely inside <c>@layer sedna.*</c>, so any rule here outranks any rule there
    /// whatever the two selectors weigh. There is no way for the library to defend
    /// itself, and nothing fails — the example simply renders wrong, only on this site.
    /// </para>
    /// <para>
    /// A page heading is always a direct child of <c>.cat-main</c>; nothing an example
    /// renders ever is. So the child combinator is exactly the line between the two, and
    /// it is only required under the containers that actually hold example markup —
    /// <c>.cat-tiles a</c> is fine, because a tile never contains an example.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_bare_element_inside_an_example_container_is_reached_by_the_child_combinator()
    {
        var css = Assets.StripComments(File.ReadAllText(CatalogueAssets.CatalogueCssPath));

        var offenders = new List<string>();

        foreach (var selector in TopLevelSelectors(css))
        {
            var parts = Split(selector);

            // Only the subject matters — it is the element the rule paints. Anything
            // with a class of its own is the other test's business.
            if (parts.Count == 0 || HasName(parts[^1])) continue;

            // Walk back from the subject to the nearest container that holds example
            // markup. Everything between the two has to be a child combinator; one
            // descendant step anywhere on that path reaches into the example.
            for (var i = parts.Count - 3; i >= 0; i -= 2)
            {
                if (!ExampleContainers.Contains(Compound(parts[i]))) continue;

                if (parts.Where((p, j) => j > i && j < parts.Count && j % 2 == 1).Any(c => c != ">"))
                    offenders.Add($"{selector}   (reaches into {Compound(parts[i])})");

                break;
            }
        }

        Assert.True(offenders.Count == 0,
            "catalogue.css is unlayered, so a descendant selector here outranks the library "
            + "inside every example this site renders. Use the child combinator — a page heading "
            + $"is a direct child, an example's markup never is:{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>The containers a rendered example lives inside.</summary>
    private static readonly HashSet<string> ExampleContainers =
        new(StringComparer.Ordinal) { ".cat-main", ".cat-ex", ".ex-demo" };

    /// <summary>Compounds and combinators, alternating, starting with a compound.</summary>
    private static List<string> Split(string selector)
    {
        var parts = System.Text.RegularExpressions.Regex
            .Split(Assets.Squash(selector), @"\s*([>+~])\s*|\s+")
            .Where(p => p is not null)
            .Select(p => p.Trim())
            .ToList();

        // Regex.Split drops the descendant combinator (it has no character of its own),
        // so it is put back to keep the alternation the loop above relies on.
        var alternating = new List<string>();
        foreach (var part in parts.Where(p => p.Length > 0))
        {
            if (alternating.Count % 2 == 1 && part is not (">" or "+" or "~")) alternating.Add(" ");
            alternating.Add(part);
        }
        return alternating;
    }

    /// <summary>The compound without its pseudo-classes and pseudo-elements.</summary>
    private static string Compound(string part) => part.Split(':')[0];

    /// <summary>Whether a compound names something of its own — a class, an id or an attribute.</summary>
    private static bool HasName(string part) =>
        Compound(part).IndexOfAny(['.', '#', '[']) >= 0;

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
