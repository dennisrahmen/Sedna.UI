using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What the frame review of the topbar, the sidebar's foot, the banner and the busy bar
/// settled, measured in a layout engine: the header's dividers have one token, a panel
/// opened from the header keeps one gap, a banner under the header carries its shadow, and
/// a busy bar enters and leaves outside its track.
/// </summary>
public class FrameReviewTests : ScriptTestBase
{
    private const string Frames =
        "const frames = n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); });";

    // ── The busy bar ───────────────────────────────────────────────────────

    /// <summary>
    /// The bar is 30% of the track, and a percentage background position is measured against
    /// the track less the bar. Its first and last frames therefore sit fully outside the track,
    /// or it pops in and vanishes before it leaves.
    /// </summary>
    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_busy_bar_enters_and_leaves_outside_its_track(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            $"""<div dir="{dir}" style="width:400px"><div class="card" id="c" aria-busy="true" style="height:60px"><div class="card-body">Loading</div></div></div>""");

        var edges = await page.EvaluateAsync<double[]>(
            $$"""
            async () => {
              {{Frames}}
              await frames(2);
              const card = document.getElementById('c');
              const anim = card.getAnimations().find(a => a.animationName === 'sedna-busy-bar');
              if (!anim) throw new Error('The busy bar has no animation.');
              anim.pause();
              const end = anim.effect.getComputedTiming().duration;
              const read = t => {
                anim.currentTime = t;
                const cs = getComputedStyle(card);
                const track = card.clientWidth;
                const size = parseFloat(cs.backgroundSize) / 100 * track;
                const x = parseFloat(cs.backgroundPositionX) / 100 * (track - size);
                // The bar's near and far edge from the track's left edge, as a share of the track.
                return [x / track, (x + size) / track];
              };
              const first = read(0), last = read(end - 0.01);
              return [first[0], first[1], last[0], last[1]];
            }
            """);

        // Left to right the bar starts with its far edge at or before the track's left edge and
        // ends with its near edge at or past the right edge; right to left it is the reverse.
        if (dir == "ltr")
        {
            Assert.True(edges[1] <= 0.001, $"The bar starts inside the track (far edge at {edges[1]:P1}).");
            Assert.True(edges[2] >= 0.999, $"The bar ends inside the track (near edge at {edges[2]:P1}).");
        }
        else
        {
            Assert.True(edges[0] >= 0.999, $"The bar starts inside the track (near edge at {edges[0]:P1}).");
            Assert.True(edges[3] <= 0.001, $"The bar ends inside the track (far edge at {edges[3]:P1}).");
        }
        Assert.Empty(errors);
    }

    // ── The header's dividers ──────────────────────────────────────────────

    [Fact]
    public async Task One_token_recolours_every_divider_in_the_header()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <div style="--topbar-border: rgb(255, 0, 0)">
              <header class="topbar" id="bar">
                <button class="topbar-btn topbar-btn--start" id="start" type="button" aria-label="Menu"><i class="ri-menu-line"></i></button>
                <div class="topbar-spacer"></div>
                <div class="topbar-section" id="section"><span>Live</span></div>
                <button class="topbar-btn" id="btn" type="button" aria-label="Help"><i class="ri-question-line"></i></button>
                <div class="user-widget" id="widget"><button class="user-trigger" type="button">AF</button></div>
              </header>
            </div>
            """);

        var colours = await page.EvaluateAsync<string[]>(
            """
            () => {
              const c = (id, prop) => getComputedStyle(document.getElementById(id))[prop];
              return [c('bar', 'borderBottomColor'), c('start', 'borderInlineEndColor'), c('section', 'borderInlineStartColor'),
                      c('btn', 'borderInlineStartColor'), c('widget', 'borderInlineStartColor')];
            }
            """);

        Assert.All(colours, c => Assert.Equal("rgb(255, 0, 0)", c));
        Assert.Empty(errors);
    }

    // ── A banner under the header ──────────────────────────────────────────

    [Fact]
    public async Task A_banner_under_the_header_carries_the_shadow_the_header_gives_up()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <div class="bare-layout" style="height:200px">
              <header class="topbar" id="a"></header>
              <div class="page-banner page-banner--info" id="banner" role="region" aria-label="Notice"><div class="page-banner-body"><span>Notice</span></div></div>
              <main class="page"></main>
            </div>
            <div class="bare-layout" style="height:120px">
              <header class="topbar" id="alone"></header>
              <main class="page"></main>
            </div>
            <div class="bare-layout" style="height:200px">
              <header class="topbar topbar--sticky" id="sticky"></header>
              <div class="page-banner" id="stickyBanner" role="region" aria-label="Notice"><div class="page-banner-body"><span>Notice</span></div></div>
              <main class="page"></main>
            </div>
            """);

        var shadows = await page.EvaluateAsync<string[]>(
            """
            () => ['a', 'banner', 'alone', 'sticky', 'stickyBanner'].map(id => getComputedStyle(document.getElementById(id)).boxShadow)
            """);
        var (barWithBanner, banner, barAlone, stickyBar, stickyBanner) = (shadows[0], shadows[1], shadows[2], shadows[3], shadows[4]);

        Assert.NotEqual("none", barAlone);
        Assert.Equal("none", barWithBanner);
        Assert.Equal(barAlone, banner);
        Assert.Equal(barAlone, stickyBar);
        Assert.Equal("none", stickyBanner);

        // Lifted over the page's own positioned rows, and under the header's rung.
        var z = await page.EvaluateAsync<string[]>(
            "() => [getComputedStyle(document.getElementById('banner')).zIndex, getComputedStyle(document.getElementById('a')).zIndex]");
        Assert.True(int.Parse(z[0]) > 0 && int.Parse(z[0]) < int.Parse(z[1]), $"The banner's z-index {z[0]} should sit between the page and the header's {z[1]}.");
        Assert.Empty(errors);
    }

    // ── A panel opened from the header ─────────────────────────────────────

    private const string HeaderPanels =
        """
        <style>body { margin: 0 }</style>
        <div id="host" style="--topbar-height: 72px">
          <header class="topbar" id="bar">
            <div class="topbar-spacer"></div>
            <button class="topbar-btn" id="apps" type="button" popovertarget="p" aria-label="Apps"><i class="ri-apps-2-line"></i></button>
            <div class="popover" id="p" popover>
              <nav class="app-switcher" aria-label="Apps">
                <a class="app-switcher-item" href="#"><span class="app-switcher-icon"><i class="ri-inbox-line"></i></span><span>Orders</span></a>
                <a class="app-switcher-item" href="#"><span class="app-switcher-icon"><i class="ri-truck-line"></i></span><span>Shipping</span></a>
                <a class="app-switcher-item" href="#"><span class="app-switcher-icon"><i class="ri-settings-3-line"></i></span><span>Admin</span></a>
              </nav>
            </div>
            <div class="user-widget" id="widget">
              <button class="user-trigger" type="button" aria-expanded="true">AF</button>
              <div class="menu" id="menu"><button class="menu-item" type="button">Sign out</button></div>
            </div>
          </header>
        </div>
        """;

    private const string Measure =
        """
        async (gap) => {
          const frames = n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); });
          if (gap) document.getElementById('host').style.setProperty('--topbar-panel-gap', gap);
          await frames(2);
          const bar = document.getElementById('widget').getBoundingClientRect();
          const menu = document.getElementById('menu').getBoundingClientRect();
          const apps = document.getElementById('apps');
          const panel = document.getElementById('p');
          if (!panel.matches(':popover-open')) apps.click();
          await frames(3);
          const p = panel.getBoundingClientRect();
          const b = apps.getBoundingClientRect();
          return {
            menuBelow: menu.top - bar.bottom, menuFromEdge: innerWidth - menu.right,
            panelBelow: p.top - b.bottom, panelEnd: b.right - p.right, panelStart: p.left, panelWidth: p.width,
          };
        }
        """;

    [Fact]
    public async Task The_user_menu_and_the_app_switcher_keep_the_same_gap_whatever_the_header_height()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(HeaderPanels);
        await page.SetViewportSizeAsync(900, 500);

        var m = await page.EvaluateAsync<System.Text.Json.JsonElement>(Measure, "4px");
        Assert.Equal(4, Get(m, "menuBelow"), 0.6);
        Assert.Equal(4, Get(m, "menuFromEdge"), 0.6);
        Assert.Equal(4, Get(m, "panelBelow"), 0.6);

        // The switcher's inline end meets the button's, less the same gap; it spans towards the inline start.
        Assert.Equal(4, Get(m, "panelEnd"), 0.6);
        Assert.True(Get(m, "panelStart") >= 0 && Get(m, "panelWidth") > 100, "The switcher should open towards the inline start, on screen.");

        // One token moves all three together.
        var wide = await page.EvaluateAsync<System.Text.Json.JsonElement>(Measure, "14px");
        Assert.Equal(14, Get(wide, "menuBelow"), 0.6);
        Assert.Equal(14, Get(wide, "menuFromEdge"), 0.6);
        Assert.Equal(14, Get(wide, "panelBelow"), 0.6);
        Assert.Equal(14, Get(wide, "panelEnd"), 0.6);
        Assert.Empty(errors);
    }

    // ── The app switcher's tile ────────────────────────────────────────────

    [Fact]
    public async Task An_app_switcher_tile_is_as_deep_below_its_name_as_above_its_mark()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <nav class="app-switcher" aria-label="Apps" style="margin:20px">
              <a class="app-switcher-item" id="item" href="#"><span class="app-switcher-icon"><i class="ri-inbox-line"></i></span><span id="name">Orders</span></a>
            </nav>
            """);

        var gaps = await page.EvaluateAsync<double[]>(
            """
            () => {
              const item = document.getElementById('item'), name = document.getElementById('name');
              const icon = item.querySelector('.app-switcher-icon');
              const i = item.getBoundingClientRect();
              return [icon.getBoundingClientRect().top - i.top, i.bottom - name.getBoundingClientRect().bottom, parseFloat(getComputedStyle(name).fontSize)];
            }
            """);

        Assert.Equal(gaps[0], gaps[1], 0.5);
        Assert.True(gaps[2] >= 14, $"The name is {gaps[2]}px; it should be 14px or more.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_switcher_without_names_draws_the_marks_alone_and_keeps_the_names_readable_to_a_screen_reader()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <nav class="app-switcher app-switcher--icons" aria-label="Apps" style="margin:20px">
              <a class="app-switcher-item" id="item" href="#"><span class="app-switcher-icon"><i class="ri-inbox-line"></i></span><span id="name">Orders</span></a>
            </nav>
            """);

        var box = await page.EvaluateAsync<double[]>(
            """
            () => {
              const r = document.getElementById('name').getBoundingClientRect();
              const i = document.getElementById('item').getBoundingClientRect();
              return [r.width, r.height, i.width];
            }
            """);

        Assert.True(box[0] <= 1 && box[1] <= 1, "The name should not be drawn.");
        Assert.True(box[2] < 80, $"The tile should fit the mark ({box[2]}px wide).");
        var accessible = await page.GetByRole(AriaRole.Link, new() { Name = "Orders" }).CountAsync();
        Assert.Equal(1, accessible);
        Assert.Empty(errors);
    }

    // ── The sidebar's foot ─────────────────────────────────────────────────

    [Fact]
    public async Task The_separator_in_the_nav_tools_has_the_same_space_above_and_below()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """<div class="nav-tools" style="width:240px"><hr class="nav-tools-sep" id="sep"></div>""");

        var margins = await page.EvaluateAsync<double[]>(
            """
            () => { const cs = getComputedStyle(document.getElementById('sep')); return [parseFloat(cs.marginTop), parseFloat(cs.marginBottom)]; }
            """);

        Assert.Equal(margins[0], margins[1]);
        Assert.Empty(errors);
    }

    private static double Get(System.Text.Json.JsonElement e, string name) => e.GetProperty(name).GetDouble();
}
