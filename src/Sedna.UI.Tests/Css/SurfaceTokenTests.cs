using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests.Css;

/// <summary>
/// The named surfaces and the elevation ladder: that they exist in both variants,
/// that the three levels are actually three colours, and that text on them is
/// readable.
/// </summary>
/// <remarks>
/// A ladder is only worth having if its steps are distinguishable and its top step
/// is still legible. Both are measurable, and neither is obvious by eye on a dark
/// theme — Dust Gray on the third level looks fine and measures 3.97:1.
/// </remarks>
public class SurfaceTokenTests
{
    private static readonly string[] Roles =
        ["--surface-app", "--surface-chrome", "--surface-content",
         "--surface-raised-1", "--surface-raised-2", "--surface-raised-3"];

    private static readonly string[] Ladder =
        ["--surface-raised-1", "--surface-raised-2", "--surface-raised-3"];

    [Theory]
    [InlineData(Tokens.Dark)]
    [InlineData(Tokens.Light)]
    public void Every_surface_role_resolves_to_a_colour(string variant)
    {
        var tokens = Tokens.Resolved(variant);

        foreach (var role in Roles)
        {
            // Rgb asserts both halves: declared at all, and ending at a colour rather
            // than at a var() that goes nowhere.
            var colour = Tokens.Rgb(tokens, role);
            Assert.InRange(colour.R + colour.G + colour.B, 0, 3);
        }
    }

    [Theory]
    [InlineData(Tokens.Dark)]
    [InlineData(Tokens.Light)]
    public void The_three_elevation_levels_are_three_different_colours(string variant)
    {
        var tokens = Tokens.Resolved(variant);
        var values = Ladder.Select(step => Tokens.Resolved(variant)[step]).ToList();

        Assert.Equal(values.Count, values.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        // And each step moves away from the page ground, so nesting reads as rising
        // rather than as noise: monotonic in luminance, in whichever direction the
        // variant runs.
        var ladder = Ladder.Select(step => Luminance(Tokens.Rgb(tokens, step))).ToList();
        var rising = ladder[1] > ladder[0];

        for (var i = 1; i < ladder.Count; i++)
            Assert.True(rising ? ladder[i] > ladder[i - 1] : ladder[i] < ladder[i - 1],
                $"{Ladder[i]} does not continue the direction {Ladder[1]} set in {variant}. "
                + "A ladder that changes direction halfway is not a ladder.");
    }

    [Theory]
    [InlineData(Tokens.Dark)]
    [InlineData(Tokens.Light)]
    public void Muted_text_clears_AA_on_every_surface_level(string variant)
    {
        var tokens = Tokens.Resolved(variant);

        // Levels 1 and 2 carry --muted as it stands. Level 3 does not: 30-cards.css
        // lifts --muted to --fg-soft there, because Dust Gray on the third dark step
        // measures 3.97:1. If that lift is ever removed, this test is what fails.
        Assert.True(Tokens.Contrast(tokens, "--muted", "--surface-raised-1") >= 4.5);
        Assert.True(Tokens.Contrast(tokens, "--muted", "--surface-raised-2") >= 4.5);

        var third = Tokens.Contrast(tokens, "--fg-soft", "--surface-raised-3");
        Assert.True(third >= 4.5,
            $"--fg-soft on --surface-raised-3 is {third:0.00}:1 in {variant}, below the AA floor — "
            + "and it is what the third level lifts --muted to, so there is nothing left above it. "
            + "Take a darker ramp step for the level rather than adjusting the text colour.");
    }

    [Fact]
    public void The_third_level_lifts_the_muted_role()
    {
        // The pairing this file measures only holds if the lift is actually in the
        // sheet: the test above would otherwise pass while nested cards shipped
        // unreadable hints.
        var css = Assets.StripComments(Assets.Css);
        var rule = css[css.IndexOf(".card .card .card", StringComparison.Ordinal)..];
        rule = rule[..rule.IndexOf('}', StringComparison.Ordinal)];

        Assert.Contains("--muted: var(--fg-soft)", Assets.Squash(rule), StringComparison.Ordinal);
    }

    [Fact]
    public void A_nested_card_takes_the_next_step_and_stops_at_the_third()
    {
        var css = Assets.StripComments(Assets.Css);

        Assert.Contains(".card .card {", css, StringComparison.Ordinal);
        Assert.Contains(".card .card .card {", css, StringComparison.Ordinal);

        // Four levels deep is the same colour as three. A fourth step would need a
        // second --muted lift and would run off the end of the ramp.
        Assert.DoesNotContain(".card .card .card .card", css, StringComparison.Ordinal);
    }

    private static double Luminance((double R, double G, double B) c) =>
        // Against black, which is a monotonic stand-in for the luminance itself and
        // keeps this file to one contrast implementation.
        Tokens.Contrast(c, (0, 0, 0));
}
