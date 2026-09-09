using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests.Branding;

/// <summary>
/// The generated surface ramp: that it covers every step the stylesheet reaches for, that it
/// reproduces Sedna's own base when given Sedna's own canvas colour, and that a base generated
/// from a neutral anchor is still readable at every elevation level.
/// </summary>
/// <remarks>
/// A theme could always supply its own base ramp; nothing could generate one. That is why every
/// app had the same blue-tinted background, and it is why these three properties are worth
/// pinning: a generated base that misses a step renders with missing colours, one that lands on
/// the brand lightness curve renders a mid grey where a canvas belongs, and one that is merely
/// plausible can still fail contrast three cards deep.
/// </remarks>
public class SurfaceRampTests
{
    [Fact]
    public void The_surface_steps_cover_every_slate_step_the_stylesheet_references()
    {
        // Derived from the sheet, never typed: a rule that starts using --slate-825 must fail
        // here rather than in a themed app, where the symptom is an unpainted surface.
        var referenced = Regex.Matches(Assets.StripComments(Assets.Css), @"var\(\s*--slate-(\d+)\s*\)")
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .OrderBy(step => step)
            .ToList();

        Assert.NotEmpty(referenced);

        var missing = referenced.Except(SednaRamp.SurfaceSteps).ToList();

        Assert.True(missing.Count == 0,
            "SednaRamp.SurfaceSteps does not generate every slate step the stylesheet uses: "
            + string.Join(", ", missing)
            + ". A theme that generates its base would leave those tokens undefined.");
    }

    [Fact]
    public void Sednas_own_canvas_colour_reproduces_Sednas_own_base()
    {
        // The profile is measured from this ramp, so feeding it back must return it. That is
        // what makes the generator a description of Sedna's base rather than a new design: if
        // this drifts, every generated theme's surfaces drifted with it.
        var slate = SednaTheme.Sedna.Palette.Slate;
        var generated = SednaRamp.Surface(slate[900]);

        foreach (var step in SednaRamp.SurfaceSteps)
        {
            var expected = slate[step];
            var actual = generated[step];

            // One 8-bit channel of rounding, not an exact string match: the round trip goes
            // through OKLCh and back, and demanding byte equality would pin the arithmetic
            // rather than the colour.
            Assert.True(Channels(expected).Zip(Channels(actual)).All(p => Math.Abs(p.First - p.Second) <= 1),
                $"slate-{step}: generated {actual}, Sedna's own is {expected}.");
        }
    }

    [Fact]
    public void A_generated_base_keeps_its_steps_in_order_and_neutral()
    {
        var ramp = SednaRamp.Surface("#18181b");

        // Monotonic: 50 is the lightest, 950 the darkest, every step between in order. A ramp
        // that folds back on itself makes --bg lighter than --card-bg somewhere.
        var luminance = SednaRamp.SurfaceSteps.Select(step => Luminance(ramp[step])).ToList();
        for (var i = 1; i < luminance.Count; i++)
            Assert.True(luminance[i] < luminance[i - 1],
                $"slate-{SednaRamp.SurfaceSteps[i]} is not darker than slate-{SednaRamp.SurfaceSteps[i - 1]}.");

        // And near-neutral, because the anchor is: the widest channel spread across the ramp
        // stays small. A "grey" base that arrives visibly coloured is the failure this catches.
        foreach (var step in SednaRamp.SurfaceSteps)
        {
            var channels = Channels(ramp[step]).ToList();
            Assert.True(channels.Max() - channels.Min() <= 12,
                $"slate-{step} = {ramp[step]} is not neutral: channel spread {channels.Max() - channels.Min()}.");
        }
    }

    [Theory]
    [InlineData("#18181b")]   // graphite, the shipped demo
    [InlineData("#0f172a")]   // Sedna's own canvas
    [InlineData("#1c1917")]   // a warm base
    public void Text_on_a_generated_base_clears_AA_at_every_elevation_level(string canvas)
    {
        // The ladder resolves through the base ramp, so a themed base moves all three levels at
        // once — including the third, where 30-cards.css lifts --muted to --fg-soft. These are
        // the pairs SurfaceTokenTests measures on the shipped sheet, re-measured on a generated base.
        var ramp = SednaRamp.Surface(canvas);
        var tokens = Tokens.Resolved();

        var fgSoft = Rgb(SednaTheme.Sedna.Palette.Slate[300]);
        var muted = Tokens.Rgb(tokens, "--muted");

        Assert.True(Tokens.Contrast(muted, Rgb(ramp[800])) >= 4.5, $"--muted on level 1 of {canvas}");
        Assert.True(Tokens.Contrast(muted, Rgb(ramp[750])) >= 4.5, $"--muted on level 2 of {canvas}");
        Assert.True(Tokens.Contrast(fgSoft, Rgb(ramp[700])) >= 4.5, $"--fg-soft on level 3 of {canvas}");
    }

    private static IEnumerable<int> Channels(string hex) =>
        [Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16)];

    private static (double R, double G, double B) Rgb(string hex)
    {
        var channels = Channels(hex).Select(c => c / 255.0).ToList();
        return (channels[0], channels[1], channels[2]);
    }

    private static double Luminance(string hex) => Tokens.Contrast(Rgb(hex), (0, 0, 0));
}
