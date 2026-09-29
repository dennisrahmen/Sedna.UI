using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every text the sheet paints on its own tint — each badge, and inline <c>code</c> — clears AA, in
/// every palette, on the surfaces it sits on.
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

    /// <summary>Each badge, as the text token and the background token <c>31-badges.css</c> pairs.</summary>
    private static readonly (string Badge, string Text, string Background)[] Badges =
    [
        (".badge", "--muted", "--badge-bg"),
        (".badge-go", "--go-fg", "--go-bg"),
        (".badge-warn", "--warn-fg", "--warn-bg"),
        (".badge-danger", "--danger-fg", "--danger-bg"),
        (".badge-info", "--info-fg", "--info-bg"),
        (".badge-secret", "--secret-fg", "--secret-bg"),
        (".badge-cyan", "--badge-cyan-fg", "--badge-cyan-bg"),
        (".badge-orange", "--badge-orange-fg", "--badge-orange-bg"),
        (".badge-teal", "--badge-teal-fg", "--badge-teal-bg"),
        (".badge-sev-1", "--sev-1-fg", "--sev-1-bg"),
        (".badge-sev-2", "--sev-2-fg", "--sev-2-bg"),
        (".badge-sev-3", "--sev-3-fg", "--sev-3-bg"),
        (".badge-sev-4", "--sev-4-fg", "--sev-4-bg"),
        (".badge-sev-5", "--sev-5-fg", "--sev-5-bg"),
    ];

    private static readonly string[] Surfaces = ["--bg", "--card-bg"];

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
        var failures = new List<string>();

        foreach (var (badge, text, background) in Badges)
            foreach (var surface in Surfaces)
            {
                var painted = Tokens.Over(tokens, background, surface);
                var ratio = Tokens.Contrast(Tokens.Rgb(tokens, text), painted);
                if (ratio < Floor)
                    failures.Add($"{badge}: {text} on {background} over {surface} is {ratio:0.000}:1");
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
