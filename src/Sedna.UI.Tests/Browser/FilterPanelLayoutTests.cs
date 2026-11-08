using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The filtering recipe measured in a layout engine: on a wide page the facet panel sits
/// beside the results at the inline start, and in a narrow column it stacks above them; on a
/// phone the same facets in a start drawer fit the screen, scroll in the body and keep Reset
/// and Apply in view on one line.
/// </summary>
public class FilterPanelLayoutTests : ScriptTestBase
{
    private const string Facets = """
        <fieldset class="form-section">
          <legend>Status</legend>
          <div class="form-section-body">
            <label class="form-check"><input type="checkbox" checked /> <span>Open</span> <span class="form-check-count">412</span></label>
            <label class="form-check"><input type="checkbox" checked /> <span>Held</span> <span class="form-check-count">38</span></label>
            <label class="form-check"><input type="checkbox" /> <span>Shipped</span> <span class="form-check-count">1,206</span></label>
            <label class="form-check"><input type="checkbox" disabled /> <span>Cancelled</span> <span class="form-check-count">0</span></label>
          </div>
        </fieldset>
        <fieldset class="form-section">
          <legend>Region</legend>
          <div class="form-section-body">
            <label class="form-check"><input type="checkbox" checked /> <span>EU-West</span> <span class="form-check-count">301</span></label>
            <label class="form-check"><input type="checkbox" /> <span>EU-Central</span> <span class="form-check-count">97</span></label>
            <label class="form-check"><input type="checkbox" /> <span>US-East</span> <span class="form-check-count">52</span></label>
          </div>
        </fieldset>
        <fieldset class="form-section">
          <legend>Carrier</legend>
          <div class="form-section-body">
            <label class="form-check"><input type="checkbox" /> <span>Road</span> <span class="form-check-count">640</span></label>
            <label class="form-check"><input type="checkbox" /> <span>Air</span> <span class="form-check-count">212</span></label>
            <label class="form-check"><input type="checkbox" /> <span>Sea</span> <span class="form-check-count">41</span></label>
          </div>
        </fieldset>
        """;

    private static string Split(string dir, int width) => $"""
        <div dir="{dir}" style="width:{width}px">
          <div class="sedna-split" style="--split-aside: 15rem">
            <aside class="card sedna-split-aside" id="panel">
              <div class="card-head"><h3>Filters</h3></div>
              <form class="card-body">{Facets}</form>
              <div class="card-foot card-foot--split">
                <button class="btn btn--sm btn--ghost" type="reset">Reset</button>
                <button class="btn btn--sm btn--brand btn--solid" type="submit">Show 302 orders</button>
              </div>
            </aside>
            <div class="sedna-col" id="results">
              <div class="toolbar"><div class="toolbar-filters">
                <span class="chip chip--active"><span class="chip-label">Status: Open, Held</span>
                  <button class="chip-dismiss" type="button" aria-label="Remove"><i class="ri-close-line"></i></button></span>
              </div></div>
              <div class="card" style="height:200px"></div>
            </div>
          </div>
        </div>
        """;

    private const string Drawer = $"""
        <div class="toolbar"><div class="toolbar-filters">
          <button class="btn" type="button" id="trigger" aria-label="Filters, 3 applied"><i class="ri-filter-3-line"></i> <span>Filters</span> <span class="badge badge--sm badge--info" id="count">3</span></button>
        </div></div>
        <dialog class="drawer drawer--start" id="drawer" aria-labelledby="drawer-title">
          <div class="drawer-header">
            <h3 id="drawer-title">Filters</h3>
            <button class="modal-close" type="button" aria-label="Close"><i class="ri-close-line"></i></button>
          </div>
          <form class="drawer-body" id="body" method="dialog">{Facets}</form>
          <div class="drawer-footer" id="footer">
            <button class="btn btn--ghost" type="reset" form="body" id="reset">Reset</button>
            <button class="btn btn--brand btn--solid" type="submit" form="body" id="apply">Show 302 orders</button>
          </div>
        </dialog>
        """;

    private const string MeasureSplit = """
        () => {
            const r = id => { const b = document.getElementById(id).getBoundingClientRect(); return { left: b.left, right: b.right, top: b.top, bottom: b.bottom }; };
            return { panel: r('panel'), results: r('results') };
        }
        """;

    private static double D(JsonElement e, string a, string b) => e.GetProperty(a).GetProperty(b).GetDouble();

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task On_a_wide_page_the_panel_sits_beside_the_results_at_the_inline_start(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Split(dir, 1000));
        var m = await page.EvaluateAsync<JsonElement>(MeasureSplit);

        // Side by side: the two start on the same line.
        Assert.Equal(D(m, "panel", "top"), D(m, "results", "top"), 0.5);
        if (dir == "ltr") Assert.True(D(m, "panel", "right") <= D(m, "results", "left"));
        else Assert.True(D(m, "panel", "left") >= D(m, "results", "right"));
        // At about the width it asked for: 15rem.
        Assert.InRange(D(m, "panel", "right") - D(m, "panel", "left"), 230, 260);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task In_a_narrow_column_the_panel_stacks_above_the_results()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Split("ltr", 480));
        var m = await page.EvaluateAsync<JsonElement>(MeasureSplit);

        Assert.True(D(m, "results", "top") >= D(m, "panel", "bottom"), "The results are not under the panel.");
        Assert.Equal(480, D(m, "panel", "right") - D(m, "panel", "left"), 0.5);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task On_a_phone_the_drawer_fits_scrolls_its_facets_and_keeps_its_buttons_in_view(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div dir="{dir}">{Drawer}</div>""");
        await page.SetViewportSizeAsync(360, 480);
        await Js.Run(page, "() => document.getElementById('drawer').showModal()");
        await Js.Settle(page);

        var m = await page.EvaluateAsync<JsonElement>("""
            () => {
                const r = id => { const b = document.getElementById(id).getBoundingClientRect(); return { left: b.left, right: b.right, top: b.top, bottom: b.bottom }; };
                const body = document.getElementById('body');
                return {
                    drawer: r('drawer'), footer: r('footer'), reset: r('reset'), apply: r('apply'),
                    trigger: r('trigger'), count: r('count'),
                    bodyScrolls: body.scrollHeight > body.clientHeight + 1,
                    overflowY: getComputedStyle(body).overflowY,
                    vw: window.innerWidth, vh: window.innerHeight
                };
            }
            """);

        var vw = m.GetProperty("vw").GetDouble();
        var vh = m.GetProperty("vh").GetDouble();
        // Inside the screen, against its inline-start edge.
        Assert.True(D(m, "drawer", "left") >= -0.5 && D(m, "drawer", "right") <= vw + 0.5);
        if (dir == "ltr") Assert.Equal(0, D(m, "drawer", "left"), 0.5);
        else Assert.Equal(vw, D(m, "drawer", "right"), 0.5);

        // The facets scroll in the body; the footer stays on the screen.
        Assert.Equal("auto", m.GetProperty("overflowY").GetString());
        Assert.True(m.GetProperty("bodyScrolls").GetBoolean(), "The facets fit a 480px screen, so the test proves nothing.");
        Assert.True(D(m, "footer", "bottom") <= vh + 0.5, "The footer is below the screen.");

        // Reset and Apply on one line, inside the drawer.
        Assert.Equal((D(m, "reset", "top") + D(m, "reset", "bottom")) / 2, (D(m, "apply", "top") + D(m, "apply", "bottom")) / 2, 0.5);
        foreach (var b in new[] { "reset", "apply" })
        {
            Assert.True(D(m, b, "left") >= D(m, "drawer", "left") - 0.5);
            Assert.True(D(m, b, "right") <= D(m, "drawer", "right") + 0.5);
        }

        // The applied count sits inside its trigger, centred on the trigger's line.
        Assert.True(D(m, "count", "left") >= D(m, "trigger", "left") && D(m, "count", "right") <= D(m, "trigger", "right"));
        var triggerCentre = (D(m, "trigger", "top") + D(m, "trigger", "bottom")) / 2;
        Assert.Equal(triggerCentre, (D(m, "count", "top") + D(m, "count", "bottom")) / 2, 1);
        Assert.Empty(errors);
    }
}
