using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The filter row holds a different control for each type of column, and the sticky
/// offset under it is one number. Every control therefore has to come out at the height
/// a plain small field does, or the rows below the header show through a seam — which a
/// source scan cannot see and a layout engine measures directly.
/// </summary>
public class TableFilterLayoutTests : ScriptTestBase
{
    private const string FilterRow = """
        <tr class="tr-filter">
          <th id="plain"><input class="form-input form-input-sm" placeholder="Order" aria-label="Order"></th>
          <th id="text">
            <div class="input-group input-group--sm">
              <span class="input-affix"><i class="ri-search-line"></i></span>
              <input class="form-input" type="search" value="orders" placeholder="Host" aria-label="Host">
              <button class="input-clear" type="button" aria-label="Clear"><i class="ri-close-line"></i></button>
            </div>
          </th>
          <th id="choice"><select class="form-select form-select-sm" aria-label="Status"><option>Any</option></select></th>
          <th id="several" class="col-filtered">
            <button class="form-input form-input-sm form-trigger" type="button" popovertarget="p">
              <i class="ri-calendar-line"></i><span>2 selected</span>
            </button>
            <div class="popover" id="p" popover><strong class="popover-title">Region</strong>Pick</div>
          </th>
          <th id="range">
            <div class="input-group input-group--sm">
              <input class="form-input" type="number" placeholder="Min" aria-label="Min">
              <span class="input-affix">–</span>
              <input class="form-input" type="number" placeholder="Max" aria-label="Max">
            </div>
          </th>
          <th id="bool">
            <div class="segmented segmented--sm" role="group" aria-label="Monitored">
              <label class="segmented-option"><input type="radio" name="m" checked>Any</label>
              <label class="segmented-option"><input type="radio" name="m">Yes</label>
              <label class="segmented-option"><input type="radio" name="m">No</label>
            </div>
          </th>
        </tr>
        """;

    [Fact]
    public async Task Every_type_of_filter_is_as_tall_as_a_small_field_and_the_row_stays_the_sticky_offset()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <table class="table table--sticky" id="t" style="width:1100px">
              <thead>
                <tr><th>Order</th><th>Host</th><th>Status</th><th class="col-filtered">Region</th><th>CPU</th><th>Monitored</th></tr>
                {FilterRow}
              </thead>
              <tbody><tr><td>1</td><td>2</td><td>3</td><td>4</td><td>5</td><td>6</td></tr></tbody>
            </table>
            """);

        var measured = await page.EvaluateAsync<double[]>("""
            () => {
                const cells = ['plain', 'text', 'choice', 'several', 'range', 'bool'].map(id => document.getElementById(id));
                const controls = cells.map(c => c.firstElementChild.getBoundingClientRect().height);
                const offset = parseFloat(getComputedStyle(document.getElementById('t')).getPropertyValue('--table-filter-height'));
                const row = document.querySelector('.tr-filter').getBoundingClientRect().height;
                return [offset, row, ...controls];
            }
            """);

        Assert.True(Math.Abs(measured[0] - measured[1]) < 0.01,
            $"The filter row is {measured[1]}px against a sticky offset of {measured[0]}px; controls: {string.Join(", ", measured.Skip(2))}.");
        foreach (var control in measured.Skip(2))
            Assert.Equal(28, control, 0.01);                    // every control at the small tier
    }

    [Fact]
    public async Task A_clear_button_shows_only_while_the_field_has_a_value()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="input-group input-group--sm" id="g">
              <span class="input-affix"><i class="ri-search-line"></i></span>
              <input class="form-input" type="search" placeholder="Host" aria-label="Host">
              <button class="input-clear" type="button" aria-label="Clear"><i class="ri-close-line"></i></button>
            </div>
            """);

        const string Shown = "() => getComputedStyle(document.querySelector('.input-clear')).display !== 'none'";
        Assert.False(await page.EvaluateAsync<bool>(Shown));

        await page.FillAsync("#g input", "orders");
        Assert.True(await page.EvaluateAsync<bool>(Shown));

        var fits = await page.EvaluateAsync<double[]>("""
            () => [document.getElementById('g').getBoundingClientRect().height,
                   document.querySelector('.input-clear').getBoundingClientRect().height]
            """);
        Assert.Equal(28, fits[0], 0.5);                         // the button does not grow the group
    }

    [Fact]
    public async Task A_header_filter_button_keeps_the_header_row_its_height_and_its_popover_reads_as_prose()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <table class="table" id="with" style="width:500px">
              <thead><tr>
                <th>Order</th>
                <th class="col-filtered">
                  <div class="sedna-row sedna-gap-0">
                    Status
                    <button class="th-filter th-filter--active" type="button" popovertarget="p" aria-label="Filter by status"><i class="ri-filter-3-line"></i></button>
                    <div class="popover" id="p" popover><strong class="popover-title">Status</strong><span id="prose">Open or held</span></div>
                  </div>
                </th>
                <th aria-sort="descending">
                  <div class="sedna-row sedna-gap-0">
                    <button class="th-sort" type="button">Placed</button>
                    <button class="th-filter" id="sorted-filter" type="button" aria-label="Filter by date"><i class="ri-filter-3-line"></i></button>
                  </div>
                </th>
              </tr></thead>
            </table>
            <table class="table" id="without" style="width:500px">
              <thead><tr><th>Order</th><th>Status</th><th aria-sort="descending"><button class="th-sort" type="button">Placed</button></th></tr></thead>
            </table>
            """);

        await page.ClickAsync("[popovertarget='p']");

        var result = await page.EvaluateAsync<string[]>("""
            () => {
                const h = id => document.querySelector('#' + id + ' thead tr').getBoundingClientRect().height;
                const prose = getComputedStyle(document.getElementById('prose'));
                const filter = document.getElementById('sorted-filter').getBoundingClientRect();
                const th = document.getElementById('sorted-filter').closest('th').getBoundingClientRect();
                return [String(h('with')), String(h('without')), prose.textTransform, prose.fontWeight,
                        String(th.right - filter.right),
                        document.getElementById('p').matches(':popover-open') ? 'open' : 'closed'];
            }
            """);

        Assert.Equal(double.Parse(result[1], System.Globalization.CultureInfo.InvariantCulture),
                     double.Parse(result[0], System.Globalization.CultureInfo.InvariantCulture), 0.5);
        Assert.Equal("none", result[2]);                        // not the header's uppercase
        Assert.Equal("400", result[3]);                         // nor its weight
        Assert.True(double.Parse(result[4], System.Globalization.CultureInfo.InvariantCulture) >= 4,
            "The filter beside a sort button sits against the cell's edge.");
        Assert.Equal("open", result[5]);
    }

    [Fact]
    public async Task A_sticky_filter_row_with_every_type_stays_flush_under_the_header()
    {
        if (NoBrowser) return;
        var rows = string.Concat(Enumerable.Range(1, 60).Select(i =>
            $"<tr><td>ORD-{4000 + i}</td><td>a</td><td>b</td><td>c</td><td>d</td><td>e</td></tr>"));
        var (page, _) = await OpenStyled($"""
            <div id="scroller" style="height:300px; width:1100px; overflow:auto">
              <table class="table table--sticky">
                <thead>
                  <tr><th>Order</th><th>Host</th><th>Status</th><th class="col-filtered">Region</th><th>CPU</th><th>Monitored</th></tr>
                  {FilterRow}
                </thead>
                <tbody>
                  <tr class="tr-group"><th colspan="6"><span class="tr-group-body">EU-West</span></th></tr>
                  {rows}
                </tbody>
              </table>
            </div>
            """);

        var tops = await page.EvaluateAsync<double[]>("""
            () => {
                const s = document.getElementById('scroller');
                s.scrollTop = 600;
                const base = s.getBoundingClientRect().top;
                const head = document.querySelector('thead tr:first-child th').getBoundingClientRect();
                const filter = document.querySelector('.tr-filter').getBoundingClientRect();
                const group = document.querySelector('.tr-group th').getBoundingClientRect();
                return [head.top - base, filter.top - base, head.height, filter.bottom - base, group.top - base];
            }
            """);

        Assert.Equal(0, tops[0], 0.01);
        Assert.Equal(tops[2], tops[1], 0.01);                   // filter row flush under the header
        Assert.InRange(tops[4], tops[3] - 1.01, tops[3]);       // group row under both, no strip between
    }
}
