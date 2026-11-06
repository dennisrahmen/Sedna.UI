using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// One tone system (69-tone.css): every family that carries a tone takes every semantic tone,
/// reads it through the shared rules, and no tone is written the old way beside it.
/// </summary>
/// <remarks>
/// Before, each family had its own list — buttons had no info, badges no brand, alerts no
/// neutral, stat tiles only warn and danger — and each lack was found by an app. A family is
/// now named once in the tone carriers, and this holds it to the whole set.
/// </remarks>
public class ToneMatrixTests
{
    private static readonly string[] Semantic = ["brand", "go", "warn", "danger", "info", "secret"];
    private static readonly string[] Categorical = ["cyan", "orange", "teal"];
    private static readonly string[] Labels = ["badge", "chip"];
    private static readonly string[] Emphasised = ["btn", "badge", "alert"];

    [Fact]
    public void Every_toned_family_takes_every_semantic_tone()
    {
        var families = Tones.Families();
        Assert.NotEmpty(families);
        foreach (var (family, tones) in families)
        {
            var missing = Semantic.Except(tones).ToList();
            Assert.True(missing.Count == 0, $".{family} lacks the tones {string.Join(", ", missing)}.");
            if (Labels.Contains(family))
                Assert.Empty(Categorical.Except(tones));
        }
    }

    [Fact]
    public void Every_tone_carrier_sets_every_tone_property()
    {
        var properties = Tones.Carriers().Values.SelectMany(p => p.Keys).ToHashSet(StringComparer.Ordinal);
        foreach (var (tone, props) in Tones.Carriers())
        {
            var missing = properties.Except(props.Keys).ToList();
            Assert.True(missing.Count == 0, $"The {tone} tone does not set --tone-{string.Join(", --tone-", missing)}.");
        }
    }

    [Fact]
    public void Every_toned_family_reads_its_tone_through_the_shared_rules()
    {
        var css = Assets.StripComments(Assets.Css);
        foreach (var family in Tones.Families().Keys)
        {
            var wired = family == "tr"
                ? Regex.IsMatch(css, @"tr:is\(\.tr--")
                : Regex.IsMatch(css, $@"\.{family}:is\(\.{family}--");
            Assert.True(wired, $".{family} has tone carriers but no rule reads --tone-* for it.");
        }
        foreach (var family in Emphasised)
        {
            Assert.Matches($@"\.{family}\.{family}--solid\s*\{{", css);
            if (family != "alert") Assert.Matches($@"\.{family}\.{family}--ghost\s*\{{", css);
        }
    }

    [Fact]
    public void No_tone_is_written_the_old_way()
    {
        // A tone as a single-dash suffix with its own colours — `.badge-go { background … }` —
        // is a second tone system beside this one. The series palette, the text utilities and the
        // user widget's `.user-info` block are names of their own, not tones of a family.
        var css = Assets.StripComments(Assets.Css);
        var old = Regex.Matches(css, @"(?<![\w-])\.(?<name>[a-z]+(?:-[a-z]+)*-(?:go|warn|danger|info|secret|brand))(?![\w-])")
            .Select(m => m.Groups["name"].Value)
            .Where(n => !n.StartsWith("series-", StringComparison.Ordinal) && !n.StartsWith("text-", StringComparison.Ordinal)
                        && !n.StartsWith("bare-", StringComparison.Ordinal) && !n.StartsWith("auth-", StringComparison.Ordinal)
                        && n != "user-info")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(old.Count == 0, "Tones written as a single-dash suffix: " + string.Join(", ", old));
    }
}
