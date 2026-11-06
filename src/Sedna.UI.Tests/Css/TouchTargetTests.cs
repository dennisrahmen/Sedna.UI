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

    /// <summary>A control: a name that says it is pressed — a close, a dismiss, a clear.</summary>
    private static readonly Regex Control = new(@"\.(?<name>[a-z]+(?:-[a-z]+)*-(?:close|dismiss|clear|filter))(?![-\w])", RegexOptions.Compiled);

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
}
