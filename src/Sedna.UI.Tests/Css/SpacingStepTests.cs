using System.Globalization;
using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The gap steps and the margin steps are one scale, and each step is named by its size: the
/// number in <c>.sedna-gap-16</c> is the pixels its token holds.
/// </summary>
/// <remarks>
/// The steps were numbered once, 1 to 6, and the guide's space between fields, 16px, fell
/// between two of them; adding it would have moved every step above it to a new number, and a
/// renumbered class keeps its name while changing its size, which an app's class check cannot
/// see. Named by size, a step can be added without moving another. These hold every name to
/// its size, and the two families to each other.
/// </remarks>
public partial class SpacingStepTests
{
    [GeneratedRegex(@"\.sedna-(?<family>gap|mt|mb)-(?<step>\d+)\s*\{\s*(?:gap|margin-top|margin-bottom)\s*:\s*(?<value>[^;]+);")]
    private static partial Regex Step();

    private static Dictionary<string, SortedDictionary<int, string>> Scales()
    {
        var scales = new Dictionary<string, SortedDictionary<int, string>>(StringComparer.Ordinal);
        foreach (Match m in Step().Matches(Assets.StripComments(Assets.Css)))
        {
            var family = m.Groups["family"].Value;
            if (!scales.TryGetValue(family, out var steps)) scales[family] = steps = [];
            steps[int.Parse(m.Groups["step"].Value, CultureInfo.InvariantCulture)] = m.Groups["value"].Value.Trim();
        }
        return scales;
    }

    /// <summary>What each <c>--space-*</c> token holds, in pixels, from the token block.</summary>
    private static Dictionary<string, int> SpaceTokens() =>
        Regex.Matches(Assets.StripComments(Assets.Css), @"(?<name>--space-\d+)\s*:\s*(?<px>\d+)px\s*;")
            .GroupBy(m => m.Groups["name"].Value)
            .ToDictionary(g => g.Key, g => int.Parse(g.First().Groups["px"].Value, CultureInfo.InvariantCulture), StringComparer.Ordinal);

    [Fact]
    public void Gaps_and_margins_have_the_same_steps()
    {
        var scales = Scales();
        Assert.Equal(["gap", "mb", "mt"], scales.Keys.Order(StringComparer.Ordinal));

        var gap = scales["gap"];
        foreach (var family in new[] { "mt", "mb" })
            Assert.True(gap.SequenceEqual(scales[family]),
                $"sedna-{family}-* is not the gap scale: gap is [{string.Join(", ", gap.Select(s => $"{s.Key}={s.Value}"))}], "
                + $"{family} is [{string.Join(", ", scales[family].Select(s => $"{s.Key}={s.Value}"))}].");
    }

    [Fact]
    public void Every_step_is_named_by_the_size_it_holds()
    {
        var tokens = SpaceTokens();
        var problems = new List<string>();
        foreach (var (step, value) in Scales()["gap"])
        {
            if (step == 0)
            {
                if (value != "0") problems.Add($"sedna-gap-0 is {value}, not 0.");
                continue;
            }

            var m = Regex.Match(value, @"^var\((--space-\d+)\)$");
            if (!m.Success) problems.Add($"sedna-gap-{step} is {value}, not a --space-* token.");
            else if (!tokens.TryGetValue(m.Groups[1].Value, out var px)) problems.Add($"sedna-gap-{step} reads {m.Groups[1].Value}, which is declared nowhere.");
            else if (px != step) problems.Add($"sedna-gap-{step} reads {m.Groups[1].Value}, which is {px}px.");
        }
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_space_between_fields_is_a_step()
    {
        // The 16px docs/design-guide/principles.md gives between fields.
        Assert.Contains(16, Scales()["gap"].Keys);
    }
}
