using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every framed panel sits flush in a card through a <c>--flush</c> modifier of its own.
/// </summary>
/// <remarks>
/// <para>
/// A panel that draws its own frame — a border and the surface radius around content it
/// clips — draws a second frame inside a card's. <c>.code-block--flush</c> answered that
/// for code; an agenda in a card answered it with an inline <c>border: 0; border-radius:
/// 0</c>, which is the gap. Every such panel now takes the same modifier.
/// </para>
/// <para>
/// The panels are found, not listed: a single-class rule that draws
/// <c>border: 1px solid var(--border)</c> with <c>--radius-surface</c> corners, and either
/// clips what it holds or paints the card's ground. A panel added later is held at once.
/// </para>
/// </remarks>
public class FlushFrameTests : ScriptTestBase
{
    /// <summary>
    /// Framed panels that never sit inside another frame, each for a reason that is about
    /// the panel and not about this test.
    /// </summary>
    private static readonly Dictionary<string, string> NeverInAFrame = new(StringComparer.Ordinal)
    {
        ["card"] = "a card in a card is a nested card, stepped up the surface ladder on purpose",
        ["chat-flyover"] = "fixed over the page, never inside another frame",
        ["search-panel"] = "the header search's dropdown, over the page and never inside another frame",
        ["inbox"] = "it is the panel of its popover, and the frame is its own",
        ["artifact"] = "a panel beside a chat; in a drawer `.drawer > .artifact` gives the frame up",
        ["media-frame"] = "the frame marks where a picture's edge is, inside a body's padding",
    };

    private static IReadOnlyList<string> FramedPanels()
    {
        var css = Assets.StripComments(Assets.Css);
        return Regex.Matches(css, @"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}")
            .Where(m => Regex.IsMatch(Assets.Squash(m.Groups["sel"].Value), @"^\.[a-z][a-z0-9-]*$"))
            .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"(?<![\w-])border\s*:\s*1px solid var\(--border\)"))
            .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"(?<![\w-])border-radius\s*:\s*var\(--radius-surface\)"))
            .Where(m => Regex.IsMatch(m.Groups["body"].Value, @"(?<![\w-])overflow\s*:\s*(?:hidden|clip)|(?<![\w-])background\s*:\s*var\(--card-bg\)"))
            .Select(m => Assets.Squash(m.Groups["sel"].Value)[1..])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> Flushable() =>
        FramedPanels().Where(n => !NeverInAFrame.ContainsKey(n)).ToList();

    [Fact]
    public void Every_framed_panel_takes_a_flush_modifier()
    {
        var css = Assets.StripComments(Assets.Css);
        var panels = Flushable();

        // The ones this was written for, so a change to the scan cannot quietly empty it.
        foreach (var known in new[] { "code-block", "diff", "cal-agenda", "cal-month", "cal-week", "cal-timeline" })
            Assert.Contains(known, panels);

        var missing = panels
            .Where(p => !Regex.IsMatch(css,
                $@"(?<![\w-])\.{Regex.Escape(p)}--flush\s*\{{[^{{}}]*border\s*:\s*(?:none|0)[^{{}}]*border-radius\s*:\s*0"))
            .ToList();

        Assert.True(missing.Count == 0,
            "These draw a frame of their own and have no `--flush` that gives it up inside a card, "
            + "so an app writes `border: 0; border-radius: 0` inline: ." + string.Join(", .", missing));

        // An exception that no longer matches the scan is a stale reason.
        var stale = NeverInAFrame.Keys.Except(FramedPanels()).ToList();
        Assert.True(stale.Count == 0, "No longer a framed panel, so drop the exception: " + string.Join(", ", stale));
    }

    [Fact]
    public async Task A_flush_panel_in_a_card_leaves_the_card_the_only_frame()
    {
        if (NoBrowser) return;
        var panels = Flushable();
        var fixture = string.Concat(panels.Select(p => $"""
            <div class="card" style="width:320px">
              <div class="{p}" data-plain="{p}" style="height:40px"></div>
              <div class="{p} {p}--flush" data-flush="{p}" style="height:40px"></div>
            </div>
            """));
        var (page, errors) = await OpenStyled(fixture);

        var frames = await page.EvaluateAsync<Dictionary<string, string[]>>("""
            () => Object.fromEntries([...document.querySelectorAll('[data-plain], [data-flush]')].map(el => {
                const cs = getComputedStyle(el);
                const key = (el.dataset.plain ? 'plain:' + el.dataset.plain : 'flush:' + el.dataset.flush);
                return [key, [cs.borderTopWidth, cs.borderInlineStartWidth, cs.borderTopLeftRadius]];
            }))
            """);

        foreach (var p in panels)
        {
            Assert.True(frames[$"plain:{p}"][0] == "1px", $"{Engine}: .{p} draws no frame of its own, so the scan found the wrong rule.");
            Assert.True(frames[$"flush:{p}"] is ["0px", "0px", "0px"],
                $"{Engine}: .{p}--flush in a card still draws {string.Join(" / ", frames[$"flush:{p}"])} (border top, border start, corner).");
        }

        Assert.Empty(errors);
    }
}
