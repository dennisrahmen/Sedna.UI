using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every badge's text clears AA on its own background, in every palette, on both surfaces a badge
/// sits on.
/// </summary>
/// <remarks>
/// <para>
/// A badge paints its text on its own tint, and a tint has no contrast of its own: it is a
/// translucent wash whose colour depends on the surface under it. <c>docs/BRANDING.md</c> §7.1
/// measures each text token on the bare canvas, which is not where a badge's text is read — teal
/// cleared there at 4.69 and measured 4.37 on its own tint. So this composites each tint over
/// <c>--bg</c> and <c>--card-bg</c> and measures the text on what is actually painted.
/// </para>
/// <para>
/// High contrast makes every tint opaque, and colour-blind mode moves go, danger and severity 2,
/// so both are measured as palettes of their own.
/// </para>
/// </remarks>
public class BadgeContrastTests
{
    private const double Floor = 4.5;

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
            $"\"{theme.Name}\", {palette}: these badges read under {Floor}:1.\n  " + string.Join("\n  ", failures)
            + "\nTake the next ramp step away from the surface for the text token, as docs/BRANDING.md "
            + "§7.1 says — do not thin the tint to make the number pass.");
    }
}
