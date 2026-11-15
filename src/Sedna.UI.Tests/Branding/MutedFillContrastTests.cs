using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every filled <c>--muted</c> modifier — a ribbon, a count badge, and the next family that adds
/// one — names its own label and sets it at AA on its fill, in every theme and palette.
/// </summary>
/// <remarks>
/// The muted tone is not one of the tone carriers <see cref="SolidToneContrastTests"/> measures,
/// so a family filling with a grey and keeping the white label of its other tones reads under AA
/// unnoticed: <c>.ribbon--muted</c> did, at 4.1:1, and the release run's axe pass caught it.
/// </remarks>
public partial class MutedFillContrastTests
{
    private const double Floor = 4.5;

    [GeneratedRegex(@"([^{}]+)\{([^{}]*)\}")]
    private static partial Regex Rule();

    [GeneratedRegex(@"(?:^|;)\s*(background(?:-color)?|--[a-z-]+-bg)\s*:\s*([^;]+)")]
    private static partial Regex Fill();

    [GeneratedRegex(@"(?:^|;)\s*color\s*:\s*([^;]+)")]
    private static partial Regex Label();

    [GeneratedRegex(@"^var\((--[a-z0-9-]+)\)$")]
    private static partial Regex Token();

    /// <summary>
    /// Each filled <c>--muted</c> rule of a family whose block writes on its fill — its base rule
    /// names a <c>color</c>. A fill nothing is written on, such as a progress bar's, is not a label.
    /// </summary>
    private static IEnumerable<(string Selector, string Fill, string? Label)> MutedFills()
    {
        var rules = Rule().Matches(Assets.Css)
            .Select(r => (Selector: r.Groups[1].Value.Trim(), Body: r.Groups[2].Value))
            .ToList();

        foreach (var (selector, body) in rules)
        {
            var muted = Regex.Match(selector, @"\.([a-z][a-z0-9-]*)--muted\b");
            if (!muted.Success) continue;
            var fill = Fill().Match(body);
            if (!fill.Success) continue;
            var block = "." + muted.Groups[1].Value;
            var writesOnFill = rules.Any(r =>
                Regex.IsMatch(r.Selector, $@"(^|[\s,>]){Regex.Escape(block)}(?![a-z0-9-])") && Label().IsMatch(r.Body));
            if (!writesOnFill) continue;
            var label = Label().Match(body);
            yield return (selector, fill.Groups[2].Value.Trim(), label.Success ? label.Groups[1].Value.Trim() : null);
        }
    }

    [Fact]
    public void The_scan_finds_the_muted_fills()
    {
        var selectors = MutedFills().Select(f => f.Selector).ToList();
        Assert.Contains(selectors, s => s.Contains(".ribbon--muted", StringComparison.Ordinal));
        Assert.Contains(selectors, s => s.Contains(".count-badge--muted", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(TintedTextContrastTests.ThemesAndPalettes), MemberType = typeof(TintedTextContrastTests))]
    public void Every_muted_fill_sets_its_label_at_AA(SednaTheme theme, string palette)
    {
        var tokens = Tokens.Resolved(palette, theme.Palette);
        var failures = new List<string>();

        foreach (var (selector, fill, label) in MutedFills())
        {
            if (label is null)
            {
                failures.Add($"{selector}: fills with {fill} but names no label colour of its own");
                continue;
            }
            var fillToken = Token().Match(fill);
            var labelToken = Token().Match(label);
            if (!fillToken.Success || !labelToken.Success)
            {
                failures.Add($"{selector}: {label} on {fill} is not a token pair this test can measure");
                continue;
            }
            // A tinted fill is measured as painted on the page.
            var painted = Tokens.Over(tokens, fillToken.Groups[1].Value, "--bg");
            var ratio = Tokens.Contrast(Tokens.Rgb(tokens, labelToken.Groups[1].Value), painted);
            if (ratio < Floor)
                failures.Add($"{selector}: {label} on {fill} is {ratio:0.000}:1");
        }

        Assert.True(failures.Count == 0,
            $"\"{theme.Name}\", {palette}: these muted fills read under {Floor}:1.\n  " + string.Join("\n  ", failures));
    }
}
