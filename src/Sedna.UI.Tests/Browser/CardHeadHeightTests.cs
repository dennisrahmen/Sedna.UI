using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>.card-head</c> is the same height whatever small control it holds: only a title, a badge,
/// an icon button, a menu button, a text button, a drag grip. A head that grew with its controls
/// moved every panel under it when a panel grid entered arrange mode and added its grips.
/// </summary>
public class CardHeadHeightTests : ScriptTestBase
{
    private const string Heads = """
        <div style="width:520px" id="stack">
          <div class="card"><div class="card-head" id="title"><h3>Open orders</h3></div></div>
          <div class="card"><div class="card-head" id="strong"><strong>Open orders</strong></div></div>
          <div class="card"><div class="card-head" id="badge"><h3>Open orders</h3><span class="badge">12</span></div></div>
          <div class="card"><div class="card-head" id="icon"><h3>Open orders</h3>
            <button class="btn btn--ghost btn--icon btn--sm" type="button" aria-label="More"><i class="ri-more-2-fill"></i></button></div></div>
          <div class="card"><div class="card-head" id="menu"><h3>Open orders</h3>
            <div class="card-head-right"><div class="menu-anchor"><button class="btn btn--sm" type="button" data-menu-toggle aria-expanded="false"><span>Actions</span><i class="ri-arrow-down-s-line"></i></button><div class="menu" hidden><button class="menu-item" type="button">Export</button></div></div></div></div></div>
          <div class="card"><div class="card-head" id="text"><h3>Open orders</h3>
            <div class="card-head-right"><button class="btn btn--sm" type="button">Refresh</button></div></div></div>
          <div class="card"><div class="card-head" id="grip">
            <span class="card-head-lead"><button class="drag-handle" type="button" data-drag-handle aria-label="Move"><i class="ri-draggable"></i></button></span>
            <h3>Open orders</h3></div></div>
          <div class="card"><div class="card-head" id="both">
            <span class="card-head-lead"><button class="drag-handle" type="button" data-drag-handle aria-label="Move"><i class="ri-draggable"></i></button></span>
            <h3>Open orders</h3>
            <div class="card-head-right"><button class="btn btn--ghost btn--icon btn--sm" type="button" aria-label="More"><i class="ri-more-2-fill"></i></button></div></div></div>
        </div>
        """;

    private static readonly string[] Ids = ["title", "strong", "badge", "icon", "menu", "text", "grip", "both"];

    private static async Task<Dictionary<string, double>> Measure(Microsoft.Playwright.IPage page) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(await page.EvaluateAsync<string>(
            "ids => JSON.stringify(Object.fromEntries(ids.map(id => [id, document.getElementById(id).getBoundingClientRect().height])))",
            Ids))!;

    [Theory]
    [InlineData(null)]
    [InlineData("compact")]
    [InlineData("touch")]
    public async Task Every_head_is_as_tall_as_one_holding_a_small_control(string? density)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Heads);
        if (density is not null) await Js.Run(page, "d => document.documentElement.setAttribute('data-density', d)", density);
        await Js.Frames(page);

        var heights = await Measure(page);
        var expected = await page.EvaluateAsync<double>("""
            () => {
              const cs = getComputedStyle(document.getElementById('title'));
              const control = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--control-height-sm'));
              return 2 * parseFloat(cs.paddingTop) + control + 1;
            }
            """);
        foreach (var id in Ids)
            Assert.True(Math.Abs(heights[id] - expected) <= 0.5,
                $"A head holding '{id}' measured {heights[id]}px, not the {expected}px a small control's head is ({density ?? "comfortable"}).");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_tab_strip_head_is_the_height_of_its_tab_not_of_a_title_row()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:520px"><div class="card"><div class="card-head card-head--tabs" id="tabs">
              <div class="tabs" role="tablist"><button class="tab" role="tab" aria-selected="true"><span>Open</span></button></div>
            </div></div></div>
            """);
        var h = await page.EvaluateAsync<double>("() => document.getElementById('tabs').getBoundingClientRect().height");
        var tab = await page.EvaluateAsync<double>("() => document.querySelector('#tabs .tab').getBoundingClientRect().height");
        Assert.True(h - tab <= 1.5, $"The tab strip's head was {h}px around a {tab}px tab.");
    }

    // The panel grid: the grips are in a head only while arranging. The head is the same
    // height with them and without, so the panels under it do not move.
    [Fact]
    public async Task A_panel_grid_head_keeps_its_height_between_viewing_and_arranging()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="panel-grid" id="grid" style="width: 960px; --panel-row: 48px; --grid-gap: 16px" data-drag-zone="dashboard" data-drag-axis="cells" aria-label="Dashboard">
              <section class="card card--resizable" id="a" data-drag-item="a" style="--panel-x: 0; --panel-y: 0; --panel-w: 6; --panel-h: 3">
                <div class="card-head"><span class="card-head-lead"><button class="drag-handle" type="button" data-drag-handle aria-label="Move a"><i class="ri-draggable"></i></button></span><h3>Open orders</h3></div>
                <div class="card-body">Body</div>
                <div class="card-resize" data-resize="--panel-h" role="separator" aria-orientation="horizontal" aria-label="Height" aria-valuemin="2" aria-valuenow="3" tabindex="0"></div>
              </section>
              <section class="card card--resizable" id="b" data-drag-item="b" style="--panel-x: 6; --panel-y: 0; --panel-w: 6; --panel-h: 3">
                <div class="card-head"><h3>Dispatch</h3>
                  <div class="card-head-right"><button class="btn btn--ghost btn--icon btn--sm" type="button" aria-label="More"><i class="ri-more-2-fill"></i></button></div></div>
                <div class="card-body">Body</div>
              </section>
              <section class="card card--resizable" id="c" data-drag-item="c" style="--panel-x: 0; --panel-y: 3; --panel-w: 12; --panel-h: 3">
                <div class="card-head"><h3>Returns</h3></div>
                <div class="card-body">Body</div>
              </section>
            </div>
            """);

        const string measure = """
            () => JSON.stringify([...document.querySelectorAll('#grid .card-head')].map(h => h.getBoundingClientRect().height))
            """;
        var viewing = await page.EvaluateAsync<string>(measure);
        await Js.Run(page, "() => document.getElementById('grid').setAttribute('data-arrange', '')");
        await Js.Frames(page);
        var arranging = await page.EvaluateAsync<string>(measure);
        await Js.Run(page, "() => document.getElementById('grid').removeAttribute('data-arrange')");
        await Js.Frames(page);
        var again = await page.EvaluateAsync<string>(measure);

        Assert.Equal(viewing, arranging);
        Assert.Equal(viewing, again);
        // And every head is one height, so the three panels line up their bodies.
        var all = System.Text.Json.JsonSerializer.Deserialize<double[]>(viewing)!;
        Assert.Single(all.Select(x => Math.Round(x)).Distinct());
    }
}
