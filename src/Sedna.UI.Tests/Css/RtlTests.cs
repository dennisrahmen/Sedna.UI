using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The layout mirrors from dir="rtl" on its own; a physical property needs a justification.
/// </summary>
public class RtlTests
{
    /// <summary>
    /// A directional property that has a logical equivalent. Matched on the property
    /// name at the start of a declaration, so `border-inline-start` and a value
    /// containing the word "left" are both untouched.
    /// </summary>
    private static readonly Regex PhysicalDirection = new(
        @"(?<![\w-])(?:"
        + @"margin-(?:left|right)|padding-(?:left|right)|"
        + @"border-(?:left|right)(?:-(?:color|width|style))?|"
        + @"border-(?:top|bottom)-(?:left|right)-radius|"
        + @"(?:left|right)\s*:|"
        + @"text-align\s*:\s*(?:left|right)"
        + @")",
        RegexOptions.Compiled);

    [Fact]
    public void No_physical_direction_properties_without_a_justification()
    {
        // Every directional property here is logical — margin-inline-start,
        // border-inline-end, inset-inline, text-align: start — which is what lets the
        // whole layout mirror from dir="rtl" with almost no rules at all. 70-rtl.css is
        // three-quarters comment for that reason.
        //
        // The exceptions are real gaps in CSS rather than shortcuts, so each one has to
        // say why on the line above it: an `/* rtl-ok: … */` marker. Two exist —
        // centring (where a translate does the offset, so direction is irrelevant) and
        // the hover hint, whose `left` is overwritten in pixels by the script anyway.
        //
        // The marker is required on the PRECEDING line rather than anywhere in the
        // file, so it cannot drift away from what it justifies.
        var offenders = new List<string>();

        foreach (var part in Directory.GetFiles(Path.Combine(Assets.ProjectDir, "css-parts"), "*.css")
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var lines = Assets.StripComments(File.ReadAllText(part)).Split('\n');
            var raw = File.ReadAllText(part).Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                if (!PhysicalDirection.IsMatch(lines[i])) continue;

                // A [dir="rtl"] rule is the whole point of 70-rtl.css: it exists to
                // express what a logical property cannot.
                if (Path.GetFileName(part) == "70-rtl.css") continue;

                // The marker may sit on the same line or anywhere in the comment block
                // immediately above it — a one-line window would reject a two-line
                // justification, which is most of them.
                var justified = raw[i].Contains("rtl-ok:", StringComparison.Ordinal);
                for (var back = 1; back <= 3 && !justified && i - back >= 0; back++)
                    justified = raw[i - back].Contains("rtl-ok:", StringComparison.Ordinal);
                if (!justified)
                    offenders.Add($"{Path.GetFileName(part)}:{i + 1}: {lines[i].Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Use the logical property — margin-inline-start, border-inline-end, inset-inline, "
            + "text-align: start — so the layout mirrors from dir=\"rtl\" on its own. Where physical "
            + "really is correct, put an /* rtl-ok: why */ comment on the line above:"
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }
}
