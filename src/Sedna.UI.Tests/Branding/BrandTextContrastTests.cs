using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every rule that paints in a brand colour, measured against the surface it actually renders
/// on — in both variants, for the built-in themes and for a generated one.
/// </summary>
/// <remarks>
/// <para>
/// <c>TokenTierTests.A_near_floor_pair_still_clears_AA</c> measures six token pairs
/// <c>docs/BRANDING.md</c> §7.1 names. That is a list, and the gap it left is the one this file
/// closes: <c>--brand-soft</c> is solved against <b>white</b> — it is the step that carries white
/// text — and <c>.markdown-body a</c> used it as body text, where what matters is the ratio
/// against the canvas underneath. On the light canvas that measured 4.35:1, under AA, for the
/// library's own brand and for every generated theme alike, and no pair in the list covered it.
/// </para>
/// <para>
/// So this derives the pairs from the stylesheet instead: every rule whose <c>color</c> resolves
/// through a brand token, paired with the tint it sits on and each surface the library paints.
/// A new rule in a brand colour has to be listed in <see cref="Uses"/> before the suite passes,
/// which is what makes the coverage a property of the sheet rather than of somebody's memory.
/// </para>
/// <para>
/// <b>Text and icons have different floors</b> — WCAG 1.4.3 asks 4.5:1 of text, 1.4.11 asks 3:1
/// of a graphic. An icon in <c>--brand-soft</c> is fine; the same colour as a paragraph's link is
/// not, and collapsing the two would either fail the icons or excuse the text.
/// </para>
/// </remarks>
public class BrandTextContrastTests
{
    /// <summary>WCAG 2.1 AA for text (1.4.3).</summary>
    private const double TextFloor = 4.5;

    /// <summary>WCAG 2.1 AA for a non-text graphic (1.4.11) — an icon glyph.</summary>
    private const double IconFloor = 3.0;

    /// <summary>
    /// The surfaces the library paints content on. A rule does not say which of them it will be
    /// placed over, so every one is measured: a chip is as legitimate on the page ground as in a
    /// card or a menu.
    /// </summary>
    private static readonly string[] Canvases = ["--bg", "--card-bg", "--bg-elevated", "--sidebar-bg"];

    private enum Ink
    {
        /// <summary>Read as text: 4.5:1.</summary>
        Text,

        /// <summary>An icon glyph or a mark: 3:1.</summary>
        Icon,
    }

    /// <summary>
    /// Every rule in the sheet that paints in a brand colour, with the tints painted under it.
    /// </summary>
    /// <remarks>
    /// The selector is the key and the fixture is only the <i>context</i> — which surface the rule
    /// puts under its own text, and whether that text is read or looked at. The colour itself is
    /// read from the stylesheet, so this table cannot claim a rule is safe by describing it
    /// wrongly.
    /// </remarks>
    private static readonly Dictionary<string, (Ink Kind, string[] Tints)> Uses = new(StringComparer.Ordinal)
    {
        [".markdown-body a"] = (Ink.Text, []),
        [".nav-link.active"] = (Ink.Text, ["--brand-tint"]),
        [".nav-link.active i"] = (Ink.Icon, ["--brand-tint"]),
        [".nav-area[aria-expanded=\"true\"]"] = (Ink.Text, ["--brand-tint"]),
        [".nav-area.active"] = (Ink.Text, ["--brand-tint"]),
        // The current bottom-bar item: on the bar's own surface, or on the tint in .bottombar--tint.
        [".bottombar-item.active i"] = (Ink.Icon, ["--brand-tint"]),
        [".bottombar-item[aria-current=\"page\"] i"] = (Ink.Icon, ["--brand-tint"]),
        [".bottombar--tint .bottombar-item.active"] = (Ink.Text, ["--brand-tint"]),
        [".bottombar--tint .bottombar-item[aria-current=\"page\"]"] = (Ink.Text, ["--brand-tint"]),
        [".bottombar-accessory > i"] = (Ink.Icon, []),
        [".user-avatar"] = (Ink.Text, ["--brand-tint"]),
        [".avatar"] = (Ink.Text, ["--brand-tint"]),
        [".dropzone--over"] = (Ink.Text, ["--brand-tint"]),
        [".dropzone--over i"] = (Ink.Icon, ["--brand-tint"]),
        // A chosen row is tinted, and pointing at it or arrowing onto it deepens the tint — so
        // the label sits on either one.
        [".form-select option:checked"] = (Ink.Text, ["--brand-tint", "--brand-tint-strong"]),
        [".form-select option::checkmark"] = (Ink.Icon, ["--brand-tint", "--brand-tint-strong"]),
        [".tab--active"] = (Ink.Text, []),
        [".tab[aria-selected=\"true\"]"] = (Ink.Text, []),
        [".tab--active:hover"] = (Ink.Text, []),
        [".tab[aria-selected=\"true\"]:hover"] = (Ink.Text, []),
        [".tab--active .tab-count"] = (Ink.Text, ["--brand-tint"]),
        [".tab[aria-selected=\"true\"] .tab-count"] = (Ink.Text, ["--brand-tint"]),
        // The "Clear all" link in a combo panel's head line, on the panel's own ground.
        [".form-combo-head button"] = (Ink.Text, []),
        [".chip--active"] = (Ink.Text, ["--brand-tint"]),
        [".chip--active i"] = (Ink.Icon, ["--brand-tint"]),
        [".chip--active .chip-dismiss"] = (Ink.Icon, ["--brand-tint"]),
        ["a.stat:hover .stat-value"] = (Ink.Text, []),
        [".page-link[aria-current=\"page\"]"] = (Ink.Text, ["--brand-tint"]),
        [".table th[aria-sort=\"ascending\"]"] = (Ink.Text, ["--table-head-bg"]),
        [".table th[aria-sort=\"descending\"]"] = (Ink.Text, ["--table-head-bg"]),
        [".steps > li[data-state=\"current\"]::before"] = (Ink.Text, []),
        [".tree-leaf[aria-current=\"true\"]"] = (Ink.Text, ["--brand-tint"]),
        [".palette-item[aria-selected=\"true\"]"] = (Ink.Text, ["--brand-tint"]),
        [".palette-item[aria-selected=\"true\"] i"] = (Ink.Icon, ["--brand-tint"]),
        [".timeline > li[data-state=\"current\"] > .timeline-mark"] = (Ink.Icon, []),
    };

    /// <summary>
    /// Every built-in theme, plus two generated ones — the path a consuming app's own brand takes.
    /// </summary>
    /// <remarks>
    /// <c>#d62828</c> is a neutral red standing in for a brand colour; it names nobody. Both
    /// generated ramps are built the way a theme builds one: the contrast-solved brand step
    /// <see cref="SednaTheme.Forest"/> and <see cref="SednaTheme.Cobalt"/> use, and the exact
    /// anchor a mandated hex needs. A generated ramp is the case that cannot be eyeballed — the
    /// shipped palette was measured by hand once, and a generated one never was.
    /// </remarks>
    public static IEnumerable<object[]> ThemesAndVariants()
    {
        var themes = new (string Name, SednaPalette Palette)[]
        {
            ("sedna", SednaTheme.Sedna.Palette),
            ("forest", SednaTheme.Forest.Palette),
            ("cobalt", SednaTheme.Cobalt.Palette),
            ("graphite", SednaTheme.Graphite.Palette),
            ("generated", WithBrand(SednaRamp.FromAnchor("#d62828", 500,
                contrastSolvedStep: new ContrastSolvedStep(600, "#ffffff", 4.6)))),
            ("generated-exact", WithBrand(SednaRamp.FromAnchor("#d62828", 600, exactAnchor: true))),
        };

        foreach (var (name, palette) in themes)
        {
            yield return [name, palette, Tokens.Dark];
            yield return [name, palette, Tokens.Light];
        }
    }

    [Theory]
    [MemberData(nameof(ThemesAndVariants))]
    public void Every_brand_coloured_rule_clears_its_floor_on_the_surface_it_sits_on(
        string theme, SednaPalette palette, string variant)
    {
        var tokens = Tokens.Resolved(variant, palette);
        var failures = new List<string>();

        foreach (var (selector, token) in BrandColouredRules())
        {
            // An unlisted rule is the other test's failure, not a silent pass here.
            if (!Uses.TryGetValue(selector, out var use)) continue;

            var floor = use.Kind == Ink.Text ? TextFloor : IconFloor;
            var ink = Tokens.Rgb(tokens, token);

            foreach (var canvas in Canvases)
            {
                foreach (var ground in Grounds(tokens, use.Tints, canvas))
                {
                    var ratio = Tokens.Contrast(ink, ground.Colour);
                    if (ratio >= floor) continue;

                    failures.Add(
                        $"{selector} — {token} on {ground.Name} is {ratio:0.00}:1, under {floor:0.0}");
                }
            }
        }

        Assert.True(failures.Count == 0,
            $"\"{theme}\", {variant} variant — brand colour measured against the surface under it:\n  "
            + string.Join("\n  ", failures)
            + "\n\nA brand token solved against white says nothing about reading it on the canvas. "
            + "Text takes --brand-text, the role measured against the surface; if that role itself "
            + "misses the floor, take the next ramp step rather than adjusting by eye, and if a tint "
            + "under it is what closed the gap, weaken the tint — see docs/BRANDING.md §7.1.");
    }

    [Fact]
    public void Every_brand_coloured_rule_is_listed_with_the_surface_it_sits_on()
    {
        // Both directions. Without the first, a new rule in a brand colour is never measured;
        // without the second, the table keeps describing a rule somebody deleted, and the
        // coverage it claims is imaginary.
        var inSheet = BrandColouredRules().Select(r => r.Selector).ToHashSet(StringComparer.Ordinal);

        var unmeasured = inSheet.Except(Uses.Keys, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var stale = Uses.Keys.Except(inSheet, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.True(unmeasured.Count == 0 && stale.Count == 0,
            "The brand-colour table has drifted from the stylesheet.\n"
            + (unmeasured.Count > 0
                ? "  Rules painting in a brand colour that nothing measures:\n    "
                  + string.Join("\n    ", unmeasured) + "\n"
                : string.Empty)
            + (stale.Count > 0
                ? "  Listed but no longer in the sheet:\n    " + string.Join("\n    ", stale) + "\n"
                : string.Empty)
            + "\nAdd the rule to Uses with the tint painted under it, or drop the stale entry.");
    }

    [Fact]
    public void Only_the_role_measured_against_the_canvas_paints_text()
    {
        // The structural half, and the one that would have caught .markdown-body a on its own:
        // --brand-soft and --brand are display roles — an icon, a spinner arc, a focus border, a
        // filled button's background — and each is solved against something other than the
        // canvas. Text in a brand colour takes --brand-text, whatever the measurement says today.
        var offenders = BrandColouredRules()
            .Where(r => Uses.TryGetValue(r.Selector, out var use) && use.Kind == Ink.Text)
            .Where(r => r.Token != "--brand-text")
            .Select(r => $"{r.Selector} paints text with {r.Token}")
            .ToList();

        Assert.True(offenders.Count == 0,
            string.Join("\n  ", offenders)
            + "\n\n--brand-text is the brand role measured against the surface text renders on. "
            + "--brand-soft is the display strength for icons and borders, solved against white in "
            + "the light variant, and reads as text at under 4.5:1 on a light canvas.");
    }

    /// <summary>
    /// Every rule whose <c>color</c> resolves through a brand token, as (selector, token), one
    /// entry per selector in a selector list.
    /// </summary>
    /// <remarks>
    /// <c>@media</c> blocks are excised first: <c>forced-colors</c> hands the palette to the OS and
    /// <c>prefers-contrast</c> is a preference on top of a variant, so neither is a state this can
    /// measure. The lookbehind is what keeps <c>border-color</c> and <c>border-top-color</c> out —
    /// a border is not text, and including them would drown the real pairs.
    /// </remarks>
    private static IEnumerable<(string Selector, string Token)> BrandColouredRules()
    {
        var css = Tokens.WithoutMediaBlocks(Assets.StripComments(Assets.Css));

        foreach (Match rule in Regex.Matches(css, @"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}"))
        {
            var colour = Regex.Match(rule.Groups["body"].Value,
                @"(?<![-\w])color\s*:\s*var\(\s*(?<token>--brand[a-z0-9-]*)\s*\)");
            if (!colour.Success) continue;

            foreach (var part in rule.Groups["selector"].Value.Split(','))
            {
                var selector = Assets.Squash(part);
                if (selector.Length > 0) yield return (selector, colour.Groups["token"].Value);
            }
        }
    }

    /// <summary>The surfaces one rule's ink actually lands on: the bare canvas, or each tint over it.</summary>
    private static IEnumerable<(string Name, (double R, double G, double B) Colour)> Grounds(
        Dictionary<string, string> tokens, string[] tints, string canvas)
    {
        if (tints.Length == 0)
        {
            yield return (canvas, Tokens.Rgb(tokens, canvas));
            yield break;
        }

        foreach (var tint in tints)
            yield return ($"{tint} over {canvas}", Tokens.Over(tokens, tint, canvas));
    }

    /// <summary>Sedna's palette with one ramp swapped in for coral — how a themed brand arrives.</summary>
    private static SednaPalette WithBrand(SednaRamp coral)
    {
        var s = SednaTheme.Sedna.Palette;
        return new SednaPalette(s.Slate, coral, s.Orbit, s.Navy, s.Green, s.Amber, s.Crimson,
            s.Violet, s.Cyan, s.Orange, s.Teal, s.Indigo);
    }
}
