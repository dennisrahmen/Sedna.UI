using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>.panel-grid</c> the reader arranges: a panel dragged by the pointer and the keyboard,
/// resized from each edge and the corner, held to its minimum and its aspect ratio, inert outside
/// arrange mode, mirrored right to left — and every arrangement the preview shows and the events
/// carry is <c>sednaUi.panels.arrange</c>'s, held to the same cases as <c>SednaPanels.Arrange</c>.
/// </summary>
/// <remarks>
/// The fixture's "app" stores what <c>panels</c> says by writing each panel's own properties, as
/// a render would, unless <c>window.render</c> is false. A MutationObserver counts nodes added
/// or removed: the script moves none.
/// </remarks>
public class PanelGridTests : ScriptTestBase
{
    // The fixtures lay panels on 12 columns by setting `--panel-cols`, which keeps the cells' sizes
    // in whole numbers; the grid's own default is 24, held by the tests under "The 24 columns".
    private const double Pitch = 64;          // a 960px grid of 12 columns with 16px gaps: 65.33 + 16
    private const double ColumnPitch = (960 - 11 * 16) / 12.0 + 16;

    private static string Panel(string id, int x, int y, int w, int h, string extra = "") => $"""
        <section class="card card--resizable" id="{id}" data-drag-item="{id}" data-drag-label="{id}" {extra}
                 style="--panel-x: {x}; --panel-y: {y}; --panel-w: {w}; --panel-h: {h}">
          <div class="card-head">
            <span class="card-head-lead"><button class="drag-handle" id="grip-{id}" type="button" data-drag-handle aria-label="Move {id}">::</button></span>
            <h3>{id}</h3>
          </div>
          <div class="card-body">Northwind Retail</div>
          <div class="card-resize card-resize--inline" id="w-{id}" data-resize="--panel-w" role="separator" aria-orientation="vertical"
               aria-label="Width of {id}" aria-valuemin="2" aria-valuenow="{w}" tabindex="0"></div>
          <div class="card-resize" id="h-{id}" data-resize="--panel-h" role="separator" aria-orientation="horizontal"
               aria-label="Height of {id}" aria-valuemin="2" aria-valuenow="{h}" tabindex="0"></div>
          <div class="card-resize card-resize--corner" id="c-{id}" data-resize="--panel-w --panel-h" aria-hidden="true"></div>
        </section>
        """;

    private static string Grid(string panels, string arrange = "data-arrange", int width = 960, int columns = 12) => $$"""
        <p class="visually-hidden" aria-live="assertive" id="live" data-drag-live
           data-drag-pickup="Picked up {item} at column {column}, row {row}."
           data-drag-move="{item}: column {column}, row {row}."
           data-drag-drop="Dropped {item} at column {column}, row {row}."
           data-drag-cancel="{item} is back at column {column}, row {row}."></p>
        <div class="panel-grid" id="grid" style="width: {{width}}px; --panel-cols: {{columns}}; --panel-row: 48px; --grid-gap: 16px"
             data-drag-zone="dashboard" data-drag-axis="cells" {{arrange}} aria-label="Dashboard">
          {{panels}}
        </div>
        <script>
          window.events = [];
          window.render = true;
          window.nodesMoved = 0;
          new MutationObserver(rs => { for (const r of rs) nodesMoved += r.addedNodes.length + r.removedNodes.length; })
            .observe(document.getElementById('grid'), { childList: true, subtree: true });
          for (const name of ['sedna-dragstart', 'sedna-drop', 'sedna-resize'])
            document.addEventListener(name, e => {
              events.push({ name, detail: e.detail });
              if (!window.render || !e.detail.panels) return;
              for (const p of e.detail.panels) {
                const el = document.getElementById(p.id);
                for (const a of ['x', 'y', 'w', 'h']) el.style.setProperty('--panel-' + a, String(p[a]));
                el.querySelector('[data-resize="--panel-w"]')?.setAttribute('aria-valuenow', String(p.w));
                el.querySelector('[data-resize="--panel-h"]')?.setAttribute('aria-valuenow', String(p.h));
              }
            });
          // Each panel's cell as it is drawn: column, row, width and height, measured.
          window.cells = () => {
            const g = document.getElementById('grid'), gr = g.getBoundingClientRect(), cs = getComputedStyle(g);
            const cols = parseInt(cs.getPropertyValue('--panel-cols')), tracks = cs.gridTemplateColumns.split(' ').map(parseFloat);
            const col = tracks[0], gap = (gr.width - tracks.reduce((a, b) => a + b, 0)) / (cols - 1), px = col + gap;
            const py = parseFloat(cs.gridAutoRows) + parseFloat(cs.rowGap), rtl = cs.direction === 'rtl';
            // The cells start below a band, when one is drawn.
            const band = g.querySelector(':scope > .panel-grid-band');
            const top = band && band.getClientRects().length && cs.getPropertyValue('--panel-row-offset').trim() === '1'
              ? band.getBoundingClientRect().bottom + parseFloat(cs.rowGap) : gr.top;
            return [...g.querySelectorAll('[data-drag-item]')].filter(e => e.getAttribute('data-dragging') !== 'pointer').map(e => {
              const r = e.getBoundingClientRect();
              const x = Math.round((rtl ? gr.right - r.right : r.left - gr.left) / px);
              return e.id + ':' + x + ',' + Math.round((r.top - top) / py) + ',' + Math.round((r.width + gap) / px) + ',' + Math.round((r.height + gap) / py);
            }).sort().join(' ');
          };
        </script>
        """;

    private static readonly string Dashboard = Grid(
        Panel("orders", 0, 0, 6, 4) + Panel("dispatch", 6, 0, 6, 4) + Panel("returns", 0, 4, 12, 3));

    private sealed record Logged(string Name, JsonElement Detail);

    private static async Task<List<Logged>> Events(IPage page)
    {
        using var doc = JsonDocument.Parse(await page.EvaluateAsync<string>("() => JSON.stringify(events)"));
        return doc.RootElement.EnumerateArray()
            .Select(e => new Logged(e.GetProperty("name").GetString()!, e.GetProperty("detail").Clone()))
            .ToList();
    }

    private static string Panels(JsonElement detail) => string.Join(" ",
        detail.GetProperty("panels").EnumerateArray()
            .Select(p => $"{p.GetProperty("id").GetString()}:{p.GetProperty("x")},{p.GetProperty("y")},{p.GetProperty("w")},{p.GetProperty("h")}")
            .Order(StringComparer.Ordinal));

    private static Task<string> Cells(IPage page) => page.EvaluateAsync<string>("() => cells()");

    private static async Task<(float X, float Y)> Centre(IPage page, string selector)
    {
        var box = (await page.Locator(selector).BoundingBoxAsync())!;
        return (box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    private static async Task Drag(IPage page, string selector, double dx, double dy, bool release = true)
    {
        var (x, y) = await Centre(page, selector);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + (float)Math.Sign(dx) * 6, y + 6, new() { Steps = 2 });
        await page.Mouse.MoveAsync(x + (float)dx, y + (float)dy, new() { Steps = 10 });
        await Js.Frames(page);
        if (release) await page.Mouse.UpAsync();
    }

    private static async Task AssertPreviewCleared(IPage page)
    {
        Assert.Equal(0, await page.EvaluateAsync<int>("() => nodesMoved"));
        Assert.Equal(0, await page.EvaluateAsync<int>(
            "() => [...document.querySelectorAll('#grid, #grid > *')].filter(el => /--panel-(preview|drop)-/.test(el.getAttribute('style') || '')).length"));
    }

    // ── The arrangement ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_script_settles_every_case_as_SednaPanels_does()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("<p>x</p>");
        var failures = await page.EvaluateAsync<string[]>("""
            json => JSON.parse(json).flatMap(c => {
                const p = a => ({ id: a[0], x: a[1], y: a[2], w: a[3], h: a[4] });
                const got = JSON.stringify(sednaUi.panels.arrange(c.panels.map(p), c.columns, c.moved && p(c.moved)));
                const want = JSON.stringify(c.expect.map(p));
                return got === want ? [] : [c.name + ': ' + got];
            })
            """, PanelLayoutCases.Json);
        Assert.Empty(failures);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_cells_are_drawn_where_the_app_put_them()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dashboard);
        Assert.Equal("dispatch:6,0,6,4 orders:0,0,6,4 returns:0,4,12,3", await Cells(page));
        Assert.Equal(4 * 48 + 3 * 16, (await page.Locator("#orders").BoundingBoxAsync())!.Height, 1.0);
        Assert.Empty(errors);
    }

    // ── Dragging ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_pointer_drag_previews_the_arrangement_and_reports_it_whole()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dashboard);

        // Onto the panel beside it: that one goes down below it, the rest follow.
        await Drag(page, "#grip-orders", 6 * ColumnPitch, 0, release: false);
        Assert.Equal("pointer", await page.GetAttributeAsync("#orders", "data-dragging"));
        Assert.Equal("dispatch:6,4,6,4 returns:0,8,12,3", await Cells(page));
        // The cell it would land on is marked on the grid.
        Assert.NotNull(await page.GetAttributeAsync("#grid", "data-drop-over"));
        var mark = await page.EvaluateAsync<string>(
            "() => ['x','y','w','h'].map(a => document.getElementById('grid').style.getPropertyValue('--panel-drop-' + a)).join(',')");
        Assert.Equal("6,0,6,4", mark);
        Assert.Equal("dashed", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('grid'), '::before').borderTopStyle"));

        await page.Mouse.UpAsync();
        var drop = (await Events(page)).Single(e => e.Name == "sedna-drop");
        Assert.Equal(6, drop.Detail.GetProperty("x").GetInt32());
        Assert.Equal(0, drop.Detail.GetProperty("y").GetInt32());
        Assert.Equal("dashboard", drop.Detail.GetProperty("to").GetString());
        Assert.Equal("dispatch:6,4,6,4 orders:6,0,6,4 returns:0,8,12,3", Panels(drop.Detail));
        // The panel's place in the reading order of the result.
        Assert.Equal(0, drop.Detail.GetProperty("index").GetInt32());

        // The app rendered it; the preview is gone and its values are all that is left.
        await Js.Frames(page);
        Assert.Equal("dispatch:6,4,6,4 orders:6,0,6,4 returns:0,8,12,3", await Cells(page));
        await AssertPreviewCleared(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_drop_back_where_it_started_sends_nothing_and_leaves_nothing()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dashboard);
        await Drag(page, "#grip-orders", 20, 10);
        Assert.DoesNotContain(await Events(page), e => e.Name == "sedna-drop");
        Assert.Equal("dispatch:6,0,6,4 orders:0,0,6,4 returns:0,4,12,3", await Cells(page));
        await AssertPreviewCleared(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_drop_the_app_ignores_goes_back_to_what_the_app_has()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dashboard);
        await Js.Run(page, "() => { window.render = false; }");
        await Drag(page, "#grip-returns", 0, -4 * Pitch);

        // Held, so nothing jumps back while the app is still storing it…
        Assert.Equal("dispatch:6,3,6,4 orders:0,3,6,4 returns:0,0,12,3", await Cells(page));
        // …and once it has had its chance and rendered nothing, the app's own values stand.
        await page.WaitForFunctionAsync("() => !document.getElementById('orders').style.getPropertyValue('--panel-preview-y')");
        Assert.Equal("dispatch:6,0,6,4 orders:0,0,6,4 returns:0,4,12,3", await Cells(page));
        await AssertPreviewCleared(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_keyboard_moves_a_panel_by_a_column_and_by_the_next_row_that_changes_anything()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dashboard);
        await page.FocusAsync("#grip-returns");
        await page.Keyboard.PressAsync("Space");
        Assert.Equal("keyboard", await page.GetAttributeAsync("#returns", "data-dragging"));
        Assert.Equal("Picked up returns at column 1, row 5.", await page.TextContentAsync("#live"));

        // Up: the first row where it lands anywhere else is above the two panels.
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("dispatch:6,3,6,4 orders:0,3,6,4 returns:0,0,12,3", await Cells(page));
        Assert.Equal("returns: column 1, row 1.", await page.TextContentAsync("#live"));
        // A full-width panel has no column to move to.
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("returns: column 1, row 1.", await page.TextContentAsync("#live"));

        await page.Keyboard.PressAsync("Space");
        var drop = (await Events(page)).Single(e => e.Name == "sedna-drop");
        Assert.True(drop.Detail.GetProperty("keyboard").GetBoolean());
        Assert.Equal("dispatch:6,3,6,4 orders:0,3,6,4 returns:0,0,12,3", Panels(drop.Detail));
        Assert.Equal("Dropped returns at column 1, row 1.", await page.TextContentAsync("#live"));

        // Across by a column, then Escape puts it back.
        await page.FocusAsync("#grip-orders");
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("orders: column 2, row 4.", await page.TextContentAsync("#live"));
        await page.Keyboard.PressAsync("Escape");
        Assert.Single(await Events(page), e => e.Name == "sedna-drop");
        Assert.Equal("dispatch:6,3,6,4 orders:0,3,6,4 returns:0,0,12,3", await Cells(page));
        await AssertPreviewCleared(page);
        Assert.Empty(errors);
    }

    // ── Resizing ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_right_edge_the_bottom_edge_and_the_corner_resize_in_whole_cells()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dashboard);

        // Wider by two columns: the panel beside it goes down.
        await Drag(page, "#w-orders", 2 * ColumnPitch + 10, 0);
        var wider = (await Events(page)).Last();
        Assert.Equal("sedna-resize", wider.Name);
        Assert.Equal("--panel-w", wider.Detail.GetProperty("property").GetString());
        Assert.Equal(8, wider.Detail.GetProperty("value").GetInt32());
        Assert.Equal("orders", wider.Detail.GetProperty("key").GetString());
        Assert.Equal("dispatch:6,4,6,4 orders:0,0,8,4 returns:0,8,12,3", Panels(wider.Detail));
        Assert.Equal("8", await page.GetAttributeAsync("#w-orders", "aria-valuenow"));

        // Shorter by a row, from the bottom edge: what was below floats up.
        await Drag(page, "#h-orders", 0, -Pitch - 8);
        var shorter = (await Events(page)).Last();
        Assert.Equal("--panel-h", shorter.Detail.GetProperty("property").GetString());
        Assert.Equal(3, shorter.Detail.GetProperty("value").GetInt32());
        Assert.Equal("dispatch:6,3,6,4 orders:0,0,8,3 returns:0,7,12,3", Panels(shorter.Detail));

        // The corner: narrower and taller at once, one event for each.
        var before = (await Events(page)).Count;
        await Drag(page, "#c-orders", -2 * ColumnPitch, Pitch);
        var both = (await Events(page)).Skip(before).ToList();
        Assert.Equal(["--panel-w", "--panel-h"], both.Select(e => e.Detail.GetProperty("property").GetString()));
        Assert.Equal("dispatch:6,0,6,4 orders:0,0,6,4 returns:0,4,12,3", Panels(both[1].Detail));
        Assert.Equal("dispatch:6,0,6,4 orders:0,0,6,4 returns:0,4,12,3", await Cells(page));
        await AssertPreviewCleared(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_keyboard_resizes_from_an_edge_a_cell_at_a_time()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dashboard);
        await page.FocusAsync("#w-dispatch");
        await page.Keyboard.PressAsync("ArrowLeft");
        // dispatch is at the grid's end: the edge moving left makes it narrower.
        Assert.Equal("dispatch:6,0,5,4 orders:0,0,6,4 returns:0,4,12,3", await Cells(page));
        await page.FocusAsync("#h-dispatch");
        await page.Keyboard.PressAsync("Shift+ArrowDown");
        Assert.Equal("dispatch:6,0,5,8 orders:0,0,6,4 returns:0,8,12,3", await Cells(page));
        Assert.Equal(2, (await Events(page)).Count(e => e.Name == "sedna-resize"));
        await AssertPreviewCleared(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_panel_stops_at_its_minimum_and_at_the_grid_end()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dashboard);
        await Drag(page, "#w-orders", -8 * ColumnPitch, 0);
        Assert.Equal(2, (await Events(page)).Last().Detail.GetProperty("value").GetInt32());   // aria-valuemin

        await Drag(page, "#c-dispatch", 3 * ColumnPitch, -6 * Pitch);
        // Already at the grid's end, and two rows tall at least.
        Assert.Contains("dispatch:6,0,6,2", await Cells(page), StringComparison.Ordinal);
        await Drag(page, "#h-dispatch", 0, -4 * Pitch);
        Assert.Contains("dispatch:6,0,6,2", await Cells(page), StringComparison.Ordinal);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_panel_with_an_aspect_ratio_keeps_it_to_the_nearest_row()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Grid(Panel("camera", 0, 0, 4, 2, "data-panel-ratio=\"16:9\"")));

        foreach (var (selector, dx, dy) in new[] { ("#w-camera", 3 * ColumnPitch, 0.0), ("#c-camera", 2 * ColumnPitch, 0.0), ("#h-camera", 0.0, -3 * Pitch) })
        {
            await Drag(page, selector, dx, dy);
            var box = (await page.Locator("#camera").BoundingBoxAsync())!;
            Assert.True(Math.Abs(box.Height - box.Width * 9 / 16) <= Pitch / 2,
                $"{selector}: {box.Width}x{box.Height} is not 16:9 to the nearest row");
        }
        Assert.Equal(3, (await Events(page)).Select(e => e.Detail.GetProperty("panels")[0].GetProperty("w").GetInt32()).Distinct().Count());
        Assert.Empty(errors);
    }

    // ── Arrange mode ────────────────────────────────────────────────────────

    [Fact]
    public async Task Outside_arrange_mode_nothing_drags_or_resizes()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Grid(
            Panel("orders", 0, 0, 6, 4) + Panel("dispatch", 6, 0, 6, 4), arrange: ""));

        Assert.Equal(0, await page.EvaluateAsync<int>(
            "() => [...document.querySelectorAll('#grid [data-drag-handle], #grid [data-resize]')].filter(el => getComputedStyle(el).display !== 'none').length"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('grid')).backgroundImage"));

        // Even pressed from a script, a handle there does nothing: the grid is resting.
        await Js.Run(page, """
            () => {
                for (const id of ['grip-orders', 'w-orders']) {
                    const el = document.getElementById(id);
                    el.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, button: 0, isPrimary: true, clientX: 10, clientY: 10 }));
                    el.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, isPrimary: true, clientX: 300, clientY: 300 }));
                    el.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, isPrimary: true, clientX: 300, clientY: 300 }));
                    el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: ' ' }));
                    el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'ArrowRight' }));
                }
            }
            """);
        Assert.Null(await page.EvaluateAsync<string?>("() => sednaUi.drag.active()"));
        Assert.Empty(await Events(page));
        Assert.Equal("dispatch:6,0,6,4 orders:0,0,6,4", await Cells(page));

        // Into arrange mode, the same markup arranges.
        await Js.Run(page, "() => document.getElementById('grid').setAttribute('data-arrange', '')");
        await page.FocusAsync("#w-orders");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Single(await Events(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_narrow_grid_stacks_its_panels_in_reading_order_and_does_not_arrange()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Grid(
            Panel("dispatch", 6, 0, 6, 4) + Panel("orders", 0, 0, 6, 4) + Panel("returns", 0, 4, 12, 3), width: 400));
        var tops = await page.EvaluateAsync<string>(
            "() => ['orders', 'dispatch', 'returns'].map(id => { const r = document.getElementById(id).getBoundingClientRect(); return Math.round(r.width) + '@' + Math.round(r.top); }).join(' ')");
        var parts = tops.Split(' ').Select(t => t.Split('@')).ToArray();
        Assert.All(parts, p => Assert.Equal("400", p[0]));
        Assert.True(int.Parse(parts[0][1]) < int.Parse(parts[1][1]) && int.Parse(parts[1][1]) < int.Parse(parts[2][1]),
            $"Stacked by row, then column: {tops}");
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('grip-orders')).display"));
        await Js.Run(page, "() => document.getElementById('grip-orders').dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: ' ' }))");
        Assert.Null(await page.EvaluateAsync<string?>("() => sednaUi.drag.active()"));
        Assert.Empty(errors);
    }

    // ── Right to left ───────────────────────────────────────────────────────

    [Fact]
    public async Task Right_to_left_the_first_column_is_on_the_right_and_every_input_mirrors()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"<div dir=\"rtl\">{Dashboard}</div>");
        var orders = (await page.Locator("#orders").BoundingBoxAsync())!;
        var dispatch = (await page.Locator("#dispatch").BoundingBoxAsync())!;
        Assert.True(orders.X > dispatch.X, "Column 0 is at the inline start, the right.");

        // Towards the inline end on screen — to the left — by six columns.
        await Drag(page, "#grip-orders", -6 * ColumnPitch, 0);
        Assert.Equal("dispatch:6,4,6,4 orders:6,0,6,4 returns:0,8,12,3",
            Panels((await Events(page)).Single(e => e.Name == "sedna-drop").Detail));

        // The width handle is on the left edge, and dragging it left makes the panel wider.
        await page.FocusAsync("#w-orders");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal(5, (await Events(page)).Last().Detail.GetProperty("value").GetInt32());

        // The keyboard's left arrow is a column further on.
        await page.FocusAsync("#grip-orders");
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("orders: column 8, row 1.", await page.TextContentAsync("#live"));
        await page.Keyboard.PressAsync("Escape");
        Assert.Empty(errors);
    }

    // ── A card's edges and corner ───────────────────────────────────────────

    [Fact]
    public async Task A_card_corner_resizes_both_ways_within_the_bounds_of_its_edges()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="width: 800px">
              <div class="card card--resizable" id="card" style="--card-width: 400px; --card-height: 240px">
                <div class="card-head"><h3>Open orders</h3></div>
                <div class="card-body"><p style="height: 900px">Rows</p></div>
                <div class="card-resize card-resize--inline" id="w" data-resize="--card-width" role="separator" aria-orientation="vertical"
                     aria-label="Width" aria-valuemin="240" aria-valuemax="600" aria-valuenow="400" tabindex="0"></div>
                <div class="card-resize" id="h" data-resize="--card-height" role="separator" aria-orientation="horizontal"
                     aria-label="Height" aria-valuemin="160" aria-valuemax="480" aria-valuenow="240" tabindex="0"></div>
                <div class="card-resize card-resize--corner" id="c" data-resize="--card-width --card-height" aria-hidden="true"></div>
              </div>
            </div>
            <script>window.events = []; document.addEventListener('sedna-resize', e => events.push({ name: e.type, detail: e.detail }));</script>
            """);

        var corner = (await page.Locator("#c").BoundingBoxAsync())!;
        var card = (await page.Locator("#card").BoundingBoxAsync())!;
        Assert.Equal(card.X + card.Width, corner.X + corner.Width, 1.0);
        Assert.Equal(card.Y + card.Height, corner.Y + corner.Height, 1.0);

        await Drag(page, "#c", 500, 80);
        card = (await page.Locator("#card").BoundingBoxAsync())!;
        Assert.Equal(600, card.Width, 0.5);      // the side handle's aria-valuemax
        Assert.Equal(320, card.Height, 4.5);     // 240 + 80, to the step
        Assert.Equal("600", await page.GetAttributeAsync("#w", "aria-valuenow"));
        Assert.Null(await page.GetAttributeAsync("#c", "aria-valuenow"));
        var events = await Events(page);
        Assert.Equal(["--card-width", "--card-height"], events.Select(e => e.Detail.GetProperty("property").GetString()));

        // The side handle on its own, from the keyboard.
        await page.FocusAsync("#w");
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal(592, (await page.Locator("#card").BoundingBoxAsync())!.Width, 0.5);
        Assert.Empty(errors);
    }

    // ── The 24 columns ──────────────────────────────────────────────────────
    // The grid's own default is the library's grid: the same 24 as `.sedna-grid-24`, so a span
    // means the same in both.

    private const double Pitch24 = (960 - 23 * 16) / 24.0 + 16;

    [Fact]
    public async Task The_grid_has_24_columns_and_a_panel_spans_what_a_sedna_span_spans()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="width: 960px; --grid-gap: 16px">
              <div class="panel-grid" id="grid" style="--panel-row: 48px">
                <section class="card" id="panel" style="--panel-x: 4; --panel-y: 0; --panel-w: 8; --panel-h: 2"><div class="card-body">Panel</div></section>
              </div>
              <div class="sedna-grid-24" id="reference" style="margin-top: 16px">
                <div class="card sedna-span-8" id="span"><div class="card-body">Span</div></div>
              </div>
            </div>
            """);
        Assert.Equal(24, await page.EvaluateAsync<int>("() => getComputedStyle(document.getElementById('grid')).gridTemplateColumns.split(' ').length"));
        Assert.Equal("24", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('grid')).getPropertyValue('--panel-cols').trim()"));
        var panel = (await page.Locator("#panel").BoundingBoxAsync())!;
        var span = (await page.Locator("#span").BoundingBoxAsync())!;
        Assert.Equal(span.Width, panel.Width, 0.5);
        // Four columns in.
        var grid = (await page.Locator("#grid").BoundingBoxAsync())!;
        Assert.Equal(4 * Pitch24, panel.X - grid.X, 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_drag_and_a_resize_count_in_24ths()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Grid(Panel("orders", 0, 0, 6, 4), columns: 24));
        Assert.Equal("orders:0,0,6,4", await Cells(page));
        await Drag(page, "#grip-orders", 5 * Pitch24, 0);
        var drop = (await Events(page)).Single(e => e.Name == "sedna-drop");
        Assert.Equal(5, drop.Detail.GetProperty("x").GetInt32());
        Assert.Equal("orders:5,0,6,4", Panels(drop.Detail));

        await Drag(page, "#w-orders", 3 * Pitch24 + 6, 0);
        var wider = (await Events(page)).Last();
        Assert.Equal("--panel-w", wider.Detail.GetProperty("property").GetString());
        Assert.Equal(9, wider.Detail.GetProperty("value").GetInt32());
        Assert.Equal("orders:5,0,9,4", Panels(wider.Detail));
        await AssertPreviewCleared(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_grid_narrower_than_its_gaps_does_not_overflow()
    {
        if (NoBrowser) return;
        // 23 gaps of 16px are 368px: a phone's column is narrower, and the gap gives way.
        var (page, errors) = await OpenStyled("""
            <div style="width: 300px">
              <div class="panel-grid" id="grid" style="--panel-row: 48px">
                <section class="card" id="panel" style="--panel-x: 0; --panel-y: 0; --panel-w: 24; --panel-h: 2"><div class="card-body">Panel</div></section>
              </div>
            </div>
            """);
        var panel = (await page.Locator("#panel").BoundingBoxAsync())!;
        Assert.Equal(300, panel.Width, 0.5);
        Assert.True(await page.EvaluateAsync<bool>("() => { const g = document.getElementById('grid'); return g.scrollWidth <= g.clientWidth; }"),
            "The grid's tracks and gaps came to more than its width.");
        Assert.Empty(errors);
    }

    // The guide: while arranging, a dot at the centre of every cell, first column to last.
    [Fact]
    public async Task Arranging_marks_the_centre_of_every_cell_with_a_dot_and_viewing_marks_none()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="width: 960px; --grid-gap: 16px; padding: 8px">
              <div class="panel-grid" id="grid" data-arrange style="--panel-row: 48px; block-size: 160px"></div>
            </div>
            """);
        var box = (await page.Locator("#grid").BoundingBoxAsync())!;

        async Task<Ink.Png> Shot() => Ink.Png.Decode(await page.Locator("#grid").ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }));
        var gap = 16.0;
        var pitchX = (box.Width + gap) / 24;
        var pitchY = 48 + gap;
        var cells = new[] { (0, 0), (11, 0), (23, 0), (0, 1), (23, 1), (7, 2) };

        var drawn = await Shot();
        foreach (var (col, row) in cells)
        {
            var cx = (int)Math.Round(col * pitchX + (pitchX - gap) / 2);
            var cy = (int)Math.Round(row * pitchY + 24);
            var corner = drawn.Pixel((int)(col * pitchX) + 2, (int)(row * pitchY) + 2);
            Assert.NotEqual(corner, drawn.Pixel(cx, cy));
        }

        await Js.Run(page, "() => document.getElementById('grid').removeAttribute('data-arrange')");
        await Js.Frames(page);
        var plain = await Shot();
        foreach (var (col, row) in cells)
        {
            var cx = (int)Math.Round(col * pitchX + (pitchX - gap) / 2);
            var cy = (int)Math.Round(row * pitchY + 24);
            Assert.Equal(plain.Pixel(2, 2), plain.Pixel(cx, cy));
        }
        Assert.Empty(errors);
    }

    // Where the column gap is capped at a share of the width, the cells are as wide as nothing and
    // the dots stay where the cells are: at each column's start, a column's pitch apart.
    [Fact]
    public async Task Where_the_column_gap_is_capped_the_dots_stay_with_the_columns()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="width: 600px; padding: 8px">
              <div class="panel-grid" id="grid" data-arrange style="--grid-gap: 40px; --panel-row: 48px; block-size: 140px"></div>
            </div>
            """);
        var box = (await page.Locator("#grid").BoundingBoxAsync())!;
        var gapX = Math.Min(40.0, box.Width / 23);               // the cap binds: 40px > 600/23
        Assert.Equal(gapX, await page.EvaluateAsync<double>(
            "() => { const g = document.getElementById('grid'); const t = getComputedStyle(g).gridTemplateColumns.split(' ').map(parseFloat); return (g.getBoundingClientRect().width - t.reduce((a, b) => a + b, 0)) / 23; }"), 0.1);

        var png = Ink.Png.Decode(await page.Locator("#grid").ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }));
        var pitchX = (box.Width + gapX) / 24;
        var pitchY = 48.0 + 40;
        var background = png.Pixel(3, 3);
        // A dot is a few pixels across, so look a pixel or two around where its centre should be.
        bool Dot(double cx, double cy) =>
            Enumerable.Range(-2, 5).Any(dx => Enumerable.Range(-2, 5).Any(dy => png.Pixel((int)Math.Round(cx) + dx, (int)Math.Round(cy) + dy) != background));
        foreach (var col in new[] { 3, 10, 17 })
        {
            Assert.True(Dot(col * pitchX, 24), $"No dot at column {col}, row 0.");
            Assert.True(Dot(col * pitchX, pitchY + 24), $"No dot at column {col}, row 1.");
            // Half way between two columns there is none.
            Assert.False(Dot(col * pitchX + pitchX / 2, 24), $"A dot between columns {col} and {col + 1}.");
        }
        Assert.Empty(errors);
    }

    // ── Panels in wrappers, a band above, panels that are not cards ──────────

    /// <summary>A window of the app's own: no head, no body, the three handles on its edges.</summary>
    private static string Window(string id, int x, int y, int w, int h) => $"""
        <div class="sedna-test-window" id="{id}" data-drag-item="{id}" data-drag-label="{id}"
             style="--panel-x: {x}; --panel-y: {y}; --panel-w: {w}; --panel-h: {h}; outline: 1px solid">
          <button id="grip-{id}" type="button" data-drag-handle aria-label="Move {id}">::</button>
          <div class="card-resize card-resize--inline" id="w-{id}" data-resize="--panel-w" role="separator" aria-orientation="vertical"
               aria-label="Width of {id}" aria-valuemin="2" aria-valuenow="{w}" tabindex="0"></div>
          <div class="card-resize" id="h-{id}" data-resize="--panel-h" role="separator" aria-orientation="horizontal"
               aria-label="Height of {id}" aria-valuemin="2" aria-valuenow="{h}" tabindex="0"></div>
          <div class="card-resize card-resize--corner" id="c-{id}" data-resize="--panel-w --panel-h" aria-hidden="true"></div>
        </div>
        """;

    [Fact]
    public async Task Panels_inside_a_contents_wrapper_are_the_grids_and_drag_as_any_panel()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Grid(
            "<div class=\"panel-grid-contents\" id=\"top\">" + Panel("orders", 0, 0, 6, 4) + Panel("dispatch", 6, 0, 6, 4) + "</div>"
            + Panel("returns", 0, 4, 12, 3)));
        Assert.Equal("contents", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('top')).display"));
        Assert.Equal("dispatch:6,0,6,4 orders:0,0,6,4 returns:0,4,12,3", await Cells(page));

        await Drag(page, "#grip-orders", 6 * ColumnPitch, 0);
        var drop = (await Events(page)).Single(e => e.Name == "sedna-drop");
        Assert.Equal("dispatch:6,4,6,4 orders:6,0,6,4 returns:0,8,12,3", Panels(drop.Detail));
        await Js.Frames(page);
        Assert.Equal("dispatch:6,4,6,4 orders:6,0,6,4 returns:0,8,12,3", await Cells(page));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => nodesMoved"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_band_above_the_panels_moves_the_cells_down_and_a_drag_still_lands_under_the_pointer()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Grid(
            "<div class=\"panel-grid-band\" id=\"band\" style=\"height: 37px\">Test mode</div>" + Panel("orders", 0, 0, 6, 4) + Panel("dispatch", 6, 0, 6, 4) + Panel("returns", 0, 4, 12, 3)));
        var layout = await page.EvaluateAsync<double[]>(@"() => {
            const g = document.getElementById('grid').getBoundingClientRect(), b = document.getElementById('band').getBoundingClientRect();
            const o = document.getElementById('orders').getBoundingClientRect();
            return [b.top - g.top, b.width - g.width, o.top - b.bottom];
        }");
        Assert.Equal(0, layout[0], 1);
        Assert.Equal(0, layout[1], 1);
        Assert.Equal(16, layout[2], 1);
        Assert.Equal("dispatch:6,0,6,4 orders:0,0,6,4 returns:0,4,12,3", await Cells(page));

        // The cell a panel lands on is counted from below the band: the band is most of a row
        // tall, so counted from the grid's top the panel would land a row down.
        await Drag(page, "#grip-orders", 6 * ColumnPitch, 0);
        var drop = (await Events(page)).Single(e => e.Name == "sedna-drop");
        Assert.Equal(0, drop.Detail.GetProperty("y").GetInt32());
        Assert.Equal("dispatch:6,4,6,4 orders:6,0,6,4 returns:0,8,12,3", Panels(drop.Detail));

        // The band gone, the cells start at the top again.
        await Js.Run(page, "() => document.getElementById('band').textContent = ''");
        await Js.Frames(page);
        Assert.Equal(0, await page.EvaluateAsync<double>("() => document.getElementById('orders').getBoundingClientRect().top - document.getElementById('grid').getBoundingClientRect().top"), 1);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_panel_that_is_not_a_card_takes_all_three_handles_on_its_edges()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Grid(Window("video", 0, 0, 6, 4) + Panel("dispatch", 6, 0, 6, 4)));
        var edges = await page.EvaluateAsync<double[]>(@"() => {
            const p = document.getElementById('video').getBoundingClientRect();
            const b = document.getElementById('h-video').getBoundingClientRect(), i = document.getElementById('w-video').getBoundingClientRect(), c = document.getElementById('c-video').getBoundingClientRect();
            return [b.bottom - p.bottom, b.left - p.left, b.width - p.width, i.right - p.right, i.top - p.top, i.height - p.height, c.right - p.right, c.bottom - p.bottom];
        }");
        Assert.All(edges, d => Assert.Equal(0, d, 1.5));

        await Drag(page, "#h-video", 0, Pitch + 8);
        var taller = (await Events(page)).Last();
        Assert.Equal("--panel-h", taller.Detail.GetProperty("property").GetString());
        Assert.Equal(5, taller.Detail.GetProperty("value").GetInt32());
        Assert.Empty(errors);
    }
}
