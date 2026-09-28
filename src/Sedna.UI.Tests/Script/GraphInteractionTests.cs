using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The pointer on the canvas, the events it sends, and the app's own markup the graph
/// fills: the tooltip, the context menu and the side panel.
/// </summary>
/// <remarks>
/// Every pointer action is a real mouse event at the record's place on the screen, so the
/// engine's own hit testing is part of what is tested — a record the mouse cannot reach is
/// a record nobody can open. The markup is filled with <c>textContent</c> and toggled with
/// <c>hidden</c>; a test here fails on a slot filled as markup, since a record's name is
/// the app's data and may be anything.
/// </remarks>
public class GraphInteractionTests : GraphTestBase
{
    private const string Tip = """
        <div class="graph-tip" data-graph-tip hidden id="tip">
          <div class="graph-tip-head"><i class="ri-fw" data-graph-icon id="tip-icon"></i>
            <span class="graph-tip-title" data-graph-field="label" id="tip-label"></span></div>
          <span class="graph-tip-meta" data-graph-field="meta" id="tip-meta"></span>
          <span class="graph-tip-meta" data-graph-field="owner" id="tip-owner"></span>
        </div>
        <div class="graph-tip" data-graph-tip="edge" hidden id="edge-tip"><span data-graph-field="label" id="edge-label"></span></div>
        """;

    private const string Menu = """
        <div class="menu graph-menu" data-graph-menu role="menu" hidden id="menu">
          <span class="menu-label" data-graph-field="label" id="menu-label"></span>
          <button class="menu-item" role="menuitem" type="button" data-graph-action="open" id="menu-open">Open</button>
          <button class="menu-item" role="menuitem" type="button" data-graph-action="focus" id="menu-focus">Show its neighbourhood</button>
          <button class="menu-item" role="menuitem" type="button" data-graph-action="expand" id="menu-expand">Unfold</button>
          <button class="menu-item" role="menuitem" type="button" data-graph-action="hide" id="menu-hide">Hide</button>
        </div>
        """;

    private const string Panel = """
        <aside class="card graph-detail" data-graph-detail hidden id="panel">
          <strong data-graph-field="label" id="panel-label"></strong>
          <span data-graph-field="meta" id="panel-meta"></span>
          <span data-graph-field="owner" id="panel-owner"></span>
          <ul data-graph-neighbours id="rows">
            <template data-graph-neighbour>
              <li><button type="button" class="list-row" data-graph-action="select">
                <span class="list-title" data-graph-field="label"></span>
                <span class="list-sub" data-graph-link></span>
              </button></li>
            </template>
          </ul>
        </aside>
        """;

    private static string Framed(string inside = Services, string attrs = "data-graph-eager") => $"""
        <div class="graph-frame" data-graph-frame>
          <div class="sedna-split">
            {Graph(inside: inside + Tip + Menu, attrs: attrs)}
            {Panel}
          </div>
        </div>
        """;

    private static readonly string[] RecordKeys =
        ["cluster", "fields", "group", "href", "id", "keyboard", "kind", "label", "meta", "parent", "tags", "tone"];

    private static async Task Click(IPage page, string node, MouseButton button = MouseButton.Left)
    {
        var (x, y) = await PointOf(page, node);
        await page.Mouse.ClickAsync(x, y, new() { Button = button });
    }

    private static async Task Hover(IPage page, string node)
    {
        var (x, y) = await PointOf(page, node);
        await page.Mouse.MoveAsync(x - 30, y - 30);
        await page.Mouse.MoveAsync(x, y, new() { Steps = 4 });
    }

    // ── Selecting ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clicking_a_record_selects_it_and_the_event_carries_the_record_as_plain_data()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed());
        await Ready(page);
        await ClearLog(page);

        await Click(page, "api");
        await Heard(page, "sedna-graph-select");

        var select = (await Log(page, "sedna-graph-select")).Single();
        Assert.Equal("g", select.On);
        Assert.False(select.Cancelable);
        var d = select.Detail;
        Assert.Equal(RecordKeys, d.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("api", d.GetProperty("id").GetString());
        Assert.Equal("Orders API", d.GetProperty("label").GetString());
        Assert.Equal("service", d.GetProperty("kind").GetString());
        Assert.Equal("orders", d.GetProperty("group").GetString());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("cluster").ValueKind);
        Assert.Equal(JsonValueKind.Null, d.GetProperty("parent").ValueKind);
        Assert.Equal("orders-console-01", d.GetProperty("meta").GetString());
        Assert.Equal("/records/api", d.GetProperty("href").GetString());
        Assert.Equal("2", d.GetProperty("tone").GetString());
        Assert.Equal(0, d.GetProperty("tags").GetArrayLength());
        Assert.Equal("Alex Fischer", d.GetProperty("fields").GetProperty("owner").GetString());
        Assert.False(d.GetProperty("keyboard").GetBoolean());
        Assert.Equal(["api"], await Eval<string[]>(page, "() => cyOf('g').$(':selected').map(n => n.id())"));
        // Clicking the canvas puts focus on the graph, so the keys carry on from there.
        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));

        // The background clears it.
        await ClearLog(page);
        var box = await page.Locator("#g .graph-canvas").BoundingBoxAsync();
        await page.Mouse.ClickAsync(box!.X + 6, box.Y + 6);
        await Heard(page, "sedna-graph-select");
        Assert.Equal(JsonValueKind.Null, (await Log(page, "sedna-graph-select")).Single().Detail.GetProperty("id").ValueKind);
        AssertQuiet();
    }

    [Fact]
    public async Task The_side_panel_shows_the_selection_with_a_row_for_each_neighbour_and_its_direction()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="api" data-meta="orders-console-01" data-owner="Alex Fischer">Orders API</li>
              <li data-node="db" data-meta="src-db-14">Orders database</li>
              <li data-node="web">Web shop</li>
              <li data-node="runner">Build runner</li>
              <li data-edge data-source="api" data-target="db" data-label="reads">reads</li>
              <li data-edge data-source="db" data-target="api" data-label="notifies">notifies</li>
              <li data-edge data-source="web" data-target="api" data-label="calls">calls</li>
              <li data-edge data-source="api" data-target="runner"></li>
            </ul>
            """));
        await Ready(page);
        await Assertions.Expect(page.Locator("#panel")).ToBeHiddenAsync();

        await Click(page, "api");

        await Assertions.Expect(page.Locator("#panel")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#panel-label")).ToHaveTextAsync("Orders API");
        await Assertions.Expect(page.Locator("#panel-owner")).ToHaveTextAsync("Alex Fischer");
        var rows = await page.EvaluateAsync<JsonElement>("""
            () => [...document.querySelectorAll('#rows [data-graph-row]')].map(r => ({
                name: r.querySelector('[data-graph-field="label"]').textContent,
                link: r.querySelector('[data-graph-link]').textContent,
                linkHidden: r.querySelector('[data-graph-link]').hidden,
                direction: r.getAttribute('data-graph-direction'),
                value: r.querySelector('button').getAttribute('value') }))
            """);
        // One row per neighbour, by name; a neighbour linked both ways is one row.
        Assert.Equal(["Build runner", "Orders database", "Web shop"], rows.EnumerateArray().Select(r => r.GetProperty("name").GetString()));
        Assert.Equal(["out", "both", "in"], rows.EnumerateArray().Select(r => r.GetProperty("direction").GetString()));
        Assert.Equal(["", "reads, notifies", "calls"], rows.EnumerateArray().Select(r => r.GetProperty("link").GetString()));
        Assert.True(rows[0].GetProperty("linkHidden").GetBoolean());
        Assert.Equal(["runner", "db", "web"], rows.EnumerateArray().Select(r => r.GetProperty("value").GetString()));
        Assert.Equal("3", await page.Locator("#rows").GetAttributeAsync("data-graph-count"));

        // A row's action acts on its neighbour.
        await page.Locator("#rows [data-graph-row] button").Nth(1).ClickAsync();
        await Assertions.Expect(page.Locator("#panel-label")).ToHaveTextAsync("Orders database");
        await Assertions.Expect(page.Locator("#panel-owner")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#rows [data-graph-row]")).ToHaveCountAsync(1);
        Assert.Equal(["db"], await Eval<string[]>(page, "() => cyOf('g').$(':selected').map(n => n.id())"));

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.select(null))");
        await Assertions.Expect(page.Locator("#panel")).ToBeHiddenAsync();
        AssertQuiet();
    }

    [Fact]
    public async Task With_data_graph_select_none_a_click_selects_nothing()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed(attrs: "data-graph-eager data-graph-select=\"none\""));
        await Ready(page);
        await ClearLog(page);

        await Click(page, "api");
        await page.WaitForTimeoutAsync(50);

        Assert.Equal(0, await Eval<int>(page, "() => cyOf('g').$(':selected').length"));
        Assert.Empty(await Log(page, "sedna-graph-select"));
        await Assertions.Expect(page.Locator("#panel")).ToBeHiddenAsync();
        AssertQuiet();
    }

    private static async Task Drag(IPage page, string id, string node)
    {
        await page.Locator("#" + id).ScrollIntoViewIfNeededAsync();
        var (x, y) = await PointOf(page, node, id);
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 60, y + 30, new() { Steps = 10 });
        await page.Mouse.UpAsync();
    }

    [Fact]
    public async Task A_record_can_be_dragged_unless_data_graph_drag_is_false()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(id: "free") + Graph(id: "fixed", attrs: "data-graph-eager data-graph-drag=\"false\""));
        await Ready(page, "free");
        await Ready(page, "fixed");

        var free = (await Positions(page, "free"))["archive"];
        await Drag(page, "free", "archive");
        Assert.NotEqual(free, (await Positions(page, "free"))["archive"]);

        var fixedAt = (await Positions(page, "fixed"))["archive"];
        await Drag(page, "fixed", "archive");
        Assert.Equal(fixedAt, (await Positions(page, "fixed"))["archive"]);
        AssertQuiet();
    }

    [Fact]
    public async Task The_wheel_scrolls_the_page_until_the_canvas_is_pressed_and_zooms_it_after()
    {
        if (NoBrowser) return;
        // A graph in a scrolling page must not swallow the scroll of a reader passing over it.
        var page = await OpenGraph(Framed() + """<div style="height: 200vh"></div>""");
        await Ready(page);
        const string zoomNow = "() => cyOf('g').zoom()";
        var zoom = await Eval<double>(page, zoomNow);
        var box = (await page.Locator("#g .graph-canvas").BoundingBoxAsync())!;
        var (x, y) = (box.X + 8, box.Y + box.Height * 0.75f);

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.WheelAsync(0, 120);
        await page.WaitForFunctionAsync("() => scrollY > 0");
        Assert.Equal(zoom, await Eval<double>(page, zoomNow));
        var scrolled = await Eval<double>(page, "() => scrollY");
        // The engine ignores the wheel for a moment after the page has scrolled, so a
        // scroll still under way does not turn into a zoom as the canvas passes the pointer.
        await page.WaitForTimeoutAsync(300);

        // Pressed: the wheel is the graph's.
        box = (await page.Locator("#g .graph-canvas").BoundingBoxAsync())!;
        (x, y) = (box.X + 8, box.Y + box.Height * 0.75f);
        await page.Mouse.ClickAsync(x, y);
        await page.Mouse.WheelAsync(0, 120);
        await page.WaitForFunctionAsync("z => cyOf('g').zoom() !== z", zoom);
        Assert.Equal(scrolled, await Eval<double>(page, "() => scrollY"));

        // Once the pointer has left the canvas, the page's again.
        zoom = await Eval<double>(page, zoomNow);
        await page.Mouse.MoveAsync(x, box.Y + box.Height + 40, new() { Steps = 3 });
        await page.Mouse.MoveAsync(x, y, new() { Steps = 3 });
        await page.Mouse.WheelAsync(0, 120);
        await page.WaitForFunctionAsync("s => scrollY > s", scrolled);
        Assert.Equal(zoom, await Eval<double>(page, zoomNow));
        AssertQuiet();
    }

    // ── Opening ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Double_clicking_opens_a_record_and_a_cancelled_open_goes_nowhere()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed());
        await Ready(page);
        await page.EvaluateAsync("() => document.addEventListener('sedna-graph-open', e => e.preventDefault())");

        var (x, y) = await PointOf(page, "api");
        await page.Mouse.DblClickAsync(x, y);
        await Heard(page, "sedna-graph-open");

        var open = (await Log(page, "sedna-graph-open")).Single();
        Assert.True(open.Cancelable);
        Assert.Equal("api", open.Detail.GetProperty("id").GetString());
        Assert.False(open.Detail.GetProperty("keyboard").GetBoolean());
        await page.WaitForTimeoutAsync(100);
        Assert.EndsWith("/fixture.html", page.Url, StringComparison.Ordinal);
        AssertQuiet();
    }

    [Fact]
    public async Task An_open_nobody_cancels_follows_data_href_through_a_real_link_a_router_can_take()
    {
        if (NoBrowser) return;
        // Blazor's router, like every client-side router, listens for clicks on anchors.
        // A location.assign() would reload the page under it.
        var page = await OpenGraph(Framed());
        await Ready(page);
        await page.EvaluateAsync("""
            () => document.addEventListener('click', e => {
                const a = e.target.closest && e.target.closest('a[href]');
                if (!a) return;
                window.routed = a.getAttribute('href');
                e.preventDefault();
            })
            """);

        var (x, y) = await PointOf(page, "api");
        await page.Mouse.DblClickAsync(x, y);
        await page.WaitForFunctionAsync("() => window.routed === '/records/api'");
        // And no anchor is left behind in the page.
        Assert.Equal(0, await Eval<int>(page, "() => document.querySelectorAll('body > a').length"));
        AssertQuiet();
    }

    [Fact]
    public async Task Without_a_router_an_open_navigates_to_data_href()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed());
        await Ready(page);

        var (x, y) = await PointOf(page, "api");
        // Listening before the double click, and for the navigation rather than the next
        // page's load: the fixture answers every path at once, so a wait started afterwards
        // can begin after the navigation has already happened.
        var navigated = page.WaitForURLAsync("**/records/api", new() { WaitUntil = WaitUntilState.Commit });
        await page.Mouse.DblClickAsync(x, y);
        await navigated;
    }

    [Fact]
    public async Task With_data_graph_open_tap_a_single_click_opens_rather_than_selects()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed(attrs: "data-graph-eager data-graph-open=\"tap\""));
        await Ready(page);
        await page.EvaluateAsync("() => document.addEventListener('sedna-graph-open', e => e.preventDefault())");
        await ClearLog(page);

        await Click(page, "db");
        await Heard(page, "sedna-graph-open");
        Assert.Empty(await Log(page, "sedna-graph-select"));
        Assert.Equal("db", (await Log(page, "sedna-graph-open")).Single().Detail.GetProperty("id").GetString());
        AssertQuiet();
    }

    // ── Pointing ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pointing_at_a_record_lights_its_neighbourhood_and_the_apps_tooltip_says_what_it_is()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed());
        await Ready(page);
        await ClearLog(page);

        await Hover(page, "api");
        await Heard(page, "sedna-graph-hover");
        await Assertions.Expect(page.Locator("#tip")).ToBeVisibleAsync();

        var hover = (await Log(page, "sedna-graph-hover")).Last();
        Assert.Equal("api", hover.Detail.GetProperty("id").GetString());
        Assert.False(hover.Detail.GetProperty("keyboard").GetBoolean());
        await Assertions.Expect(page.Locator("#tip-label")).ToHaveTextAsync("Orders API");
        await Assertions.Expect(page.Locator("#tip-meta")).ToHaveTextAsync("orders-console-01");
        await Assertions.Expect(page.Locator("#tip-owner")).ToHaveTextAsync("Alex Fischer");
        Assert.Equal("ri-fw ri-server-line", await page.Locator("#tip-icon").GetAttributeAsync("class"));
        Assert.Equal("2", await page.Locator("#tip").GetAttributeAsync("data-graph-tone"));
        // Its neighbourhood is lit; the rest is dimmed.
        var opacity = await Eval<double[]>(page,
            "() => ['api', 'db', 'web', 'archive'].map(id => cyOf('g').getElementById(id).numericStyle('opacity'))");
        Assert.Equal([1d, 1d, 1d], opacity[..3]);
        Assert.True(opacity[3] < 0.5, $"A record outside the neighbourhood kept an opacity of {opacity[3]}.");
        // Placed inside the frame, beside the record.
        Assert.True(await Eval<bool>(page, """
            () => { const t = document.getElementById('tip').getBoundingClientRect(), g = document.getElementById('g').getBoundingClientRect();
                    return t.left >= g.left && t.right <= g.right && t.top >= g.top && t.bottom <= g.bottom && t.width > 0; }
            """));

        // A record without the field hides that slot; one without an icon hides the icon.
        await Hover(page, "db");
        await Assertions.Expect(page.Locator("#tip-label")).ToHaveTextAsync("Orders database");
        await Assertions.Expect(page.Locator("#tip-owner")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#tip-icon")).ToBeHiddenAsync();

        // Leaving the canvas takes the tooltip down and the light with it.
        await page.Mouse.MoveAsync(2, 2);
        await Assertions.Expect(page.Locator("#tip")).ToBeHiddenAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('archive').numericStyle('opacity') === 1");
        AssertQuiet();
    }

    [Fact]
    public async Task A_tooltip_slot_is_filled_as_text_even_when_the_name_looks_like_markup()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="x" data-meta="&lt;img src=x onerror=&quot;window.injected=1&quot;&gt;">&lt;b&gt;Bold&lt;/b&gt; service</li>
              <li data-node="y">Other</li>
              <li data-edge data-source="x" data-target="y">x to y</li>
            </ul>
            """));
        await Ready(page);

        await Hover(page, "x");
        await Assertions.Expect(page.Locator("#tip-label")).ToHaveTextAsync("<b>Bold</b> service");
        Assert.Equal(0, await Eval<int>(page, "() => document.querySelectorAll('#tip b, #tip img').length"));
        await Click(page, "x");
        await Assertions.Expect(page.Locator("#panel-meta")).ToHaveTextAsync("<img src=x onerror=\"window.injected=1\">");
        Assert.Equal(0, await Eval<int>(page, "() => document.querySelectorAll('#panel img, #panel b').length"));
        Assert.False(await Eval<bool>(page, "() => 'injected' in window"));
        AssertQuiet();
    }

    [Fact]
    public async Task Without_a_tooltip_of_its_own_the_graph_uses_the_librarys_hover_hint()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);

        await Hover(page, "api");
        var hint = page.Locator(".sedna-tip");
        await Assertions.Expect(hint).ToHaveTextAsync("Orders API — orders-console-01");
        await Assertions.Expect(hint).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));
        AssertQuiet();
    }

    [Fact]
    public async Task Pointing_at_a_link_shows_the_link_tooltip_with_its_label()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed(attrs: "data-graph-eager data-graph-layout=\"preset\"", inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="api" data-x="0" data-y="0">Orders API</li>
              <li data-node="db" data-x="400" data-y="0">Orders database</li>
              <li data-edge data-source="api" data-target="db" data-label="reads">The orders API reads the database</li>
            </ul>
            """));
        await Ready(page);

        var mid = await page.EvaluateAsync<JsonElement>("""
            () => { const host = document.querySelector('#g .graph-canvas').getBoundingClientRect();
                    const m = cyOf('g').edges()[0].renderedMidpoint(); return { x: host.left + m.x, y: host.top + m.y }; }
            """);
        var (x, y) = ((float)mid.GetProperty("x").GetDouble(), (float)mid.GetProperty("y").GetDouble());
        await page.Mouse.MoveAsync(x, y - 40);
        await page.Mouse.MoveAsync(x, y, new() { Steps = 4 });

        await Assertions.Expect(page.Locator("#edge-tip")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#edge-label")).ToHaveTextAsync("reads");
        await Assertions.Expect(page.Locator("#tip")).ToBeHiddenAsync();
        AssertQuiet();
    }

    // ── The context menu ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_right_click_opens_the_apps_menu_filled_for_that_record_where_the_pointer_is()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed());
        await Ready(page);
        await ClearLog(page);

        await Click(page, "api", MouseButton.Right);
        await Heard(page, "sedna-graph-context");

        var context = (await Log(page, "sedna-graph-context")).Single();
        Assert.True(context.Cancelable);
        Assert.Equal("api", context.Detail.GetProperty("id").GetString());
        Assert.Equal("Orders API", context.Detail.GetProperty("label").GetString());
        var rendered = await page.EvaluateAsync<JsonElement>("() => cyOf('g').getElementById('api').renderedPosition()");
        // In the graph's own pixels: where the pointer was.
        Assert.InRange(Math.Abs(rendered.GetProperty("x").GetDouble() - context.Detail.GetProperty("x").GetDouble()), 0, 1.5);
        Assert.InRange(Math.Abs(rendered.GetProperty("y").GetDouble() - context.Detail.GetProperty("y").GetDouble()), 0, 1.5);

        await Assertions.Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#menu-label")).ToHaveTextAsync("Orders API");
        Assert.Equal("api", await page.Locator("#menu").GetAttributeAsync("data-graph-target"));
        // Open applies to a record with a data-href; unfolding to a folded group only.
        await Assertions.Expect(page.Locator("#menu-open")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#menu-expand")).ToBeHiddenAsync();
        // Where the pointer was, inside the frame.
        Assert.True(await Eval<bool>(page, """
            () => { const m = document.getElementById('menu').getBoundingClientRect(), g = document.getElementById('g').getBoundingClientRect();
                    return m.left >= g.left && m.right <= g.right && m.top >= g.top && m.bottom <= g.bottom; }
            """));

        // An item acts on the menu's record, and picking it closes the menu and gives the
        // keyboard back to the graph.
        await page.Locator("#menu-focus").ClickAsync();
        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('archive').hasClass('hidden')");
        Assert.Equal(["api", "db", "queue", "runner", "web"], await Shown(page));
        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));
        AssertQuiet();
    }

    [Fact]
    public async Task The_menu_hides_open_for_a_record_with_nowhere_to_go()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed());
        await Ready(page);

        await Click(page, "db", MouseButton.Right);
        await Assertions.Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#menu-label")).ToHaveTextAsync("Orders database");
        await Assertions.Expect(page.Locator("#menu-open")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#menu-hide")).ToBeVisibleAsync();

        // A click elsewhere closes it.
        await page.Mouse.ClickAsync(3, 3);
        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();
        AssertQuiet();
    }

    [Fact]
    public async Task A_cancelled_context_event_leaves_the_apps_menu_closed()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed());
        await Ready(page);
        await page.EvaluateAsync("() => document.addEventListener('sedna-graph-context', e => e.preventDefault())");

        await Click(page, "api", MouseButton.Right);
        await Heard(page, "sedna-graph-context");
        await page.WaitForTimeoutAsync(50);
        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();
        AssertQuiet();
    }

    [Fact]
    public async Task Escape_closes_a_menu_opened_by_the_pointer_and_leaves_focus_on_the_graph()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed());
        await Ready(page);

        await Click(page, "api", MouseButton.Right);
        await Assertions.Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();
        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));
        AssertQuiet();
    }

    [Fact]
    public async Task Escape_in_the_menu_closes_it_and_gives_focus_back_to_the_graph()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed());
        await Ready(page);

        await Click(page, "api", MouseButton.Right);
        await page.Locator("#menu-focus").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("menu-hide", await Eval<string>(page, "() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();
        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_scroll_closes_the_menu()
    {
        if (NoBrowser) return;
        // The menu is placed at a point on the canvas; once the page scrolls, that point is
        // somewhere else, so it closes as every other .menu does.
        var page = await OpenGraph(Framed() + """<div style="height: 200vh"></div>""");
        await Ready(page);

        await Click(page, "api", MouseButton.Right);
        await Assertions.Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await page.EvaluateAsync("() => window.scrollBy(0, 300)");
        await page.WaitForFunctionAsync("() => scrollY > 0");

        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();
        AssertQuiet();
    }

    // ── Events over a graph's life ────────────────────────────────────────────

    [Fact]
    public async Task Ready_is_sent_once_and_change_after_every_filter_search_and_new_data()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph() + """
            <div data-graph-for="g">
              <button class="chip" type="button" data-graph-filter="kind" value="store" aria-pressed="true" id="stores">Stores</button>
              <input type="search" data-graph-search aria-label="Find" id="find">
            </div>
            """);
        await Ready(page);
        await ClearLog(page);

        await page.Locator("#stores").ClickAsync();
        await Heard(page, "sedna-graph-change", 1);
        Assert.Equal(4, (await Log(page, "sedna-graph-change")).Last().Detail.GetProperty("nodes").GetInt32());

        await page.Locator("#find").FillAsync("web");
        await Heard(page, "sedna-graph-change", 2);
        Assert.Equal(1, (await Log(page, "sedna-graph-change")).Last().Detail.GetProperty("matches").GetInt32());

        await page.EvaluateAsync("() => document.querySelector('#g [data-node=\"archive\"]').remove()");
        await Heard(page, "sedna-graph-change", 3);
        Assert.Equal(6, (await Log(page, "sedna-graph-change")).Last().Detail.GetProperty("totalNodes").GetInt32());

        Assert.Empty(await Log(page, "sedna-graph-ready"));
        AssertQuiet();
    }
}
