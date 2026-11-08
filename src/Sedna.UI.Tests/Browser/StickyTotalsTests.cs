using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.table--sticky-foot</c> holds the totals at the bottom of the scroller while the body
/// scrolls under them, opaque, beside a pinned column; a <c>.tr-pinned</c> record in the
/// <c>&lt;thead&gt;</c> stays under the headings; and a keyboard grid keeps the cell it
/// reaches clear of both.
/// </summary>
public class StickyTotalsTests : ScriptTestBase
{
    private static string Table(string role = "")
    {
        var rows = string.Concat(Enumerable.Range(0, 40).Select(i =>
            $"""<tr><td id="r{i}">Store {i:00}</td><td class="col-num">{i * 7}</td><td class="col-num">{i * 3}</td><td class="col-num">{i}</td></tr>"""));
        return $"""
            <div class="sedna-scroll sedna-scroll-x" id="s" style="--scroll-max: 240px; max-height: 240px; width: 300px" tabindex="0">
              <table class="table table--sticky table--sticky-foot table--pin-start" id="t" {role} aria-label="Stock by store" style="min-width: 640px">
                <thead>
                  <tr><th>Store</th><th class="col-num">Lamps</th><th class="col-num">Arms</th><th class="col-num">Mats</th></tr>
                  <tr class="tr-pinned" id="mine"><td id="mine0">Store 07 (yours)</td><td class="col-num">49</td><td class="col-num">21</td><td class="col-num">7</td></tr>
                </thead>
                <tbody>{rows}</tbody>
                <tfoot><tr id="total"><th scope="row" id="total0">Total</th><td class="col-num">5,460</td><td class="col-num">2,340</td><td class="col-num" id="total3">780</td></tr></tfoot>
              </table>
            </div>
            """;
    }

    [Fact]
    public async Task The_totals_stay_at_the_bottom_of_the_scroller_and_the_pinned_row_under_the_headings()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Table());

        foreach (var top in new[] { 0, 300, 100_000 })
        {
            await Js.Run(page, $"() => document.getElementById('s').scrollTop = {top}");
            var r = await page.EvaluateAsync<double[]>("""
                () => {
                    const s = document.getElementById('s'), box = s.getBoundingClientRect();
                    const bottom = box.top + s.clientTop + s.clientHeight;
                    const head = document.querySelector('#t thead tr').getBoundingClientRect();
                    return [document.getElementById('total').getBoundingClientRect().bottom, bottom,
                            document.getElementById('mine').getBoundingClientRect().top, head.bottom];
                }
                """);
            Assert.True(Math.Abs(r[0] - r[1]) < 1.5, $"At scrollTop {top} the totals end at {r[0]}, the scroller at {r[1]}.");
            Assert.True(Math.Abs(r[2] - r[3]) < 1.5, $"At scrollTop {top} the pinned row starts at {r[2]}, under a header ending at {r[3]}.");
        }
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_totals_are_opaque_and_paint_over_the_pinned_column_of_the_rows_beneath()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Table());
        await Js.Run(page, "() => { const s = document.getElementById('s'); s.scrollTop = 300; s.scrollLeft = 200; }");

        // What is on top at the middle of the totals' pinned cell, and of an unpinned one.
        var hits = await page.EvaluateAsync<string[]>("""
            () => ['total0', 'total3'].map(id => {
                const r = document.getElementById(id).getBoundingClientRect();
                const s = document.getElementById('s').getBoundingClientRect();
                const x = Math.min(Math.max(r.left + r.width / 2, s.left + 2), s.right - 20);
                const el = document.elementFromPoint(x, r.top + r.height / 2);
                return el.closest('tr')?.id ?? el.tagName;
            })
            """);
        Assert.Equal(["total", "total"], hits);

        // The row header of the totals is pinned with its column, and both its surface and
        // the totals' are opaque.
        var cells = await page.EvaluateAsync<string[]>("""
            () => ['total0', 'total3', 'mine0'].map(id => {
                const cs = getComputedStyle(document.getElementById(id));
                return cs.position + '|' + cs.backgroundColor;
            })
            """);
        Assert.StartsWith("sticky|", cells[0], StringComparison.Ordinal);
        foreach (var c in cells)
        {
            var colour = c.Split('|')[1];
            Assert.False(colour.StartsWith("rgba(", StringComparison.Ordinal) && !colour.EndsWith(", 1)", StringComparison.Ordinal),
                $"A pinned band's surface is translucent: {colour}");
            Assert.NotEqual("transparent", colour);
        }
        Assert.StartsWith("sticky|", cells[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_grid_keeps_the_cell_it_reaches_above_the_sticky_totals()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Table("role=\"grid\""));
        await page.Locator("#r0").FocusAsync();

        for (var i = 0; i < 12; i++)
        {
            await page.Keyboard.PressAsync("ArrowDown");
            var clear = await page.EvaluateAsync<bool>("""
                () => {
                    const c = document.activeElement.getBoundingClientRect();
                    const foot = document.querySelector('#t tfoot').getBoundingClientRect();
                    const head = document.querySelector('#t thead').getBoundingClientRect();
                    return c.bottom <= foot.top + 1 && c.top >= head.bottom - 1;
                }
                """);
            Assert.True(clear, $"After {i + 1} moves the focused cell is under a sticky band.");
        }
    }
}
