using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The states of a cell in a grid — focused, selected, edited, rejected, changed — and of a
/// row in edit mode, as a layout engine paints them, beside a pinned column and under a
/// sticky header.
/// </summary>
public class CellStateTests : ScriptTestBase
{
    private const string NoTransitions = "<style>*, *::before, *::after { transition: none !important; }</style>";

    /// <summary>A wide grid with a sticky header and both edges pinned, in a narrow scroller.</summary>
    private static string Pinned(string dir = "ltr") => $"""
        <div dir="{dir}">
        <div class="sedna-scroll sedna-scroll-x" id="s" style="--scroll-max: 220px; width: 420px" tabindex="0" role="group" aria-label="Lines">
          <table class="table table--sticky table--pin-start table--pin-end" role="grid" id="g" aria-label="Order lines" aria-multiselectable="true">
            <thead><tr>{string.Concat(Enumerable.Range(0, 8).Select(c => $"<th id=\"h{c}\">Column {c}</th>"))}</tr></thead>
            <tbody>
              {string.Concat(Enumerable.Range(0, 12).Select(r => "<tr>" + string.Concat(Enumerable.Range(0, 8).Select(c =>
                  c == 3 ? $"<td id=\"c{r}{c}\">{r * 7}<input class=\"cell-editor\" value=\"{r * 7}\" aria-label=\"Quantity {r}\"></td>"
                         : $"<td id=\"c{r}{c}\">Value {r}-{c}</td>")) + "</tr>"))}
            </tbody>
          </table>
        </div>
        </div>
        """;

    [Fact]
    public async Task Pinned_columns_and_the_sticky_header_still_pin_in_a_grid_and_the_keyboard_never_lands_under_them()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Pinned(), NoTransitions);

        var positions = await page.EvaluateAsync<string[]>("""
            () => ['h0', 'c00', 'c07', 'c03'].map(id => getComputedStyle(document.getElementById(id)).position)
            """);
        // An editable cell is positioned for its editor; the pinned ones stay sticky.
        Assert.Equal(["sticky", "sticky", "sticky", "relative"], positions);

        await page.Locator("#c01").ClickAsync();
        for (var i = 0; i < 6; i++) await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("c07", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        // Back along the row: each cell the keyboard reaches is clear of both pinned columns.
        for (var i = 0; i < 5; i++)
        {
            await page.Keyboard.PressAsync("ArrowLeft");
            var clear = await page.EvaluateAsync<string>("""
                () => {
                    const c = document.activeElement.getBoundingClientRect();
                    const row = document.activeElement.parentElement;
                    const start = row.cells[0].getBoundingClientRect(), end = row.cells[row.cells.length - 1].getBoundingClientRect();
                    return c.left >= start.right - 1 && c.right <= end.left + 1 ? '' : `${document.activeElement.id} at ${c.left}–${c.right}, pins end ${start.right} and start ${end.left}`;
                }
                """);
            Assert.True(clear.Length == 0, $"{Engine}: {clear}");
        }

        // Down the column: each cell clears the sticky header.
        for (var i = 0; i < 11; i++) await page.Keyboard.PressAsync("ArrowDown");
        for (var i = 0; i < 10; i++) await page.Keyboard.PressAsync("ArrowUp");
        var underHead = await page.EvaluateAsync<string>("""
            () => {
                const c = document.activeElement.getBoundingClientRect(), h = document.querySelector('thead').getBoundingClientRect();
                return c.top >= h.bottom - 1 ? '' : `${document.activeElement.id} at ${c.top}, the header ends at ${h.bottom}`;
            }
            """);
        Assert.True(underHead.Length == 0, $"{Engine}: {underHead}");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_focused_cell_is_ringed_and_a_selected_range_is_tinted_over_each_cells_own_surface()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Pinned(), NoTransitions);

        await page.Locator("#c11").ClickAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        var ring = await page.EvaluateAsync<string[]>("""
            () => { const s = getComputedStyle(document.activeElement); return [document.activeElement.id, s.outlineStyle, s.outlineWidth]; }
            """);
        Assert.Equal(["c12", "solid", "2px"], ring);

        // From the header across into the body, with Shift: the header cell is selected
        // and keeps its opaque surface under the tint; a pinned cell takes it too.
        await page.Locator("#h0").ClickAsync();
        await page.Keyboard.PressAsync("Shift+ArrowDown");
        await page.Keyboard.PressAsync("Shift+ArrowRight");
        var paint = await page.EvaluateAsync<string[]>("""
            () => {
                const probe = document.createElement('i');
                document.body.append(probe);
                const resolve = t => { probe.style.color = `var(${t})`; return getComputedStyle(probe).color; };
                const head = getComputedStyle(document.getElementById('h1'));
                const pinned = getComputedStyle(document.getElementById('c00'));
                const plain = getComputedStyle(document.getElementById('c01'));
                return [
                    head.backgroundColor === resolve('--bg-elevated') ? 'opaque' : head.backgroundColor,
                    head.backgroundImage.includes(resolve('--brand-tint')) ? 'tinted' : head.backgroundImage,
                    pinned.backgroundImage.includes(resolve('--brand-tint')) ? 'tinted' : pinned.backgroundImage,
                    plain.backgroundImage.includes(resolve('--brand-tint')) ? 'tinted' : plain.backgroundImage,
                    getComputedStyle(document.getElementById('c02')).backgroundImage,
                ];
            }
            """);
        Assert.Equal(["opaque", "tinted", "tinted", "tinted", "none"], paint);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_rejected_or_changed_cell_is_marked_in_its_inline_end_corner_and_a_rejected_editor_takes_the_danger_edge(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <table class="table" role="grid" dir="{dir}" aria-label="Order lines">
              <tbody><tr>
                <td id="dirty" data-dirty>12<input class="cell-editor" value="12" aria-label="Quantity"></td>
                <td id="bad">-3<input class="cell-editor" id="bad-editor" value="-3" aria-invalid="true" aria-label="Quantity"></td>
                <td id="both" data-dirty aria-invalid="true">x</td>
                <td id="plain">4</td>
              </tr></tbody>
            </table>
            """, NoTransitions);

        var marks = await page.EvaluateAsync<string[]>("""
            dir => {
                const probe = document.createElement('i');
                document.body.append(probe);
                const resolve = t => { probe.style.color = `var(${t})`; return getComputedStyle(probe).color; };
                const want = { dirty: resolve('--info-fg'), bad: resolve('--field-border-danger'), both: resolve('--field-border-danger') };
                const out = [];
                for (const id of ['dirty', 'bad', 'both']) {
                    const a = getComputedStyle(document.getElementById(id), '::after');
                    if (a.content === 'none') { out.push(`${id}: no mark`); continue; }
                    if (a.borderTopColor !== want[id]) out.push(`${id}: the mark is ${a.borderTopColor}, not ${want[id]}`);
                    const end = dir === 'rtl' ? a.left : a.right;
                    if (end !== '0px') out.push(`${id}: the mark is not in the inline-end corner (${a.left} / ${a.right})`);
                    if (parseFloat(a.borderTopWidth) < 4) out.push(`${id}: the mark is ${a.borderTopWidth} high`);
                }
                if (getComputedStyle(document.getElementById('plain'), '::after').content !== 'none') out.push('plain: marked');
                document.getElementById('bad').setAttribute('data-editing', '');
                const editor = getComputedStyle(document.getElementById('bad-editor'));
                if (!editor.boxShadow.includes(want.bad)) out.push(`bad: the editor's edge is ${editor.boxShadow}`);
                return out;
            }
            """, dir);

        Assert.True(marks.Length == 0, $"{Engine}, {dir}:\n  " + string.Join("\n  ", marks));

        // Focused, a rejected editor keeps its danger edge and takes the ring.
        await page.Locator("#bad-editor").FocusAsync();
        var focused = await page.EvaluateAsync<bool>("""
            () => {
                const probe = document.createElement('i');
                document.body.append(probe);
                const resolve = t => { probe.style.color = `var(${t})`; return getComputedStyle(probe).color; };
                const s = getComputedStyle(document.getElementById('bad-editor')).boxShadow;
                return s.includes(resolve('--field-border-danger')) && s.includes(resolve('--focus-ring'));
            }
            """);
        Assert.True(focused, $"{Engine}: a focused rejected editor lost its edge or its ring.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_row_in_edit_mode_shows_every_editor_as_a_field_on_the_soft_surface()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <table class="table table--pin-start" role="grid" aria-label="Order lines">
              <tbody>
                <tr id="row" data-editing>
                  <td id="name">Desk lamp<input class="cell-editor" id="en" value="Desk lamp" aria-label="Line"></td>
                  <td id="qty" class="col-num">12<input class="cell-editor" id="eq" value="12" aria-label="Quantity"></td>
                  <td><button class="btn btn--sm" type="button" data-row-save>Save</button></td>
                </tr>
                <tr><td id="other">Monitor arm<input class="cell-editor" id="eo" value="Monitor arm" aria-label="Line"></td><td>4</td><td></td></tr>
              </tbody>
            </table>
            """, NoTransitions);

        var state = await page.EvaluateAsync<string[]>("""
            () => {
                const probe = document.createElement('i');
                document.body.append(probe);
                const resolve = t => { probe.style.color = `var(${t})`; return getComputedStyle(probe).color; };
                const shown = id => getComputedStyle(document.getElementById(id)).display !== 'none';
                const qty = getComputedStyle(document.getElementById('eq'));
                const pinned = getComputedStyle(document.getElementById('name'));
                return [
                    String(shown('en') && shown('eq')),
                    String(shown('eo')),
                    qty.boxShadow.includes(resolve('--field-border')) ? 'edge' : qty.boxShadow,
                    qty.textAlign,
                    getComputedStyle(document.getElementById('qty')).backgroundColor === resolve('--surface-soft') ? 'soft' : 'not soft',
                    pinned.backgroundImage.includes(resolve('--surface-soft')) ? 'soft' : pinned.backgroundImage,
                ];
            }
            """);
        Assert.Equal(["true", "false", "edge", "end", "soft", "soft"], state);
        Assert.Empty(errors);
    }
}
