using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.table--fixed</c> sizes each column from its header cell's <c>--col-width</c> and is as
/// wide as its columns; a <c>.col-resize</c> grip sits inside its cell at the inline-end edge,
/// mirrored right to left, and stays with its cell under a pinned column and a sticky header;
/// a header row being reordered draws its line inside the cells; a <c>.form-check</c> in a
/// <c>.menu</c> is a row as wide as the panel.
/// </summary>
public class TableColumnLayoutTests : ScriptTestBase
{
    private static string Grip(string key) =>
        $"""<div class="col-resize" id="g-{key}" data-resize="--col-width" data-resize-key="{key}" role="separator" aria-orientation="vertical" aria-label="Resize {key}" tabindex="0"></div>""";

    private static string Table(string dir = "ltr", string extra = "")
    {
        var rows = string.Concat(Enumerable.Range(0, 30).Select(i =>
            $"""<tr><td>ORD-{4200 + i}</td><td>Northwind Retail, Central Warehouse and Returns</td><td>Hamburg</td><td class="col-num">{i * 7}.00</td></tr>"""));
        return $"""
            <div dir="{dir}">
              <div class="sedna-scroll sedna-scroll-x" id="s" style="--scroll-max: 240px; max-height: 240px; width: 360px" tabindex="0">
                <table class="table table--fixed {extra}" id="t" aria-label="Orders">
                  <thead><tr>
                    <th id="h-order" style="--col-width: 120px"><span>Order</span>{Grip("order")}</th>
                    <th id="h-customer" style="--col-width: 200px"><span>Customer</span>{Grip("customer")}</th>
                    <th id="h-ship"><span>Ship to</span>{Grip("ship")}</th>
                    <th id="h-total" class="col-num" style="--col-width: 112px; --col-min: 100px"><span>Total</span>{Grip("total")}</th>
                  </tr></thead>
                  <tbody>{rows}</tbody>
                </table>
              </div>
            </div>
            """;
    }

    private sealed record Box(double Left, double Right, double Top, double Bottom, double Width);

    private static async Task<Box> BoxOf(Microsoft.Playwright.IPage page, string id)
    {
        var r = await page.EvaluateAsync<double[]>(
            "id => { const r = document.getElementById(id).getBoundingClientRect(); return [r.left, r.right, r.top, r.bottom, r.width]; }", id);
        return new Box(r[0], r[1], r[2], r[3], r[4]);
    }

    [Fact]
    public async Task Each_column_is_as_wide_as_its_header_says_and_the_table_is_their_sum()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Table());

        Assert.Equal(120, (await BoxOf(page, "h-order")).Width, 0.5);
        Assert.Equal(200, (await BoxOf(page, "h-customer")).Width, 0.5);
        Assert.Equal(160, (await BoxOf(page, "h-ship")).Width, 0.5);          // the table's 10rem
        Assert.Equal(112, (await BoxOf(page, "h-total")).Width, 0.5);
        Assert.Equal(592, (await BoxOf(page, "t")).Width, 1.0);

        // A value longer than its column ends in an ellipsis inside it.
        var cell = await page.EvaluateAsync<string[]>("""
            () => { const td = document.querySelector('#t tbody td:nth-child(2)'), cs = getComputedStyle(td);
                    return [cs.textOverflow, String(td.scrollWidth > td.clientWidth), String(Math.round(td.getBoundingClientRect().width))]; }
            """);
        Assert.Equal(["ellipsis", "true", "200"], cell);

        // A column's own minimum holds against a width set below it.
        await Js.Run(page, "() => document.getElementById('h-total').style.setProperty('--col-width', '40px')");
        Assert.Equal(100, (await BoxOf(page, "h-total")).Width, 0.5);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_grip_sits_inside_its_cell_on_the_inline_end_edge(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Table(dir));

        foreach (var key in new[] { "order", "customer", "ship", "total" })
        {
            var cell = await BoxOf(page, key == "ship" ? "h-ship" : $"h-{key}");
            var grip = await BoxOf(page, $"g-{key}");
            Assert.InRange(grip.Width, 6, 12);
            Assert.Equal(cell.Top, grip.Top, 0.5);
            Assert.Equal(cell.Bottom, grip.Bottom, 0.5);
            if (dir == "ltr") Assert.Equal(cell.Right, grip.Right, 0.5);
            else Assert.Equal(cell.Left, grip.Left, 0.5);
        }

        // The bounds the script reads are lengths, whatever unit the column wrote them in.
        var bounds = await page.EvaluateAsync<string[]>("""
            () => ['g-order', 'g-total'].map(id => { const cs = getComputedStyle(document.getElementById(id));
                return cs.getPropertyValue('--resize-min').trim() + ' ' + cs.getPropertyValue('--resize-max').trim(); })
            """);
        Assert.Equal(["64px 640px", "100px 640px"], bounds);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_pinned_column_keeps_its_grip_on_top_and_a_sticky_header_keeps_every_grip_in_view(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Table(dir, "table--sticky table--pin-start"));

        // Scrolled along and down: the pinned cell and its grip stay at the start of the
        // scroller, under the header that stays at its top, and the next column slides
        // under them.
        await Js.Run(page, $"() => {{ const s = document.getElementById('s'); s.scrollTop = 300; s.scrollLeft = {(dir == "rtl" ? -60 : 60)}; }}");
        await Js.Frames(page);
        var hits = await page.EvaluateAsync<string[]>("""
            () => {
                const s = document.getElementById('s').getBoundingClientRect();
                const at = id => { const r = document.getElementById(id).getBoundingClientRect();
                    const el = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
                    return el ? (el.id || el.closest('[id]').id) : ''; };
                const order = document.getElementById('h-order').getBoundingClientRect();
                return [at('g-order'), at('g-customer'),
                        String(Math.abs(order.top - s.top) < 2),
                        String(Math.abs((getComputedStyle(document.getElementById('s')).direction === 'rtl' ? s.right - order.right : order.left - s.left)) < 2)];
            }
            """);
        Assert.Equal("g-order", hits[0]);
        Assert.Equal("g-customer", hits[1]);
        Assert.Equal("true", hits[2]);
        Assert.Equal("true", hits[3]);

        // The customer grip, once the pinned column covers it, is under the pinned cell.
        await Js.Run(page, $"() => document.getElementById('s').scrollLeft = {(dir == "rtl" ? -200 : 200)}");
        await Js.Frames(page);
        var covered = await page.EvaluateAsync<string>("""
            () => { const r = document.getElementById('g-customer').getBoundingClientRect(), o = document.getElementById('h-order').getBoundingClientRect();
                    if (r.right <= o.left || r.left >= o.right) return 'not under';
                    const el = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
                    return el.closest('th').id; }
            """);
        Assert.Equal("h-order", covered);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_line_where_a_moved_column_lands_is_drawn_inside_the_cell(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div dir="{dir}">
              <table class="table table--fixed" style="--col-width: 120px">
                <thead><tr data-drag-zone="cols" data-drag-axis="x">
                  <th>Order</th>
                  <th id="a" data-drag-item="a" data-drop-edge="before">Customer</th>
                  <th id="b" data-drag-item="b" data-drop-edge="after">Total</th>
                </tr></thead>
                <tbody><tr><td>ORD-4209</td><td>Alex Fischer</td><td>86.40</td></tr></tbody>
              </table>
            </div>
            """);

        var lines = await page.EvaluateAsync<double[]>("""
            () => ['a', 'b'].flatMap(id => {
                const cell = document.getElementById(id), r = cell.getBoundingClientRect(), cs = getComputedStyle(cell, '::before');
                const start = getComputedStyle(cell).direction === 'rtl' ? r.right : r.left;
                return [parseFloat(cs.borderInlineStartWidth), parseFloat(cs.height), r.height,
                        parseFloat(cs.insetInlineStart), parseFloat(cs.insetInlineEnd)];
            })
            """);
        // before: 3px wide, the full height, at the inline start; after: at the inline end.
        Assert.Equal(3, lines[0]); Assert.Equal(lines[2], lines[1], 0.5); Assert.Equal(0, lines[3]);
        Assert.Equal(3, lines[5]); Assert.Equal(lines[7], lines[6], 0.5); Assert.Equal(0, lines[9]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_choice_in_a_menu_is_a_row_as_wide_as_the_panel_and_leaves_it_open()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="menu-anchor">
              <button class="btn btn--sm" id="toggle" type="button" data-menu-toggle aria-expanded="false"><span>Columns</span></button>
              <div class="menu" id="m" hidden>
                <span class="menu-label">Show columns</span>
                <label class="form-check" id="c1"><input type="checkbox" checked /> <span>Customer</span></label>
                <label class="form-check" id="c2"><input type="checkbox" checked disabled /> <span>Order</span></label>
                <button class="menu-item" id="reset" type="button"><span>Reset columns</span></button>
              </div>
            </div>
            """);

        await page.ClickAsync("#toggle");
        var check = await BoxOf(page, "c1");
        var item = await BoxOf(page, "reset");
        Assert.Equal(item.Width, check.Width, 0.5);
        Assert.Equal(item.Left, check.Left, 0.5);
        Assert.Equal(item.Bottom - item.Top, check.Bottom - check.Top, 1.0);

        await page.ClickAsync("#c1");
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('m').hidden"));
        Assert.False(await page.EvaluateAsync<bool>("() => document.querySelector('#c1 input').checked"));
        Assert.Equal("not-allowed", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('c2')).cursor"));

        await page.ClickAsync("#reset");
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('m').hidden"));
        Assert.Empty(errors);
    }
}
