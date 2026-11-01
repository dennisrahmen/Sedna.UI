using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A filled primary button's boundary clears 3:1 against every surface it sits on, in every palette
/// and built-in theme — through its fill or through its edge.
/// </summary>
/// <remarks>
/// <c>--brand</c> is solved for white text on it, which is a different question from where the
/// button ends: in the dark variant it measured 2.93:1 against <c>--card-bg</c>, under the 3:1 WCAG
/// 1.4.11 asks of a control's boundary. The label was fine; the button's shape was not. So the
/// border is <c>--brand-edge</c>, the display strength, and this measures whichever of fill and edge
/// stands out more — a boundary is visible if either does.
/// </remarks>
public class ButtonEdgeContrastTests
{
    private const double Floor = 3.0;

    private static readonly string[] Grounds = ["--bg", "--card-bg", "--bg-elevated", "--surface-raised-2", "--surface-raised-3"];

    [Fact]
    public void The_primary_button_draws_its_border_in_the_edge_role_in_every_state()
    {
        var css = Assets.StripComments(Assets.Css);
        var rule = Regex.Match(css, @"(?<![-\w])\.btn-primary\s*\{(?<body>[^{}]*)\}");
        Assert.True(rule.Success, ".btn-primary is not declared.");
        Assert.Matches(@"border-color\s*:\s*var\(\s*--brand-edge\s*\)", rule.Groups["body"].Value);

        // Hover and active darken the fill, which moves it further from a dark card; the edge has
        // to stay where it is, so neither state may set a border colour of its own.
        foreach (var state in new[] { "hover", "active" })
        {
            var body = Regex.Match(css, $@"\.btn-primary:{state}\s*\{{(?<body>[^{{}}]*)\}}");
            Assert.True(body.Success, $".btn-primary:{state} is not declared.");
            Assert.DoesNotContain("border-color", body.Groups["body"].Value, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(TintedTextContrastTests.ThemesAndPalettes), MemberType = typeof(TintedTextContrastTests))]
    public void The_primary_buttons_boundary_clears_3_to_1(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var failures = new List<string>();

        foreach (var ground in Grounds)
        {
            var fill = Tokens.Contrast(tokens, "--brand", ground);
            var edge = Tokens.Contrast(tokens, "--brand-edge", ground);
            if (Math.Max(fill, edge) < Floor)
                failures.Add($"on {ground}: fill {fill:0.00}:1, edge {edge:0.00}:1");
        }

        Assert.True(failures.Count == 0,
            $"\"{theme.Name}\", {palette}: the primary button's boundary is under {Floor}:1.\n  "
            + string.Join("\n  ", failures)
            + "\nTake a lighter --brand-edge step; never lighten --brand, which carries white text.");
    }
}
