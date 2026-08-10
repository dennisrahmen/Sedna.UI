namespace Sedna.UI.Tests;

/// <summary>
/// <c>--brand</c> resolves to <c>coral-600</c> (<c>01-tokens.css</c>), and that is the background
/// of <c>.btn-primary</c> whose label is <c>--on-solid</c> — pure white. WCAG AA needs 4.5:1 for
/// that pairing.
/// </summary>
/// <remarks>
/// <para>
/// Sedna's own <c>coral-600</c> clears it (barely) only because <c>docs/BRANDING.md</c> §3.1 says
/// it was hand-solved for the boundary: "the darkest-chroma point on the Sedna Red hue that still
/// carries white text at AA". <see cref="SednaRamp.FromAnchor"/> does not do that — it takes
/// lightness from a fixed per-step curve shared by every hue, with no per-hue contrast check at
/// all. Two different hues at the same OKLCH lightness can sit far apart in WCAG relative
/// luminance (which weights R/G/B 0.2126/0.7152/0.0722, not evenly), so a curve fit to clear AA
/// for one hue has no reason to clear it for another.
/// </para>
/// <para>
/// This is exactly what happens to <see cref="SednaTheme.Forest"/> (green anchor) and
/// <see cref="SednaTheme.Cobalt"/> (blue anchor): both generate their <c>coral-600</c> — the
/// brand slot's step, regardless of what hue it holds — from the curve, and both land under 4.5:1.
/// </para>
/// </remarks>
public class SednaThemeContrastTests
{
    [Theory]
    [MemberData(nameof(SednaThemeTests.BuiltInThemes), MemberType = typeof(SednaThemeTests))]
    public void The_brand_step_carries_white_text_at_AA(SednaTheme theme)
    {
        var brandHex = theme.Palette.Coral[600];
        var ratio = Contrast("#ffffff", brandHex);

        Assert.True(ratio >= 4.5,
            $"\"{theme.Name}\"'s coral-600 ({brandHex}) is white-on-brand {ratio:0.00}:1, below the "
            + "4.5:1 WCAG AA floor a .btn-primary label (--on-solid, white) needs against --brand. "
            + "See docs/BRANDING.md §3.1 — the step has to be solved for contrast, not read off "
            + "the shared lightness curve.");
    }

    /// <summary>
    /// Every filled control whose label is <c>--on-solid</c> (white), and the ramp step the
    /// semantic tier points at for its background. Read off <c>01-tokens.css</c>.
    /// </summary>
    /// <remarks>
    /// The brand is not the only white-on-solid pair. <c>--go-solid</c>, <c>--warn-solid</c> and
    /// <c>--danger-solid</c> are filled backgrounds under the same white label, and a theme that
    /// regenerates one of those families — as <see cref="SednaTheme.Forest"/> does with
    /// <c>green</c>, to clear the brand-hue collision — puts a generated colour under white text
    /// without anything having checked it. Solving the brand step alone would leave
    /// "Send" inaccessible while "Save" was fine, which is a worse failure than both being wrong
    /// because it looks deliberate.
    /// </remarks>
    public static IEnumerable<object[]> WhiteOnSolidPairs()
    {
        foreach (var theme in new[] { SednaTheme.Sedna, SednaTheme.Forest, SednaTheme.Cobalt })
        {
            yield return [theme, "--brand", "coral", 600];
            yield return [theme, "--go-solid", "green", 700];
            yield return [theme, "--warn-solid", "amber", 700];
            yield return [theme, "--danger-solid", "crimson", 700];
        }
    }

    [Theory]
    [MemberData(nameof(WhiteOnSolidPairs))]
    public void Every_filled_control_carries_white_text_at_AA(
        SednaTheme theme, string token, string family, int step)
    {
        var hex = theme.Palette.Ramps().Single(r => r.Family == family).Ramp[step];
        var ratio = Contrast("#ffffff", hex);

        Assert.True(ratio >= 4.5,
            $"\"{theme.Name}\": {token} resolves to {family}-{step} ({hex}), and white on it is "
            + $"{ratio:0.00}:1 — below the 4.5:1 AA floor for the --on-solid label every filled "
            + "control renders. If this family was generated, its filled step needs solving for "
            + "contrast the same way the brand step is; see docs/BRANDING.md §3.1.");
    }

    /// <summary>
    /// WCAG 2.1 relative-luminance contrast ratio, written independently of
    /// <c>Oklch.Contrast</c> — this test exists to check that helper's callers produce an
    /// accessible result, so it should not share a formula with the thing it is checking.
    /// </summary>
    private static double Contrast(string hexA, string hexB)
    {
        var la = RelativeLuminance(hexA);
        var lb = RelativeLuminance(hexB);
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var span = hex.AsSpan().TrimStart('#');
        double r = Convert.ToInt32(span[..2].ToString(), 16) / 255.0;
        double g = Convert.ToInt32(span[2..4].ToString(), 16) / 255.0;
        double b = Convert.ToInt32(span[4..6].ToString(), 16) / 255.0;
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);

        static double Linear(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
