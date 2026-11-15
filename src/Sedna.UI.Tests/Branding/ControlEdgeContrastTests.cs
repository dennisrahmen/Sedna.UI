using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Where a control ends is visible: every control that draws an edge draws it in a role that clears
/// 3:1 against every surface it sits on (WCAG 1.4.11), in every theme and palette — a button, a chip,
/// a swatch, a copy button, a field — and a toned button's boundary does through its edge or its fill.
/// </summary>
/// <remarks>
/// <c>--border</c> is a calm line colour for a card's or a row's edge, about 1.7:1 on the dark
/// page, and a plain <c>.btn</c> once drew its edge in it: the button's shape all but vanished on a
/// dark band. The fix is a role, not a value — a control's edge is <c>--control-border</c> — and this
/// holds every clickable rule to it, so the next control cannot be drawn with a divider's colour.
/// </remarks>
public class ControlEdgeContrastTests
{
    private const double Floor = 3.0;

    /// <summary>The surfaces a toned control's ink is measured on: every level a card nests to.</summary>
    private static readonly string[] Grounds = ["--bg", "--card-bg", "--bg-elevated", "--surface-raised-2", "--surface-raised-3"];

    /// <summary>
    /// The surfaces a neutral edge is measured on — the ones <see cref="StateContrastTests"/> holds a
    /// field's edge to. The nested card levels are lighter, and a neutral edge on them is a known gap
    /// shared with every field.
    /// </summary>
    private static readonly string[] NeutralGrounds = ["--bg", "--card-bg", "--bg-elevated"];

    /// <summary>The roles a control's edge may be drawn in. Each is measured below.</summary>
    private static readonly string[] NeutralEdges = ["--control-border", "--field-border", "--border-hover"];

    /// <summary>
    /// Controls the stylesheet cannot tell by their cursor: links shaped as buttons, and a chip,
    /// which is something the reader acts on by definition (53-chip.css).
    /// </summary>
    private static readonly string[] LinkShapedControls =
        [".chip", ".back-to-top", ".fab-dial > .menu > .menu-item", ".form-input::file-selector-button"];

    private static readonly Regex Rule = new(@"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex EdgeColour = new(@"(?<![-\w])border(?:-color)?\s*:\s*(?:\d+px\s+(?:solid|dashed)\s+)?var\(\s*(?<t>--[a-z0-9-]+)", RegexOptions.Compiled);

    [Fact]
    public void Every_clickable_rule_draws_its_edge_in_a_control_role()
    {
        var css = Assets.StripComments(Assets.Css);
        var failures = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in Rule.Matches(css))
        {
            var selector = Regex.Replace(m.Groups["sel"].Value.Trim(), @"\s+", " ");
            var body = m.Groups["body"].Value;
            var linkShaped = LinkShapedControls.Contains(selector);
            if (linkShaped) seen.Add(selector);
            if (!linkShaped && !Regex.IsMatch(body, @"cursor\s*:\s*pointer")) continue;

            var edge = EdgeColour.Match(body);
            if (!edge.Success) continue;
            var token = edge.Groups["t"].Value;
            if (!Allowed(token))
                failures.Add($"{selector} draws its edge in {token}");
        }

        Assert.True(failures.Count == 0,
            "A control's edge is what says where it ends, and has to clear 3:1. Draw it in --control-border "
            + "(or a tone's edge role), never in --border, which is a divider's colour:\n  " + string.Join("\n  ", failures));
        Assert.Equal(LinkShapedControls.OrderBy(s => s, StringComparer.Ordinal), seen.OrderBy(s => s, StringComparer.Ordinal));
    }

    private static bool Allowed(string token) =>
        NeutralEdges.Contains(token) || token.StartsWith("--tone-", StringComparison.Ordinal)
        || token is "--brand-edge" or "--field-border-focus";

    [Theory]
    [MemberData(nameof(TintedTextContrastTests.ThemesAndPalettes), MemberType = typeof(TintedTextContrastTests))]
    public void Every_neutral_edge_role_clears_3_to_1(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var failures = new List<string>();
        foreach (var role in NeutralEdges)
            foreach (var ground in NeutralGrounds)
            {
                var g = Tokens.Rgb(tokens, ground);
                var ratio = Tokens.Contrast(Tokens.Over(tokens, role, g), g);
                if (ratio < Floor) failures.Add($"{role} on {ground}: {ratio:0.00}:1");
            }
        Assert.True(failures.Count == 0, $"\"{theme.Name}\", {palette}:\n  " + string.Join("\n  ", failures));
    }

    /// <summary>The toned families that are controls: a button and a chip.</summary>
    public static IEnumerable<object[]> TonesThemesAndPalettes() =>
        from family in new[] { "btn", "chip" }
        from tone in Tones.Families()[family]
        from row in TintedTextContrastTests.ThemesAndPalettes()
        select new[] { family + "--" + tone, row[0], row[1] };

    [Fact]
    public void A_toned_control_draws_its_edge_in_its_ink()
    {
        var css = Assets.StripComments(Assets.Css);
        foreach (var family in new[] { "btn", "chip" })
        {
            var rule = Regex.Match(css, $@"\.{family}:is\(\.{family}--brand[^)]*\)\s*\{{(?<body>[^{{}}]*)\}}");
            Assert.True(rule.Success, $"The toned .{family} rule is not declared.");
            Assert.Matches(@"border-color\s*:\s*var\(\s*--tone-fg\s*\)", rule.Groups["body"].Value);
        }
    }

    [Theory]
    [MemberData(nameof(TonesThemesAndPalettes))]
    public void A_toned_controls_ink_clears_3_to_1_as_its_edge(string toned, SednaTheme theme, string palette)
    {
        var tone = toned[(toned.IndexOf("--", StringComparison.Ordinal) + 2)..];
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var carrier = Tones.Carriers()[tone];
        var failures = new List<string>();

        foreach (var ground in Grounds)
        {
            // Soft and outline alike draw the edge in the tone's ink, on the ground around it.
            var g = Tokens.Rgb(tokens, ground);
            var ratio = Tokens.Contrast(Tokens.Over(tokens, carrier["fg"], g), g);
            if (ratio < Floor) failures.Add($".{toned} on {ground}: {ratio:0.00}:1 ({carrier["fg"]})");
        }

        Assert.True(failures.Count == 0, $"\"{theme.Name}\", {palette}:\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public void The_outline_emphasis_has_no_fill_and_a_control_edge()
    {
        var css = Assets.StripComments(Assets.Css);
        var rule = Regex.Match(css, @"(?<![-\w])\.btn\.btn--outline\s*\{(?<body>[^{}]*)\}");
        Assert.True(rule.Success, ".btn.btn--outline is not declared.");
        Assert.Matches(@"background\s*:\s*transparent", rule.Groups["body"].Value);
        Assert.Matches(@"border-color\s*:\s*var\(\s*--tone-fg\s*,\s*var\(\s*--control-border\s*\)\s*\)", rule.Groups["body"].Value);
    }
}
