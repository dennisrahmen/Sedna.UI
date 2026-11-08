using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.form-actions--sticky</c> and <c>.form-actions--save-bar</c> measured in a layout engine:
/// stuck to the bottom of what scrolls the form, settled at the form's end, clear of the home
/// indicator and of the frame's bottom bar, mirrored, and whole on a narrow phone.
/// </summary>
/// <remarks>
/// The device's inset is stood in for by <c>--safe-block-end</c>, as <c>SafeAreaTests</c>
/// explains: no browser a test can drive reports one.
/// </remarks>
public class SaveBarTests : ScriptTestBase
{
    private const double Inset = 34;

    private const string Notched = "<style>:root { --safe-block-end: 34px; }</style>";

    private static string Form(string rowClass = "form-actions--save-bar", string formAttrs = "data-dirty") => $"""
        <form id="form" {formAttrs}>
          <div class="form-field" style="height:1600px">
            <label class="form-label" for="notes">Delivery notes</label>
            <textarea class="form-input" id="notes"></textarea>
          </div>
          <div class="form-actions {rowClass}" id="row">
            <p class="form-actions-status" id="status"><i class="ri-error-warning-line"></i><span>Unsaved changes</span></p>
            <button class="btn" type="reset" id="discard">Discard</button>
            <button class="btn btn--brand btn--solid" type="submit" id="save">Save</button>
          </div>
        </form>
        <p style="height:600px">After the form.</p>
        """;

    private static string Bar(string barClass = "", bool accessory = false) => $"""
        <nav class="bottombar {barClass}" id="bar" aria-label="Main">
          {(accessory ? """<div class="bottombar-accessory"><span class="bottombar-accessory-text">Syncing</span></div>""" : "")}
          <div class="bottombar-items">
            <a class="bottombar-item active" href="#" aria-current="page"><span class="bottombar-icon"><i class="ri-inbox-line"></i></span><span class="bottombar-label">Orders</span></a>
            <a class="bottombar-item" href="#"><span class="bottombar-icon"><i class="ri-archive-line"></i></span><span class="bottombar-label">Stock</span></a>
          </div>
        </nav>
        """;

    private static string Shell(string layoutClass, string page, string bar = "") => $"""
        <div class="layout {layoutClass}">
          <aside class="sidebar"><div class="nav-scroll"><a class="nav-link" href="#">Orders</a></div></aside>
          <div class="content">
            <header class="topbar"><span class="topbar-spacer"></span></header>
            <main class="page" id="page">{page}</main>
            {bar}
          </div>
        </div>
        <div id="space5" style="padding-top: var(--space-5)"></div>
        """;

    private sealed record Geometry(
        double[] Row, double[] Form, double[] Page, double[] Bar, double[] Status, double[] Save, double[] Discard,
        string Display, double PadEnd, double Space5, double Viewport, double ScrollWidth, double ClientWidth, double DocWidth);

    private const string Measure =
        """
        () => {
          const el = id => document.getElementById(id);
          const r = id => { const e = el(id); if (!e) return [0, 0, 0, 0]; const b = e.getBoundingClientRect(); return [b.left, b.right, b.top, b.bottom]; };
          const row = el('row');
          const cs = getComputedStyle(row);
          return {
            row: r('row'), form: r('form'), page: r('page'), bar: r('bar'),
            status: r('status'), save: r('save'), discard: r('discard'),
            display: cs.display,
            padEnd: parseFloat(cs.paddingBottom),
            space5: parseFloat(getComputedStyle(el('space5')).paddingTop),
            viewport: window.innerHeight,
            scrollWidth: row.scrollWidth, clientWidth: row.clientWidth,
            docWidth: document.documentElement.scrollWidth - window.innerWidth,
          };
        }
        """;

    private static async Task<Geometry> Read(IPage page) =>
        System.Text.Json.JsonSerializer.Deserialize<Geometry>(
            await page.EvaluateAsync<string>("() => JSON.stringify((" + Measure + ")())"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

    private static Task ScrollPage(IPage page, string to) =>
        Js.Run(page, "to => { const p = document.getElementById('page'); p.scrollTop = to === 'end' ? p.scrollHeight : Number(to); }", to);

    [Theory]
    [InlineData("form-actions--save-bar")]
    [InlineData("form-actions--sticky")]
    public async Task The_row_sticks_to_the_bottom_of_the_page_and_settles_at_the_form_end(string rowClass)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Shell("", Form(rowClass)));
        await page.SetViewportSizeAsync(1280, 700);

        var top = await Read(page);
        Assert.Equal("flex", top.Display);
        Assert.Equal(top.Page[3], top.Row[3], 1.0);
        Assert.True(top.Form[3] > top.Page[3] + 100, "The fixture's form should run past the page's bottom edge.");

        // Scrolled past the form, the row stays at the form's end rather than following the page.
        await ScrollPage(page, "end");
        var end = await Read(page);
        Assert.Equal(end.Form[3], end.Row[3], 1.0);
        Assert.True(end.Row[3] < end.Page[3] - 100, "The row followed the page past the end of its form.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_save_bar_shows_only_while_the_form_is_dirty_and_the_sticky_row_always()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Shell("", Form(formAttrs: "")));

        Assert.Equal("none", (await Read(page)).Display);

        await Js.Run(page, "() => document.getElementById('form').setAttribute('data-dirty', '')");
        Assert.Equal("flex", (await Read(page)).Display);

        await Js.Run(page, "() => { const f = document.getElementById('form'); f.removeAttribute('data-dirty'); document.getElementById('row').className = 'form-actions form-actions--sticky'; }");
        Assert.Equal("flex", (await Read(page)).Display);
    }

    [Fact]
    public async Task Inside_a_card_the_row_sticks_to_the_page_the_card_scrolls_in()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Shell("", $"""<div class="card"><div class="card-body">{Form()}</div></div>"""));
        await page.SetViewportSizeAsync(1280, 700);

        var g = await Read(page);
        Assert.Equal(g.Page[3], g.Row[3], 1.0);
    }

    // A scroller inside the page has no padding of the page's, so the row reaches no
    // further than its edge — under .layout--flow and a bottom bar too.
    [Theory]
    [InlineData("")]
    [InlineData("layout--bottombar layout--flow")]
    public async Task In_a_scroller_inside_the_page_the_row_sticks_to_that_scroller(string layoutClass)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(
            Shell(layoutClass, $"""<div class="sedna-scroll" id="scroller" style="max-height:300px">{Form()}</div>""", Bar()), Notched);
        await page.SetViewportSizeAsync(375, 700);

        var g = await Read(page);
        var scroller = await page.EvaluateAsync<double>("() => document.getElementById('scroller').getBoundingClientRect().bottom");
        Assert.Equal(scroller, g.Row[3], 1.0);
        Assert.Equal(g.Space5, g.PadEnd, 0.5);
    }

    [Fact]
    public async Task On_the_device_edge_the_row_pads_itself_clear_of_the_home_indicator()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Shell("", Form()), Notched);
        await page.SetViewportSizeAsync(1280, 700);

        var g = await Read(page);
        Assert.Equal(g.Space5 + Inset, g.PadEnd, 0.5);
        Assert.Equal(g.Viewport, g.Row[3], 1.0);
    }

    [Fact]
    public async Task Above_a_bottom_bar_the_row_stops_at_the_bar_and_leaves_the_inset_to_it()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Shell("layout--bottombar", Form(), Bar()), Notched);
        await page.SetViewportSizeAsync(375, 700);

        var g = await Read(page);
        Assert.Equal(g.Space5, g.PadEnd, 0.5);
        Assert.True(g.Row[3] <= g.Bar[2] + 0.5, $"The row ({g.Row[3]}) runs under the bottom bar ({g.Bar[2]}).");
        Assert.Equal(g.Bar[2], g.Row[3], 1.0);

        // Wide, the bar hides and the page reaches the device's edge again.
        await page.SetViewportSizeAsync(1280, 700);
        var wide = await Read(page);
        Assert.Equal(wide.Space5 + Inset, wide.PadEnd, 0.5);
        Assert.Equal(wide.Viewport, wide.Row[3], 1.0);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("bottombar--tint", false)]
    [InlineData("bottombar--icons", true)]
    [InlineData("bottombar--card", false)]
    [InlineData("bottombar--card", true)]
    public async Task When_the_document_flows_the_row_stands_on_the_bottom_bar(string barClass, bool accessory)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Shell("layout--bottombar layout--flow", Form(), Bar(barClass, accessory)), Notched);
        await page.SetViewportSizeAsync(375, 700);
        await Js.Run(page, "() => window.scrollTo(0, 600)");
        await Js.Frames(page);

        var g = await Read(page);
        Assert.Equal(g.Viewport, g.Bar[3], 1.0);
        Assert.Equal(g.Bar[2], g.Row[3], 1.0);
        Assert.Equal(g.Space5, g.PadEnd, 0.5);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_status_takes_the_leading_side_and_save_the_trailing_one(string dir)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Shell("", Form()));
        await Js.Run(page, "dir => document.documentElement.dir = dir", dir);
        await page.SetViewportSizeAsync(1280, 700);

        var g = await Read(page);
        if (dir == "ltr")
        {
            Assert.Equal(g.Row[0], g.Status[0], 1.0);
            Assert.Equal(g.Row[1], g.Save[1], 1.0);
            Assert.True(g.Discard[1] <= g.Save[0], "Discard should come before Save.");
        }
        else
        {
            Assert.Equal(g.Row[1], g.Status[1], 1.0);
            Assert.Equal(g.Row[0], g.Save[0], 1.0);
            Assert.True(g.Discard[0] >= g.Save[1], "Discard should come before Save.");
        }
        // One row on a wide screen: every item shares the status's line.
        Assert.True(g.Save[2] < g.Status[3] && g.Save[3] > g.Status[2], "The buttons left the status's line.");
    }

    [Fact]
    public async Task On_a_narrow_phone_the_row_wraps_inside_the_screen()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Shell("layout--bottombar", Form(), Bar()));
        await page.SetViewportSizeAsync(320, 640);
        await Js.Run(page, "() => document.querySelector('#status span').textContent = 'Unsaved changes to the delivery notes and the opening hours'");

        var g = await Read(page);
        Assert.True(g.ScrollWidth <= g.ClientWidth, $"The row overflows: {g.ScrollWidth} > {g.ClientWidth}.");
        Assert.True(g.DocWidth <= 0, "The page scrolls sideways.");
        Assert.True(g.Save[1] <= 320 && g.Save[0] >= 0, "Save is off the screen.");
        Assert.True(g.Status[3] <= g.Save[2] + 0.5, "The status should wrap above the buttons, not overlap them.");
        Assert.True(g.Row[3] <= g.Bar[2] + 0.5, "The row runs under the bottom bar.");
        Assert.Empty(errors);
    }
}
