using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Four states measured rather than scanned: a busy button that must not change size, an
/// empty row whose oversized colspan must not add columns, a filter row that must pin under
/// its header, and a small stat whose label must not shrink with its value.
/// </summary>
public class ControlStateTests : ScriptTestBase
{
    [Fact]
    public async Task A_busy_button_keeps_its_size_and_shows_a_ring_in_its_own_ink()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <button class="btn btn-primary" id="idle" type="button"><i class="ri-save-line"></i> Save changes</button>
            <button class="btn btn-primary" id="busy" type="button" aria-busy="true"><i class="ri-save-line"></i> Save changes</button>
            """);

        var result = await page.EvaluateAsync<double[]>("""
            () => {
                const idle = document.getElementById('idle').getBoundingClientRect();
                const b = document.getElementById('busy');
                const busy = b.getBoundingClientRect();
                const ring = getComputedStyle(b, '::after');
                return [idle.width - busy.width, idle.height - busy.height,
                        ring.borderTopColor === getComputedStyle(b).color ? 1 : 0,
                        getComputedStyle(b).webkitTextFillColor === 'rgba(0, 0, 0, 0)' ? 1 : 0];
            }
            """);

        // Same box — the label is painted out, not taken out.
        Assert.Equal(0, result[0], 0.5);
        Assert.Equal(0, result[1], 0.5);
        // The ring is the variant's own ink, and the label is invisible.
        Assert.Equal(1, result[2]);
        Assert.Equal(1, result[3]);
    }

    [Fact]
    public async Task An_empty_row_spans_the_table_without_adding_columns()
    {
        if (NoBrowser) return;
        // colspan="999" is only safe if the browser clamps it: extra implied columns would
        // take width from the real ones.
        var (page, _) = await OpenStyled("""
            <table class="table" id="with" style="width:600px">
              <thead><tr><th>Order</th><th>Status</th><th>Owner</th></tr></thead>
              <tbody><tr class="tr-empty"><td colspan="999">No order matches these filters.</td></tr></tbody>
            </table>
            <table class="table" id="without" style="width:600px">
              <thead><tr><th>Order</th><th>Status</th><th>Owner</th></tr></thead>
              <tbody><tr><td></td><td></td><td></td></tr></tbody>
            </table>
            """);

        var widths = await page.EvaluateAsync<double[][]>("""
            () => ['with', 'without'].map(id =>
                [...document.querySelectorAll('#' + id + ' th')].map(th => th.getBoundingClientRect().width)
                .concat([document.querySelector('#' + id + ' tbody td').getBoundingClientRect().width]))
            """);

        var with = widths[0];
        Assert.Equal(600, with[3], 1.5);                   // the cell spans the whole table
        Assert.Equal(600, with[0] + with[1] + with[2], 1.5); // and no hidden column took a share
    }

    [Fact]
    public async Task A_filter_row_pins_under_the_header_and_a_group_row_under_both()
    {
        if (NoBrowser) return;
        var rows = string.Concat(Enumerable.Range(1, 60).Select(i => $"<tr><td>ORD-{4000 + i}</td><td>Open</td></tr>"));
        var (page, _) = await OpenStyled($"""
            <div id="scroller" style="height:300px; overflow:auto">
              <table class="table table--sticky">
                <thead>
                  <tr><th>Order</th><th>Status</th></tr>
                  <tr class="tr-filter">
                    <th><input class="form-input form-input-sm" aria-label="Filter by order"></th>
                    <th><input class="form-input form-input-sm" aria-label="Filter by status"></th>
                  </tr>
                </thead>
                <tbody>
                  <tr class="tr-group"><th colspan="2"><span class="tr-group-body">EU-West</span></th></tr>
                  {rows}
                </tbody>
              </table>
            </div>
            """);

        var tops = await page.EvaluateAsync<double[]>("""
            () => {
                const s = document.getElementById('scroller');
                s.scrollTop = 500;
                const base = s.getBoundingClientRect().top;
                const head = document.querySelector('thead tr:first-child th').getBoundingClientRect();
                const filter = document.querySelector('.tr-filter th').getBoundingClientRect();
                const group = document.querySelector('.tr-group th').getBoundingClientRect();
                return [head.top - base, filter.top - base, head.height, group.top - base, filter.height];
            }
            """);

        Assert.Equal(0, tops[0], 1);                        // header pinned to the top
        Assert.Equal(tops[2], tops[1], 1.5);                // filter row directly under it
        Assert.Equal(tops[1] + tops[4], tops[3], 1.5);      // group row under both, overlapping neither
    }

    [Fact]
    public async Task A_small_stat_steps_down_its_value_and_nothing_else()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="stat-row">
              <span class="stat" id="n"><span class="stat-label">Open</span><span class="stat-value">12</span></span>
              <span class="stat stat--sm" id="t"><span class="stat-label">Last import</span><span class="stat-value">12.09.2026 02:30</span></span>
            </div>
            """);

        var sizes = await page.EvaluateAsync<double[]>("""
            () => ['n', 't'].flatMap(id => ['.stat-label', '.stat-value'].map(sel =>
                parseFloat(getComputedStyle(document.querySelector('#' + id + ' ' + sel)).fontSize)))
            """);

        Assert.Equal(sizes[0], sizes[2]);       // labels match across the row
        Assert.True(sizes[3] < sizes[1], $"The small value is {sizes[3]}px against {sizes[1]}px.");
    }
}
