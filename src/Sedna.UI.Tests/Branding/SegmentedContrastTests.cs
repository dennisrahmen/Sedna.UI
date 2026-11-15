using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The chosen option of a segmented control is told from the track by more than its text colour: its fill
/// or its edge clears 3:1 against the track it sits in, on every surface the control is set on, in every
/// palette and built-in theme.
/// </summary>
/// <remarks>
/// WCAG 1.4.11 asks 3:1 of a state indicator. The fill is <c>--bg-elevated</c> over a track of
/// <c>--surface-strong</c>, a few percent apart in the dark variant and, in a neutral theme such as
/// graphite, barely a step — the chosen option then differed from its neighbours in the colour of its text
/// alone. The edge is the controls' own, <c>--control-border</c>, which is held at 3:1 against the page.
/// </remarks>
public class SegmentedContrastTests
{
    private const double Floor = 3.0;

    /// <summary>
    /// The surfaces a control is set on. The two deeper elevation steps are left out: a field's own edge,
    /// <c>--control-border</c>, is 2.1–2.5:1 there for every control in the library, a limit of nesting
    /// cards three deep that this component does not own.
    /// </summary>
    private static readonly string[] Grounds = ["--bg", "--card-bg", "--bg-elevated"];

    [Fact]
    public void The_chosen_option_draws_an_edge_in_the_controls_own_colour()
    {
        var css = Assets.StripComments(Assets.Css);
        var rule = Regex.Match(css, @"\.segmented-option:has\(input:checked\)\s*\{(?<body>[^{}]*)\}");
        Assert.True(rule.Success, "The chosen option's rule is not declared.");
        Assert.Matches(@"box-shadow\s*:\s*inset\s+0\s+0\s+0\s+1px\s+var\(\s*--control-border\s*\)", rule.Groups["body"].Value);
        Assert.Matches(@"background\s*:\s*var\(\s*--bg-elevated\s*\)", rule.Groups["body"].Value);
    }

    public static IEnumerable<object[]> ThemesAndPalettes() => TintedTextContrastTests.ThemesAndPalettes();

    [Theory]
    [MemberData(nameof(ThemesAndPalettes))]
    public void The_chosen_option_clears_3_to_1_against_its_track(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var failures = new List<string>();

        foreach (var ground in Grounds)
        {
            var track = Tokens.Over(tokens, "--surface-strong", ground);
            var fill = Tokens.Contrast(Tokens.Rgb(tokens, "--bg-elevated"), track);
            var edge = Tokens.Contrast(Tokens.Rgb(tokens, "--control-border"), track);
            if (Math.Max(fill, edge) < Floor)
                failures.Add($"on {ground}: fill {fill:0.00}:1, edge {edge:0.00}:1");
        }

        Assert.True(failures.Count == 0,
            $".segmented-option:has(input:checked), \"{theme.Name}\", {palette}: the chosen option is under {Floor}:1 against its track.\n  "
            + string.Join("\n  ", failures));
    }
}
