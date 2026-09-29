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

    /// <summary>
    /// The catalogue's explorer markup — a split of the graph's card and a filling side
    /// panel — with a record whose neighbours are far taller than any screen.
    /// </summary>
    private static string Explorer()
    {
        var spokes = Enumerable.Range(1, 40).ToArray();
        var data = "<ul class=\"graph-data\" data-graph-data><li data-node=\"hub\">Orders API</li>"
            + string.Concat(spokes.Select(i => $"<li data-node=\"n{i}\">Record {i}</li>"))
            + string.Concat(spokes.Select(i => $"<li data-edge data-source=\"hub\" data-target=\"n{i}\"></li>"))
            + "</ul>";
        return $"""
            <div class="graph-frame" data-graph-frame style="--graph-height: 20rem">
              <div class="toolbar">
                <button class="btn btn-sm" type="button" data-graph-action="fullscreen" aria-pressed="false" id="full">Full screen</button>
              </div>
              <div class="sedna-split">
                <div class="card" id="graph-card">
                  {Graph(inside: data)}
                </div>
                <aside class="card card--fill sedna-split-aside graph-detail" data-graph-detail hidden id="panel">
                  <div class="card-head"><strong data-graph-field="label"></strong></div>
                  <div class="card-body" id="panel-body">
                    <ul class="list" data-graph-neighbours>
                      <template data-graph-neighbour>
                        <li><div class="list-row"><button class="btn-bare list-title" type="button" data-graph-action="select" data-graph-field="label"></button></div></li>
                      </template>
                    </ul>
                  </div>
                  <div class="card-foot"><button class="btn btn-sm" type="button" data-graph-action="clear">Close</button></div>
                </aside>
              </div>
            </div>
            """;
    }

    private sealed record FrameLayout(
        bool Full, bool FrameScrolls, double ContentBottom, double CardBottom, double CardHeight,
        double PanelTop, double PanelBottom, double PanelHeight, bool BodyScrolls,
        double GraphHeight, double CanvasHeight, double Drawn);

    private static async Task<FrameLayout> LayoutOf(IPage page) =>
        (await Eval<JsonElement>(page, LayoutScript)).Deserialize<FrameLayout>(JsonSerializerOptions.Web)!;

    private const string LayoutScript = """
        () => {
            const frame = document.querySelector('[data-graph-frame]');
            const box = s => document.querySelector(s).getBoundingClientRect();
            const body = document.querySelector('#panel-body');
            return {
                full: document.fullscreenElement === frame,
                frameScrolls: frame.scrollHeight > frame.clientHeight,
                contentBottom: frame.getBoundingClientRect().bottom - parseFloat(getComputedStyle(frame).paddingBottom),
                cardBottom: box('#graph-card').bottom,
                cardHeight: box('#graph-card').height,
                panelTop: box('#panel').top,
                panelBottom: box('#panel').bottom,
                panelHeight: box('#panel').height,
                bodyScrolls: body.scrollHeight > body.clientHeight,
                graphHeight: box('#g').height,
                canvasHeight: document.querySelector('#g .graph-canvas').clientHeight,
                drawn: cyOf('g').height(),
            };
        }
        """;

    private static async Task<FrameLayout> GoFullScreen(IPage page)
    {
        // A real click: the browser grants full screen only to a user's gesture.
        await page.Locator("#full").ClickAsync();
        await page.WaitForFunctionAsync("() => document.fullscreenElement !== null");
        // The graph redraws at its new size a moment after the change.
        await page.WaitForFunctionAsync(
            "() => cyOf('g').height() === document.querySelector('#g .graph-canvas').clientHeight");
        return await LayoutOf(page);
    }

    [Fact]
    public async Task In_full_screen_the_side_panel_is_held_to_the_graphs_row_whatever_it_holds()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Explorer());
        await Ready(page);
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.select('hub'))");
        await Assertions.Expect(page.Locator("#panel")).ToBeVisibleAsync();

        // On the page, the panel is as tall as the graph and its body scrolls.
        var onPage = await LayoutOf(page);
        Assert.True(onPage.PanelHeight <= onPage.GraphHeight + 1, $"panel {onPage.PanelHeight} over graph {onPage.GraphHeight}");
        Assert.True(onPage.BodyScrolls);

        // Beside the graph: one row, as tall as the screen leaves it, the panel held to it.
        var wide = await GoFullScreen(page);
        Assert.True(wide.Full);
        Assert.False(wide.FrameScrolls, "the frame scrolls");
        Assert.Equal(wide.ContentBottom, wide.CardBottom, 1.0);
        Assert.Equal(wide.ContentBottom, wide.PanelBottom, 1.0);
        Assert.Equal(wide.CardHeight, wide.PanelHeight, 1.0);
        Assert.True(wide.BodyScrolls, "the panel's body does not scroll");
        Assert.True(wide.GraphHeight > onPage.GraphHeight, $"graph {wide.GraphHeight} did not grow");
        Assert.Equal(wide.CanvasHeight, wide.Drawn, 1.0);

        // Leaving gives the page's layout back.
        await page.EvaluateAsync("() => document.exitFullscreen()");
        await page.WaitForFunctionAsync("() => document.fullscreenElement === null");
        var back = await LayoutOf(page);
        Assert.Equal(onPage.GraphHeight, back.GraphHeight, 1.0);
        Assert.True(back.PanelHeight <= back.GraphHeight + 1, $"panel {back.PanelHeight} over graph {back.GraphHeight}");

        // Stacked on a narrow screen: the graph and the panel share it, and both stay on it.
        await page.SetViewportSizeAsync(480, 800);
        var narrow = await GoFullScreen(page);
        Assert.True(narrow.Full);
        Assert.False(narrow.FrameScrolls, "the frame scrolls");
        Assert.True(narrow.PanelTop >= narrow.CardBottom, "the split did not stack");
        Assert.Equal(narrow.ContentBottom, narrow.PanelBottom, 1.0);
        Assert.True(narrow.BodyScrolls, "the panel's body does not scroll");
        Assert.True(narrow.GraphHeight > 200, $"graph {narrow.GraphHeight} squeezed out");
        Assert.Equal(narrow.CanvasHeight, narrow.Drawn, 1.0);
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
        // Its neighbourhood is lit and drawn over the veil; the rest stays under it, its name hushed.
        var lit = await Eval<bool[]>(page,
            "() => ['api', 'db', 'web', 'archive'].map(id => cyOf('g').getElementById(id).hasClass('lit'))");
        Assert.Equal([true, true, true, false], lit);
        Assert.Equal("1", await Eval<string>(page, "() => document.querySelector('#g [data-graph-veil]').style.opacity"));
        Assert.Equal(0d, await Eval<double>(page, "() => cyOf('g').getElementById('archive').numericStyle('text-opacity')"));
        Assert.Equal(1d, await Eval<double>(page, "() => cyOf('g').getElementById('db').numericStyle('text-opacity')"));
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

    // ── Leaving, groups, slots, separators ────────────────────────────────────

    [Fact]
    public async Task Leaving_the_canvas_in_one_jump_ends_the_lighting_and_the_tooltip()
    {
        if (NoBrowser) return;
        // A pointer that leaves in one move never passes a record's edge on the way out.
        var page = await OpenGraph(Framed());
        await Ready(page);
        await Hover(page, "api");
        await Assertions.Expect(page.Locator("#tip")).ToBeVisibleAsync();
        // The rest is dimmed by the veil over the drawing, the neighbourhood drawn on it.
        Assert.True(await page.EvaluateAsync<int>("() => cyOf('g').elements('.lit').length") > 0);
        Assert.Equal("1", await page.EvaluateAsync<string>("() => document.querySelector('#g [data-graph-veil]').style.opacity"));

        await page.Mouse.MoveAsync(2, 2);
        await Assertions.Expect(page.Locator("#tip")).ToBeHiddenAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').elements('.lit').length === 0");
        Assert.Equal("0", await page.EvaluateAsync<string>("() => document.querySelector('#g [data-graph-veil]').style.opacity"));
        AssertQuiet();
    }

    [Fact]
    public async Task Pointing_at_a_group_lights_what_is_inside_it()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="team">Platform team</li>
              <li data-node="runner" data-parent="team">build-runner-04</li>
              <li data-node="db" data-parent="team">src-db-14</li>
              <li data-node="web">orders-console-01</li>
              <li data-edge data-source="web" data-target="team"></li>
            </ul>
            """));
        await Ready(page);
        var edge = await page.EvaluateAsync<JsonElement>("""
            () => { const cy = cyOf('g'); const r = document.querySelector('#g .graph-canvas').getBoundingClientRect();
                    const b = cy.getElementById('team').renderedBoundingBox({ includeLabels: false });
                    return { x: r.left + b.x1 + 4, y: r.top + (b.y1 + b.y2) / 2 }; }
            """);
        await page.Mouse.MoveAsync((float)edge.GetProperty("x").GetDouble(), (float)edge.GetProperty("y").GetDouble(), new() { Steps = 4 });
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('team').hasClass('lit')");

        Assert.Equal(0, await page.EvaluateAsync<int>("() => cyOf('g').getElementById('team').descendants().filter(n => !n.hasClass('lit')).length"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_label_written_beside_a_field_leaves_with_it()
    {
        if (NoBrowser) return;
        // A value the record does not have takes its label with it: the <dt> before a <dd>
        // slot, and anything marked data-graph-if with that field's name.
        var page = await OpenGraph(Framed(inside: Services + """
            <div class="graph-tip" data-graph-tip hidden id="tip2">
              <dl><dt id="owner-term">Owner</dt><dd data-graph-field="owner" id="owner"></dd></dl>
              <span data-graph-if="meta" id="host-row">Host <span data-graph-field="meta"></span></span>
            </div>
            """));
        await Ready(page);

        await Hover(page, "api");
        await Assertions.Expect(page.Locator("#tip2")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#owner-term")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#host-row")).ToBeVisibleAsync();

        await page.Mouse.MoveAsync(2, 2);
        await Hover(page, "queue");
        await Assertions.Expect(page.Locator("#tip2")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#owner-term")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#host-row")).ToBeHiddenAsync();
        AssertQuiet();
    }

    [Fact]
    public async Task A_menu_shows_a_separator_only_between_items_that_are_showing()
    {
        if (NoBrowser) return;
        // Opened on the background, the items that need a record hide — and the separator
        // that stood between them and the rest has nothing left to separate.
        var page = await OpenGraph(Framed(inside: Services + """
            <div class="menu graph-menu" data-graph-menu role="menu" hidden id="menu2">
              <button class="menu-item" role="menuitem" type="button" data-graph-action="open" data-graph-always id="m-open">Open</button>
              <button class="menu-item" role="menuitem" type="button" data-graph-action="focus" id="m-focus">Show its neighbourhood</button>
              <hr class="menu-sep" id="sep" />
              <button class="menu-item" role="menuitem" type="button" data-graph-action="show-all" id="m-all">Show everything</button>
            </div>
            """));
        await Ready(page);

        var corner = await page.EvaluateAsync<JsonElement>(
            "() => { const r = document.querySelector('#g .graph-canvas').getBoundingClientRect(); return { x: r.left + 6, y: r.top + 6 }; }");
        await page.Mouse.ClickAsync((float)corner.GetProperty("x").GetDouble(), (float)corner.GetProperty("y").GetDouble(), new() { Button = MouseButton.Right });
        await Assertions.Expect(page.Locator("#menu2")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#m-all")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#sep")).ToBeHiddenAsync();

        await page.Keyboard.PressAsync("Escape");
        await Click(page, "api", MouseButton.Right);
        await Assertions.Expect(page.Locator("#m-focus")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#sep")).ToBeVisibleAsync();
        AssertQuiet();
    }
}
