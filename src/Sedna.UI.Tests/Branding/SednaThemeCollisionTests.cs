using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Makes <c>docs/BRANDING.md</c> §3.3's rule real instead of advisory: "a theme whose brand hue
/// lands within ~15° of a semantic family must move that family." Every built-in theme's brand
/// (the <c>coral</c> ramp) is measured in OKLCH against every family it did — or did not — move,
/// using the real, gamut-clamped colours <see cref="SednaRamp"/> actually emits rather than the
/// raw anchor hue. A raw-anchor check would have missed cobalt's collision entirely: its anchor's
/// hue sits ~26–28° from Orbit Blue, comfortably over the threshold, but <see
/// cref="SednaRamp.FromAnchor"/> clamps out-of-gamut steps into sRGB, which does not preserve
/// hue — the theme's lightest generated steps drift toward Orbit Blue and close to within 5°.
/// </summary>
public class SednaThemeCollisionTests
{
    /// <summary>
    /// docs/BRANDING.md §3.3's own number. A pair closer than this reads as one colour at a
    /// glance — the two rejected/forced examples the manual measures (Sedna Red vs the old
    /// danger red at 7°, vs crimson at 32°) sit either side of it.
    /// </summary>
    private const double MinHueDegrees = 15.0;

    /// <summary>Every family a brand hue can collide with, and the ramp that carries it.</summary>
    private static readonly (string Role, string Family, Func<SednaPalette, SednaRamp> Ramp)[] SemanticFamilies =
    [
        ("go", "green", p => p.Green),
        ("warn", "amber", p => p.Amber),
        ("danger", "crimson", p => p.Crimson),
        ("secret", "violet", p => p.Violet),
        ("accent/info", "orbit", p => p.Orbit),
    ];

    [Theory]
    [MemberData(nameof(SednaThemeTests.BuiltInThemes), MemberType = typeof(SednaThemeTests))]
    public void A_theme_keeps_its_brand_hue_away_from_every_semantic_family(SednaTheme theme)
    {
        var brandSteps = ReferencedSteps("coral");
        Assert.NotEmpty(brandSteps);

        var failures = new List<string>();

        foreach (var (role, family, ramp) in SemanticFamilies)
        {
            var familySteps = ReferencedSteps(family);
            Assert.NotEmpty(familySteps);

            var distance = WorstCaseHueDistance(
                theme.Palette.Coral, brandSteps, ramp(theme.Palette), familySteps,
                out var brandStep, out var brandHue, out var familyStep, out var familyHue);

            if (distance <= MinHueDegrees)
            {
                failures.Add(
                    $"  {role} ({family}): {distance:0.0}° — coral-{brandStep} (H={brandHue:0.0}) vs "
                    + $"{family}-{familyStep} (H={familyHue:0.0})");
            }
        }

        Assert.True(failures.Count == 0,
            $"\"{theme.Name}\" puts its brand within {MinHueDegrees:0}° of a semantic family it did not "
            + "move (docs/BRANDING.md §3.3):\n" + string.Join("\n", failures)
            + "\n\nA failure here means a filled primary button and that family's filled control "
            + "(e.g. a solid \"go\" or \"danger\" badge) would read as the same colour at a glance. "
            + "Either move the family onto a different anchor, as forest moves go and cobalt moves "
            + "orbit, or the brand anchor itself needs to change.");
    }

    /// <summary>
    /// The minimum OKLCH hue distance between any referenced step of <paramref name="brand"/>
    /// and any referenced step of <paramref name="family"/> — the worst case, since a collision
    /// anywhere either family is actually rendered is a real collision. Reads the ramps'
    /// real, generated colours rather than an anchor hex, so out-of-gamut clamping in
    /// <see cref="SednaRamp.FromAnchor"/> (which does not preserve hue) cannot hide a collision
    /// the anchor alone would miss.
    /// </summary>
    private static double WorstCaseHueDistance(
        SednaRamp brand, IReadOnlyList<int> brandSteps,
        SednaRamp family, IReadOnlyList<int> familySteps,
        out int worstBrandStep, out double worstBrandHue,
        out int worstFamilyStep, out double worstFamilyHue)
    {
        var best = double.MaxValue;
        int bBrandStep = 0, bFamilyStep = 0;
        double bBrandHue = 0, bFamilyHue = 0;

        foreach (var bStep in brandSteps)
        {
            var (_, _, brandHue) = Oklch.FromHex(brand[bStep]);
            foreach (var fStep in familySteps)
            {
                var (_, _, familyHue) = Oklch.FromHex(family[fStep]);
                var distance = HueDistance(brandHue, familyHue);
                if (distance < best)
                {
                    best = distance;
                    bBrandStep = bStep; bBrandHue = brandHue;
                    bFamilyStep = fStep; bFamilyHue = familyHue;
                }
            }
        }

        worstBrandStep = bBrandStep; worstBrandHue = bBrandHue;
        worstFamilyStep = bFamilyStep; worstFamilyHue = bFamilyHue;
        return best;
    }

    /// <summary>Shortest distance between two hue angles on the 360° circle.</summary>
    private static double HueDistance(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    private static readonly Regex VarUsage = new(@"var\(\s*--([a-z]+)-(\d+)\s*\)", RegexOptions.Compiled);

    /// <summary>
    /// Every step of <paramref name="family"/> the shipped semantic tier actually reaches for
    /// (dark, light, colour-vision and contrast alike) — the same steps
    /// <see cref="SednaThemeTests.A_theme_palette_covers_every_step_both_variants_reference"/>
    /// requires a theme to declare, so this test measures exactly the colours that render.
    /// </summary>
    private static IReadOnlyList<int> ReferencedSteps(string family)
    {
        var css = Assets.StripComments(Assets.Css);
        return VarUsage.Matches(css)
            .Where(m => m.Groups[1].Value == family)
            .Select(m => int.Parse(m.Groups[2].Value))
            .Distinct()
            .ToList();
    }
}
