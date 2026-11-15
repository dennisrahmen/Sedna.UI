using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A container that is entirely one link — a card, a stat, a list row, a tile — answers the
/// pointer and the keyboard on its own frame: a hover state and a focus ring. A family that
/// can be an <c>&lt;a&gt;</c> and has neither is the gap a marketing page found in
/// <c>.card</c>; this holds every family to the same two states.
/// </summary>
public class LinkFamilyTests
{
    /// <summary>Whole-container links. Each has a hover and a focus ring of its own.</summary>
    private static readonly string[] Containers =
    [
        "card", "stat", "list-row", "chip", "source-card", "link-preview", "file-item", "file-tile",
        "app-switcher-item", "cal-event", "workflow-node",
    ];

    /// <summary>
    /// Links that are a run of text or a part of a larger control: they keep the browser's own
    /// focus outline, which is the right ring for text, and have no frame to give a state to.
    /// </summary>
    private static readonly string[] TextLinks =
    [
        "breadcrumb-item", "file-cell", "topbar-version", "topbar-version--pre", "cal-agenda-title",
        "inbox-item-title", "toast-body",
    ];

    private static readonly Regex Rule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

    private static List<(string Selector, string Body)> Rules() =>
        Rule.Matches(Assets.StripComments(Assets.Css))
            .Select(m => (Assets.Squash(m.Groups["selector"].Value), m.Groups["body"].Value))
            .ToList();

    private static bool Names(string selector, string family) =>
        Regex.IsMatch(selector, @"\." + Regex.Escape(family) + @"(?![\w-])");

    [Fact]
    public void Every_container_that_can_be_a_link_has_a_hover_and_a_focus_ring()
    {
        var rules = Rules();
        var offenders = new List<string>();

        foreach (var family in Containers)
        {
            var own = rules.Where(r => Names(r.Selector, family)).ToList();
            if (!own.Any(r => r.Selector.Contains(":hover", StringComparison.Ordinal)))
                offenders.Add($".{family} has no :hover state");
            if (!own.Any(r => Regex.IsMatch(r.Selector, @":focus-visible|:focus-within")
                              && (r.Body.Contains("--focus-ring", StringComparison.Ordinal)
                                  || r.Body.Contains("outline", StringComparison.Ordinal))))
                offenders.Add($".{family} has no :focus-visible ring");
        }

        Assert.True(offenders.Count == 0,
            "A container that is one link answers the pointer and the keyboard on its own frame, as a.stat and a .list-row do:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void A_family_the_stylesheet_styles_as_a_link_is_a_container_or_a_text_link_and_says_so()
    {
        // `a.family` and `:is(a, button).family` are how the stylesheet says a family is a link. One that
        // is neither listed above as a container, which then has the two states, nor as a text link,
        // is a new linked family nobody has decided about.
        var css = Assets.StripComments(Assets.Css);
        var linked = Regex.Matches(css, @"(?<![\w-])a\.(?<n>[a-z][\w-]*)")
            .Concat(Regex.Matches(css, @":is\(a(?:, button)?\)\.(?<n>[a-z][\w-]*)"))
            .Select(m => m.Groups["n"].Value)
            .Distinct()
            .Where(n => !Containers.Contains(n) && !TextLinks.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(linked.Count == 0,
            "These families are styled as links and are in neither list in LinkFamilyTests. A container that is one link "
            + "goes in Containers and gets a :hover and a :focus-visible ring; a link that is a run of text goes in TextLinks:\n  "
            + string.Join(", ", linked));
    }

    [Fact]
    public void Both_lists_name_families_the_stylesheet_has()
    {
        var css = Assets.StripComments(Assets.Css);
        var missing = Containers.Concat(TextLinks)
            .Where(f => !Regex.IsMatch(css, @"\." + Regex.Escape(f) + @"(?![\w-])"))
            .ToList();
        Assert.True(missing.Count == 0, "No longer in the stylesheet: " + string.Join(", ", missing));
    }
}
