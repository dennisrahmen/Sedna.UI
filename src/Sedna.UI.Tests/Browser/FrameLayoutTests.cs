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

        // The first item still carries its own top padding, and that padding is inside
        // its background — so the row itself starts 4px down instead of putting an
        // active link's tint hard against the divider.
        Assert.Equal("8px", await page.EvalOnSelectorAsync<string>(
            ".nav-scroll .nav-link", "el => getComputedStyle(el).paddingTop"));

        var rowGap = await page.EvaluateAsync<double>("""
            () => Math.round(document.querySelector('.nav-scroll .nav-link').getBoundingClientRect().top
                           - document.querySelector('.brand').getBoundingClientRect().bottom)
            """);

        Assert.Equal(4, rowGap);

        Assert.Empty(errors);
    }

    /// <summary>A nav-group at the top of the nav and a nav-group inside a section.</summary>
    private const string Groups =
        """
        <div class="layout" style="height:460px">
          <aside class="sidebar">
            <a class="brand" href="#"><span class="brand-logo"></span>
              <span class="brand-text"><strong>Console</strong></span></a>
            <nav class="nav">
              <div class="nav-scroll">
                <a class="nav-link" id="outer-flat" href="#"><i class="ri-inbox-line"></i><span>Orders</span></a>
                <details class="nav-group" open>
                  <summary id="outer-summary"><i class="ri-building-line"></i><span>Fulfilment</span></summary>
                  <a class="nav-link" id="outer-sub" href="#"><span>On hold</span></a>
                </details>
                <div class="nav-section">
                  <span class="nav-section-label">Administration</span>
                  <a class="nav-link" id="section-flat" href="#"><i class="ri-key-2-line"></i><span>API keys</span></a>
                  <details class="nav-group" open>
                    <summary id="section-summary"><i class="ri-refresh-line"></i><span>Directory sync</span></summary>
                    <a class="nav-link" id="section-sub" href="#"><span>Field mapping</span></a>
                  </details>
                </div>
              </div>
            </nav>
          </aside>
          <div class="content"><div class="page"><p>page</p></div></div>
        </div>
        """;

    [Fact]
    public async Task A_nav_group_indents_with_its_siblings_at_whatever_depth_it_sits()
    {
        if (NoBrowser) return;
        // `.nav-section .nav-link` set the depth on links alone, so a .nav-group inside
        // a section sat 8px to the left of the links either side of it, and its
        // sub-items indented off the outer depth rather than the section's. Both halves
        // parse fine and are invisible to a source scan. --nav-indent is the one knob
        // every row reads, and this measures that they all read it.
        var (page, errors) = await OpenStyled(Groups);

        // The inline depth of each row, which is what --nav-indent sets. Measured off
        // computed padding rather than off the label's position: the label's x depends
        // on the icon's advance width, and the icon font is not loaded here.
        // [outerFlat, outerSummary, outerSub, sectionFlat, sectionSummary, sectionSub]
        var pad = await page.EvaluateAsync<int[]>("""
            () => ['outer-flat', 'outer-summary', 'outer-sub',
                   'section-flat', 'section-summary', 'section-sub']
                .map(id => parseFloat(
                    getComputedStyle(document.getElementById(id)).paddingInlineStart))
            """);

        // A group's own row sits at exactly its siblings' depth.
        Assert.Equal(pad[0], pad[1]);
        Assert.Equal(pad[3], pad[4]);

        // A section is one rung deeper than the top of the nav — for the group too.
        Assert.Equal(16, pad[0]);
        Assert.Equal(24, pad[3]);
        Assert.Equal(24, pad[4]);

        // A sub-item clears its own parent's icon and the gap after it, off its own
        // depth: 16 + 16 + 8 at the top of the nav, 24 + 16 + 8 inside a section.
        Assert.Equal(40, pad[2]);
        Assert.Equal(48, pad[5]);

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

    [Theory]
    [InlineData("sidebar-logo", 30)]
    [InlineData("bare-logo", 28)]
    public async Task The_brand_tile_sizes_and_clips_a_logo_image(string id, int size)
    {
        if (NoBrowser) return;
        // A tile filled with a colour is what every example showed, and an app puts its
        // actual logo in it — an image whose intrinsic size is its own and whose corners
        // are square. So the tile has to clip and the image has to fill it, or every
        // consuming app writes the same four declarations. Both tiles are measured
        // because they are two different sizes set by two different parts.
        var (page, errors) = await OpenStyled(
            """
            <div class="layout" style="height:200px">
              <aside class="sidebar">
                <a class="brand" href="#">
                  <span class="brand-logo"><img id="sidebar-logo" alt="" src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='120' height='80' viewBox='0 0 120 80'%3E%3Crect width='120' height='80' fill='%231E293B'/%3E%3C/svg%3E"></span>
                  <span class="brand-text"><strong>Northwind Retail</strong></span>
                </a>
              </aside>
              <div class="content"><div class="page">
                <div class="bare-brand">
                  <span class="brand-logo"><img id="bare-logo" alt="" src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='120' height='80' viewBox='0 0 120 80'%3E%3Crect width='120' height='80' fill='%231E293B'/%3E%3C/svg%3E"></span>
                  <span>Northwind Retail</span>
                </div>
              </div></div>
            </div>
            """);

        // [image width, image height, tile width, tile height, tile left - image left]
        var box = await page.EvaluateAsync<double[]>($$"""
            () => {
                const img = document.getElementById('{{id}}');
                const tile = img.parentElement;
                const i = img.getBoundingClientRect(), t = tile.getBoundingClientRect();
                return [i.width, i.height, t.width, t.height, t.left - i.left];
            }
            """);

        // The image is the tile, not its own 120×80.
        Assert.Equal(size, box[0], 1);
        Assert.Equal(size, box[1], 1);
        Assert.Equal(size, box[2], 1);
        Assert.Equal(size, box[3], 1);
        Assert.Equal(0, box[4], 1);

        var style = await page.EvaluateAsync<string[]>($$"""
            () => {
                const img = document.getElementById('{{id}}');
                const i = getComputedStyle(img), t = getComputedStyle(img.parentElement);
                return [i.objectFit, i.display, t.overflow, t.borderTopLeftRadius];
            }
            """);

        // Cropped rather than squashed, no inline baseline gap under it, and clipped to
        // the tile's own corners — the radius is what makes the clip necessary at all.
        Assert.Equal("cover", style[0]);
        Assert.Equal("block", style[1]);
        Assert.Equal("hidden", style[2]);
        Assert.NotEqual("0px", style[3]);

        Assert.Empty(errors);
    }

    private static double Pixels(string value) =>
        double.Parse(value.Replace("px", "", StringComparison.Ordinal),
            System.Globalization.CultureInfo.InvariantCulture);
}
