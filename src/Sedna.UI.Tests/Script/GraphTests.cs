using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The graph from markup to drawing: how it starts, what it reads, how it follows the
/// app's list, and how it is taken down — asserted against <c>docs/graph.md</c>.
/// </summary>
/// <remarks>
/// The front door in <c>Sedna.UI.js</c> is on every page and imports the module, which
/// imports the engine, by relative URL. These tests load all three from the shipped files
/// at their <c>wwwroot</c> paths, so a broken import, a missing file or a wrong path fails
/// here exactly as it would in an app.
/// </remarks>
public class GraphTests : GraphTestBase
{
    // ── Starting ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_graph_draws_its_markup_list_and_says_it_is_ready()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());

        await State(page, "g", "ready");
        await Heard(page, "sedna-graph-ready");

        Assert.Equal(7, await Eval<int>(page, "() => cyOf('g').nodes().length"));
        Assert.Equal(5, await Eval<int>(page, "() => cyOf('g').edges().length"));
        // Drawn on a canvas inside the app's .graph-canvas, and the wait is down.
        Assert.True(await Eval<bool>(page, "() => document.querySelector('#g .graph-canvas canvas') !== null"));
        await Assertions.Expect(page.Locator("#g .graph-wait")).ToBeHiddenAsync();

        var ready = (await Log(page, "sedna-graph-ready")).Single();
        Assert.Equal("g", ready.On);
        Assert.Equal(7, ready.Detail.GetProperty("nodes").GetInt32());
        Assert.Equal(5, ready.Detail.GetProperty("edges").GetInt32());
        Assert.Equal(7, ready.Detail.GetProperty("totalNodes").GetInt32());
        Assert.Equal(5, ready.Detail.GetProperty("totalEdges").GetInt32());
        Assert.Equal(0, ready.Detail.GetProperty("matches").GetInt32());
        Assert.Equal(JsonValueKind.Null, ready.Detail.GetProperty("selected").ValueKind);
        AssertQuiet();
    }

    [Fact]
    public async Task A_page_without_a_graph_never_downloads_the_module_or_the_engine()
    {
        if (NoBrowser) return;
        // Sedna.UI.js is on every page of every app; the engine is a few hundred kilobytes
        // that almost none of them need.
        var page = await OpenGraph("<main><p>No graph here.</p></main>");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.DoesNotContain(Requests, r => r.Url.Contains("Sedna.UI.graph.js", StringComparison.Ordinal) || r.Url.Contains("/lib/cytoscape/", StringComparison.Ordinal));
        Assert.Equal("object", await Eval<string>(page, "() => typeof sednaUi.graph"));
        AssertQuiet();
    }

    private const string FarBelow = """<div style="height: 400vh" aria-hidden="true"></div>""";

    [Fact]
    public async Task A_graph_far_below_the_fold_starts_only_when_it_is_scrolled_near()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(FarBelow + Graph(attrs: ""));
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Not started, and nothing downloaded for it.
        Assert.Null(await StateOf(page));
        Assert.DoesNotContain(Requests, r => r.Url.Contains("Sedna.UI.graph.js", StringComparison.Ordinal));

        // Within a screen of the viewport is near enough: it is drawn by the time it is seen.
        await page.EvaluateAsync("() => window.scrollTo(0, document.getElementById('g').offsetTop - 1.5 * innerHeight)");
        await State(page, "g", "ready");
        Assert.Contains(Requests, r => r.Url.EndsWith("/js/Sedna.UI.graph.js", StringComparison.Ordinal));
        Assert.Contains(Requests, r => r.Url.EndsWith("/lib/cytoscape/cytoscape.js", StringComparison.Ordinal));
        AssertQuiet();
    }

    [Fact]
    public async Task An_eager_graph_starts_at_once_wherever_it_is()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(FarBelow + Graph(attrs: "data-graph-eager"));

        await State(page, "g", "ready");
        Assert.Equal(0, await Eval<double>(page, "() => scrollY"));
        AssertQuiet();
    }

    [Fact]
    public async Task Init_starts_every_graph_in_a_root_at_once_and_counts_them()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(FarBelow + $"""
            <section id="here">
              {Graph(attrs: "", id: "one")}
              {Graph(attrs: "", id: "two")}
            </section>
            {Graph(attrs: "", id: "elsewhere")}
            """);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Null(await StateOf(page, "one"));

        var started = await Eval<int>(page, "() => sednaUi.graph.init(document.getElementById('here'))");

        Assert.Equal(2, started);
        Assert.Equal("ready", await StateOf(page, "one"));
        Assert.Equal("ready", await StateOf(page, "two"));
        Assert.Null(await StateOf(page, "elsewhere"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_deferred_graph_keeps_its_wait_up_until_its_records_arrive_by_call()
    {
        if (NoBrowser) return;
        // The Blazor pattern: the element renders with no records, SetDataAsync follows
        // from OnAfterRenderAsync. An empty state flashing in between reads as "there is
        // nothing" to everyone who looks during that moment.
        var page = await OpenGraph(Graph(inside: "", attrs: "data-graph-eager data-graph-deferred"));

        await Ready(page);
        Assert.Equal("loading", await StateOf(page));
        await Assertions.Expect(page.Locator("#g .graph-wait")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#g .graph-empty").First).ToBeHiddenAsync();
        // Nor is it ready: ready is the first drawing, and there is none yet.
        Assert.Empty(await Log(page, "sedna-graph-ready"));

        var stats = await page.EvaluateAsync<JsonElement>("""
            () => sednaUi.graph.get(document.getElementById('g')).then(g => g.set({
                nodes: [{ id: 'api', label: 'Orders API' }, { id: 'db', label: 'Orders database' }],
                edges: [{ source: 'api', target: 'db', label: 'reads' }]
            }))
            """);

        Assert.Equal(2, stats.GetProperty("nodes").GetInt32());
        Assert.Equal(1, stats.GetProperty("edges").GetInt32());
        Assert.Equal("ready", await StateOf(page));
        await Assertions.Expect(page.Locator("#g .graph-wait")).ToBeHiddenAsync();
        var ready = (await Log(page, "sedna-graph-ready")).Single();
        Assert.Equal(2, ready.Detail.GetProperty("nodes").GetInt32());

        // Only the first set is a first drawing.
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.set({ nodes: [{ id: 'api' }], edges: [] }))");
        Assert.Single(await Log(page, "sedna-graph-ready"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_deferred_graph_that_is_handed_nothing_says_it_is_empty()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: "", attrs: "data-graph-eager data-graph-deferred"));
        await Ready(page);

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.set({ nodes: [], edges: [] }))");

        Assert.Equal("empty", await StateOf(page));
        await Assertions.Expect(page.Locator("#g .graph-empty:not(.graph-empty--filtered, .graph-empty--error)")).ToBeVisibleAsync();
        AssertQuiet();
    }

    [Fact]
    public async Task A_graph_with_no_records_shows_the_apps_empty_state_and_only_that_one()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """<ul class="graph-data" data-graph-data></ul>"""));

        await State(page, "g", "empty");
        await Assertions.Expect(page.Locator("#g .graph-empty:not(.graph-empty--filtered, .graph-empty--error)")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#g .graph-empty--filtered")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#g .graph-empty--error")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#g .graph-wait")).ToBeHiddenAsync();
        AssertQuiet();
    }

    [Fact]
    public async Task Records_at_a_url_are_fetched_as_json_with_the_pages_cookies()
    {
        if (NoBrowser) return;
        // The records never cross a Blazor circuit: the browser asks for them itself, as
        // the signed-in reader.
        const string json = """
            { "nodes": [ { "id": "api", "label": "Orders API", "kind": "service", "tone": "2", "fields": { "owner": "Alex Fischer" } },
                         { "id": "db", "label": "Orders database", "kind": "store" } ],
              "edges": [ { "source": "api", "target": "db", "label": "reads" } ] }
            """;
        var page = await OpenGraph(
            Graph(inside: "", attrs: "data-graph-eager data-graph-src=\"/api/graph\""),
            head: "<script>document.cookie = 'session=alex-fischer; path=/';</script>",
            serve: new Dictionary<string, Served> { ["/api/graph"] = new(json) });

        await State(page, "g", "ready");

        Assert.Equal(["api", "db"], await Shown(page));
        Assert.Equal("Alex Fischer", await Eval<string>(page, "() => cyOf('g').getElementById('api').data('fields').owner"));
        var request = Requests.Single(r => r.Url.EndsWith("/api/graph", StringComparison.Ordinal));
        var headers = await request.AllHeadersAsync();
        Assert.Contains("application/json", headers["accept"], StringComparison.Ordinal);
        Assert.True(headers.TryGetValue("cookie", out var cookie), "The request carried no cookie.");
        Assert.Contains("session=alex-fischer", cookie, StringComparison.Ordinal);
        AssertQuiet();
    }

    [Fact]
    public async Task Records_that_cannot_be_fetched_show_the_apps_error_state()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(
            Graph(inside: "", attrs: "data-graph-eager data-graph-src=\"/api/graph\""),
            serve: new Dictionary<string, Served> { ["/api/graph"] = new("{}", Status: 500) });

        await State(page, "g", "error");
        await Assertions.Expect(page.Locator("#g .graph-empty--error")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#g .graph-wait")).ToBeHiddenAsync();
        // Said in the console, with what went wrong — as a warning: the app's endpoint failed,
        // not the script.
        Assert.Contains(Warnings, e => e.Contains("/api/graph", StringComparison.Ordinal) && e.Contains("500", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_graph_whose_records_could_not_be_fetched_stays_alive_and_reload_recovers_it()
    {
        if (NoBrowser) return;
        // The server was down for a moment. The graph and its controls are still there, and
        // the reload button is the reader's way back — no page reload, no circuit restart.
        var page = await OpenGraph(
            Graph(inside: """<div class="graph-tools"><button type="button" data-graph-action="reload" id="reload" aria-label="Try again"></button></div>""",
                attrs: "data-graph-eager data-graph-src=\"/api/graph\""),
            serve: new Dictionary<string, Served> { ["/api/graph"] = new("{}", Status: 503) });
        await Ready(page);
        Assert.Equal("error", await StateOf(page));
        // Nothing the app calls rejects in the meantime.
        Assert.True(await Eval<bool>(page, "() => sednaUi.graph.invoke('g', 'stats').then(s => s.totalNodes === 0, () => false)"));

        // Still down: still the error, and still no exception into the caller.
        var stats = await page.EvaluateAsync<JsonElement>("() => sednaUi.graph.invoke('g', 'reload')");
        Assert.Equal(0, stats.GetProperty("totalNodes").GetInt32());
        Assert.Equal("error", await StateOf(page));

        await page.RouteAsync("**/api/graph", route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Body = """{ "nodes": [ { "id": "api", "label": "Orders API" }, { "id": "db", "label": "Orders database" } ], "edges": [ { "source": "api", "target": "db" } ] }""",
        }));
        await page.Locator("#reload").ClickAsync();

        await State(page, "g", "ready");
        Assert.Equal(["api", "db"], await Shown(page));
        await Assertions.Expect(page.Locator("#g .graph-empty--error")).ToBeHiddenAsync();
        Assert.All(Errors, e => Assert.Contains("503", e, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_graph_without_a_canvas_says_so_and_goes_into_its_error_state()
    {
        if (NoBrowser) return;
        var page = await OpenGraph("""
            <div class="graph" data-graph id="g" data-graph-eager aria-label="Broken">
              <ul class="graph-data" data-graph-data><li data-node="a">A</li></ul>
            </div>
            """);

        await State(page, "g", "error");
        Assert.Contains(Errors, e => e.Contains("graph-canvas", StringComparison.Ordinal));
    }

    // ── The model ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_documented_record_attribute_reaches_the_engine()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="api" data-label="Orders API" data-kind="service" data-tone="2" data-tone-team="5"
                  data-shape="diamond" data-icon="ri-server-line" data-group="orders" data-cluster="shop"
                  data-weight="9" data-root data-hub data-href="/records/api" data-meta="orders-console-01"
                  data-tags="core billing" data-owner="Alex Fischer" data-team-lead="Alex Fischer">The orders API, run by Alex Fischer</li>
              <li data-node="db" data-display="box" data-muted data-weight="1" data-x="120" data-y="-40">Orders database</li>
              <li data-edge data-source="api" data-target="db">The orders API reads the orders database</li>
            </ul>
            """));
        await Ready(page);

        var api = await page.EvaluateAsync<JsonElement>("""
            () => {
                const n = cyOf('g').getElementById('api');
                const d = n.data();
                return { label: d.label, kind: d.kind, tone: d.tone, tones: d.tones, shape: n.style('shape'), icon: d.icon,
                         image: String(n.style('background-image')), group: d.group, cluster: d.cluster, root: d.root,
                         weight: n.width(), fontWeight: n.style('font-weight'), href: d.href, meta: d.meta, tags: d.tags,
                         fields: d.fields, border: flat(n.style('border-color')), brand: paintOf('var(--brand)') };
            }
            """);
        Assert.Equal("Orders API", api.GetProperty("label").GetString());
        Assert.Equal("service", api.GetProperty("kind").GetString());
        Assert.Equal("2", api.GetProperty("tone").GetString());
        Assert.Equal("5", api.GetProperty("tones").GetProperty("team").GetString());
        Assert.Equal("round-diamond", api.GetProperty("shape").GetString());
        Assert.Equal("ri-server-line", api.GetProperty("icon").GetString());
        Assert.StartsWith("data:image/png", api.GetProperty("image").GetString(), StringComparison.Ordinal);
        Assert.Equal("orders", api.GetProperty("group").GetString());
        Assert.Equal("shop", api.GetProperty("cluster").GetString());
        // The root is ringed in the brand colour, and a hub is named in bold.
        Assert.True(api.GetProperty("root").GetBoolean());
        Assert.Equal(api.GetProperty("brand").GetString(), api.GetProperty("border").GetString());
        Assert.Equal("600", api.GetProperty("fontWeight").GetString());
        Assert.Equal("/records/api", api.GetProperty("href").GetString());
        Assert.Equal("orders-console-01", api.GetProperty("meta").GetString());
        Assert.Equal(["core", "billing"], api.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
        // Any other data-* is the app's own field, under its dataset name.
        Assert.Equal("Alex Fischer", api.GetProperty("fields").GetProperty("owner").GetString());
        Assert.Equal("Alex Fischer", api.GetProperty("fields").GetProperty("teamLead").GetString());

        var db = await page.EvaluateAsync<JsonElement>("""
            () => {
                const n = cyOf('g').getElementById('db');
                return { display: n.data('display'), shape: n.style('shape'), muted: n.data('muted'), x: n.data('x'), y: n.data('y'),
                         valign: n.style('text-valign'), width: n.width() };
            }
            """);
        Assert.Equal("box", db.GetProperty("display").GetString());
        Assert.Equal("round-rectangle", db.GetProperty("shape").GetString());
        Assert.Equal("center", db.GetProperty("valign").GetString());
        Assert.True(db.GetProperty("muted").GetBoolean());
        Assert.Equal(120, db.GetProperty("x").GetDouble());
        Assert.Equal(-40, db.GetProperty("y").GetDouble());

        // Heavier is larger: the weight is the size, not the number of links.
        Assert.True(await Eval<bool>(page, """
            () => {
                const cy = cyOf('g');
                cy.getElementById('db').data('display', 'dot');
                return cy.getElementById('api').width() > cy.getElementById('db').width();
            }
            """));
        AssertQuiet();
    }

    [Fact]
    public async Task Every_documented_link_attribute_reaches_the_engine()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(attrs: "data-graph-eager data-graph-arrows=\"target\"", inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="api">Orders API</li>
              <li data-node="db">Orders database</li>
              <li data-node="queue">Order queue</li>
              <li data-edge="reads" data-source="api" data-target="db" data-label="reads" data-kind="reads" data-tone="danger"
                  data-line="dashed" data-weight="heavy" data-arrow="both" data-muted data-via="replica">The orders API reads the orders database</li>
              <li data-edge data-source="api" data-target="queue" data-line="dotted" data-weight="light">The orders API writes to the queue</li>
              <li data-edge data-source="api" data-target="queue" data-weight="3">And again</li>
            </ul>
            """));
        await Ready(page);

        var reads = await page.EvaluateAsync<JsonElement>("""
            () => {
                const e = cyOf('g').getElementById('reads');
                return { label: e.data('label'), kind: e.data('kind'), colour: flat(e.style('line-color')), danger: paintOf('var(--danger-solid)'),
                         line: e.style('line-style'), weight: e.data('weight'), source: e.style('source-arrow-shape'),
                         target: e.style('target-arrow-shape'), muted: e.data('muted'), via: e.data('fields').via };
            }
            """);
        Assert.Equal("reads", reads.GetProperty("label").GetString());
        Assert.Equal("reads", reads.GetProperty("kind").GetString());
        Assert.Equal(reads.GetProperty("danger").GetString(), reads.GetProperty("colour").GetString());
        Assert.Equal("dashed", reads.GetProperty("line").GetString());
        Assert.Equal(2, reads.GetProperty("weight").GetDouble());
        Assert.Equal("triangle", reads.GetProperty("source").GetString());
        Assert.Equal("triangle", reads.GetProperty("target").GetString());
        Assert.True(reads.GetProperty("muted").GetBoolean());
        Assert.Equal("replica", reads.GetProperty("via").GetString());

        // An unnamed link is named after its ends, and a second between the same two is
        // told apart — so a re-read of the list finds the same line again.
        var unnamed = await page.EvaluateAsync<JsonElement>("""
            () => cyOf('g').edges('[source = "api"][target = "queue"]').map(e => ({
                id: e.id(), line: e.style('line-style'), weight: e.data('weight'),
                source: e.style('source-arrow-shape'), target: e.style('target-arrow-shape') }))
            """);
        Assert.Equal(["api→queue", "api→queue#2"], unnamed.EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        var dotted = unnamed[0];
        Assert.Equal("dotted", dotted.GetProperty("line").GetString());
        Assert.True(dotted.GetProperty("weight").GetDouble() < 1);
        // data-graph-arrows is the default for a link that does not say.
        Assert.Equal("none", dotted.GetProperty("source").GetString());
        Assert.Equal("triangle", dotted.GetProperty("target").GetString());
        Assert.Equal(3, unnamed[1].GetProperty("weight").GetDouble());
        AssertQuiet();
    }

    [Fact]
    public async Task Every_documented_shape_is_drawn_and_an_unknown_one_is_a_circle_with_a_warning()
    {
        if (NoBrowser) return;
        // A polygon is drawn rounded: a hard corner reads as a different shape at 14 pixels.
        var expected = new Dictionary<string, string>
        {
            ["circle"] = "ellipse", ["square"] = "rectangle", ["rounded"] = "round-rectangle",
            ["diamond"] = "round-diamond", ["hexagon"] = "round-hexagon", ["octagon"] = "round-octagon",
            ["pentagon"] = "round-pentagon", ["triangle"] = "round-triangle", ["tag"] = "round-tag",
            ["star"] = "star", ["barrel"] = "barrel", ["rhomboid"] = "rhomboid", ["vee"] = "vee",
            ["blob"] = "ellipse",
        };
        var records = string.Concat(expected.Keys.Select(s => $"""<li data-node="{s}" data-shape="{s}">{s}</li>"""));
        var page = await OpenGraph(Graph(inside: $"""<ul class="graph-data" data-graph-data>{records}</ul>"""));
        await Ready(page);

        var drawn = (await page.EvaluateAsync<string[][]>("() => cyOf('g').nodes().map(n => [n.id(), n.style('shape')])"))
            .ToDictionary(p => p[0], p => p[1]);
        foreach (var (shape, engine) in expected) Assert.True(drawn[shape] == engine, $"{shape} was drawn as {drawn[shape]}, not {engine}.");
        Assert.Contains(Warnings, w => w.Contains("\"blob\" is not a shape", StringComparison.Ordinal));
        AssertQuiet("is not a shape");
    }

    [Fact]
    public async Task An_items_text_is_its_label_unless_data_label_says_otherwise()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="api">
                  Orders
                  API
              </li>
              <li data-node="db" data-label="Database">The orders database, read by the orders API</li>
              <li data-node="bare"></li>
            </ul>
            """));
        await Ready(page);

        Assert.Equal(["Orders API", "Database", "bare"], await Eval<string[]>(page,
            "() => ['api', 'db', 'bare'].map(id => cyOf('g').getElementById(id).data('label'))"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_record_with_a_parent_is_drawn_inside_it()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="team" data-tone="3">Platform team</li>
              <li data-node="runner" data-parent="team">build-runner-04</li>
              <li data-node="db" data-parent="team">src-db-14</li>
              <li data-node="web">orders-console-01</li>
              <li data-edge data-source="runner" data-target="web">deploys</li>
            </ul>
            """));
        await Ready(page);

        var inside = await page.EvaluateAsync<JsonElement>("""
            () => {
                const cy = cyOf('g');
                const team = cy.getElementById('team');
                const box = team.boundingBox();
                const within = id => { const p = cy.getElementById(id).position(); return p.x > box.x1 && p.x < box.x2 && p.y > box.y1 && p.y < box.y2; };
                return { parent: cy.getElementById('runner').parent().id(), isParent: team.isParent(),
                         children: team.children().map(n => n.id()).sort(), runner: within('runner'), db: within('db'), web: within('web') };
            }
            """);
        Assert.Equal("team", inside.GetProperty("parent").GetString());
        Assert.True(inside.GetProperty("isParent").GetBoolean());
        Assert.Equal(["db", "runner"], inside.GetProperty("children").EnumerateArray().Select(e => e.GetString()));
        Assert.True(inside.GetProperty("runner").GetBoolean());
        Assert.True(inside.GetProperty("db").GetBoolean());
        Assert.False(inside.GetProperty("web").GetBoolean());
        AssertQuiet();
    }

    [Fact]
    public async Task The_preset_layout_puts_each_record_where_its_x_and_y_say()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(attrs: "data-graph-eager data-graph-layout=\"preset\"", inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="a" data-x="0" data-y="0">A</li>
              <li data-node="b" data-x="300" data-y="0">B</li>
              <li data-node="c" data-x="150" data-y="-120.5">C</li>
              <li data-edge data-source="a" data-target="b">a to b</li>
            </ul>
            """));
        await Ready(page);

        var at = await Positions(page);
        Assert.Equal((0d, 0d), at["a"]);
        Assert.Equal((300d, 0d), at["b"]);
        Assert.Equal((150d, -120.5d), at["c"]);
        AssertQuiet();
    }

    [Fact]
    public async Task A_link_to_a_record_that_is_not_there_is_dropped_with_a_warning_and_the_rest_is_drawn()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="api">Orders API</li>
              <li data-node="db">Orders database</li>
              <li data-edge data-source="api" data-target="db">reads</li>
              <li data-edge data-source="api" data-target="gone">writes to a record nobody listed</li>
            </ul>
            """));

        await State(page, "g", "ready");
        Assert.Equal(["api→db"], await ShownLinks(page));
        Assert.Contains(Warnings, w => w.StartsWith("Sedna.UI graph:", StringComparison.Ordinal) && w.Contains("not in the graph", StringComparison.Ordinal));
        AssertQuiet("not in the graph");
    }

    [Fact]
    public async Task A_duplicate_id_keeps_the_first_and_warns_about_the_second()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="api">Orders API</li>
              <li data-node="api">Another orders API</li>
              <li data-node="db">Orders database</li>
              <li data-edge="api" data-source="api" data-target="db">A link that takes a record's id</li>
              <li data-edge="reads" data-source="api" data-target="db">reads</li>
              <li data-edge="reads" data-source="db" data-target="api">reads back</li>
            </ul>
            """));

        await State(page, "g", "ready");
        Assert.Equal(["api", "db"], await Shown(page));
        Assert.Equal("Orders API", await Eval<string>(page, "() => cyOf('g').getElementById('api').data('label')"));
        Assert.Equal(["reads"], await ShownLinks(page));
        Assert.Equal("api", await Eval<string>(page, "() => cyOf('g').getElementById('reads').data('source')"));
        Assert.Contains(Warnings, w => w.Contains("two records share the id \"api\"", StringComparison.Ordinal));
        Assert.Contains(Warnings, w => w.Contains("\"api\" is used twice", StringComparison.Ordinal));
        Assert.Contains(Warnings, w => w.Contains("\"reads\" is used twice", StringComparison.Ordinal));
        AssertQuiet("share the id", "is used twice");
    }

    [Fact]
    public async Task A_parent_that_is_missing_or_a_cycle_of_parents_is_drawn_without_one()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="a" data-parent="b">A</li>
              <li data-node="b" data-parent="a">B</li>
              <li data-node="c" data-parent="nobody">C</li>
            </ul>
            """));

        await State(page, "g", "ready");
        Assert.Equal(["a", "b", "c"], await Shown(page));
        Assert.Equal(1, await Eval<int>(page, "() => cyOf('g').nodes().filter(n => n.isChild()).length"));
        Assert.False(await Eval<bool>(page, "() => cyOf('g').getElementById('c').isChild()"));
        Assert.Contains(Warnings, w => w.Contains("form a cycle", StringComparison.Ordinal));
        Assert.Contains(Warnings, w => w.Contains("\"nobody\", which is not a record", StringComparison.Ordinal));
        AssertQuiet("form a cycle", "which is not a record");
    }

    [Fact]
    public async Task A_colour_is_not_a_tone_and_the_record_is_painted_in_the_brand_instead()
    {
        if (NoBrowser) return;
        // Every colour on the canvas is a token, so the theme reaches it. A colour the app
        // hands over would be the one thing no theme, variant or forced-colours mode moves.
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="api" data-tone="#ff0000">Orders API</li>
              <li data-node="db" data-tone="rgb(255, 0, 0)">Orders database</li>
            </ul>
            """) + """<span class="graph-swatch series-1" id="brand"></span>""");
        await Ready(page);

        var colours = await Eval<string[]>(page, """
            () => [flat(cyOf('g').getElementById('api').style('background-color')),
                   flat(cyOf('g').getElementById('db').style('background-color')),
                   paintOf(getComputedStyle(document.getElementById('brand')).color)]
            """);
        Assert.Equal(colours[2], colours[0]);
        Assert.Equal(colours[2], colours[1]);
        Assert.NotEqual("rgb(255,0,0)", colours[0]);
        Assert.Contains(Warnings, w => w.Contains("\"#ff0000\" is not a tone", StringComparison.Ordinal));
        AssertQuiet("is not a tone");
    }

    [Fact]
    public async Task Every_documented_tone_is_the_colour_of_the_series_class_of_the_same_name()
    {
        if (NoBrowser) return;
        // A legend swatch wearing .series-3 and a record with data-tone="3" are one colour
        // by construction — in every theme, which is why neither is a literal.
        string[] series = ["1", "2", "3", "4", "5", "6", "go", "warn", "danger", "info", "muted"];
        var records = string.Concat(series.Select(t => $"""<li data-node="r-{t}" data-tone="{t}">Tone {t}</li>"""));
        var swatches = string.Concat(series.Select(t => $"""<span class="graph-swatch series-{t}" id="s-{t}"></span>"""));
        var page = await OpenGraph(Graph(inside: $"""
            <ul class="graph-data" data-graph-data>{records}
              <li data-node="r-brand" data-tone="brand">Brand</li><li data-node="r-accent" data-tone="accent">Accent</li>
              <li data-node="r-none">No tone</li>
            </ul>
            """) + swatches);
        await Ready(page);

        var pairs = await page.EvaluateAsync<string[][]>("""
            tones => tones.map(t => [t, flat(cyOf('g').getElementById('r-' + t).style('background-color')),
                                     paintOf(getComputedStyle(document.getElementById('s-' + t)).color)])
            """, series);
        foreach (var pair in pairs) Assert.True(pair[1] == pair[2], $"Tone {pair[0]} was painted {pair[1]}, its swatch {pair[2]}.");
        var named = await Eval<string[]>(page, """
            () => ['brand', 'accent', 'none'].map(t => flat(cyOf('g').getElementById('r-' + t).style('background-color')))
                .concat([paintOf('var(--brand)'), paintOf('var(--accent)'), paintOf(getComputedStyle(document.getElementById('s-1')).color)])
            """);
        Assert.Equal(named[3], named[0]);
        Assert.Equal(named[4], named[1]);
        // No tone is series 1, the brand.
        Assert.Equal(named[5], named[2]);
        AssertQuiet();
    }

    [Fact]
    public async Task A_graph_can_start_on_one_records_neighbourhood()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(attrs: "data-graph-eager data-graph-focus=\"web\""));
        await Ready(page);
        Assert.Equal(["api", "cache", "web"], await Shown(page));

        var two = await OpenGraph(Graph(attrs: "data-graph-eager data-graph-focus=\"web\" data-graph-focus-depth=\"2\""));
        await Ready(two);
        Assert.Equal(["api", "cache", "db", "queue", "runner", "web"], await Shown(two));
        AssertQuiet();
    }

    // Which of the canvases in .graph-canvas hold a WebGL context: getContext('2d') answers
    // null for a canvas that already has one of another kind.
    private const string WebglLayers = """
        id => [...document.querySelectorAll('#' + id + ' .graph-canvas canvas')].filter(c => c.getContext('2d') === null).length
        """;

    [Fact]
    public async Task The_canvas_renderer_draws_unless_the_app_chooses_webgl()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(id: "plain") + """<div id="later"></div>""");
        await Ready(page, "plain");
        Assert.Equal(0, await page.EvaluateAsync<int>(WebglLayers, "plain"));

        // The WebGL half needs a browser that has WebGL 2 at all.
        if (!await Eval<bool>(page, "() => !!document.createElement('canvas').getContext('webgl2')")) return;
        await page.EvaluateAsync("html => { document.getElementById('later').innerHTML = html; }",
            Graph(id: "gpu", attrs: "data-graph-eager data-graph-renderer=\"webgl\""));
        await Ready(page, "gpu");
        Assert.True(await page.EvaluateAsync<int>(WebglLayers, "gpu") > 0);
        Assert.Equal("ready", await StateOf(page, "gpu"));
        // Headless Chromium draws WebGL in software and says so; that is the browser's own
        // console, not the graph's.
        AssertQuiet("software WebGL", "GL Driver Message");
    }

    [Fact]
    public async Task Get_refuses_an_element_that_is_not_a_graph()
    {
        if (NoBrowser) return;
        var page = await OpenGraph("""<div id="plain"></div>""");

        Assert.True(await Eval<bool>(page, "() => sednaUi.graph.get('plain').then(() => false, e => e instanceof Error)"));
        Assert.True(await Eval<bool>(page, "() => sednaUi.graph.get('nowhere').then(() => false, e => e instanceof Error)"));
        AssertQuiet();
    }

    // ── Live data ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_item_added_to_the_list_is_drawn_beside_what_it_links_to_and_the_rest_stays_put()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);
        var before = await Positions(page);
        await ClearLog(page);

        // What a Blazor render does when a record arrives: two new items in the list.
        await page.EvaluateAsync("""
            () => {
                const list = document.querySelector('#g [data-graph-data]');
                list.insertAdjacentHTML('beforeend',
                    '<li data-node="mail" data-kind="service" data-group="orders">Mail sender</li>' +
                    '<li data-edge data-source="queue" data-target="mail" data-label="feeds">The order queue feeds the mail sender</li>');
            }
            """);
        await Heard(page, "sedna-graph-change");

        Assert.Equal(8, await Eval<int>(page, "() => cyOf('g').nodes().length"));
        Assert.Equal(6, await Eval<int>(page, "() => cyOf('g').edges().length"));
        var after = await Positions(page);
        // Nothing the new record touches has moved.
        foreach (var id in new[] { "api", "db", "web", "cache", "runner", "archive" })
            Assert.Equal(before[id], after[id]);
        // It is placed beside the record it links to, not somewhere at the edge of the map.
        var distance = await Eval<double>(page, """
            () => { const cy = cyOf('g'); const a = cy.getElementById('mail').position(), b = cy.getElementById('queue').position();
                    return Math.hypot(a.x - b.x, a.y - b.y); }
            """);
        Assert.True(distance < 200, $"The new record was placed {distance:0} from the record it links to.");
        var change = (await Log(page, "sedna-graph-change")).Last();
        Assert.Equal(8, change.Detail.GetProperty("totalNodes").GetInt32());
        AssertQuiet();
    }

    [Fact]
    public async Task An_item_removed_from_the_list_leaves_the_drawing_and_the_rest_stays_put()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);
        var before = await Positions(page);
        await ClearLog(page);

        await page.EvaluateAsync("""
            () => {
                document.querySelector('#g [data-node="runner"]').remove();
                document.querySelector('#g [data-edge][data-source="runner"]').remove();
            }
            """);
        await Heard(page, "sedna-graph-change");

        Assert.DoesNotContain("runner", await Shown(page));
        Assert.Equal(4, await Eval<int>(page, "() => cyOf('g').edges().length"));
        var after = await Positions(page);
        foreach (var (id, at) in after) Assert.Equal(before[id], at);
        AssertQuiet();
    }

    [Fact]
    public async Task An_item_renamed_in_the_list_is_restyled_in_place()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);
        var before = await Positions(page);
        await ClearLog(page);

        await page.EvaluateAsync("""
            () => {
                const li = document.querySelector('#g [data-node="db"]');
                li.textContent = 'Orders replica';
                li.setAttribute('data-tone', '6');
            }
            """);
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('db').data('label') === 'Orders replica'");

        var after = await Positions(page);
        foreach (var (id, at) in before) Assert.Equal(at, after[id]);
        Assert.Equal("6", await Eval<string>(page, "() => cyOf('g').getElementById('db').data('tone')"));
        Assert.True(await Eval<bool>(page,
            "() => flat(cyOf('g').getElementById('db').style('background-color')) === paintOf('var(--viz-6)')"));
        AssertQuiet();
    }

    [Fact]
    public async Task An_item_added_inside_a_group_is_drawn_inside_it()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="team">Platform team</li>
              <li data-node="runner" data-parent="team">build-runner-04</li>
              <li data-node="console">orders-console-01</li>
              <li data-edge data-source="runner" data-target="console">deploys</li>
            </ul>
            """));
        await Ready(page);
        await ClearLog(page);

        await page.EvaluateAsync("""
            () => document.querySelector('#g [data-graph-data]').insertAdjacentHTML('beforeend',
                '<li data-node="gpu" data-parent="team">gpu-runner-02</li>')
            """);
        await Heard(page, "sedna-graph-change");

        Assert.Equal("team", await Eval<string>(page, "() => cyOf('g').getElementById('gpu').parent().id()"));
        Assert.True(await Eval<bool>(page, """
            () => { const cy = cyOf('g'), b = cy.getElementById('team').boundingBox(), p = cy.getElementById('gpu').position();
                    return p.x > b.x1 && p.x < b.x2 && p.y > b.y1 && p.y < b.y2; }
            """));
        AssertQuiet();
    }

    [Fact]
    public async Task Set_replaces_the_records_changing_only_what_changed()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: "", attrs: "data-graph-eager data-graph-deferred"));
        await Ready(page);
        await page.EvaluateAsync("""
            () => sednaUi.graph.get('g').then(g => g.set({
                nodes: [{ id: 'api', label: 'Orders API' }, { id: 'db', label: 'Orders database' },
                        { id: 'web', label: 'Web shop' }, { id: 'runner', label: 'Build runner' }],
                edges: [{ source: 'web', target: 'api' }, { source: 'api', target: 'db' }, { source: 'runner', target: 'api' }]
            }))
            """);
        var before = await Positions(page);
        await page.EvaluateAsync("() => { window.keptApi = cyOf('g').getElementById('api'); }");

        var stats = await page.EvaluateAsync<JsonElement>("""
            () => sednaUi.graph.get('g').then(g => g.set({
                nodes: [{ id: 'api', label: 'Orders service' }, { id: 'db', label: 'Orders database' },
                        { id: 'web', label: 'Web shop' }, { id: 'mail', label: 'Mail sender' }],
                edges: [{ source: 'web', target: 'api' }, { source: 'api', target: 'db' }, { source: 'api', target: 'mail' }]
            }))
            """);

        Assert.Equal(4, stats.GetProperty("nodes").GetInt32());
        Assert.Equal(3, stats.GetProperty("edges").GetInt32());
        Assert.Equal(["api", "db", "mail", "web"], await Shown(page));
        // The same element, renamed — not a new one in its place.
        Assert.True(await Eval<bool>(page, "() => keptApi.same(cyOf('g').getElementById('api')) && !keptApi.removed()"));
        Assert.Equal("Orders service", await Eval<string>(page, "() => cyOf('g').getElementById('api').data('label')"));
        var after = await Positions(page);
        Assert.Equal(before["db"], after["db"]);
        Assert.Equal(before["web"], after["web"]);
        AssertQuiet();
    }

    [Fact]
    public async Task Set_can_move_a_record_into_a_group_that_arrives_in_the_same_update_and_retarget_a_link()
    {
        if (NoBrowser) return;
        // A reorganisation arrives as one update: the new team, the record filed under it,
        // and the link that now points somewhere else — under the id it always had.
        var page = await OpenGraph(Graph(inside: "", attrs: "data-graph-eager data-graph-deferred"));
        await Ready(page);
        await page.EvaluateAsync("""
            () => sednaUi.graph.get('g').then(g => g.set({
                nodes: [{ id: 'runner', label: 'build-runner-04' }, { id: 'db', label: 'src-db-14' }, { id: 'console', label: 'orders-console-01' }],
                edges: [{ id: 'deploys', source: 'runner', target: 'db', label: 'deploys' }]
            }))
            """);

        await page.EvaluateAsync("""
            () => sednaUi.graph.get('g').then(g => g.set({
                nodes: [{ id: 'platform', label: 'Platform team' }, { id: 'runner', label: 'build-runner-04', parent: 'platform' },
                        { id: 'db', label: 'src-db-14' }, { id: 'console', label: 'orders-console-01' }],
                edges: [{ id: 'deploys', source: 'runner', target: 'console', label: 'deploys to', kind: 'release' }]
            }))
            """);

        var result = await page.EvaluateAsync<JsonElement>("""
            () => { const cy = cyOf('g'), e = cy.getElementById('deploys'), r = cy.getElementById('runner');
                    return { parent: r.parent().id(), isParent: cy.getElementById('platform').isParent(), edges: cy.edges().length,
                             source: e.source().id(), target: e.target().id(), label: e.data('label'), kind: e.data('kind') }; }
            """);
        Assert.Equal("platform", result.GetProperty("parent").GetString());
        Assert.True(result.GetProperty("isParent").GetBoolean());
        Assert.Equal(1, result.GetProperty("edges").GetInt32());
        Assert.Equal("runner", result.GetProperty("source").GetString());
        Assert.Equal("console", result.GetProperty("target").GetString());
        Assert.Equal("deploys to", result.GetProperty("label").GetString());
        Assert.Equal("release", result.GetProperty("kind").GetString());
        AssertQuiet();
    }

    [Fact]
    public async Task Reload_fetches_the_records_again_and_shows_what_changed()
    {
        if (NoBrowser) return;
        const string first = """
            { "nodes": [ { "id": "api", "label": "Orders API" }, { "id": "db", "label": "Orders database" }, { "id": "web", "label": "Web shop" } ],
              "edges": [ { "source": "web", "target": "api" }, { "source": "api", "target": "db" } ] }
            """;
        const string second = """
            { "nodes": [ { "id": "api", "label": "Orders service" }, { "id": "db", "label": "Orders database" }, { "id": "web", "label": "Web shop" },
                         { "id": "mail", "label": "Mail sender" } ],
              "edges": [ { "source": "web", "target": "api" }, { "source": "api", "target": "db" }, { "source": "db", "target": "mail" } ] }
            """;
        var page = await OpenGraph(
            Graph(inside: """<div class="graph-tools"><button type="button" data-graph-action="reload" id="reload" aria-label="Reload"></button></div>""",
                attrs: "data-graph-eager data-graph-src=\"/api/graph\""),
            serve: new Dictionary<string, Served> { ["/api/graph"] = new(first) });
        await Ready(page);
        var before = await Positions(page);
        // The server has moved on since the page loaded.
        await page.RouteAsync("**/api/graph", route => route.FulfillAsync(new() { ContentType = "application/json", Body = second }));
        await ClearLog(page);

        await page.Locator("#reload").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('mail').nonempty()");

        Assert.Equal("Orders service", await Eval<string>(page, "() => cyOf('g').getElementById('api').data('label')"));
        var after = await Positions(page);
        Assert.Equal(before["web"], after["web"]);
        Assert.Equal(before["api"], after["api"]);

        // From script too, and through the bridge, which answers stats.
        var stats = await page.EvaluateAsync<JsonElement>("() => sednaUi.graph.invoke('g', 'reload')");
        Assert.Equal(4, stats.GetProperty("nodes").GetInt32());
        Assert.Equal(3, stats.GetProperty("edges").GetInt32());
        AssertQuiet();
    }

    [Fact]
    public async Task An_apps_own_style_rules_are_drawn_on_top_and_follow_the_theme()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);
        const string db = "() => [flat(cyOf('g').getElementById('db').style('background-color')), cyOf('g').getElementById('db').numericStyle('border-width'), paintOf('var(--viz-5)'), paintOf('var(--viz-4)')]";

        await page.EvaluateAsync("""
            () => sednaUi.graph.get('g').then(g => g.style([
                { selector: 'node[kind = "store"]', style: { 'background-color': 'var(--viz-5)', 'border-width': 5 } }]))
            """);
        var dark = await Eval<JsonElement>(page, db);
        Assert.Equal(dark[2].GetString(), dark[0].GetString());
        Assert.Equal(5, dark[1].GetDouble());

        // A theme change repaints everything, and keeps the app's rule — in the new theme's colour.
        await page.EvaluateAsync("() => sednaUi.settings.save('variant', 'light')");
        await page.WaitForFunctionAsync("c => flat(cyOf('g').getElementById('db').style('background-color')) !== c", dark[0].GetString());
        var light = await Eval<JsonElement>(page, db);
        Assert.NotEqual(dark[2].GetString(), light[2].GetString());
        Assert.Equal(light[2].GetString(), light[0].GetString());
        Assert.Equal(5, light[1].GetDouble());

        // A second call replaces the first.
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.style([]))");
        var plain = await Eval<JsonElement>(page, db);
        Assert.Equal(plain[3].GetString(), plain[0].GetString());
        AssertQuiet();
    }

    [Fact]
    public async Task Any_layout_the_engine_knows_is_accepted_and_an_unknown_one_is_refused_with_a_warning()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(
            Graph(id: "g", attrs: "data-graph-eager data-graph-layout=\"breadthfirst\"")
            + Graph(id: "unknown", attrs: "data-graph-eager data-graph-layout=\"hairball\""));
        await Ready(page);
        await Ready(page, "unknown");
        Assert.Equal(7, (await Positions(page)).Values.Distinct().Count());
        Assert.Contains(Warnings, w => w.Contains("\"hairball\" is not a layout; using force", StringComparison.Ordinal));

        var before = await Positions(page);
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.layout('nonsense'))");
        Assert.Contains(Warnings, w => w.Contains("\"nonsense\" is not a layout", StringComparison.Ordinal));
        var still = await Positions(page);
        foreach (var (id, at) in before) Assert.Equal(at, still[id]);

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.layout('circle'))");
        Assert.NotEqual(before["api"], (await Positions(page))["api"]);
        AssertQuiet("is not a layout");
    }

    // ── Colour and theme ──────────────────────────────────────────────────────

    private const string Swatches = """
        <span class="graph-swatch series-2" id="swatch-2"></span>
        <span class="graph-swatch series-4" id="swatch-4"></span>
        """;

    private const string ToneColours = """
        () => {
            const cy = cyOf('g');
            const swatch = id => paintOf(getComputedStyle(document.getElementById(id)).color);
            return [flat(cy.getElementById('api').style('background-color')), swatch('swatch-2'),
                    flat(cy.getElementById('db').style('background-color')), swatch('swatch-4')];
        }
        """;

    [Fact]
    public async Task A_records_tone_is_its_series_swatchs_colour_and_follows_the_variant()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph() + Swatches);
        await Ready(page);

        var dark = await Eval<string[]>(page, ToneColours);
        Assert.Equal(dark[1], dark[0]);
        Assert.Equal(dark[3], dark[2]);

        // Waits on tone 4, not tone 2: a series may keep its colour across variants — tone 2 is
        // cyan-600 in both — so only a tone the light block moves can show the repaint.
        await page.EvaluateAsync("() => sednaUi.settings.save('variant', 'light')");
        await page.WaitForFunctionAsync(
            "c => flat(cyOf('g').getElementById('db').style('background-color')) !== c", dark[2]);

        var light = await Eval<string[]>(page, ToneColours);
        Assert.NotEqual(dark[3], light[3]);
        Assert.Equal(light[1], light[0]);
        Assert.Equal(light[3], light[2]);
        AssertQuiet();
    }

    [Fact]
    public async Task Forced_colours_paint_the_canvas_in_the_readers_system_colours()
    {
        if (NoBrowser) return;
        // Windows high contrast replaces every colour on the page with the reader's
        // palette. A canvas is out of the browser's reach, so the graph has to follow.
        var page = await OpenGraph(Graph(), forcedColors: ForcedColors.Active);
        await Ready(page);

        var painted = await page.EvaluateAsync<JsonElement>("""
            () => {
                const cy = cyOf('g');
                return { api: flat(cy.getElementById('api').style('background-color')),
                         db: flat(cy.getElementById('db').style('background-color')),
                         link: flat(cy.edges()[0].style('line-color')),
                         text: paintOf('CanvasText') };
            }
            """);
        var system = painted.GetProperty("text").GetString();
        Assert.Equal(system, painted.GetProperty("api").GetString());
        Assert.Equal(system, painted.GetProperty("db").GetString());
        Assert.Equal(system, painted.GetProperty("link").GetString());
        AssertQuiet();
    }

    [Fact]
    public async Task Turning_forced_colours_on_repaints_a_graph_already_drawn()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);
        var before = await Eval<string>(page, "() => flat(cyOf('g').getElementById('api').style('background-color'))");

        await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });

        await page.WaitForFunctionAsync(
            "() => flat(cyOf('g').getElementById('api').style('background-color')) === paintOf('CanvasText')");
        Assert.NotEqual(before, await Eval<string>(page, "() => paintOf('CanvasText')"));
        AssertQuiet();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_graph_whose_element_leaves_the_document_is_taken_down_with_it()
    {
        if (NoBrowser) return;
        // A Blazor navigation removes the page's markup. An engine left running for it
        // would keep its observers, its timers and its canvas for as long as the tab.
        var page = await OpenGraph(Graph());
        await Ready(page);
        await page.EvaluateAsync("() => { window.keptCy = cyOf('g'); window.keptEl = document.getElementById('g'); keptEl.remove(); }");

        await page.WaitForFunctionAsync("() => keptCy.destroyed()");
        Assert.False(await Eval<bool>(page, "() => keptEl.hasAttribute('data-graph-state')"));
        // Nothing left behind to be painted or listened to.
        await page.EvaluateAsync("() => { sednaUi.settings.save('variant', 'light'); window.dispatchEvent(new Event('resize')); }");
        await page.WaitForTimeoutAsync(100);
        AssertQuiet();
    }

    [Fact]
    public async Task A_graph_moved_within_one_task_is_not_taken_down()
    {
        if (NoBrowser) return;
        // Blazor moves an element by removing it and inserting it again.
        var page = await OpenGraph(Graph() + """<div id="elsewhere"></div>""");
        await Ready(page);

        await page.EvaluateAsync("() => { window.keptCy = cyOf('g'); document.getElementById('elsewhere').appendChild(document.getElementById('g')); }");
        await page.WaitForTimeoutAsync(100);

        Assert.False(await Eval<bool>(page, "() => keptCy.destroyed()"));
        Assert.Equal("ready", await StateOf(page));
        AssertQuiet();
    }

    [Fact]
    public async Task A_graph_moved_before_it_started_still_starts_when_it_comes_near()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(FarBelow + """<div id="first">""" + Graph(attrs: "") + """</div><div id="second"></div>""");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Blazor's move: out of one place and into another, in one task.
        await page.EvaluateAsync("() => document.getElementById('second').appendChild(document.getElementById('g'))");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Null(await StateOf(page));

        await page.EvaluateAsync("() => window.scrollTo(0, document.getElementById('g').offsetTop - 1.5 * innerHeight)");
        await State(page, "g", "ready");
        AssertQuiet();
    }

    [Fact]
    public async Task A_graph_removed_before_it_started_is_forgotten_and_found_again_if_it_comes_back()
    {
        if (NoBrowser) return;
        var page = await OpenGraph("""<div id="top"></div>""" + FarBelow + Graph(attrs: ""));
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.EvaluateAsync("() => { window.kept = document.getElementById('g'); kept.remove(); }");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.DoesNotContain(Requests, r => r.Url.Contains("Sedna.UI.graph.js", StringComparison.Ordinal));
        Assert.Null(await Eval<string?>(page, "() => kept.getAttribute('data-graph-state')"));

        // Rendered again, this time at the top of the page.
        await page.EvaluateAsync("() => document.getElementById('top').appendChild(kept)");
        await State(page, "g", "ready");
        AssertQuiet();
    }

    private const string BlazorMarker ="""<!--Blazor:{"type":"server","prerenderId":"3f2a","key":{"locationHash":"0","formattedComponentKey":""},"sequence":0,"descriptor":"x"}-->""";

    [Fact]
    public async Task A_prerendered_Blazor_page_holds_its_graphs_until_Blazor_has_started()
    {
        if (NoBrowser) return;
        // The interactive render replaces the prerendered markup element for element: a
        // graph drawn before it would be drawn twice, and flash its wait in between.
        var page = await OpenGraph(BlazorMarker + Graph() + "<!--Blazor:{\"prerenderId\":\"3f2a\"}-->");

        // In one go, well inside the few seconds after which a held graph starts anyway:
        // the eager graph waits; the interactive render replaces the element with an
        // identical one; Blazor's initializer says it has started.
        var held = await Eval<bool>(page, """
            async () => {
                await new Promise(r => setTimeout(r, 300));
                const old = document.getElementById('g');
                const waited = !old.hasAttribute('data-graph-state');
                window.oldGraph = old;
                old.replaceWith(old.cloneNode(true));
                (await import('/Sedna.UI.lib.module.js')).afterServerStarted();
                return waited;
            }
            """);
        Assert.True(held, "An eager graph on a prerendered page started before Blazor had.");

        await State(page, "g", "ready");
        await Heard(page, "sedna-graph-ready");
        Assert.Single(await Log(page, "sedna-graph-ready"));
        Assert.False(await Eval<bool>(page, "() => oldGraph.hasAttribute('data-graph-state')"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_held_graph_starts_by_itself_if_Blazor_never_says_it_has_started()
    {
        if (NoBrowser) return;
        // A page whose circuit never starts still shows its graph, a few seconds late.
        var page = await OpenGraph(BlazorMarker + Graph());
        Assert.True(await Eval<bool>(page,
            "async () => { await new Promise(r => setTimeout(r, 300)); return !document.getElementById('g').hasAttribute('data-graph-state'); }"));

        await page.WaitForFunctionAsync(
            "() => document.getElementById('g').getAttribute('data-graph-state') === 'ready'",
            null, new() { Timeout = 15_000 });
        AssertQuiet();
    }

    [Fact]
    public async Task The_bridge_answers_null_with_a_warning_rather_than_throwing()
    {
        if (NoBrowser) return;
        // An exception crossing into Blazor from an event handler tears the circuit down.
        var page = await OpenGraph(Graph());
        await Ready(page);

        Assert.True(await Eval<bool>(page, "() => sednaUi.graph.invoke('not-here', 'stats').then(r => r === null)"));
        Assert.True(await Eval<bool>(page, "() => sednaUi.graph.invoke('g', 'no-such-method', []).then(r => r === null)"));
        var stats = await page.EvaluateAsync<JsonElement>("() => sednaUi.graph.invoke('g', 'stats')");
        Assert.Equal(7, stats.GetProperty("nodes").GetInt32());
        // An export is the file's bytes, which .NET reads as a stream; with no graph, none.
        var svg = await Eval<string>(page,
            "() => sednaUi.graph.invoke('g', 'export', ['svg']).then(bytes => bytes instanceof Uint8Array ? new TextDecoder().decode(bytes) : 'not bytes')");
        Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
        Assert.True(await Eval<bool>(page,
            "() => sednaUi.graph.invoke('not-here', 'export', ['png']).then(bytes => bytes instanceof Uint8Array && bytes.length === 0)"));

        Assert.Contains(Warnings, w => w.Contains("\"not-here\"", StringComparison.Ordinal));
        Assert.Contains(Warnings, w => w.Contains("\"no-such-method\" is not a graph method", StringComparison.Ordinal));
        AssertQuiet("not-here", "is not a graph method");
    }

    [Fact]
    public async Task A_group_removed_in_the_same_update_that_frees_its_records_keeps_the_records()
    {
        if (NoBrowser) return;
        // Removing a group removes what is inside it, so the records that stay are taken out first.
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="team">Platform team</li>
              <li data-node="runner" data-parent="team">build-runner-04</li>
              <li data-node="db" data-parent="team">src-db-14</li>
              <li data-node="web">orders-console-01</li>
            </ul>
            """));
        await Ready(page);

        var after = await page.EvaluateAsync<string[]>("""
            async () => {
                const g = await sednaUi.graph.get('g');
                await g.set({ nodes: [{ id: 'runner', label: 'build-runner-04' }, { id: 'db', label: 'src-db-14' }, { id: 'web', label: 'orders-console-01' }], edges: [] });
                return g.cy.nodes().map(n => n.id() + (n.isChild() ? '<' + n.parent().id() : '')).sort();
            }
            """);
        Assert.Equal(["db", "runner", "web"], after);
        AssertQuiet();
    }
}
