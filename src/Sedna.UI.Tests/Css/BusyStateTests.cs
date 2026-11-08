using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The busy state is written once for every control and every surface (69-busy.css): each
/// family it names is one the sheet declares, every control that takes a focus ring and an
/// icon is in it or deliberately left out, and reduced motion stops what it animates.
/// </summary>
public class BusyStateTests
{
    private static string Part => File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", "69-busy.css"));

    private static IEnumerable<string> Classes(string selectors) =>
        Regex.Matches(selectors, @"\.(?<c>[a-z][a-z0-9-]+)").Select(m => m.Groups["c"].Value).Distinct(StringComparer.Ordinal);

    [Fact]
    public void Every_family_it_names_is_declared_elsewhere_in_the_sheet()
    {
        var css = Assets.StripComments(Assets.Css);
        var named = Classes(Assets.StripComments(Part)).ToList();
        Assert.NotEmpty(named);
        var missing = named.Where(c => Regex.Matches(css, $@"\.{Regex.Escape(c)}(?![\w-])").Count < 2).ToList();
        Assert.True(missing.Count == 0, "69-busy.css names classes nothing else declares: " + string.Join(", ", missing));
    }

    /// <summary>
    /// A control that draws the focus ring is one a reader activates, and so one whose action
    /// can be running. These have no running action of their own, or show their busy
    /// state some other way, and are left out on purpose.
    /// </summary>
    private static readonly HashSet<string> NotBusy = new(StringComparer.Ordinal)
    {
        "btn",                                  // its own label-hiding busy state, 33-buttons.css
        "form-check", "switch", "form-range", "form-color", "form-combo-btn", "input-clear", "form-input",
        "form-select", "form-combo-box", "dropzone", "form-file",   // fields: read-only, not busy
        "chip-dismiss", "modal-close", "toast-close", "th-sort", "th-filter", "skip-link", "swatch",
        "deck-dot", "drag-item", "drag-handle", "card-resize", "split-resize", "col-resize", "value-trigger",
        "accordion", "tree", "nav-group", "nav-area", "topbar-nav-link", "reasoning",
        "agent-tool",                           // opens on a call; the call's state is its badge
        "output-details", "media-transcript", "media-transcript-time", "media-open", "lightbox-prev",
        "lightbox-next", "code-block-copy", "code-block-expand", "file-item", "file-tile", "file-tile-open",
        "link-preview", "gallery", "media-frame", "chat-reaction", "chat-emoji", "chat-jump", "chat-launcher",
        "chat-pane", "chat", "cal-event", "cal-block", "cal-pick", "cal-day", "cal-weeknum", "datepicker-title",
        "workflow", "workflow-node", "graph", "stat", "list--cards", "list", "input-group", "segmented",
        "form-combo-group-action", "form-combo-input", "form-combo-option", "form-combo",
        "choice-tile",                          // a choice, read-only not busy
        "cal-mark",                             // opens a list; nothing of its own runs
        "sticky-group-toggle",                  // folds a group; nothing of its own runs
        "editor",                               // a field: read-only, not busy — the form it sits in is
        "input-reveal",                         // shows the field's text; nothing of its own runs
        "rating-star", "rating-half",           // a choice, as a radio is
        "inbox-item",                           // its ring is its title's, a link; nothing of its own runs
        "page-banner-dismiss",                  // removes the banner; nothing of its own runs
        "app-switcher-item",                    // a link to another app
        "page-toc-fold", "page-toc-link",       // folds the contents, jumps within the page; nothing runs
        "back-to-top",                          // scrolls the page it is in; nothing runs
        "sheet-handle",                         // moves its sheet between heights; nothing of its own runs
    };

    [Fact]
    public void Every_control_with_a_focus_ring_has_a_busy_state_or_a_reason_not_to()
    {
        var css = Assets.StripComments(Tokens.WithoutMediaBlocks(Assets.Css));
        var busy = Classes(Assets.StripComments(Part)).ToHashSet(StringComparer.Ordinal);
        var rings = Regex.Matches(css, @"(?<s>[^{}]+)\{[^{}]*var\(--focus-ring\)[^{}]*\}")
            .SelectMany(m => m.Groups["s"].Value.Split(','))
            .Select(s => Regex.Match(s.Trim(), @"^[a-z]*\.(?<c>[a-z][a-z0-9-]*?)(?=[:.\[ >)]|$)").Groups["c"].Value)
            .Where(c => c.Length > 0 && !c.StartsWith("btn--", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var missing = rings.Where(c => !busy.Contains(c) && !NotBusy.Contains(c)).ToList();
        Assert.True(missing.Count == 0,
            "These controls take the focus ring and have no busy state in 69-busy.css, and no reason "
            + "in NotBusy: " + string.Join(", ", missing));
    }
}
