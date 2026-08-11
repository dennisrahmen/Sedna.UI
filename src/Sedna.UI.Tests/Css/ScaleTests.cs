using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Spacing, type and motion ride their scales, so a literal cannot escape the token layer.
/// </summary>
public class ScaleTests
{
    [Fact]
    public void Spacing_type_and_motion_ride_their_scales()
    {
        // Same reasoning as the colour rule, applied to the other three things an app
        // may want to rescale: a literal is invisible to the token layer, so one
        // hard-coded 14px means "make this app denser" cannot reach that rule.
        //
        // Scoped to the properties that genuinely express space, size of type, and
        // duration. Widths, heights and offsets are excluded on purpose — a 28px close
        // button and a 34px switch track are dimensions of a thing, not space between
        // things, and forcing them onto a spacing ramp would let a density change break
        // the switch.
        //
        // 0 and 1px are always allowed: zero is not a scale step, and a 1px hairline is
        // a border, not spacing.
        const string spacingProps =
            @"padding|padding-top|padding-right|padding-bottom|padding-left|"
            + @"padding-block|padding-inline|padding-inline-start|padding-inline-end|"
            + @"margin|margin-top|margin-right|margin-bottom|margin-left|"
            + @"margin-block|margin-inline|margin-inline-start|margin-inline-end|"
            + @"gap|row-gap|column-gap";

        var css = Assets.StripComments(Assets.Css);
        var offenders = new List<string>();

        // The token block declares the scales, so it is the one place literals live.
        var tokenRanges = TokenBlockRanges(css);
        bool InTokenBlock(int index) => tokenRanges.Any(r => index >= r.Start && index < r.End);

        void Scan(string pattern, string what)
        {
            foreach (var m in Regex.Matches(css, pattern).Cast<Match>())
            {
                if (InTokenBlock(m.Index)) continue;

                var value = m.Groups["value"].Value;
                foreach (var lit in Regex.Matches(value, @"(?<![\w.-])(\d+)px(?![\w-])").Cast<Match>())
                {
                    if (lit.Groups[1].Value is "0" or "1") continue;
                    var line = css.Take(m.Index).Count(c => c == '\n') + 1;
                    offenders.Add($"line {line}: {what} — {Assets.Squash(m.Value)}");
                }
            }
        }

        Scan($@"(?<![\w-])(?:{spacingProps})\s*:(?<value>[^;}}]+)", "spacing");
        Scan(@"(?<![\w-])font-size\s*:(?<value>[^;}]+)", "type");

        // Durations: any bare `<n>s` outside the token block.
        foreach (var m in Regex.Matches(css, @"(?<![\w-])(?:transition|animation)\s*:(?<value>[^;}]+)").Cast<Match>())
        {
            if (InTokenBlock(m.Index)) continue;
            foreach (var _ in Regex.Matches(m.Groups["value"].Value, @"(?<![\w.])\d*\.?\d+s(?![\w])"))
            {
                var line = css.Take(m.Index).Count(c => c == '\n') + 1;
                offenders.Add($"line {line}: motion — {Assets.Squash(m.Value)}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Spacing, type sizes and durations must come from --space-*, --text-* and --motion-* / "
            + "--*-duration, or an app cannot rescale them. A literal here is invisible to the token "
            + "layer:" + $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    /// <summary>Character ranges covered by a token block, so its own literals are exempt.</summary>
    private static List<(int Start, int End)> TokenBlockRanges(string css) =>
        Regex.Matches(css, @"(?::root(?:\[[^\]]*\])*)\s*\{[^{}]*\}")
            .Select(m => (m.Index, m.Index + m.Length))
            .ToList();
}
