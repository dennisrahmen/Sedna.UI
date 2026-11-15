using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.page-head--collapse</c> measured in a layout engine: it folds over the first stretch
/// of scrolling and then stays at the top of what scrolls the page, the page under it never
/// moves while it does, and under reduced motion it is the ordinary head.
/// </summary>
/// <remarks>
/// The fold is a scroll-driven animation, so a scroll position is the whole input: each
/// reading sets one and waits two frames for the animation to sample it. Both engines the
/// library supports have scroll-driven animations; the first test says so, because every
/// other one would pass vacuously against the static fallback in an engine without them.
/// </remarks>
public class PageHeadCollapseTests : ScriptTestBase
{
    private const string Head = """
        <div class="page-head page-head--collapse" id="head">
          <div class="page-head-text">
            <h1 id="title">Held orders</h1>
            <p id="lede">Orders waiting on stock, oldest first, with the reason each is held.</p>
          </div>
          <div class="page-head-actions">
            <button class="btn" type="button">Refresh</button>
          </div>
        </div>
        <div id="next" style="height:2400px">The list.</div>
        """;

    private static string InBox(string head = Head, int width = 700) =>
        $"""<div class="sedna-scroll" id="box" style="max-height:400px; width:{width}px">{head}</div>""";

    private static string InFrame(string page = Head) => $"""
        <div class="layout">
          <div class="content">
            <header class="topbar"><span class="topbar-spacer"></span></header>
            <main class="page" id="box">{page}</main>
          </div>
        </div>
        """;

    private sealed record Reading(
        double BoxTop, double HeadTop, double HeadBottom, double TitleTop, double NextInContent,
        double LedeOpacity, string LedeVisibility, string Shadow, string Position, double PadTop);

    private static async Task<Reading> At(IPage page, int scrollTop)
    {
        await Js.Run(page, "y => { document.getElementById('box').scrollTop = y; }", scrollTop);
        await Js.Frames(page);
        return System.Text.Json.JsonSerializer.Deserialize<Reading>(await page.EvaluateAsync<string>("""
            () => {
              const el = id => document.getElementById(id);
              const box = el('box'), head = el('head'), lede = el('lede');
              const r = e => e.getBoundingClientRect();
              const hs = getComputedStyle(head), ls = getComputedStyle(lede);
              return JSON.stringify({
                boxTop: r(box).top, headTop: r(head).top, headBottom: r(head).bottom, titleTop: r(el('title')).top,
                nextInContent: r(el('next')).top - r(box).top + box.scrollTop,
                ledeOpacity: parseFloat(ls.opacity), ledeVisibility: ls.visibility,
                shadow: hs.boxShadow, position: hs.position, padTop: parseFloat(hs.paddingTop),
              });
            }
            """), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
    }

    // At rest the edge is the start of its fade: none, or a shadow with no colour.
    private static bool NoShadow(string shadow) =>
        shadow == "none" || shadow.StartsWith("rgba(0, 0, 0, 0)", StringComparison.Ordinal);

    [Fact]
    public async Task The_engine_has_scroll_driven_animations()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("<p>x</p>");
        Assert.True(await page.EvaluateAsync<bool>("() => CSS.supports('animation-timeline: scroll()')"),
            $"{BrowserEngine.Name} has no scroll-driven animations, so the head never folds there.");
    }

    [Fact]
    public async Task At_the_top_the_head_is_as_written()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(InBox());

        var top = await At(page, 0);
        Assert.Equal(1, top.LedeOpacity, 2);
        Assert.Equal("visible", top.LedeVisibility);
        Assert.True(NoShadow(top.Shadow), $"The head drew an edge at rest: {top.Shadow}.");
        Assert.Equal(0, top.PadTop, 1);
        Assert.Equal(top.BoxTop, top.HeadTop, 1);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Scrolled_past_its_range_the_head_is_folded_and_stays_at_the_top()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(InBox());

        foreach (var y in new[] { 200, 900 })
        {
            var r = await At(page, y);
            Assert.Equal("sticky", r.Position);
            Assert.Equal(r.BoxTop, r.HeadTop, 1);
            Assert.Equal(0, r.LedeOpacity, 2);
            Assert.Equal("hidden", r.LedeVisibility);
            Assert.False(NoShadow(r.Shadow), "The folded head drew no edge.");
            // Its own padding holds the title off the edge it is stuck to.
            Assert.Equal(r.HeadTop + r.PadTop, r.TitleTop, 1);
            Assert.True(r.PadTop > 0);
        }
    }

    // 320px wide the actions wrap onto a line of their own under the title.
    [Theory]
    [InlineData(700)]
    [InlineData(320)]
    public async Task The_page_under_the_head_does_not_move_while_it_folds(int width)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(InBox(width: width));

        // Where the content after the head sits in the scroller's content, at rest, part way
        // through the fold, and once folded. A head that changed the flow would move it.
        var rest = await At(page, 0);
        foreach (var y in new[] { 20, 40, 60, 79, 120, 400 })
        {
            var r = await At(page, y);
            Assert.True(Math.Abs(rest.NextInContent - r.NextInContent) <= 1,
                $"At {y}px the content after the head moved from {rest.NextInContent} to {r.NextInContent}.");
        }
    }

    [Fact]
    public async Task In_the_frame_the_head_reaches_through_the_page_padding_to_its_edge()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(InFrame());
        await page.SetViewportSizeAsync(1280, 700);

        var r = await At(page, 600);
        Assert.Equal("sticky", r.Position);
        Assert.Equal(r.BoxTop, r.HeadTop, 1);
    }

    [Fact]
    public async Task A_head_under_a_breadcrumb_folds_without_moving_the_page()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(InFrame(
            """<nav class="breadcrumb" aria-label="Breadcrumb"><a class="breadcrumb-item" href="#">Orders</a><span class="breadcrumb-item" aria-current="page">Held</span></nav>""" + Head));
        await page.SetViewportSizeAsync(1280, 700);

        var rest = await At(page, 0);
        foreach (var y in new[] { 30, 60, 300 })
        {
            var r = await At(page, y);
            Assert.True(Math.Abs(rest.NextInContent - r.NextInContent) <= 1,
                $"At {y}px the content after the head moved from {rest.NextInContent} to {r.NextInContent}.");
        }
    }

    [Fact]
    public async Task Under_reduced_motion_the_head_does_not_fold_and_scrolls_away()
    {
        if (NoBrowser) return;
        var page = await Open(InBox(), head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);

        var r = await At(page, 300);
        Assert.Equal("static", r.Position);
        Assert.Equal(1, r.LedeOpacity, 2);
        Assert.Equal("visible", r.LedeVisibility);
        Assert.True(NoShadow(r.Shadow), $"The head drew an edge under reduced motion: {r.Shadow}.");
        Assert.True(r.HeadBottom < r.BoxTop, "The head stayed in view under reduced motion.");
    }

    // ── The strip above the folded head ─────────────────────────────────────
    // A head stuck to its scroller's top covers the scroller from its edge down: no row of the
    // list under it shows above or beside the title, at any stage of the fold, on either pixel
    // density. Sampled from the pixels, since the geometry alone said the head was at the top
    // while a strip of the page's text was reported above it.

    private const string RowsUnder = """
        <ul class="list" id="rows">
          <li><div class="list-row"><span class="list-main"><span class="list-title">Order 10482 stock</span><span class="list-sub">Waiting on 2 desk lamps since 3 November</span></span></div></li>
          <li><div class="list-row"><span class="list-main"><span class="list-title">Order 10477 stock</span><span class="list-sub">Waiting on a bookcase since 1 November</span></span></div></li>
          <li><div class="list-row"><span class="list-main"><span class="list-title">Order 10469 stock</span><span class="list-sub">Waiting on 4 dining chairs since 30 October</span></span></div></li>
          <li><div class="list-row"><span class="list-main"><span class="list-title">Order 10451 stock</span><span class="list-sub">Waiting on a rug since 27 October</span></span></div></li>
          <li><div class="list-row"><span class="list-main"><span class="list-title">Order 10440 stock</span><span class="list-sub">Waiting on a wardrobe since 24 October</span></span></div></li>
          <li><div class="list-row"><span class="list-main"><span class="list-title">Order 10432 stock</span><span class="list-sub">Waiting on 6 storage boxes since 22 October</span></span></div></li>
          <li><div class="list-row"><span class="list-main"><span class="list-title">Order 10418 stock</span><span class="list-sub">Waiting on a desk since 19 October</span></span></div></li>
          <li><div class="list-row"><span class="list-main"><span class="list-title">Order 10406 stock</span><span class="list-sub">Waiting on a mirror since 16 October</span></span></div></li>
        </ul>
        """;

    // Text is far from the fill; a fractional edge only blends the fill with itself.
    private static bool Near((int R, int G, int B) a, (int R, int G, int B) b) =>
        Math.Abs(a.R - b.R) <= 6 && Math.Abs(a.G - b.G) <= 6 && Math.Abs(a.B - b.B) <= 6;

    [Theory]
    [InlineData(1, 700)]
    [InlineData(2, 700)]
    [InlineData(1, 300)]
    [InlineData(1.1, 700)]
    [InlineData(0.9, 700)]
    [InlineData(2.2, 700)]
    [InlineData(1.8, 300)]
    public async Task Nothing_of_the_list_shows_above_the_title_while_the_head_folds(double scale, int width)
    {
        if (NoBrowser) return;
        var head = Head.Replace("""<div id="next" style="height:2400px">The list.</div>""", "", StringComparison.Ordinal);
        var (page, _) = await OpenStyled(
            $"""<div style="padding: 20px"><div class="sedna-scroll sedna-scroll--md" id="box" style="width:{width}px">{head}<div id="next"></div>{RowsUnder}</div></div>""",
            deviceScaleFactor: (float)scale);

        foreach (var y in new[] { 0, 10, 25, 40, 60, 80, 100, 140, 200, 260 })
        {
            await Js.Run(page, "y => { document.getElementById('box').scrollTop = y; }", y);
            await Js.Frames(page);
            var png = Ink.Png.Decode(await page.Locator("#box").ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }));
            var headBox = await page.EvaluateAsync<double[]>("""
                () => { const r = document.getElementById('head').getBoundingClientRect(), b = document.getElementById('box').getBoundingClientRect();
                        const t = document.querySelector('#head .page-head-text').getBoundingClientRect();
                        return [r.top - b.top, r.bottom - b.top, t.right - b.left]; }
                """);
            Assert.True(Math.Abs(headBox[0]) <= 0.5, $"At {y}px the head started {headBox[0]}px below the scroller's top.");

            // The rows above the title's own line, right across the text column: the head's fill and nothing else.
            var fill = png.Pixel((int)Math.Ceiling(scale), (int)Math.Ceiling(scale));
            var textRight = (int)Math.Min(headBox[2] * scale, png.Width - 1);
            var rows = (int)Math.Floor(6 * scale);
            for (var row = 0; row < rows; row++)
                for (var x = 0; x < textRight; x++)
                    Assert.True(Near(png.Pixel(x, row), fill),
                        $"At {y}px, scale {scale}, width {width}: pixel ({x}, {row}) was {png.Pixel(x, row)}, not the head's {fill}.");
        }
    }
}
