using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>table[role=grid]</c> is one tab stop with arrow-key movement between cells, a
/// range selection the app can read, editing in place, and a tab stop and a focus that
/// survive the app re-rendering its rows.
/// </summary>
public class GridKeyboardTests : ScriptTestBase
{
    /// <summary>Three body rows of three cells; the third column edits in place.</summary>
    private const string Grid = """
        <button id="before">Before</button>
        <table class="table" role="grid" id="g" aria-label="Order lines" aria-multiselectable="true">
          <thead><tr><th id="h0">Line</th><th id="h1">Store</th><th id="h2" class="col-num">Quantity</th></tr></thead>
          <tbody>
            <tr><td id="c00">Desk lamp</td><td id="c01">Northwind Retail</td><td id="c02" class="col-num">12<input class="cell-editor" id="e02" type="text" value="12" aria-label="Quantity, desk lamp"></td></tr>
            <tr><td id="c10">Monitor arm</td><td id="c11"><button id="b11" type="button">Open</button></td><td id="c12" class="col-num">4<input class="cell-editor" id="e12" type="text" value="4" aria-label="Quantity, monitor arm"></td></tr>
            <tr><td id="c20">Footrest</td><td id="c21"><input type="checkbox" id="x21" aria-label="Gift"></td><td id="c22" class="col-num">1<input class="cell-editor" id="e22" type="text" value="1" aria-label="Quantity, footrest"></td></tr>
          </tbody>
        </table>
        <button id="after">After</button>
        """;

    private static Task<string> Active(Microsoft.Playwright.IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement.id");

    [Fact]
    public async Task The_grid_is_one_tab_stop_and_its_controls_are_out_of_the_tab_order()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Grid);

        await page.Locator("#before").FocusAsync();
        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("c00", await Active(page));

        // The button and the checkbox in the cells are skipped: Tab leaves the grid.
        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("after", await Active(page));

        var stops = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('#g [tabindex="0"]')].map(e => e.id)
            """);
        Assert.Equal(["c00"], stops);
        Assert.Equal("-1", await page.GetAttributeAsync("#b11", "tabindex"));
        Assert.Equal("-1", await page.GetAttributeAsync("#x21", "tabindex"));

        // Shift+Tab comes back to the cell the reader left, not to the first one.
        await page.Locator("#c00").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync(Keys.Tab);
        await page.Keyboard.PressAsync(Keys.ShiftTab);
        Assert.Equal("c10", await Active(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Arrows_Home_End_and_the_primary_chords_move_between_cells_without_wrapping()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);
        await page.Locator("#c00").FocusAsync();

        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("c01", await Active(page));
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("c11", await Active(page));
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("c10", await Active(page));
        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("h0", await Active(page));
        // At an edge an arrow stays put rather than wrapping.
        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("h0", await Active(page));

        await page.Keyboard.PressAsync("End");
        Assert.Equal("h2", await Active(page));
        await page.Keyboard.PressAsync("Home");
        Assert.Equal("h0", await Active(page));
        await page.Keyboard.PressAsync(Keys.Primary("End"));
        Assert.Equal("c22", await Active(page));
        await page.Keyboard.PressAsync(Keys.Primary("Home"));
        Assert.Equal("h0", await Active(page));

        // The tab stop moves with the focus.
        Assert.Equal("0", await page.GetAttributeAsync("#h0", "tabindex"));
        Assert.Equal("-1", await page.GetAttributeAsync("#c22", "tabindex"));
    }

    [Fact]
    public async Task In_a_right_to_left_grid_the_left_arrow_goes_to_the_next_cell()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"<div dir=\"rtl\">{Grid}</div>");
        await page.Locator("#c00").FocusAsync();

        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("c01", await Active(page));
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("c00", await Active(page));
    }

    [Fact]
    public async Task Page_keys_move_by_the_rows_the_scroller_shows_and_stop_at_the_ends()
    {
        if (NoBrowser) return;
        var rows = string.Concat(Enumerable.Range(0, 40).Select(i =>
            $"""<tr><td id="r{i}">Line {i}</td><td>{i * 3}</td></tr>"""));
        var (page, _) = await OpenStyled($"""
            <div class="sedna-scroll" id="s" style="--scroll-max: 200px; max-height: 200px">
              <table class="table table--sticky" role="grid" aria-label="Lines">
                <thead><tr><th>Line</th><th>Count</th></tr></thead>
                <tbody>{rows}</tbody>
              </table>
            </div>
            """);

        var page1 = await page.EvaluateAsync<int>("""
            () => {
                const s = document.getElementById('s'), head = s.querySelector('thead').getBoundingClientRect().height;
                const row = document.getElementById('r0').parentElement.getBoundingClientRect().height;
                return Math.max(1, Math.floor((s.clientHeight - head) / row) - 1);
            }
            """);
        Assert.InRange(page1, 2, 10);

        await page.Locator("#r0").FocusAsync();
        await page.Keyboard.PressAsync("PageDown");
        Assert.Equal($"r{page1}", await Active(page));

        // The cell the page key reached is in view, below the sticky header.
        var clear = await page.EvaluateAsync<bool>("""
            () => {
                const s = document.getElementById('s').getBoundingClientRect();
                const head = document.querySelector('thead').getBoundingClientRect();
                const c = document.activeElement.getBoundingClientRect();
                return c.top >= head.bottom - 1 && c.bottom <= s.bottom + 1;
            }
            """);
        Assert.True(clear, "The cell a page key reached is hidden under the header or below the scroller.");

        await page.Keyboard.PressAsync("PageUp");
        Assert.Equal("r0", await Active(page));
        for (var i = 0; i < 20; i++) await page.Keyboard.PressAsync("PageDown");
        Assert.Equal("r39", await Active(page));
    }

    [Fact]
    public async Task Shift_with_a_move_selects_a_range_the_app_reads_and_a_plain_move_clears_it()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);
        await page.Locator("#c00").FocusAsync();

        await page.Keyboard.PressAsync("Shift+ArrowRight");
        await page.Keyboard.PressAsync("Shift+ArrowDown");
        Assert.Equal("c11", await Active(page));

        const string Selected = "() => [...document.querySelectorAll('#g [aria-selected=\"true\"]')].map(e => e.id)";
        Assert.Equal(["c00", "c01", "c10", "c11"], await page.EvaluateAsync<string[]>(Selected));

        var range = await page.EvaluateAsync<JsonElement>("() => sednaUi.grid.range('g')");
        // Row numbers count the header row, as table.rows does.
        Assert.Equal(1, range.GetProperty("top").GetInt32());
        Assert.Equal(0, range.GetProperty("left").GetInt32());
        Assert.Equal(2, range.GetProperty("bottom").GetInt32());
        Assert.Equal(1, range.GetProperty("right").GetInt32());

        // Shift with an arrow back shrinks the rectangle, and the cells that left it say so.
        await page.Keyboard.PressAsync("Shift+ArrowLeft");
        Assert.Equal(["c00", "c10"], await page.EvaluateAsync<string[]>(Selected));
        Assert.Equal("false", await page.GetAttributeAsync("#c01", "aria-selected"));

        // A move without Shift clears it, and so the range reads as nothing.
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Empty(await page.EvaluateAsync<string[]>(Selected));
        Assert.True(await page.EvaluateAsync<bool>("() => sednaUi.grid.range('g') === null"));

        // Escape clears a range too.
        await page.Keyboard.PressAsync("Shift+ArrowDown");
        Assert.NotEmpty(await page.EvaluateAsync<string[]>(Selected));
        await page.Keyboard.PressAsync("Escape");
        Assert.Empty(await page.EvaluateAsync<string[]>(Selected));

        // Shift with a click selects from the active cell to the cell clicked.
        await page.Locator("#c00").ClickAsync();
        await page.Locator("#c11").ClickAsync(new() { Modifiers = [Microsoft.Playwright.KeyboardModifier.Shift] });
        Assert.Equal(["c00", "c01", "c10", "c11"], await page.EvaluateAsync<string[]>(Selected));
    }

    [Fact]
    public async Task A_grid_that_is_not_multiselectable_moves_on_Shift_and_selects_nothing()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid.Replace(" aria-multiselectable=\"true\"", "", StringComparison.Ordinal));
        await page.Locator("#c00").FocusAsync();

        await page.Keyboard.PressAsync("Shift+ArrowRight");
        Assert.Equal("c01", await Active(page));
        Assert.Equal(0, await page.Locator("#g [aria-selected]").CountAsync());
    }

    [Fact]
    public async Task Enter_edits_a_cell_and_commits_with_one_change_and_Escape_puts_the_value_back()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);
        await Js.Run(page, """
            () => {
                window.events = [];
                const e = document.getElementById('e02');
                e.addEventListener('change', () => window.events.push('change:' + e.value));
                e.addEventListener('input', () => window.events.push('input:' + e.value));
            }
            """);
        await page.Locator("#c02").FocusAsync();

        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("e02", await Active(page));
        Assert.Equal("", await page.GetAttributeAsync("#c02", "data-editing"));
        Assert.True(await page.Locator("#e02").IsVisibleAsync());

        // Enter selects the whole value, so typing replaces it.
        await page.Keyboard.TypeAsync("15");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("c02", await Active(page));
        Assert.Null(await page.GetAttributeAsync("#c02", "data-editing"));
        Assert.False(await page.Locator("#e02").IsVisibleAsync());
        Assert.Equal("15", await page.InputValueAsync("#e02"));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.events.filter(e => e.startsWith('change')).length"));
        Assert.Equal("change:15", await page.EvaluateAsync<string>("() => window.events.filter(e => e.startsWith('change'))[0]"));

        // F2 keeps the value and puts the caret at its end; Escape takes the edit back.
        await Js.Run(page, "() => { window.events = []; }");
        await page.Keyboard.PressAsync("F2");
        await page.Keyboard.TypeAsync("0");
        Assert.Equal("150", await page.InputValueAsync("#e02"));
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("c02", await Active(page));
        Assert.Equal("15", await page.InputValueAsync("#e02"));
        Assert.Null(await page.GetAttributeAsync("#c02", "data-editing"));
        var events = await page.EvaluateAsync<string[]>("() => window.events");
        Assert.DoesNotContain(events, e => e.StartsWith("change", StringComparison.Ordinal));
        Assert.Equal("input:15", events[^1]);

        // A double-click edits too, and a click elsewhere commits.
        await page.Locator("#c12").DblClickAsync();
        Assert.Equal("e12", await Active(page));
        await page.Keyboard.PressAsync(Keys.SelectAll);
        await page.Keyboard.TypeAsync("6");
        await page.Locator("#c00").ClickAsync();
        await page.WaitForFunctionAsync("() => !document.getElementById('c12').hasAttribute('data-editing')");
        Assert.Equal("6", await page.InputValueAsync("#e12"));
    }

    [Fact]
    public async Task Tab_while_editing_commits_and_edits_the_next_cell_that_can_be()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);
        await page.Locator("#c02").FocusAsync();

        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("20");
        // The next cell in reading order is the first of the next row, which has no editor:
        // it takes the focus, and the edit before it is committed.
        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("c10", await Active(page));
        Assert.Null(await page.GetAttributeAsync("#c02", "data-editing"));
        Assert.Equal("20", await page.InputValueAsync("#e02"));

        // Back with Shift+Tab, while editing, into the cell before — which edits.
        await page.Locator("#c12").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync(Keys.ShiftTab);
        Assert.Equal("c11", await Active(page));
        await page.Locator("#c02").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("e12", await Active(page));
    }

    [Fact]
    public async Task Enter_moves_into_a_cells_control_the_arrows_move_on_from_it_and_Escape_comes_back()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);
        await page.Locator("#c21").FocusAsync();

        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("x21", await Active(page));
        await page.Keyboard.PressAsync("Space");
        Assert.True(await page.IsCheckedAsync("#x21"));

        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("c21", await Active(page));

        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("c11", await Active(page));
    }

    [Fact]
    public async Task After_the_app_re_renders_its_rows_the_cell_in_the_same_place_has_the_tab_stop_and_the_focus()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);
        await page.Locator("#c00").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("c11", await Active(page));

        // A render that replaces every row with new nodes, as a list re-rendered without
        // @key does: the focused cell is gone and focus falls to the page.
        await Js.Run(page, """
            () => {
                const body = document.querySelector('#g tbody');
                // Without the attributes the script wrote: a render does not carry them over.
                body.innerHTML = body.innerHTML.replace(/ tabindex="[^"]*"/g, '').replace(/id="(\w+)"/g, 'id="n$1"');
            }
            """);
        await page.WaitForFunctionAsync("() => document.activeElement.id === 'nc11'");
        Assert.Equal("0", await page.GetAttributeAsync("#nc11", "tabindex"));
        // The new nodes' controls are taken out of the tab order as well.
        Assert.Equal("-1", await page.GetAttributeAsync("#nb11", "tabindex"));

        // Deleting the focused row hands the place to the row now in it.
        await Js.Run(page, "() => document.getElementById('nc11').parentElement.remove()");
        await page.WaitForFunctionAsync("() => document.activeElement.id === 'nc21'");

        // Deleting the last rows clamps to the last row left.
        await page.Keyboard.PressAsync("ArrowDown");
        await Js.Run(page, "() => document.getElementById('nc21').parentElement.remove()");
        await page.WaitForFunctionAsync("() => document.activeElement.id === 'nc01'");
    }

    [Fact]
    public async Task A_render_while_the_reader_is_elsewhere_moves_the_tab_stop_and_leaves_the_focus_alone()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid);
        await page.Locator("#c10").FocusAsync();
        await page.Locator("#after").FocusAsync();

        await Js.Run(page, "() => document.getElementById('c10').parentElement.remove()");
        await page.WaitForFunctionAsync("() => document.getElementById('c20').getAttribute('tabindex') === '0'");
        Assert.Equal("after", await Active(page));
    }

    [Fact]
    public async Task A_row_in_edit_mode_gets_the_focus_in_its_first_editor_and_Enter_and_Escape_press_its_buttons()
    {
        if (NoBrowser) return;
        // The app's part, done by a stand-in: Edit puts the row in edit mode and renders
        // Save and Cancel in place of the button; Save and Cancel take it out again.
        var (page, _) = await OpenStyled("""
            <table class="table" role="grid" id="g" aria-label="Order lines">
              <tbody>
                <tr id="row">
                  <td id="name">Desk lamp<input class="cell-editor" id="en" value="Desk lamp" aria-label="Line"></td>
                  <td id="qty" class="col-num">12<input class="cell-editor" id="eq" value="12" aria-label="Quantity"></td>
                  <td id="act"><button id="edit" type="button">Edit</button></td>
                </tr>
                <tr><td id="next">Monitor arm</td><td>4</td><td></td></tr>
              </tbody>
            </table>
            <script>
              window.saved = 0; window.cancelled = 0;
              const act = document.getElementById('act'), row = document.getElementById('row');
              const view = () => { row.removeAttribute('data-editing'); act.innerHTML = '<button id="edit" type="button">Edit</button>'; };
              act.addEventListener('click', e => {
                const b = e.target.closest('button'); if (!b) return;
                if (b.id === 'edit') { row.setAttribute('data-editing', ''); act.innerHTML = '<button id="save" type="button" data-row-save>Save</button><button id="cancel" type="button" data-row-cancel>Cancel</button>'; }
                else if (b.id === 'save') { window.saved++; view(); }
                else { window.cancelled++; view(); }
              });
            </script>
            """);

        await page.Locator("#act").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("edit", await Active(page));
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => document.activeElement.id === 'en'");
        Assert.True(await page.Locator("#eq").IsVisibleAsync());
        // In edit mode the row's controls are walked with Tab like a form's.
        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("eq", await Active(page));
        Assert.Null(await page.GetAttributeAsync("#save", "tabindex"));

        // Escape presses Cancel; the editor it was in is hidden, and its cell has the focus.
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForFunctionAsync("() => document.activeElement.id === 'qty'");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.cancelled"));

        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => document.activeElement.id === 'en'");
        // Enter in a single-line editor presses Save.
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => document.activeElement.id === 'name'");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.saved"));
        Assert.Equal("-1", await page.GetAttributeAsync("#edit", "tabindex"));
    }
}
