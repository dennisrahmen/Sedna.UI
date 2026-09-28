using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The layouts and the plugins — each imported from beside the engine the first time a
/// graph uses it — the minimap, and the SVG and PNG export.
/// </summary>
/// <remarks>
/// A plugin is a separate file the module finds by relative URL, so a renamed file or a
/// wrong path is invisible until a graph asks for it. Each test here asks for one and
/// asserts what it drew, not only that it loaded.
/// </remarks>
public class GraphPluginTests : GraphTestBase
{
    private static bool Fetched(IEnumerable<IRequest> requests, string file) =>
        requests.Any(r => r.Url.EndsWith("/lib/cytoscape/" + file, StringComparison.Ordinal));

    // ── Layouts ───────────────────────────────────────────────────────────────

    private const string Chart = """
        <ul class="graph-data" data-graph-data>
          <li data-node="lead">Alex Fischer</li>
          <li data-node="ops">Operations</li>
          <li data-node="dev">Development</li>
          <li data-node="runner">build-runner-04</li>
          <li data-edge data-source="lead" data-target="ops"></li>
          <li data-edge data-source="lead" data-target="dev"></li>
          <li data-edge data-source="dev" data-target="runner"></li>
        </ul>
        """;

    [Fact]
    public async Task Dagre_lays_a_hierarchy_out_in_rank_order_in_its_direction()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(
            Graph(id: "down", inside: Chart, attrs: "data-graph-eager data-graph-layout=\"dagre\"")
            + Graph(id: "across", inside: Chart, attrs: "data-graph-eager data-graph-layout=\"dagre\" data-graph-direction=\"LR\""));
        await Ready(page, "down");
        await Ready(page, "across");

        var down = await Positions(page, "down");
        Assert.True(down["ops"].Y > down["lead"].Y && down["dev"].Y > down["lead"].Y && down["runner"].Y > down["dev"].Y);
        // One rank, one row.
        Assert.Equal(down["ops"].Y, down["dev"].Y);

        var across = await Positions(page, "across");
        Assert.True(across["ops"].X > across["lead"].X && across["runner"].X > across["dev"].X);
        Assert.Equal(across["ops"].X, across["dev"].X);

        Assert.True(Fetched(Requests, "cytoscape-dagre.js"));
        Assert.False(Fetched(Requests, "cytoscape-fcose.js"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_graph_that_uses_no_plugin_loads_none()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var plugins = Requests.Where(r => r.Url.Contains("/lib/cytoscape/", StringComparison.Ordinal)
                                          && !r.Url.EndsWith("/cytoscape.js", StringComparison.Ordinal)).ToList();
        Assert.Empty(plugins);
        AssertQuiet();
    }

    private const string Nested = """
        <ul class="graph-data" data-graph-data>
          <li data-node="team">Platform team</li>
          <li data-node="runner" data-parent="team">build-runner-04</li>
          <li data-node="db" data-parent="team">src-db-14</li>
          <li data-node="shop">Shop team</li>
          <li data-node="web" data-parent="shop">Web shop</li>
          <li data-node="cache" data-parent="shop">Page cache</li>
          <li data-node="console">orders-console-01</li>
          <li data-edge data-source="runner" data-target="db"></li>
          <li data-edge data-source="web" data-target="cache"></li>
          <li data-edge data-source="web" data-target="db"></li>
          <li data-edge data-source="console" data-target="web"></li>
        </ul>
        """;

    [Fact]
    public async Task Fcose_lays_out_nested_groups_the_same_way_every_time()
    {
        if (NoBrowser) return;
        // fcose starts from random positions; the drawing a reader learned yesterday has
        // to be the one that comes back, so the start is seeded.
        var page = await OpenGraph(
            Graph(id: "one", inside: Nested, attrs: "data-graph-eager data-graph-layout=\"fcose\"")
            + Graph(id: "two", inside: Nested, attrs: "data-graph-eager data-graph-layout=\"fcose\""));
        await Ready(page, "one");
        await Ready(page, "two");

        var one = await Positions(page, "one");
        var two = await Positions(page, "two");
        foreach (var (id, at) in one) Assert.Equal(at, two[id]);
        Assert.Equal(one.Count, one.Values.Distinct().Count());
        Assert.True(await Eval<bool>(page, """
            () => ['runner', 'db', 'web', 'cache'].every(id => {
                const n = cyOf('one').getElementById(id), b = n.parent().boundingBox(), p = n.position();
                return p.x > b.x1 && p.x < b.x2 && p.y > b.y1 && p.y < b.y2; })
            """));
        // The page's own randomness is given back once the layout has run.
        Assert.True(await Eval<bool>(page, "() => Math.random !== undefined && Math.random() !== Math.random()"));
        Assert.True(Fetched(Requests, "cytoscape-fcose.js"));
        AssertQuiet();
    }

    [Theory]
    [InlineData("force")]
    [InlineData("islands")]
    [InlineData("rings")]
    [InlineData("concentric")]
    [InlineData("tree")]
    [InlineData("grid")]
    [InlineData("circle")]
    public async Task Every_layout_draws_the_same_records_the_same_way_every_time(string layout)
    {
        if (NoBrowser) return;
        var page = await OpenGraph(
            Graph(id: "one", attrs: $"data-graph-eager data-graph-layout=\"{layout}\"")
            + Graph(id: "two", attrs: $"data-graph-eager data-graph-layout=\"{layout}\""));
        await Ready(page, "one");
        await Ready(page, "two");

        var one = await Positions(page, "one");
        var two = await Positions(page, "two");
        foreach (var (id, at) in one) Assert.Equal(at, two[id]);
        // And every record has a place of its own.
        Assert.Equal(one.Count, one.Values.Distinct().Count());
        AssertQuiet();
    }

    [Fact]
    public async Task Islands_give_each_group_an_island_of_its_own()
    {
        if (NoBrowser) return;
        var records = string.Concat(Enumerable.Range(0, 24).Select(i =>
            $"""<li data-node="n{i}" data-group="{(i % 3 == 0 ? "orders" : i % 3 == 1 ? "shop" : "platform")}">Record {i}</li>"""));
        var links = string.Concat(Enumerable.Range(3, 21).Select(i => $"""<li data-edge data-source="n{i}" data-target="n{i % 3}"></li>"""));
        var page = await OpenGraph(Graph(inside: $"""<ul class="graph-data" data-graph-data>{records}{links}<li data-edge data-source="n0" data-target="n1"></li></ul>""",
            attrs: "data-graph-eager data-graph-layout=\"islands\""));
        await Ready(page);

        var overlaps = await Eval<string[]>(page, """
            () => {
                const cy = cyOf('g');
                const box = g => cy.nodes(`[group = "${g}"]`).boundingBox({ includeLabels: false });
                const groups = ['orders', 'shop', 'platform'];
                const out = [];
                for (let i = 0; i < groups.length; i++) for (let j = i + 1; j < groups.length; j++) {
                    const a = box(groups[i]), b = box(groups[j]);
                    if (a.x1 < b.x2 && b.x1 < a.x2 && a.y1 < b.y2 && b.y1 < a.y2) out.push(groups[i] + '/' + groups[j]);
                }
                return out;
            }
            """);
        Assert.Empty(overlaps);
        AssertQuiet();
    }

    // ── Outlines ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hulls_outline_every_group_of_two_or_more_and_the_toggle_takes_them_away()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(attrs: "data-graph-eager data-graph-hulls", inside: Services + """
            <div class="graph-tools"><button type="button" data-graph-action="hulls" aria-pressed="false" id="hulls" aria-label="Outlines"></button></div>
            """));
        await Ready(page);

        // orders has three records and shop two; archive, alone, gets none.
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#g .graph-canvas svg path').length === 2");
        Assert.Equal("true", await page.Locator("#hulls").GetAttributeAsync("aria-pressed"));
        var fills = await Eval<string[]>(page, "() => [...document.querySelectorAll('#g .graph-canvas svg path')].map(p => p.getAttribute('style') || p.getAttribute('fill') || '')");
        Assert.All(fills, f => Assert.DoesNotMatch(new Regex("#[0-9a-f]{3,8}\\b", RegexOptions.IgnoreCase), f));

        await page.Locator("#hulls").ClickAsync();
        Assert.Equal("false", await page.Locator("#hulls").GetAttributeAsync("aria-pressed"));
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#g .graph-canvas svg path').length === 0");

        await page.Locator("#hulls").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#g .graph-canvas svg path').length === 2");
        Assert.True(Fetched(Requests, "cytoscape-bubblesets.js"));
        AssertQuiet();
    }

    // ── Folding ───────────────────────────────────────────────────────────────

    private const string Folding = """
        <ul class="graph-data" data-graph-data>
          <li data-node="team">Platform team</li>
          <li data-node="runner" data-parent="team">build-runner-04</li>
          <li data-node="db" data-parent="team">src-db-14</li>
          <li data-node="other" data-collapsed>On call</li>
          <li data-node="pager" data-parent="other">Pager</li>
          <li data-node="console">orders-console-01</li>
          <li data-edge data-source="runner" data-target="console"></li>
          <li data-edge data-source="db" data-target="console"></li>
          <li data-edge data-source="pager" data-target="console"></li>
        </ul>
        <div class="graph-tools">
          <button type="button" data-graph-action="expand" value="other" id="expand-other">Unfold on call</button>
          <button type="button" data-graph-action="collapse" value="team" id="collapse-team">Fold the platform team</button>
          <button type="button" data-graph-action="expand-all" id="expand-all">Unfold all</button>
          <button type="button" data-graph-action="collapse-all" id="collapse-all">Fold all</button>
        </div>
        <div class="menu graph-menu" data-graph-menu role="menu" hidden id="menu">
          <button class="menu-item" role="menuitem" type="button" data-graph-action="expand" id="menu-expand">Unfold</button>
          <button class="menu-item" role="menuitem" type="button" data-graph-action="collapse" id="menu-collapse">Fold</button>
        </div>
        """;

    private static Task<bool> Folded(IPage page, string id) =>
        page.EvaluateAsync<bool>("id => cyOf('g').getElementById(id).hasClass('cy-expand-collapse-collapsed-node')", id);

    private static Task<bool> Drawn(IPage page, string id) =>
        page.EvaluateAsync<bool>("id => cyOf('g').getElementById(id).inside()", id);

    [Fact]
    public async Task A_group_starts_folded_with_data_collapsed_and_the_actions_fold_and_unfold()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Folding, attrs: "data-graph-eager data-graph-collapse"));
        await Ready(page);

        Assert.True(await Folded(page, "other"));
        Assert.False(await Drawn(page, "pager"));
        Assert.False(await Folded(page, "team"));
        Assert.True(Fetched(Requests, "cytoscape-expand-collapse.js"));
        await ClearLog(page);

        await page.Locator("#expand-other").ClickAsync();
        await Heard(page, "sedna-graph-expand");
        Assert.True(await Drawn(page, "pager"));
        var expand = (await Log(page, "sedna-graph-expand")).Single();
        Assert.Equal("other", expand.Detail.GetProperty("id").GetString());
        Assert.Equal("On call", expand.Detail.GetProperty("label").GetString());

        await page.Locator("#collapse-team").ClickAsync();
        await Heard(page, "sedna-graph-collapse");
        Assert.True(await Folded(page, "team"));
        Assert.False(await Drawn(page, "runner"));
        Assert.Equal("team", (await Log(page, "sedna-graph-collapse")).Single().Detail.GetProperty("id").GetString());
        // A fold is a change: the stats say what is on screen now.
        var change = (await Log(page, "sedna-graph-change")).Last();
        Assert.Equal(4, change.Detail.GetProperty("nodes").GetInt32());

        await page.Locator("#expand-all").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').nodes('.cy-expand-collapse-collapsed-node').length === 0");
        Assert.True(await Drawn(page, "runner") && await Drawn(page, "pager"));

        await page.Locator("#collapse-all").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').nodes('.cy-expand-collapse-collapsed-node').length === 2");
        AssertQuiet();
    }

    [Fact]
    public async Task The_menu_offers_unfold_on_a_folded_group_and_fold_on_an_open_one()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Folding, attrs: "data-graph-eager data-graph-collapse"));
        await Ready(page);

        var (x, y) = await PointOf(page, "other");
        await page.Mouse.ClickAsync(x, y, new() { Button = MouseButton.Right });
        await Assertions.Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#menu-expand")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#menu-collapse")).ToBeHiddenAsync();

        await page.Locator("#menu-expand").ClickAsync();
        await Heard(page, "sedna-graph-expand");
        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();

        (x, y) = await PointOf(page, "console");
        await page.Mouse.ClickAsync(x, y, new() { Button = MouseButton.Right });
        await Assertions.Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#menu-expand")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#menu-collapse")).ToBeHiddenAsync();
        AssertQuiet();
    }

    [Fact]
    public async Task A_group_marked_collapsed_is_folded_when_it_arrives_by_call()
    {
        if (NoBrowser) return;
        // ISednaGraphs.SetDataAsync sends Collapsed; the page has no data-graph-collapse,
        // so the fold plugin is loaded for it.
        var page = await OpenGraph(Graph(inside: "", attrs: "data-graph-eager data-graph-deferred"));
        await Ready(page);

        await page.EvaluateAsync("""
            () => sednaUi.graph.get('g').then(g => g.set({
                nodes: [{ id: 'team', label: 'Platform team', collapsed: true }, { id: 'runner', label: 'build-runner-04', parent: 'team' },
                        { id: 'db', label: 'src-db-14', parent: 'team' }, { id: 'console', label: 'orders-console-01' }],
                edges: [{ source: 'runner', target: 'console' }]
            }))
            """);
        Assert.True(await Folded(page, "team"));
        Assert.False(await Drawn(page, "runner"));

        // One that arrives later is folded as it arrives, and the first stays folded.
        await page.EvaluateAsync("""
            () => sednaUi.graph.get('g').then(g => g.set({
                nodes: [{ id: 'team', label: 'Platform team', collapsed: true }, { id: 'runner', label: 'build-runner-04', parent: 'team' },
                        { id: 'db', label: 'src-db-14', parent: 'team' }, { id: 'console', label: 'orders-console-01' },
                        { id: 'oncall', label: 'On call', collapsed: true }, { id: 'pager', label: 'Pager', parent: 'oncall' }],
                edges: [{ source: 'runner', target: 'console' }]
            }))
            """);
        Assert.True(await Folded(page, "oncall"));
        Assert.False(await Drawn(page, "pager"));
        Assert.True(await Folded(page, "team"));
        AssertQuiet();
    }

    [Fact]
    public async Task New_data_keeps_a_folded_group_folded()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Folding, attrs: "data-graph-eager data-graph-collapse"));
        await Ready(page);

        await page.EvaluateAsync("""
            () => document.querySelector('#g [data-graph-data]').insertAdjacentHTML('beforeend',
                '<li data-node="mail">Mail sender</li><li data-edge data-source="console" data-target="mail"></li>')
            """);
        await page.WaitForFunctionAsync(
            "() => cyOf('g').getElementById('mail').nonempty() && cyOf('g').getElementById('other').hasClass('cy-expand-collapse-collapsed-node')");

        Assert.False(await Drawn(page, "pager"));
        Assert.False(await Folded(page, "team"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_record_added_to_a_folded_group_is_filed_inside_it_and_the_group_stays_folded()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Folding, attrs: "data-graph-eager data-graph-collapse"));
        await Ready(page);

        await page.EvaluateAsync("""
            () => document.querySelector('#g [data-graph-data]').insertAdjacentHTML('beforeend',
                '<li data-node="phone" data-parent="other">Phone</li>')
            """);
        await page.WaitForFunctionAsync(
            "() => (cyOf('g').getElementById('other').data('collapsedChildren') || cyOf('g').collection()).some(n => n.id() === 'phone')");

        Assert.True(await Folded(page, "other"));
        Assert.False(await Drawn(page, "phone"));
        AssertQuiet();
    }

    // ── Drawing links ─────────────────────────────────────────────────────────

    private const string Triangle = """
        <ul class="graph-data" data-graph-data>
          <li data-node="api" data-x="0" data-y="0">Orders API</li>
          <li data-node="db" data-x="320" data-y="0">Orders database</li>
          <li data-node="web" data-x="160" data-y="180">Web shop</li>
          <li data-edge data-source="web" data-target="api">calls</li>
        </ul>
        <div class="graph-tools"><button type="button" data-graph-action="connect" aria-pressed="false" id="connect" aria-label="Draw links"></button></div>
        """;

    private static async Task Draw(IPage page, string from, string to, bool drawing = true)
    {
        var (fx, fy) = await PointOf(page, from);
        var (tx, ty) = await PointOf(page, to);
        await page.Mouse.MoveAsync(fx, fy);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(fx + 10, fy + 4, new() { Steps = 3 });
        await page.Mouse.MoveAsync(tx, ty, new() { Steps = 20 });
        // Released once the line has found the record under the pointer, as a reader's is.
        if (drawing) await page.WaitForFunctionAsync("() => cyOf('g').nodes('.eh-target').nonempty()");
        await page.Mouse.UpAsync();
    }

    [Fact]
    public async Task Dragging_from_one_record_to_another_asks_the_app_for_the_link_and_draws_none_itself()
    {
        if (NoBrowser) return;
        // A drawn link is the app's to add, as a drop is: the line on screen goes, and the
        // record of it comes back through the app's own data.
        var page = await OpenGraph(Graph(inside: Triangle, attrs: "data-graph-eager data-graph-layout=\"preset\" data-graph-connect"));
        await Ready(page);
        Assert.NotNull(await page.Locator("#g").GetAttributeAsync("data-graph-drawing"));
        Assert.Equal("true", await page.Locator("#connect").GetAttributeAsync("aria-pressed"));
        var at = await Positions(page);

        await Draw(page, "api", "db");
        await Heard(page, "sedna-graph-connect");

        var connect = (await Log(page, "sedna-graph-connect")).Single();
        Assert.Equal("api", connect.Detail.GetProperty("source").GetString());
        Assert.Equal("db", connect.Detail.GetProperty("target").GetString());
        Assert.Equal(["web→api"], await Eval<string[]>(page, "() => cyOf('g').edges().map(e => e.id())"));
        // Drawing is not dragging: the record stayed where it was.
        Assert.Equal(at["api"], (await Positions(page))["api"]);

        // The app adds it to its list, and the graph draws it.
        await page.EvaluateAsync("""
            () => document.querySelector('#g [data-graph-data]').insertAdjacentHTML('beforeend',
                '<li data-edge data-source="api" data-target="db">reads</li>')
            """);
        await page.WaitForFunctionAsync("() => cyOf('g').edges().length === 2");
        AssertQuiet();
    }

    [Fact]
    public async Task A_managed_graph_keeps_the_link_the_reader_drew()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Triangle, attrs: "data-graph-eager data-graph-layout=\"preset\" data-graph-connect data-graph-managed"));
        await Ready(page);

        await Draw(page, "db", "web");
        await Heard(page, "sedna-graph-connect");

        await page.WaitForFunctionAsync("() => cyOf('g').edges().length === 2");
        Assert.True(await Eval<bool>(page, "() => cyOf('g').edges('[source = \"db\"][target = \"web\"]').length === 1"));
        AssertQuiet();
    }

    [Fact]
    public async Task The_connect_toggle_switches_between_drawing_links_and_moving_records()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Triangle, attrs: "data-graph-eager data-graph-layout=\"preset\""));
        await Ready(page);
        Assert.Null(await page.Locator("#g").GetAttributeAsync("data-graph-drawing"));

        await page.Locator("#connect").ClickAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('g').hasAttribute('data-graph-drawing')");
        Assert.Equal("true", await page.Locator("#connect").GetAttributeAsync("aria-pressed"));
        Assert.True(Fetched(Requests, "cytoscape-edgehandles.js"));
        await Draw(page, "web", "db");
        await Heard(page, "sedna-graph-connect");

        await page.Locator("#connect").ClickAsync();
        await page.WaitForFunctionAsync("() => !document.getElementById('g').hasAttribute('data-graph-drawing')");
        Assert.Equal("false", await page.Locator("#connect").GetAttributeAsync("aria-pressed"));
        // Off again, a drag moves the record and draws nothing.
        var before = (await Positions(page))["api"];
        await Draw(page, "api", "db", drawing: false);
        Assert.Single(await Log(page, "sedna-graph-connect"));
        Assert.NotEqual(before, (await Positions(page))["api"]);
        AssertQuiet();
    }

    // ── The minimap ───────────────────────────────────────────────────────────

    private const string WithMinimap = Services + """<canvas class="graph-minimap" data-graph-minimap aria-hidden="true" id="minimap"></canvas>""";

    [Fact]
    public async Task The_minimap_draws_the_drawing_in_miniature()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: WithMinimap));
        await Ready(page);

        await page.WaitForFunctionAsync("""
            () => { const c = document.getElementById('minimap'); if (!c.width) return false;
                    const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
                    let painted = 0; for (let i = 3; i < d.length; i += 4) if (d[i] > 0) painted++;
                    return painted > 50; }
            """);
        // Drawn at the canvas's own size, not stretched from a default.
        Assert.True(await Eval<bool>(page, "() => { const c = document.getElementById('minimap'); return c.width >= c.clientWidth && c.clientWidth > 0; }"));
        AssertQuiet();
    }

    [Fact]
    public async Task Pressing_the_minimap_moves_the_view_there()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: WithMinimap));
        await Ready(page);
        // The minimap repaints on a timer once the records' drawn size has settled.
        await page.EvaluateAsync("() => { window.before = document.getElementById('minimap').toDataURL(); }");
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.zoom(3))");
        await page.WaitForFunctionAsync("() => document.getElementById('minimap').toDataURL() !== before");
        // Records are held at their screen size once the zoom settles; press after that.
        await page.WaitForFunctionAsync("() => cyOf('g').nodes().first().data('zoom') > 1");

        var box = (await page.Locator("#minimap").BoundingBoxAsync())!;
        await page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);

        // The middle of the minimap is the middle of the drawing; it is now the middle of the view.
        var offset = await Eval<double[]>(page, """
            () => { const cy = cyOf('g'); const b = cy.nodes().boundingBox({ includeLabels: false, includeOverlays: false });
                    const x = cy.pan().x + cy.zoom() * (b.x1 + b.w / 2), y = cy.pan().y + cy.zoom() * (b.y1 + b.h / 2);
                    return [x - cy.width() / 2, y - cy.height() / 2]; }
            """);
        Assert.InRange(Math.Abs(offset[0]), 0, 4);
        Assert.InRange(Math.Abs(offset[1]), 0, 4);

        // The left of it is the left of the drawing.
        var before = await Eval<double>(page, "() => cyOf('g').pan().x");
        await page.Mouse.ClickAsync(box.X + box.Width * 0.2f, box.Y + box.Height / 2);
        Assert.True(await Eval<double>(page, "() => cyOf('g').pan().x") > before);
        AssertQuiet();
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_svg_export_is_a_well_formed_file_of_the_whole_drawing_with_a_title_per_record()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);
        // Zoomed in on one corner: the export is still the whole drawing.
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.zoom(4))");

        var svg = await page.EvaluateAsync<JsonElement>("""
            async () => {
                const blob = await sednaUi.graph.get('g').then(g => g.export('svg'));
                const text = await blob.text();
                const doc = new DOMParser().parseFromString(text, 'image/svg+xml');
                const api = cyOf('g').getElementById('api');
                return {
                    type: blob.type, text,
                    broken: doc.getElementsByTagName('parsererror').length,
                    root: doc.documentElement.localName,
                    title: doc.querySelector('svg > title')?.textContent,
                    records: [...doc.querySelectorAll('g.graph-record > title')].map(t => t.textContent),
                    links: doc.querySelectorAll('g.graph-link').length,
                    apiFill: [...doc.querySelectorAll('g.graph-record')].find(g => g.querySelector('title').textContent.startsWith('Orders API'))
                        ?.querySelector('ellipse, rect, polygon')?.getAttribute('fill'),
                    canvasFill: api.style('background-color')
                };
            }
            """);
        Assert.Equal("image/svg+xml", svg.GetProperty("type").GetString());
        Assert.Equal(0, svg.GetProperty("broken").GetInt32());
        Assert.Equal("svg", svg.GetProperty("root").GetString());
        Assert.Equal("Service dependencies", svg.GetProperty("title").GetString());
        var titles = svg.GetProperty("records").EnumerateArray().Select(t => t.GetString()).ToList();
        Assert.Equal(7, titles.Count);
        Assert.Contains("Orders API — orders-console-01", titles);
        Assert.Contains("Archive", titles);
        Assert.Equal(5, svg.GetProperty("links").GetInt32());
        // Every colour is the one the canvas resolved, not a literal of the export's own.
        Assert.Equal(Squash(svg.GetProperty("canvasFill").GetString()), Squash(svg.GetProperty("apiFill").GetString()));
        var text = svg.GetProperty("text").GetString()!;
        Assert.DoesNotMatch(new Regex(@"#[0-9a-f]{3,8}\b", RegexOptions.IgnoreCase), text);
        Assert.DoesNotContain("var(", text, StringComparison.Ordinal);
        AssertQuiet();
    }

    private static string Squash(string? s) => Regex.Replace(s ?? "", @"\s+", "");

    [Fact]
    public async Task The_png_export_is_a_png_of_the_whole_drawing_at_twice_its_size()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.zoom(4))");

        var png = await page.EvaluateAsync<JsonElement>("""
            async () => {
                const blob = await sednaUi.graph.get('g').then(g => g.export('png'));
                const head = [...new Uint8Array(await blob.slice(0, 8).arrayBuffer())];
                const bitmap = await createImageBitmap(blob);
                const whole = cyOf('g').elements().boundingBox();
                return { type: blob.type, size: blob.size, head, width: bitmap.width, drawn: whole.w };
            }
            """);
        Assert.Equal("image/png", png.GetProperty("type").GetString());
        Assert.True(png.GetProperty("size").GetInt32() > 1000);
        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], png.GetProperty("head").EnumerateArray().Select(b => b.GetInt32()));
        var ratio = png.GetProperty("width").GetDouble() / png.GetProperty("drawn").GetDouble();
        Assert.InRange(ratio, 1.8, 2.2);
        AssertQuiet();
    }

    [Fact]
    public async Task The_export_buttons_download_a_file_named_by_data_graph_filename_else_the_graphs_id()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Services + """
            <div class="graph-tools">
              <button type="button" data-graph-action="export-svg" data-graph-filename="service-map" id="svg">SVG</button>
              <button type="button" data-graph-action="export-png" id="png">PNG</button>
            </div>
            """));
        await Ready(page);

        var svg = await page.RunAndWaitForDownloadAsync(() => page.Locator("#svg").ClickAsync());
        Assert.Equal("service-map.svg", svg.SuggestedFilename);
        var file = await svg.PathAsync();
        Assert.StartsWith("<svg", await File.ReadAllTextAsync(file!), StringComparison.Ordinal);

        var png = await page.RunAndWaitForDownloadAsync(() => page.Locator("#png").ClickAsync());
        Assert.Equal("g.png", png.SuggestedFilename);
        var bytes = await File.ReadAllBytesAsync((await png.PathAsync())!);
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, bytes[..4]);
        AssertQuiet();
    }
}
