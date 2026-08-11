using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every control that can sit in a row with another control takes its height from
/// <c>--control-height</c>.
/// </summary>
/// <remarks>
/// <para>
/// The failure this pins is not hypothetical, it is what was shipping: <c>.btn</c>
/// pinned itself to <c>33px</c>, <c>.form-input</c> came out at <c>35.5px</c> from its
/// own padding and line-height, <c>.search-input</c> was told <c>34px</c> by hand, and
/// <c>.btn-icon</c> restated the button's number a second time — so a button beside a
/// field was two and a half pixels short, a button <i>inside</i> an input group
/// stretched to a third height, and a square icon button was square only by
/// coincidence.
/// </para>
/// <para>
/// A shared control height is not something each part can decide for itself, and the
/// symptom of it drifting is the kind nobody can name and everybody sees. So the token
/// is the only permitted source, and this is what says so.
/// </para>
/// </remarks>
public class ControlHeightTests
{
    /// <summary>
    /// The selectors that establish the height of a control, and the property each
    /// one does it with.
    /// </summary>
    /// <remarks>
    /// Hand-kept, because "is this a control that lines up with other controls" is a
    /// design judgement rather than something derivable from the stylesheet. A new
    /// control-shaped class is added here in the same edit — that is the point of the
    /// list.
    /// </remarks>
    public static TheoryData<string, string> Controls() => new()
    {
        { ".btn", "min-height" },
        { ".btn-sm", "min-height" },
        { ".btn-lg", "min-height" },
        { ".btn-icon", "width" },
        { ".btn-icon.btn-sm", "width" },
        { ".btn-icon.btn-lg", "width" },
        { ".form-input", "min-height" },
        { ".form-input-sm", "min-height" },
        { ".form-input-lg", "min-height" },
        { ".form-value-display", "min-height" },
        { ".input-group", "min-height" },
        { ".input-group--sm", "min-height" },
        { ".input-group--lg", "min-height" },
        { ".search-input", "height" },
        // The chip is interactive, so it takes the control heights too. The badge does
        // not appear here on purpose: it is a label with its own type scale.
        { ".chip", "min-height" },
        { ".chip-lg", "min-height" },
    };

    [Theory]
    [MemberData(nameof(Controls))]
    public void A_control_takes_its_height_from_the_token(string selector, string property)
    {
        var body = RuleBody(selector);

        Assert.NotNull(body);

        var match = Regex.Match(body!, $@"(?<![\w-]){Regex.Escape(property)}\s*:\s*(?<value>[^;}}]+)");

        Assert.True(match.Success,
            $"{selector} no longer declares {property}. It is a control that lines up with other "
            + "controls, so it has to take its size from --control-height or say here why not.");

        Assert.Contains("var(--control-height", match.Groups["value"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_control_heights_are_plausible_targets()
    {
        // Only the sanity a source scan can honestly assert. Whether the token actually
        // BINDS — whether a control's own padding and line box already exceed it, in
        // which case the height is a suggestion the control ignores — is a question for
        // a layout engine, and FrameLayoutTests measures it in one.
        var tokens = Assets.StripComments(Assets.Css);

        var small = Px(Value(tokens, "--control-height-sm"));
        var normal = Px(Value(tokens, "--control-height"));

        Assert.True(normal > small, "The normal control cannot be shorter than the small one.");
        // WCAG 2.5.8 puts the minimum target at 24px; a control below that is a target
        // nobody can hit on a phone.
        Assert.True(small >= 24, $"--control-height-sm is {small}px, under the 24px target floor.");
    }

    /// <summary>
    /// The declarations of the rule whose selector list contains
    /// <paramref name="selector"/> exactly, or null.
    /// </summary>
    /// <remarks>
    /// Matched by regex rather than through <c>Assets.TopLevelRules</c>: the shipped
    /// sheet is entirely inside <c>@layer</c> blocks, so its top-level rules are the
    /// six layers and nothing else. A brace-free body is enough here — every rule
    /// these selectors appear in is flat.
    /// </remarks>
    private static string? RuleBody(string selector)
    {
        var css = Assets.StripComments(Assets.Css);

        foreach (var rule in Regex.Matches(css, @"(?<selector>[^{}@;]+)\{(?<body>[^{}]*)\}").Cast<Match>())
        {
            var selectors = rule.Groups["selector"].Value.Split(',').Select(Assets.Squash);
            if (selectors.Contains(selector, StringComparer.Ordinal))
                return rule.Groups["body"].Value;
        }

        return null;
    }

    private static string Value(string css, string token) =>
        Regex.Match(css, $@"{Regex.Escape(token)}\s*:\s*([^;]+)").Groups[1].Value.Trim();

    private static int Px(string value) => int.Parse(value.Replace("px", "", StringComparison.Ordinal).Trim(),
        System.Globalization.CultureInfo.InvariantCulture);
}
