using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every control the sheet draws under the touch target grows its hit area on a coarse
/// pointer (69-touch.css), so a small control added later cannot be left out.
/// </summary>
public class TouchTargetTests
{
    private const int Target = 32;

    private static readonly Regex Rule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

    /// <summary>A control: a name that says it is pressed — a close, a dismiss, a clear, a reveal.</summary>
    private static readonly Regex Control = new(@"\.(?<name>[a-z]+(?:-[a-z]+)*-(?:close|dismiss|clear|filter|reveal))(?![-\w])", RegexOptions.Compiled);

    [Fact]
    public void Every_small_control_grows_its_hit_area_on_a_coarse_pointer()
    {
        var css = Assets.StripComments(Assets.Css);
        var small = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match rule in Rule.Matches(Tokens.WithoutMediaBlocks(css)))
        {
            var size = Regex.Match(rule.Groups["body"].Value, @"(?<![-\w])(?:width|height)\s*:\s*(?<px>\d+)px");
            if (!size.Success || int.Parse(size.Groups["px"].Value) >= Target) continue;
            foreach (Match name in Control.Matches(rule.Groups["selector"].Value))
                small.Add(name.Groups["name"].Value);
        }
        Assert.NotEmpty(small);

        var coarse = string.Concat(Assets.MediaBlocks(css)
            .Where(m => m.Condition.Contains("pointer: coarse", StringComparison.Ordinal))
            .Select(m => m.Body));
        var missing = small.Where(n => !Regex.IsMatch(coarse, $@"\.{n}(?![-\w])[^{{}}]*::before")).ToList();

        Assert.True(missing.Count == 0,
            $"These controls are drawn under {Target}px and have no hit area on a coarse pointer: "
            + string.Join(", ", missing) + ". Add them to 69-touch.css.");
    }

    [Fact]
    public void A_password_fields_reveal_button_is_one_of_them()
    {
        var css = Assets.StripComments(Assets.Css);
        var coarse = string.Concat(Assets.MediaBlocks(css)
            .Where(m => m.Condition.Contains("pointer: coarse", StringComparison.Ordinal))
            .Select(m => m.Body));

        Assert.Matches(@"\.input-reveal(?![-\w])[^{}]*::before", coarse);
    }

    /// <summary>
    /// A rating's stars sit side by side, so a hit area grown around one would land on its
    /// neighbour: on a coarse pointer the editable stars grow to the target themselves, and
    /// a star split into halves grows until each half is at least WCAG's 24px.
    /// </summary>
    [Fact]
    public void An_editable_ratings_stars_reach_the_target_on_a_coarse_pointer()
    {
        var css = Assets.StripComments(Assets.Css);
        var coarse = string.Concat(Assets.MediaBlocks(css)
            .Where(m => m.Condition.Contains("pointer: coarse", StringComparison.Ordinal))
            .Select(m => m.Body));

        var whole = Regex.Match(coarse, @"\.rating:has\(input\)\s*\{[^{}]*--rating-size\s*:\s*(?<v>[^;}]+)");
        Assert.True(whole.Success, "An editable .rating does not resize its stars on a coarse pointer.");
        Assert.Equal("var(--touch-target)", whole.Groups["v"].Value.Trim());

        var half = Regex.Match(coarse, @"\.rating:has\(\.rating-half\)\s*\{[^{}]*--rating-size\s*:\s*calc\(var\(--touch-target\)\s*\*\s*(?<k>[\d.]+)\)");
        Assert.True(half.Success, "A .rating split into halves does not resize its stars on a coarse pointer.");
        var target = int.Parse(Regex.Match(css, @"--touch-target\s*:\s*(\d+)px").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var k = double.Parse(half.Groups["k"].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(target * k / 2 >= 24, $"Half a star is {target * k / 2}px on a coarse pointer, under the 24px floor.");
    }

    /// <summary>A one-time code's boxes are fields, as tall and as wide as the control height.</summary>
    [Fact]
    public void A_one_time_codes_boxes_are_control_sized()
    {
        var css = Assets.StripComments(Assets.Css);
        var box = Regex.Match(css, @"\.form-code\s*>\s*\.form-input\s*\{(?<body>[^{}]*)\}");
        Assert.True(box.Success);
        Assert.Matches(@"(?<![-\w])width\s*:\s*var\(--control-height\)", box.Groups["body"].Value);
        var normal = int.Parse(Regex.Match(css, @"--control-height\s*:\s*(\d+)px").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(normal >= Target, $"--control-height is {normal}px, under the {Target}px touch target.");
    }
}
