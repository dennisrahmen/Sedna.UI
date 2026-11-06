using System.Text;
using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every focus ring the sheet draws is restated for forced colours, where it would otherwise not
/// be painted at all.
/// </summary>
/// <remarks>
/// Each ring is a <c>box-shadow</c> of <c>var(--focus-ring)</c>, and forced colours paint no
/// shadow — so a control whose ring is not restated as an outline in 71-forced-colors.css shows
/// nothing where the keyboard is. The list there was hand-kept and a third of the controls were
/// missing from it. Every rule that draws the ring is found here, so a new control cannot be.
/// </remarks>
public class ForcedColorsFocusTests
{
    private static readonly Regex Rule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

    [Fact]
    public void Every_focus_ring_is_restated_for_forced_colours()
    {
        var css = Assets.StripComments(Assets.Css);
        var forced = ForcedBlocks(css);
        Assert.NotEmpty(forced);

        var restated = Rule.Matches(forced)
            .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"(?<![-\w])outline\s*:\s*(?!none)"))
            .SelectMany(m => SplitSelectors(m.Groups["selector"].Value))
            .Select(Normalise)
            .ToHashSet(StringComparer.Ordinal);

        var rings = Rule.Matches(Tokens.WithoutMediaBlocks(css))
            .Where(m => m.Groups["body"].Value.Contains("var(--focus-ring)", StringComparison.Ordinal))
            .SelectMany(m => SplitSelectors(m.Groups["selector"].Value))
            .Select(s => (Selector: s, Key: Normalise(s)))
            .ToList();
        Assert.NotEmpty(rings);

        var missing = rings
            .Where(r => !restated.Contains(r.Key) && !restated.Any(f => r.Key.EndsWith(" " + f, StringComparison.Ordinal)))
            .Select(r => r.Selector)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "These focus rings are box-shadows with no outline in @media (forced-colors: active), so "
            + "forced colours show no focus on them. Add each to the focus list in 71-forced-colors.css:\n  "
            + string.Join("\n  ", missing));
    }

    /// <summary>
    /// The selector a forced-colours outline has to name: without a tone or an emphasis, which
    /// only change the ring's colour, and without the pseudo-element a range thumb is drawn on.
    /// </summary>
    private static string Normalise(string selector)
    {
        var s = Assets.Squash(selector);
        s = Regex.Replace(s, @":is\(\.btn--[a-z]+(?:,\s*\.btn--[a-z]+)*\)", "");
        s = Regex.Replace(s, @"\.btn--solid\b", "");
        s = Regex.Replace(s, @"\[aria-invalid=""true""\]", "");
        s = Regex.Replace(s, @"::-?[a-z-]+$", "");
        return s;
    }

    private static IEnumerable<string> SplitSelectors(string list)
    {
        var depth = 0;
        var current = new StringBuilder();
        foreach (var c in list)
        {
            if (c == '(') depth++;
            if (c == ')') depth--;
            if (c == ',' && depth == 0)
            {
                if (current.ToString().Trim() is { Length: > 0 } s) yield return s;
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        if (current.ToString().Trim() is { Length: > 0 } last) yield return last;
    }

    /// <summary>The bodies of every <c>@media (forced-colors: active)</c> block, braces counted.</summary>
    private static string ForcedBlocks(string css)
    {
        var bodies = new StringBuilder();
        foreach (Match open in Regex.Matches(css, @"@media[^{]*forced-colors\s*:\s*active[^{]*\{"))
        {
            var depth = 1;
            var i = open.Index + open.Length;
            var start = i;
            for (; i < css.Length && depth > 0; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}') depth--;
            }
            bodies.Append(css, start, i - 1 - start).Append('\n');
        }
        return bodies.ToString();
    }
}
