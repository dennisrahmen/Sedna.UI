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
