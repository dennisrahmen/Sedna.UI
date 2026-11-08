using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>[data-resize]</c>: a card's height and a split's aside move with a drag, snap to the
/// step, stop at their bounds, answer the keyboard, reset, and say where they ended.
/// </summary>
public class ResizeTests : ScriptTestBase
{
    private const string Card = """
        <div style="width:400px">
          <div class="card card--resizable" id="card">
            <div class="card-head"><h3>Open orders</h3></div>
            <div class="card-body"><p style="height:600px">Rows</p></div>
            <div class="card-resize" id="h" data-resize="--card-height" role="separator" aria-orientation="horizontal"
                 aria-label="Resize" aria-valuemin="120" aria-valuemax="400" tabindex="0"></div>
          </div>
        </div>
        <script>window.ended = []; document.addEventListener('sedna-resize', e => window.ended.push(e.detail.value));</script>
        """;

    private const string Split = """
        <div style="width:900px">
          <div class="sedna-split sedna-split--stack-sm" id="s">
            <div id="main">Main</div>
            <div class="split-resize" id="h" data-resize="--split-aside" data-resize-measure=".sedna-split-aside" role="separator"
                 aria-orientation="vertical" aria-label="Resize" aria-valuemin="160" aria-valuemax="480" tabindex="0"></div>
            <aside class="sedna-split-aside" id="aside">Aside</aside>
          </div>
        </div>
        """;

    private static Task<double> Height(IPage page, string id) =>
        page.EvaluateAsync<double>("id => document.getElementById(id).getBoundingClientRect().height", id);

    private static Task<double> Width(IPage page, string id) =>
        page.EvaluateAsync<double>("id => document.getElementById(id).getBoundingClientRect().width", id);

    [Fact]
    public async Task A_card_grows_by_a_drag_snapped_to_the_step_and_stops_at_its_maximum()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Card);
        await Js.Run(page, "() => document.getElementById('card').style.setProperty('--card-height', '200px')");
        var box = (await page.Locator("#h").BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + 53, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        Assert.Equal(256, await Height(page, "card"), 0.5);           // 200 + 53, snapped to 8

        await page.Mouse.MoveAsync(x, y + 53);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + 500, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        Assert.Equal(400, await Height(page, "card"), 0.5);           // aria-valuemax

        Assert.Equal("400", await page.GetAttributeAsync("#h", "aria-valuenow"));
        Assert.Equal(new[] { 256.0, 400.0 }, await page.EvaluateAsync<double[]>("() => window.ended"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_keyboard_moves_it_a_step_and_Home_puts_it_back()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Card);
        await Js.Run(page, "() => document.getElementById('card').style.setProperty('--card-height', '200px')");
        await page.FocusAsync("#h");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal(208, await Height(page, "card"), 0.5);
        await page.Keyboard.PressAsync("Shift+ArrowUp");
        Assert.Equal(176, await Height(page, "card"), 0.5);

        await page.Keyboard.PressAsync("Home");
        Assert.Equal("", await page.EvaluateAsync<string>("() => document.getElementById('card').style.getPropertyValue('--card-height')"));
        Assert.True(await Height(page, "card") > 600, "Without a height the card is as tall as its content.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_split_aside_after_the_handle_grows_as_the_handle_moves_towards_the_main_column()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Split);
        var before = await Width(page, "aside");
        var box = (await page.Locator("#h").BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - 64, y, new() { Steps = 4 });
        await page.Mouse.UpAsync();

        Assert.Equal(Math.Round((before + 64) / 8) * 8, await Width(page, "aside"), 1.0);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_handle_writes_on_the_element_it_names_and_snaps_to_its_own_step()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="width:400px">
              <div class="card card--resizable" id="card" style="--card-height: 200px">
                <div class="card-body"><p style="height:600px">Rows</p></div>
              </div>
              <div class="card-resize" id="h" data-resize="--card-height" data-resize-on="card" data-resize-measure=".card-body"
                   data-resize-step="20" role="separator" aria-orientation="horizontal" aria-label="Resize" aria-valuemin="100" aria-valuenow="200" tabindex="0"></div>
            </div>
            """);

        await page.FocusAsync("#h");
        await page.Keyboard.PressAsync("ArrowDown");
        // Written on #card, not on the handle's parent, and measured from the body inside it.
        Assert.False(string.IsNullOrEmpty(await page.EvaluateAsync<string>("() => document.getElementById('card').style.getPropertyValue('--card-height')")));
        Assert.Equal("", await page.EvaluateAsync<string>("() => document.getElementById('h').parentElement.style.getPropertyValue('--card-height')"));
        var value = double.Parse((await page.GetAttributeAsync("#h", "aria-valuenow"))!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0, value % 20);         // snapped to the handle's own step
        Assert.Empty(errors);
    }

    private const string Ceiling = """
        <div style="width:400px">
          <div class="card card--resizable" id="short">
            <div class="card-head"><h3>Due today</h3></div>
            <div class="card-body"><p style="height:60px; margin:0">Two orders</p></div>
            <div class="card-resize" id="hs" data-resize="--card-max-height" data-resize-fold="80" role="separator" aria-orientation="horizontal"
                 aria-label="Resize Due today" aria-valuemin="120" aria-valuemax="600" tabindex="0"></div>
          </div>
          <div class="card card--resizable" id="long">
            <div class="card-head"><h3>Open orders</h3></div>
            <div class="card-body"><p style="height:900px; margin:0">Rows</p></div>
            <div class="card-resize" id="hl" data-resize="--card-max-height" data-resize-fold="80" role="separator" aria-orientation="horizontal"
                 aria-label="Resize Open orders" aria-valuemin="120" aria-valuemax="600" aria-valuenow="320" tabindex="0"></div>
          </div>
        </div>
        <script>window.ended = []; document.addEventListener('sedna-resize', e => window.ended.push([e.target.id, e.detail.value, e.detail.folded]));</script>
        """;

    private static async Task Drag(IPage page, string handle, float by)
    {
        var box = (await page.Locator(handle).BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + by, new() { Steps = 6 });
        await page.Mouse.UpAsync();
    }

    [Fact]
    public async Task A_ceiling_lets_a_short_card_follow_its_content_and_caps_a_long_one()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Ceiling);
        var natural = await Height(page, "short");
        await Js.Run(page, "() => document.getElementById('long').style.setProperty('--card-max-height', '320px')");
        Assert.Equal(320, await Height(page, "long"), 0.5);

        await Drag(page, "#hs", 200);                                  // a ceiling far above its content
        Assert.Equal(natural, await Height(page, "short"), 0.5);       // follows the content, no empty room

        await Drag(page, "#hl", 80);                                   // the long one starts from its real height
        Assert.Equal(400, await Height(page, "long"), 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_drag_below_the_fold_threshold_folds_and_keeps_the_readers_height()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Ceiling);
        await Js.Run(page, "() => document.getElementById('long').style.setProperty('--card-max-height', '320px')");

        var box = (await page.Locator("#hl").BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y - 280, new() { Steps = 6 });   // 320 - 280 = 40, under the 80 threshold
        Assert.Equal("", await page.GetAttributeAsync("#hl", "data-resize-folding"));
        await page.Mouse.UpAsync();

        Assert.Null(await page.GetAttributeAsync("#hl", "data-resize-folding"));
        Assert.Equal("320px", await page.EvaluateAsync<string>("() => document.getElementById('long').style.getPropertyValue('--card-max-height')"));
        Assert.Equal("320", await page.GetAttributeAsync("#hl", "aria-valuenow"));
        Assert.Equal("[[\"long\",320,true]]", await page.EvaluateAsync<string>("() => JSON.stringify(window.ended)"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_arrow_towards_smaller_at_the_minimum_folds()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Ceiling);
        await Js.Run(page, "() => document.getElementById('long').style.setProperty('--card-max-height', '128px')");
        await page.FocusAsync("#hl");

        await page.Keyboard.PressAsync("ArrowUp");                    // 128 -> 120, the minimum: a resize
        await page.Keyboard.PressAsync("ArrowUp");                    // at the minimum and asked for less: a fold
        Assert.Equal("[[\"long\",120,false],[\"long\",120,true]]", await page.EvaluateAsync<string>("() => JSON.stringify(window.ended)"));
        Assert.Equal("120px", await page.EvaluateAsync<string>("() => document.getElementById('long').style.getPropertyValue('--card-max-height')"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_handle_with_no_fold_threshold_never_folds()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Card);
        await Js.Run(page, "() => document.getElementById('card').style.setProperty('--card-height', '120px')");
        await page.FocusAsync("#h");
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("[120]", await page.EvaluateAsync<string>("() => JSON.stringify(window.ended)"));
        Assert.Empty(errors);
    }

    // ── Table columns ──────────────────────────────────────────────────────────

    private static string Columns(string dir = "ltr", string role = "") => $"""
        <div dir="{dir}" style="width: 900px">
          <table class="table table--fixed" id="t" {role} aria-label="Orders">
            <thead><tr>
              <th id="h-order" style="--col-width: 120px"><span>Order</span></th>
              <th id="h-customer" style="--col-width: 200px; --col-min: 96px; --col-max: 18rem">
                <button class="th-sort" id="sort" type="button">Customer</button>
                <div class="col-resize" id="g" data-resize="--col-width" data-resize-key="customer" role="separator"
                     aria-orientation="vertical" aria-label="Resize Customer" aria-valuenow="200" tabindex="0"></div>
              </th>
              <th id="h-total" style="--col-width: 120px"><span>Total</span></th>
            </tr></thead>
            <tbody>
              <tr><td id="c-order">ORD-4209</td><td id="c-customer">Northwind Retail</td><td>1,248.00</td></tr>
              <tr><td>ORD-4204</td><td id="c-customer-2">Alex Fischer</td><td>86.40</td></tr>
            </tbody>
          </table>
        </div>
        <script>window.ended = []; document.addEventListener('sedna-resize', e => window.ended.push([e.detail.key, e.detail.value, e.target.id]));</script>
        """;

    private static Task<string> Ended(IPage page) => page.EvaluateAsync<string>("() => JSON.stringify(window.ended)");

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_column_grip_drags_its_column_wider_towards_the_inline_end_and_names_the_column(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Columns(dir));
        var box = (await page.Locator("#g").BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);
        var outward = dir == "rtl" ? -1 : 1;

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + outward * 43, y, new() { Steps = 4 });
        await page.Mouse.UpAsync();

        Assert.Equal(240, await Width(page, "h-customer"), 0.5);           // 200 + 43, snapped to 8
        Assert.Equal(240, await Width(page, "c-customer"), 0.5);           // the whole column
        Assert.Equal(120, await Width(page, "h-order"), 0.5);
        Assert.Equal("240px", await page.EvaluateAsync<string>("() => document.getElementById('h-customer').style.getPropertyValue('--col-width')"));
        Assert.Equal("[[\"customer\",240,\"h-customer\"]]", await Ended(page));
        Assert.Equal("240", await page.GetAttributeAsync("#g", "aria-valuenow"));

        // Its bounds are the column's own --col-min and --col-max, in whatever unit.
        await page.Mouse.MoveAsync(x + outward * 43, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + outward * 400, y, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        Assert.Equal(288, await Width(page, "h-customer"), 0.5);           // 18rem
        box = (await page.Locator("#g").BoundingBoxAsync())!;
        x = box.X + box.Width / 2;
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x - outward * 400, y, new() { Steps = 4 });
        await page.Mouse.UpAsync();
        Assert.Equal(96, await Width(page, "h-customer"), 0.5);

        // A press on the grip is the grip's: the sort button beside it was not clicked.
        Assert.Equal(0, await page.EvaluateAsync<int>("() => document.querySelectorAll('[data-dragging]').length"));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_keyboard_moves_a_column_grip_the_way_the_arrow_points_and_Home_puts_the_column_back(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Columns(dir));
        var wider = dir == "rtl" ? "ArrowLeft" : "ArrowRight";
        var narrower = dir == "rtl" ? "ArrowRight" : "ArrowLeft";

        await page.FocusAsync("#g");
        await page.Keyboard.PressAsync(wider);
        Assert.Equal(208, await Width(page, "h-customer"), 0.5);
        await page.Keyboard.PressAsync("Shift+" + narrower);
        Assert.Equal(176, await Width(page, "h-customer"), 0.5);

        await page.Keyboard.PressAsync("Home");
        // Back to what the CSS gives it: the table's own --col-width, the cell's having gone.
        Assert.Equal(160, await Width(page, "h-customer"), 0.5);
        Assert.Equal("[[\"customer\",208,\"h-customer\"],[\"customer\",176,\"h-customer\"],[\"customer\",null,\"h-customer\"]]", await Ended(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Alt_Shift_with_an_arrow_on_the_header_cell_resizes_its_column_and_keeps_the_focus()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Columns(role: "role=\"grid\""));
        await Js.Run(page, "() => document.getElementById('h-customer').focus()");

        await page.Keyboard.PressAsync("Alt+Shift+ArrowRight");
        Assert.Equal(208, await Width(page, "h-customer"), 0.5);
        Assert.Equal("h-customer", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Shift+Alt+Home");
        Assert.Equal(160, await Width(page, "h-customer"), 0.5);

        // A cell with no grip of its own is left alone.
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("h-total", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Alt+Shift+ArrowRight");
        Assert.Equal(120, await Width(page, "h-total"), 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task In_a_keyboard_grid_the_grip_keeps_its_own_axis_and_the_grid_keeps_the_rest()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Columns(role: "role=\"grid\""));

        // The grid takes the grip out of the tab order, as it does every control in a cell.
        Assert.Equal("-1", await page.GetAttributeAsync("#g", "tabindex"));
        await Js.Run(page, "() => document.getElementById('g').focus()");

        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal(216, await Width(page, "h-customer"), 0.5);
        Assert.Equal("g", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Home");
        Assert.Equal(160, await Width(page, "h-customer"), 0.5);
        Assert.Equal("g", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        // Down is the grid's: the focus moves on to the cell below, and nothing resizes.
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("c-customer", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Equal(160, await Width(page, "h-customer"), 0.5);

        // Escape from the grip goes back to its cell.
        await Js.Run(page, "() => document.getElementById('g').focus()");
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("h-customer", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Empty(errors);
    }
}
