using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every text the sheet paints on its own tint — each badge, and inline <c>code</c> — clears AA, in
/// every palette, on every surface it sits on — a hovered row included.
/// </summary>
/// <remarks>
/// <para>
/// A tint has no contrast of its own: it is a translucent wash whose colour depends on the surface
/// under it. <c>docs/BRANDING.md</c> §7.1 measures each text token on the bare canvas, which is not
/// where this text is read — teal cleared there at 4.69 and measured 4.37 on its own tint. So this
/// composites each tint over what is under it and measures the text on what is actually painted.
/// </para>
/// <para>
/// High contrast makes every tint opaque, and colour-blind mode moves go, danger and severity 2,
/// so both are measured as palettes of their own.
/// </para>
/// </remarks>
public class TintedTextContrastTests
{
    private const double Floor = 4.5;

    private const string Advice =
        "\nTake the next ramp step away from the surface for the text token, as docs/BRANDING.md "
        + "§7.1 says — do not thin the tint to make the number pass.";

    /// <summary>
    /// Every badge <c>31-badges.css</c> paints on a tint. The text and tint tokens are read from the
    /// stylesheet, so this list says which badges are measured and never what they are painted in.
    /// </summary>
    private static readonly string[] Badges =
    [
        ".badge", ".badge-go", ".badge-warn", ".badge-danger", ".badge-info", ".badge-secret",
        ".badge-cyan", ".badge-orange", ".badge-teal",
        ".badge-sev-1", ".badge-sev-2", ".badge-sev-3", ".badge-sev-4", ".badge-sev-5",
        ".badge-ai",
    ];

    /// <summary>
    /// Where a badge is read: each canvas, and a hovered row — <c>.tr-clickable:hover</c>, the
    /// selected search result and combo option all paint <c>--bg-hover</c> under the badge's tint.
    /// </summary>
    private static readonly string[] Surfaces = ["--bg", "--card-bg", "--bg-hover"];

    private static readonly Regex Rule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

    /// <summary>
    /// Each single-class <c>.badge</c> or <c>.badge-*</c> rule that sets both a colour and a
    /// background through tokens, as (selector, text token, tint token).
    /// </summary>
    private static Dictionary<string, (string Text, string Tint)> TintedBadgeRules()
    {
        var css = Tokens.WithoutMediaBlocks(Assets.StripComments(Assets.Css));
        var rules = new Dictionary<string, (string, string)>(StringComparer.Ordinal);

        foreach (Match rule in Rule.Matches(css))
        {
            var body = rule.Groups["body"].Value;
            var text = Regex.Match(body, @"(?<![-\w])color\s*:\s*var\(\s*(?<t>--[a-z0-9-]+)\s*\)");
            var tint = Regex.Match(body, @"(?<![-\w])background\s*:\s*var\(\s*(?<t>--[a-z0-9-]+)\s*\)");
            if (!text.Success || !tint.Success) continue;

            foreach (var part in rule.Groups["selector"].Value.Split(','))
            {
                var selector = Assets.Squash(part);
                if (Regex.IsMatch(selector, @"^\.badge(-[a-z0-9-]+)?$"))
                    rules[selector] = (text.Groups["t"].Value, tint.Groups["t"].Value);
            }
        }

        return rules;
    }

    [Fact]
    public void Every_tinted_badge_in_the_sheet_is_measured()
    {
        var rules = TintedBadgeRules();

        Assert.True(Badges.All(rules.ContainsKey),
            "These badges no longer paint text on a tint through tokens: "
            + string.Join(", ", Badges.Where(b => !rules.ContainsKey(b))) + ".");
        Assert.True(rules.Keys.All(Badges.Contains),
            "These badges paint text on a tint and are not measured: "
            + string.Join(", ", rules.Keys.Where(k => !Badges.Contains(k)))
            + ". Add them to Badges.");
    }

    /// <summary>
    /// Where running text is set, and so where inline <c>code</c> is read: each canvas, a hovered
    /// <c>.tr-clickable</c> row, and a <c>.callout</c>, whose <c>--surface-soft</c> is itself a wash
    /// over the canvas. The last two read lowest in light.
    /// </summary>
    private static readonly (string Name, Func<Dictionary<string, string>, (double R, double G, double B)> Paint)[]
        RunningTextGrounds =
        [
            ("--bg", t => Tokens.Rgb(t, "--bg")),
            ("--card-bg", t => Tokens.Rgb(t, "--card-bg")),
            ("a hovered row (--bg-hover)", t => Tokens.Rgb(t, "--bg-hover")),
            (".callout on --bg", t => Tokens.Over(t, "--surface-soft", "--bg")),
            (".callout on --card-bg", t => Tokens.Over(t, "--surface-soft", "--card-bg")),
        ];

    public static IEnumerable<object[]> ThemesAndPalettes() =>
        from row in SednaThemeTests.BuiltInThemes()
        from palette in Tokens.Palettes
        select new[] { row[0], palette };

    [Theory]
    [MemberData(nameof(ThemesAndPalettes))]
    public void Every_badge_reads_at_AA_on_its_own_background(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var rules = TintedBadgeRules();
        var failures = new List<string>();

        foreach (var badge in Badges)
            foreach (var surface in Surfaces)
            {
                var (text, tint) = rules[badge];
                var painted = Tokens.Over(tokens, tint, surface);
                var ratio = Tokens.Contrast(Tokens.Rgb(tokens, text), painted);
                if (ratio < Floor)
                    failures.Add($"{badge}: {text} on {tint} over {surface} is {ratio:0.000}:1");
            }

        Assert.True(failures.Count == 0,
            $"\"{theme.Name}\", {palette}: these badges read under {Floor}:1.\n  "
            + string.Join("\n  ", failures) + Advice);
    }

    [Theory]
    [MemberData(nameof(ThemesAndPalettes))]
    public void Inline_code_reads_at_AA_wherever_running_text_is_set(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var failures = new List<string>();

        foreach (var (ground, paint) in RunningTextGrounds)
        {
            var painted = Tokens.Over(tokens, "--code-bg", paint(tokens));
            var ratio = Tokens.Contrast(Tokens.Rgb(tokens, "--code-fg"), painted);
            if (ratio < Floor)
                failures.Add($"code: --code-fg on --code-bg over {ground} is {ratio:0.000}:1");
        }

        Assert.True(failures.Count == 0,
            $"\"{theme.Name}\", {palette}: inline code reads under {Floor}:1.\n  "
            + string.Join("\n  ", failures) + Advice);
    }
}
