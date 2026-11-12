using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// No animation in the stylesheet holds a property that makes its element the containing
/// block of a fixed descendant after it has finished.
/// </summary>
/// <remarks>
/// <para>
/// A finished animation with fill mode <c>both</c> or <c>forwards</c> stays in effect,
/// and an element with a <c>transform</c>, <c>filter</c> or similar property in effect is
/// the containing block of every <c>position: fixed</c> descendant — whatever the value it
/// rests at. Every anchored panel in the library is fixed (<c>.menu</c>,
/// <c>.popover</c>, <c>.form-combo-panel</c>), so such an element traps the panel inside
/// itself and the nearest <c>overflow: hidden</c> clips it. That is what the staggered
/// rows did to a row-actions menu inside a card.
/// </para>
/// <para>
/// An entrance animation that has to hide its element before its turn keeps fill mode
/// <c>backwards</c>: the first keyframe applies through the delay, and nothing remains
/// once it ends. <see cref="EntranceAnimationTests"/> measures the result in a browser.
/// </para>
/// </remarks>
public class EntranceAnimationScanTests
{
    private static readonly char[] Separators = [' ', '\t', '\n', '\r'];

    /// <summary>
    /// Every property that, while an animation holds it, makes the element the containing
    /// block of a fixed descendant.
    /// </summary>
    private static readonly string[] ContainingBlockProperties =
    [
        "transform", "translate", "rotate", "scale", "perspective", "filter", "backdrop-filter",
        "contain", "container-type", "will-change", "offset-path", "transform-style",
    ];

    private static readonly Regex Shorthand = new(@"(?<![\w-])animation\s*:\s*(?<value>[^;}]+)", RegexOptions.Compiled);
    private static readonly Regex FillLonghand = new(@"(?<![\w-])animation-fill-mode\s*:\s*(?<value>[^;}]+)", RegexOptions.Compiled);
    private static readonly Regex NameLonghand = new(@"(?<![\w-])animation-name\s*:\s*(?<value>[^;}]+)", RegexOptions.Compiled);

    [Fact]
    public void No_held_animation_makes_its_element_a_containing_block()
    {
        var css = Assets.StripComments(File.ReadAllText(Assets.Readable("Sedna.UI.css")));
        var keyframes = Keyframes(css);
        Assert.True(keyframes.Count >= 10, $"Only {keyframes.Count} @keyframes were read; the parser has stopped matching.");

        var held = Rules(css)
            .SelectMany(rule => HeldAnimations(rule.Body, keyframes.Keys).Select(name => (rule.Selector, Name: name)))
            .ToList();
        Assert.True(held.Count > 0, "No animation with fill mode both or forwards was found; the parser has stopped matching.");

        var trapping = held
            .Select(h => (h.Selector, h.Name, Properties: ContainingBlockProperties
                .Where(p => Regex.IsMatch(keyframes[h.Name], $@"(?<![\w-]){Regex.Escape(p)}\s*:"))
                .ToList()))
            .Where(h => h.Properties.Count > 0)
            .Select(h => $"{Assets.Squash(h.Selector)} → {h.Name} holds {string.Join(", ", h.Properties)}")
            .Distinct()
            .ToList();

        Assert.True(trapping.Count == 0,
            "These animations keep a property in effect after they finish that traps a fixed descendant — an "
            + "anchored .menu or .popover inside the element is then clipped by the nearest overflow. Use fill mode "
            + $"backwards, or none:{Environment.NewLine}{string.Join(Environment.NewLine, trapping)}");
    }

    /// <summary>The names of the animations a rule runs with fill mode both or forwards.</summary>
    private static IEnumerable<string> HeldAnimations(string body, IEnumerable<string> known)
    {
        var names = known.ToHashSet(StringComparer.Ordinal);

        foreach (Match m in Shorthand.Matches(body))
        {
            foreach (var item in m.Groups["value"].Value.Split(','))
            {
                var words = item.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                if (!words.Any(w => w is "both" or "forwards")) continue;
                foreach (var w in words.Where(names.Contains)) yield return w;
            }
        }

        // The longhands, item by item: the nth fill mode belongs to the nth name.
        var fill = FillLonghand.Match(body);
        var name = NameLonghand.Match(body);
        if (fill.Success && name.Success)
        {
            var fills = fill.Groups["value"].Value.Split(',').Select(s => s.Trim()).ToArray();
            var named = name.Groups["value"].Value.Split(',').Select(s => s.Trim()).ToArray();
            for (var i = 0; i < named.Length; i++)
                if (fills[i % fills.Length] is "both" or "forwards" && names.Contains(named[i]))
                    yield return named[i];
        }
    }

    /// <summary>Every <c>@keyframes</c> by name, with its body.</summary>
    private static Dictionary<string, string> Keyframes(string css)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (selector, body) in Rules(css, keepKeyframes: true))
        {
            var m = Regex.Match(selector, @"^@keyframes\s+(?<name>[\w-]+)$");
            if (m.Success) found[m.Groups["name"].Value] = body;
        }
        return found;
    }

    /// <summary>
    /// The style rules of the sheet, with those inside a <c>@layer</c>, <c>@media</c> or
    /// <c>@supports</c> block read as rules of their own.
    /// </summary>
    private static IEnumerable<(string Selector, string Body)> Rules(string css, bool keepKeyframes = false)
    {
        foreach (var (selector, body) in Assets.TopLevelRules(css))
        {
            var s = Assets.Squash(selector);
            if (s.StartsWith("@keyframes", StringComparison.Ordinal))
            {
                if (keepKeyframes) yield return (s, body);
            }
            else if (s.StartsWith('@'))
            {
                foreach (var inner in Rules(body, keepKeyframes)) yield return inner;
            }
            else if (!keepKeyframes)
            {
                yield return (s, body);
            }
        }
    }
}
