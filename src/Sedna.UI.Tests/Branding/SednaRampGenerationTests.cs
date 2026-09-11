namespace Sedna.UI.Tests;

/// <summary>
/// The test that makes <see cref="SednaRamp.FromAnchor"/> trustworthy: generate Sedna's own
/// coral and orbit ramps from nothing but their anchor colour, and compare against the values
/// <c>docs/BRANDING.md</c> §2 supplies. If the generator cannot reproduce a ramp Sedna's own
/// designer produced by the same stated method (§2.1), one of the two is wrong — that is the
/// finding this test exists to surface, not a threshold to loosen until it stops firing.
/// </summary>
/// <remarks>
/// Navy is deliberately not tested here even though it also has a documented anchor
/// (Sedna Navy, step 900): the generator's shared lightness curve was fit using navy's own
/// values for the two steps (50, 950) the eight support ramps don't reach, so reproducing navy
/// would be partly circular. Coral and orbit are held out — no part of either was used to fit
/// the curve or the chroma-bell width — so their reproduction is a genuine check.
/// </remarks>
public class SednaRampGenerationTests
{
    /// <summary>
    /// The per-step tolerance, in the OKLab-Euclidean metric <see cref="PerceptualDelta"/>
    /// measures. Chosen before running the test, not after: roughly 2–3× the smallest
    /// difference generally considered perceptible in OKLab (≈0.02), which reads as "some
    /// steps are visibly a little off" rather than "indistinguishable" — a reasonable bar for a
    /// ramp generated from one anchor colour, not a re-derivation of a hand-tuned one.
    /// </summary>
    private const double Threshold = 0.05;

    [Theory]
    [InlineData("coral", 500)] // Sedna Red, #ff6b4a — docs/BRANDING.md §1.4 / §2.3
    [InlineData("orbit", 400)] // Orbit Blue, #59c3ff — docs/BRANDING.md §1.4 / §2.4
    public void The_generator_reproduces_the_ramp_from_its_anchor_within_the_threshold(
        string family, int anchorStep)
    {
        var actual = SednaTheme.Sedna.Palette.Ramps().Single(r => r.Family == family).Ramp;
        var anchorHex = actual[anchorStep];

        var generated = SednaRamp.FromAnchor(anchorHex, anchorStep);

        var worst = 0.0;
        var worstStep = 0;
        var report = new List<string>();

        foreach (var step in actual.Steps)
        {
            var delta = PerceptualDelta(generated[step], actual[step]);
            report.Add($"    {step,4}  generated={generated[step]}  actual={actual[step]}  dE={delta:0.0000}");
            if (delta > worst) { worst = delta; worstStep = step; }
        }

        Assert.True(worst < Threshold,
            $"SednaRamp.FromAnchor(\"{anchorHex}\", {anchorStep}) reproduces {family} with a worst " +
            $"per-step difference of {worst:0.0000} (at step {worstStep}), which is over the " +
            $"{Threshold:0.00} threshold this test asserts. Either the generator or the claim that " +
            $"{family} follows docs/BRANDING.md §2.1's stated method is wrong. Per step:\n" +
            string.Join("\n", report));
    }

    /// <summary>
    /// A neutral red standing in for a brand colour a corporate design mandates. Not any real
    /// organisation's — the point is only that it is a hex somebody is not allowed to move.
    /// </summary>
    private const string Mandated = "#d62828";

    [Fact]
    public void The_anchor_step_is_not_the_anchor_colour_by_default()
    {
        // The behaviour exactAnchor exists for, asserted so the XML doc cannot quietly go back
        // to calling anchorHex "the exact colour at this step". Lightness comes from the shared
        // curve, so the anchor contributes hue and chroma and the step is a different colour.
        var ramp = SednaRamp.FromAnchor(Mandated, 600);

        Assert.NotEqual(Mandated, ramp[600], StringComparer.OrdinalIgnoreCase);
    }

    // 600 and 700, the two steps this colour's own lightness fits between. Pinning it at
    // 500 is rejected instead, by An_exact_anchor_that_does_not_fit_its_step_is_rejected —
    // a mid-dark red is not a step-500 colour, and the ramp would reverse direction there.
    [Theory]
    [InlineData(600)]
    [InlineData(700)]
    public void An_exact_anchor_is_kept_verbatim_at_its_step(int anchorStep)
    {
        var ramp = SednaRamp.FromAnchor(Mandated, anchorStep, exactAnchor: true);

        Assert.Equal(Mandated, ramp[anchorStep], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_exact_anchor_leaves_the_rest_of_the_ramp_generated()
    {
        // "The neighbouring steps are still generated around it": only the anchor step moves,
        // so the ramp keeps the per-step visual weight that makes two families comparable.
        var curve = SednaRamp.FromAnchor(Mandated, 600);
        var pinned = SednaRamp.FromAnchor(Mandated, 600, exactAnchor: true);

        foreach (var step in curve.Steps)
        {
            if (step == 600) continue;
            Assert.Equal(curve[step], pinned[step], StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void An_exact_anchor_still_descends_in_lightness()
    {
        // The property the whole ramp rests on: --brand-hover is the step after --brand and
        // --brand-active the one after that, so a pinned colour must not reverse the direction.
        var ramp = SednaRamp.FromAnchor(Mandated, 600, exactAnchor: true);

        var lightness = ramp.Steps.Select(s => Oklch.FromHex(ramp[s]).L).ToList();
        for (var i = 1; i < lightness.Count; i++)
            Assert.True(lightness[i] < lightness[i - 1],
                $"step {ramp.Steps[i]} is lighter than step {ramp.Steps[i - 1]}.");
    }

    [Fact]
    public void An_exact_anchor_that_does_not_fit_its_step_is_rejected()
    {
        // A near-white pinned at 900 would sit lighter than every step above it. Rejected with
        // the step it does fit, rather than emitting a ramp whose hover state is the lighter one.
        var error = Assert.Throws<ArgumentException>(
            () => SednaRamp.FromAnchor("#ffe9e3", 900, exactAnchor: true));

        Assert.Contains("non-monotonic", error.Message, StringComparison.Ordinal);
        Assert.Contains("fits step", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_exact_anchor_and_a_contrast_solve_cannot_claim_the_same_step()
    {
        // Both decide one step's lightness, by opposite rules. Silently letting one win would
        // give a theme either a brand that is not its brand or one that fails its own floor.
        var error = Assert.Throws<ArgumentException>(() => SednaRamp.FromAnchor(
            Mandated, 600, exactAnchor: true,
            contrastSolvedStep: new ContrastSolvedStep(600, "#ffffff", 4.6)));

        Assert.Contains("both claim step 600", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_exact_anchor_needs_its_step_to_be_generated()
    {
        Assert.Throws<ArgumentException>(() => SednaRamp.FromAnchor(
            Mandated, 600, steps: [200, 300, 400, 500], exactAnchor: true));
    }

    [Fact]
    public void An_exact_anchor_can_sit_beside_a_contrast_solve_on_another_step()
    {
        // The combination a theme actually wants: the mandated colour at 600, and 700 solved
        // for a floor of its own.
        var ramp = SednaRamp.FromAnchor(
            Mandated, 600, exactAnchor: true,
            contrastSolvedStep: new ContrastSolvedStep(700, "#ffffff", 5.5));

        Assert.Equal(Mandated, ramp[600], StringComparer.OrdinalIgnoreCase);
        Assert.True(Oklch.Contrast("#ffffff", ramp[700]) >= 5.5);
    }

    /// <summary>Euclidean distance in OKLab-ish space (L, and C/H read back to a/b) between two hex colours.</summary>
    private static double PerceptualDelta(string generatedHex, string actualHex)
    {
        var (gl, gc, gh) = Oklch.FromHex(generatedHex);
        var (al, ac, ah) = Oklch.FromHex(actualHex);

        var (ga, gb) = ToAb(gc, gh);
        var (aa, ab) = ToAb(ac, ah);

        return Math.Sqrt(Math.Pow(gl - al, 2) + Math.Pow(ga - aa, 2) + Math.Pow(gb - ab, 2));

        static (double A, double B) ToAb(double c, double h)
        {
            var radians = h * Math.PI / 180;
            return (c * Math.Cos(radians), c * Math.Sin(radians));
        }
    }
}
