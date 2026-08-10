using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Frame geometry that only a layout engine can answer: whether the rail scrolls,
/// whether the flyout escapes the container that scrolls it, and where an anchored
/// popover actually lands.
/// </summary>
/// <remarks>
/// None of this is visible to a source scan. The rail spent a release with
/// <c>overflow: visible</c> on its scroller, which parses fine and reads fine and
/// silently puts half the navigation past the bottom of the viewport.
/// </remarks>
public class FrameLayoutTests : ScriptTestBase
{
    /// <summary>A sidebar with far more links than fit, which is the case that bites.</summary>
    private static string Rail(bool collapsed) =>
        $$"""
        <div class="layout" style="height:400px">
          <aside class="sidebar {{(collapsed ? "collapsed" : "")}}">
            <a class="brand" href="#"><span class="brand-logo"></span>
              <span class="brand-text"><strong>Console</strong></span></a>
            <nav class="nav">
              <div class="nav-scroll">
                {{string.Concat(Enumerable.Range(0, 30).Select(i =>
                    $"""<a class="nav-link" href="#" data-tip="Item {i} label"><i class="ri-inbox-line"></i><span>Item {i}</span></a>"""))}}
              </div>
              <div class="nav-tools">
                <a class="nav-link nav-link-tool" href="#"><i class="ri-github-fill"></i><span>Repo</span></a>
              </div>
            </nav>
          </aside>
          <div class="content"><div class="page"><p>page</p></div></div>
        </div>
        """;

    [Fact]
    public async Task The_collapsed_rail_scrolls_and_keeps_its_tools_on_screen()
    {
        if (NoBrowser) return;
        // overflow: visible on .nav-scroll let the rail's flyout escape the 56px
        // column, and took the scrolling with it: the nav grew to its content
        // height, pushed .nav-tools past the bottom of the sidebar, and made every
        // link below the fold unreachable.
        var (page, errors) = await OpenStyled(Rail(collapsed: true));

        // [width, scrolls, toolsBottom, sidebarBottom]
        var state = await page.EvaluateAsync<int[]>("""
            () => {
                const sidebar = document.querySelector('.sidebar').getBoundingClientRect();
                const scroll = document.querySelector('.nav-scroll');
                const tools = document.querySelector('.nav-tools').getBoundingClientRect();
                return [
                    Math.round(sidebar.width),
                    scroll.scrollHeight > scroll.clientHeight ? 1 : 0,
                    Math.round(tools.bottom),
                    Math.round(sidebar.bottom)
                ];
            }
            """);

        Assert.Equal(56, state[0]);
        Assert.Equal(1, state[1]);
        Assert.True(state[2] <= state[3],
            $"The nav footer is at {state[2]} and the sidebar ends at {state[3]} — "
            + "the rail has overflowed instead of scrolling.");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_rail_flyout_escapes_the_scroll_container_and_tracks_its_own_item()
    {
        if (NoBrowser) return;
        // The flyout is position: fixed so an ancestor's overflow cannot clip it, and
        // anchor-positioned so it still knows where to go. Every item declares the
        // SAME anchor-name — anchor-scope is what stops the last one in tree order
        // winning for all of them, which would put every flyout beside the bottom item.
        var (page, errors) = await OpenStyled(Rail(collapsed: true));

        // The rule is :hover::after, so the flyout only exists while the pointer is
        // on the item. A pseudo-element has no rect API, and a position-area box
        // reports every inset as 0 — the used values live in the containing block
        // rather than in `left`. So the containing block is measured instead: an
        // unlayered `width: 100%`, which is what an app's own stylesheet would be,
        // resolves against exactly the region position-area chose.
        await page.AddStyleTagAsync(new()
        {
            Content = ".sidebar.collapsed [data-tip]:hover::after { width: 100%; }",
        });
        await page.Locator(".nav-link").First.HoverAsync();

        // [position, containing-block width, the width it should be, the viewport]
        var placement = await page.EvaluateAsync<string[]>("""
            () => {
                const after = getComputedStyle(document.querySelector('.nav-link'), '::after');
                const rail = document.querySelector('.sidebar').getBoundingClientRect();
                return [after.position, after.width, (innerWidth - rail.right) + 'px', innerWidth + 'px'];
            }
            """);

        // Fixed is what escapes the scroll container; anchoring is what aims it.
        Assert.Equal("fixed", placement[0]);

        // Everything to the right of the rail, and nothing to the left of it. A
        // pixel of tolerance for the sub-pixel edge of the sidebar's own border.
        var actual = Pixels(placement[1]);
        var beside = Pixels(placement[2]);
        Assert.True(Math.Abs(actual - beside) <= 1,
            $"The flyout's containing block is {actual}px wide; the strip beside the rail is "
            + $"{beside}px and the whole viewport is {Pixels(placement[3])}px. It is not anchored.");

        // Every item declares the same anchor-name, so without anchor-scope the last
        // one in tree order would win for all of them and every flyout would appear
        // beside the bottom item. The scope is not observable from the pseudo's
        // geometry, so the declaration itself is the assertion.
        Assert.Equal("--sedna-rail-tip", await page.EvalOnSelectorAsync<string>(
            ".nav-link", "el => getComputedStyle(el).anchorScope"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_nav_scroller_starts_at_the_header_divider()
    {
        if (NoBrowser) return;
        // A margin above .nav put the scroll container below the divider, so scrolled
        // content slid under a strip of empty sidebar rather than under the divider —
        // and the scrollbar and the scroll shadow both started late.
        var (page, errors) = await OpenStyled(Rail(collapsed: false));

        var gap = await page.EvaluateAsync<double>("""
            () => Math.round(document.querySelector('.nav-scroll').getBoundingClientRect().top
                           - document.querySelector('.brand').getBoundingClientRect().bottom)
            """);

        Assert.Equal(0, gap);

        // The first item has not moved: it carries its own top padding.
        Assert.Equal("8px", await page.EvalOnSelectorAsync<string>(
            ".nav-scroll .nav-link", "el => getComputedStyle(el).paddingTop"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_popover_opens_anchored_under_the_control_that_opened_it()
    {
        if (NoBrowser) return;
        // popovertarget makes the button the popover's implicit anchor, which is what
        // lets position-area place it with no anchor-name anywhere. Without that,
        // every popover in an app needs a unique --dashed-ident in an inline style.
        var (page, errors) = await OpenStyled(
            """
            <div style="padding:40px">
              <button class="btn" type="button" popovertarget="p1">Why?</button>
              <div class="popover" id="p1" popover>
                <strong class="popover-title">Breached</strong>
                <p>Priority 2 carries a four-hour target.</p>
              </div>
            </div>
            """);

        // Closed until asked for: the UA hides a popover that is not open, and the
        // library must not have overridden that with a display of its own.
        Assert.False(await page.Locator("#p1").IsVisibleAsync());

        await page.ClickAsync("button[popovertarget]");

        // [triggerLeft, triggerBottom, popoverLeft, popoverTop, popoverWidth]
        var box = await page.EvaluateAsync<int[]>("""
            () => {
                const t = document.querySelector('button[popovertarget]').getBoundingClientRect();
                const p = document.querySelector('#p1').getBoundingClientRect();
                return [Math.round(t.left), Math.round(t.bottom),
                        Math.round(p.left), Math.round(p.top), Math.round(p.width)];
            }
            """);

        Assert.True(await page.Locator("#p1").IsVisibleAsync());
        // Below the trigger, its leading edges aligned, with the gap the part sets.
        Assert.Equal(box[0], box[2]);
        Assert.Equal(box[1] + 6, box[3]);
        Assert.True(box[4] <= 320);

        // Light dismiss is the platform's, and the reason there is no sednaUi.popover.
        await page.Keyboard.PressAsync("Escape");
        Assert.False(await page.Locator("#p1").IsVisibleAsync());

        Assert.Empty(errors);
    }

    private static double Pixels(string value) =>
        double.Parse(value.Replace("px", "", StringComparison.Ordinal),
            System.Globalization.CultureInfo.InvariantCulture);
}
