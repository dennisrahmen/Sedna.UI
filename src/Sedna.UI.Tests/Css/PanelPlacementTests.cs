using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every panel anchored to a trigger opens on the side <c>--panel-placement</c> names.
/// </summary>
/// <remarks>
/// <para>
/// The token is read with a style query per placement, one
/// <c>@container style(--panel-placement: …)</c> block for each of the three that are
/// not the default, and each anchored panel restates its <c>position-area</c> inside
/// every one of them. A panel added with a <c>position-area</c> of its own and no rule
/// in those blocks would open below its trigger whatever the app asked for, with
/// nothing failing — so this reads the stylesheet for every rule that places a box
/// with <c>position-area</c> and asks that each is placed in all three blocks.
/// </para>
/// <para>
/// <c>AnchoredPlacementTests</c> measures where each panel lands; this is the half that
/// catches the next panel, which no browser fixture yet knows about.
/// </para>
/// </remarks>
public class PanelPlacementTests
{
    private static readonly string[] Placements = ["block-start", "inline-end", "inline-start"];

    /// <summary>
    /// Boxes placed with <c>position-area</c> that are not a panel opened from a trigger,
    /// each with the reason it has no placement to choose.
    /// </summary>
    private static readonly Dictionary<string, string> NotPanels = new(StringComparer.Ordinal)
    {
        [".sidebar.collapsed [data-tip]:hover::after"] =
            "the collapsed rail's hover label: the rail is the frame's inline-start edge, so the label has one side to open on",
        [".layout--responsive .sidebar [data-tip]:hover::after"] =
            "the same label on the responsive rail, which mirrors the collapsed one",
        [".menu-sub-anchor > .menu-sub"] =
            "a submenu opens beside its item by construction, wherever its own menu opened",
        [".menu[popover]"] =
            "a menu at the pointer opens where the reader clicked; a point has no side to choose",
    };

    [Fact]
    public void Every_anchored_panel_takes_every_placement()
    {
        var css = Assets.StripComments(File.ReadAllText(Assets.Readable("Sedna.UI.css")));
        var rules = Rules(css, []).ToList();

        var panels = rules
            .Where(r => !r.Context.Any(c => c.StartsWith("@container style(--panel-placement", StringComparison.Ordinal)
                                           || c.StartsWith("@media", StringComparison.Ordinal)))
            .Where(r => PositionArea(r.Body) is { } area && area != "none")
            .SelectMany(r => SplitSelectors(r.Selector))
            .Where(s => !NotPanels.ContainsKey(s))
            .Distinct()
            .ToList();

        Assert.True(panels.Count >= 6, $"Expected the anchored panels to be found; found {string.Join(", ", panels)}.");

        var missing = new List<string>();
        foreach (var placement in Placements)
        {
            var placed = rules
                .Where(r => r.Context.Contains($"@container style(--panel-placement: {placement})"))
                .Where(r => PositionArea(r.Body) is { } area && area != "none")
                .SelectMany(r => SplitSelectors(r.Selector))
                .ToList();

            foreach (var panel in panels)
            {
                // A modifier (`.menu--start`) is placed by its block's rule where the
                // placement does not distinguish it, so either form counts.
                var block = Regex.Replace(panel, @"(\.[a-z0-9-]+?)--[a-z0-9-]+", "$1");
                if (!placed.Any(s => s.EndsWith(panel, StringComparison.Ordinal) || s.EndsWith(block, StringComparison.Ordinal)))
                    missing.Add($"{panel} has no position-area under @container style(--panel-placement: {placement})");
            }
        }

        Assert.True(missing.Count == 0,
            "Every panel anchored to a trigger opens where --panel-placement says, which is a rule "
            + "for it in each placement's style query (58-menu.css has the pattern). A box placed "
            + "with position-area that is not such a panel goes in NotPanels with its reason."
            + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Every_placement_keeps_a_way_back_where_there_is_no_room()
    {
        var css = Assets.StripComments(File.ReadAllText(Assets.Readable("Sedna.UI.css")));
        var none = Rules(css, [])
            .Where(r => r.Context.Any(c => c.StartsWith("@container style(--panel-placement", StringComparison.Ordinal)))
            .Where(r => Regex.IsMatch(r.Body, @"position-try-fallbacks\s*:\s*none"))
            .Select(r => Assets.Squash(r.Selector))
            .ToList();

        Assert.True(none.Count == 0,
            "A chosen placement still flips where there is no room on its side: "
            + string.Join(", ", none));
    }

    [Fact]
    public void The_token_is_declared_with_its_default()
    {
        var tokens = string.Join("\n", Assets.TokenBlocks(Assets.StripComments(Assets.Css)).Select(b => b.Body));
        Assert.Matches(@"--panel-placement\s*:\s*block-end\s*;", tokens);
    }

    private static string? PositionArea(string body)
    {
        var m = Regex.Match(body, @"(?:^|[;{\s])position-area\s*:\s*([^;]+)");
        return m.Success ? Assets.Squash(m.Groups[1].Value) : null;
    }

    private static IEnumerable<string> SplitSelectors(string selector)
    {
        var depth = 0;
        var start = 0;
        var s = Assets.Squash(selector);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')') depth--;
            else if (s[i] == ',' && depth == 0)
            {
                yield return s[start..i].Trim();
                start = i + 1;
            }
        }
        yield return s[start..].Trim();
    }

    /// <summary>Every style rule in a block, with the at-rules it sits in.</summary>
    private static IEnumerable<(string Selector, string Body, string[] Context)> Rules(string block, string[] context)
    {
        foreach (var (prelude, body) in Assets.TopLevelRules(block))
        {
            var head = Assets.Squash(prelude);
            if (head.StartsWith('@'))
            {
                foreach (var rule in Rules(body, [.. context, head]))
                    yield return rule;
            }
            else
            {
                yield return (head, body, context);
            }
        }
    }
}
