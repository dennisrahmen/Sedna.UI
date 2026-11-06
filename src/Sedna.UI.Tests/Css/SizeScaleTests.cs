using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// One size scale: a family that offers a size step offers both, <c>--sm</c> and <c>--lg</c>
/// around its default, so an app that sizes one component of a row can size every other.
/// </summary>
public class SizeScaleTests
{
    [Fact]
    public void Every_sized_family_has_a_small_and_a_large_step()
    {
        var css = Assets.StripComments(Assets.Css);
        var sizes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(css, @"\.(?<family>[a-z][a-z0-9-]*?)--(?<size>sm|lg|xl|xs)(?![\w-])"))
        {
            if (!sizes.TryGetValue(m.Groups["family"].Value, out var set))
                sizes[m.Groups["family"].Value] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(m.Groups["size"].Value);
        }
        Assert.NotEmpty(sizes);

        var lopsided = sizes.Where(s => !(s.Value.Contains("sm") && s.Value.Contains("lg")))
            .Select(s => $".{s.Key} has only --{string.Join(", --", s.Value.Order(StringComparer.Ordinal))}")
            .ToList();
        Assert.True(lopsided.Count == 0, "Add the missing step:\n  " + string.Join("\n  ", lopsided));
    }
}
