using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A menu is as wide as its content and no narrower than its trigger, a value trigger is as
/// wide as its value and chevron, and a group heading takes the same room above it wherever
/// the menu sits.
/// </summary>
/// <remarks>
/// Every number here is a layout result — <c>anchor-size()</c> against a trigger,
/// <c>width: max-content</c> against a pseudo-element's room — so it is measured, not read from the
/// stylesheet. The menu once had a 220px floor, and a trigger a floor of its own, which left two
/// short items in a wide panel and a caret far from its label.
/// </remarks>
public class MenuWidthTests : ScriptTestBase
{
    private const string Footer = """
        <div style="position:fixed; inset-inline:0; inset-block-end:0; padding:12px; display:flex; justify-content:space-between">
          <div class="menu-anchor" style="--panel-placement: block-start">
            <button class="value-trigger" id="vt" type="button" data-menu-toggle aria-expanded="false"><span>Carton sealed</span></button>
            <div class="menu menu--start" id="m1" hidden>
              <button class="menu-item" type="button"><i class="ri-image-line"></i> <span>Items on the bench</span></button>
              <button class="menu-item" type="button"><i class="ri-image-line"></i> <span>Carton sealed</span></button>
            </div>
          </div>
          <div class="menu-anchor">
            <button class="btn btn--sm" id="more" type="button" data-menu-toggle aria-expanded="false"><i class="ri-more-2-fill"></i> <span>Photo</span></button>
            <div class="menu" id="m2" hidden>
              <button class="menu-item" type="button"><i class="ri-download-line"></i> <span>Download</span></button>
              <button class="menu-item" type="button"><i class="ri-flag-line"></i> <span>Flag damage</span></button>
            </div>
          </div>
        </div>
        """;

    [Fact]
    public async Task A_menu_is_as_wide_as_its_widest_row_and_not_a_fixed_floor()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Footer);
        await page.Locator("#more").ClickAsync();

        var m = await page.EvaluateAsync<JsonElement>("""
            () => { const menu = document.getElementById('m2').getBoundingClientRect();
                    const widest = Math.max(...[...document.querySelectorAll('#m2 .menu-item')].map(i => {
                        const r = document.createRange(); r.selectNodeContents(i);
                        return i.getBoundingClientRect().width - i.clientWidth + r.getBoundingClientRect().width; }));
                    return { menu: menu.width, trigger: document.getElementById('more').getBoundingClientRect().width,
                             floor: getComputedStyle(document.getElementById('m2')).minWidth }; }
            """);

        Assert.True(m.GetProperty("menu").GetDouble() < 200, $"A two-item menu is {m.GetProperty("menu").GetDouble()}px wide.");
        Assert.True(m.GetProperty("menu").GetDouble() >= m.GetProperty("trigger").GetDouble() - 0.5, "The menu is narrower than its trigger.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_menu_is_never_narrower_than_a_trigger_wider_than_its_items()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="padding:16px">
              <div class="menu-anchor">
                <button class="btn" id="wide" type="button" data-menu-toggle aria-expanded="false" style="width:320px"><span>Choose a carrier</span></button>
                <div class="menu" id="m" hidden><button class="menu-item" type="button">Pick up</button></div>
              </div>
            </div>
            """);
        await page.Locator("#wide").ClickAsync();

        var w = await page.EvaluateAsync<double[]>("() => [document.getElementById('m').getBoundingClientRect().width, document.getElementById('wide').getBoundingClientRect().width]");

        Assert.True(w[0] >= w[1] - 0.5, $"The menu is {w[0]}px under a {w[1]}px trigger.");
    }

    [Fact]
    public async Task A_value_trigger_is_as_wide_as_its_value_and_chevron()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Footer);

        var m = await page.EvaluateAsync<JsonElement>("""
            () => { const t = document.getElementById('vt'), s = t.querySelector('span');
                    const cs = getComputedStyle(t);
                    const after = parseFloat(getComputedStyle(t, '::after').borderLeftWidth) * 2;
                    const pad = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight) + 2;
                    return { trigger: t.getBoundingClientRect().width, value: s.getBoundingClientRect().width,
                             cut: s.scrollWidth > s.clientWidth, expected: s.scrollWidth + parseFloat(cs.columnGap) + after + pad }; }
            """);

        Assert.False(m.GetProperty("cut").GetBoolean(), "The value is cut in a trigger that has room.");
        Assert.True(Math.Abs(m.GetProperty("expected").GetDouble() - m.GetProperty("trigger").GetDouble()) < 1,
            $"The trigger is {m.GetProperty("trigger").GetDouble()}px; its value and chevron need {m.GetProperty("expected").GetDouble()}px.");
    }

    [Fact]
    public async Task A_measure_still_reserves_room_for_a_longer_value()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="padding:16px"><div class="menu-anchor">
              <button class="value-trigger" id="a" type="button" style="--value-trigger-measure: 16ch"><span>Viewer</span></button></div>
              <div class="menu-anchor"><button class="value-trigger" id="b" type="button"><span>Viewer</span></button></div></div>
            """);

        var w = await page.EvaluateAsync<double[]>("() => ['a','b'].map(id => document.getElementById(id).getBoundingClientRect().width)");

        Assert.True(w[0] > w[1] + 40, $"A 16ch measure gave {w[0]}px against {w[1]}px without one.");
    }

    [Fact]
    public async Task A_group_heading_has_the_same_room_above_it_in_a_table_cell_as_on_a_page()
    {
        if (NoBrowser) return;
        const string Menu = """
            <div class="menu" style="position:static" id="{0}"><span class="menu-label">Access level</span>
              <button class="menu-item" type="button"><i class="ri-eye-line"></i> <span>Viewer</span></button></div>
            """;
        var (page, _) = await OpenStyled($"""
            <div style="padding:16px">{string.Format(Menu, "free")}
              <table class="table"><tbody><tr><td>{string.Format(Menu, "cell")}</td></tr></tbody></table></div>
            """);

        var h = await page.EvaluateAsync<double[][]>("""
            () => ['free','cell'].map(id => { const l = document.querySelector('#' + id + ' .menu-label');
                const r = document.createRange(); r.selectNodeContents(l);
                return [l.getBoundingClientRect().height, r.getBoundingClientRect().top - document.getElementById(id).getBoundingClientRect().top]; })
            """);

        Assert.Equal(h[0][0], h[1][0], 0.5);
        Assert.Equal(h[0][1], h[1][1], 0.5);
    }
}
