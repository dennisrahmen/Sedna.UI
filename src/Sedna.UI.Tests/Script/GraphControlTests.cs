using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Ordinary controls the app writes — chips, checkboxes, selects, radios, a search field,
/// buttons — driving a graph with no script of the app's own.
/// </summary>
/// <remarks>
/// A control is found by delegation from <c>document</c>, so every test here clicks or
/// types into the real element the way a reader does; none of them calls the handle. The
/// fixture's <see cref="GraphTestBase.Services"/> map has three groups, a record in none,
/// a muted record and a record with no link.
/// </remarks>
public class GraphControlTests : GraphTestBase
{
    private static string Framed(string toolbar, string inside = Services, string attrs = "data-graph-eager") => $"""
        <div class="graph-frame" data-graph-frame>
          <div class="toolbar">{toolbar}</div>
          {Graph(inside: inside, attrs: attrs)}
        </div>
        """;

    // ── Filters ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_chip_turned_off_hides_its_value_and_nothing_else()
    {
        if (NoBrowser) return;
        // A chip is a legend key that switches: turning "Orders" off hides the orders
        // group — not every record that happens to have no group at all.
        var page = await OpenGraph(Framed("""
            <button class="chip" type="button" data-graph-filter="group" value="orders" aria-pressed="true" id="orders">Orders</button>
            """));
        await Ready(page);
        await ClearLog(page);

        await page.Locator("#orders").ClickAsync();
        await Heard(page, "sedna-graph-change");

        Assert.Equal("false", await page.Locator("#orders").GetAttributeAsync("aria-pressed"));
        Assert.Equal(["archive", "cache", "runner", "web"], await Shown(page));
        // A link loses its line with either end.
        Assert.Equal(["web→cache"], await ShownLinks(page));
        var change = (await Log(page, "sedna-graph-change")).Last();
        Assert.Equal(4, change.Detail.GetProperty("nodes").GetInt32());
        Assert.Equal(7, change.Detail.GetProperty("totalNodes").GetInt32());

        await ClearLog(page);
        await page.Locator("#orders").ClickAsync();
        await Heard(page, "sedna-graph-change");
        Assert.Equal("true", await page.Locator("#orders").GetAttributeAsync("aria-pressed"));
        Assert.Equal(7, (await Shown(page)).Length);
        AssertQuiet();
    }

    [Fact]
    public async Task A_checkbox_chip_keeps_its_own_state_and_hides_its_value_while_unchecked()
    {
        if (NoBrowser) return;
        // A checkbox's state can be a Blazor binding's; the script reads it and never
        // writes it.
        var page = await OpenGraph(Framed("""
            <label><input type="checkbox" data-graph-filter="kind" value="store" checked id="stores"> Stores</label>
            """));
        await Ready(page);

        await page.Locator("#stores").UncheckAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('db').hasClass('hidden')");

        Assert.Equal(["api", "queue", "runner", "web"], await Shown(page));
        Assert.False(await page.Locator("#stores").IsCheckedAsync());

        await page.Locator("#stores").CheckAsync();
        await page.WaitForFunctionAsync("() => !cyOf('g').getElementById('db').hasClass('hidden')");
        Assert.Equal(7, (await Shown(page)).Length);
        AssertQuiet();
    }

    [Fact]
    public async Task A_chip_on_edge_kind_hides_links_of_that_kind_and_keeps_every_record()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <button class="chip" type="button" data-graph-filter="edge.kind" value="reads" aria-pressed="true" id="reads">Reads</button>
            """));
        await Ready(page);

        await page.Locator("#reads").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('api→db').hasClass('hidden')");

        Assert.Equal(7, (await Shown(page)).Length);
        Assert.Equal(["api→queue", "runner→api", "web→api"], await ShownLinks(page));
        AssertQuiet();
    }

    [Fact]
    public async Task A_select_chooses_its_value_and_the_empty_value_chooses_everything()
    {
        if (NoBrowser) return;
        // A select is a choice: "the orders group" does not include a record in no group.
        var page = await OpenGraph(Framed("""
            <select data-graph-filter="group" id="group" aria-label="Group">
              <option value="">Every group</option><option value="orders">Orders</option><option value="shop">Shop</option>
            </select>
            """));
        await Ready(page);

        await page.Locator("#group").SelectOptionAsync("orders");
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('web').hasClass('hidden')");
        Assert.Equal(["api", "db", "queue"], await Shown(page));

        await page.Locator("#group").SelectOptionAsync("shop");
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('api').hasClass('hidden')");
        Assert.Equal(["cache", "web"], await Shown(page));

        await page.Locator("#group").SelectOptionAsync("");
        await page.WaitForFunctionAsync("() => !cyOf('g').getElementById('api').hasClass('hidden')");
        Assert.Equal(7, (await Shown(page)).Length);
        AssertQuiet();
    }

    [Fact]
    public async Task Radios_choose_one_value_and_the_empty_one_chooses_everything()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <label><input type="radio" name="kind" data-graph-filter="kind" value="" checked id="all"> All</label>
            <label><input type="radio" name="kind" data-graph-filter="kind" value="service" id="services"> Services</label>
            <label><input type="radio" name="kind" data-graph-filter="kind" value="store queue" id="storage"> Storage</label>
            """));
        await Ready(page);

        await page.Locator("#services").CheckAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('db').hasClass('hidden')");
        Assert.Equal(["api", "web"], await Shown(page));

        // A value of several words chooses any of them.
        await page.Locator("#storage").CheckAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('api').hasClass('hidden')");
        Assert.Equal(["archive", "cache", "db", "queue"], await Shown(page));

        await page.Locator("#all").CheckAsync();
        await page.WaitForFunctionAsync("() => !cyOf('g').getElementById('api').hasClass('hidden')");
        Assert.Equal(7, (await Shown(page)).Length);
        AssertQuiet();
    }

    [Fact]
    public async Task Filters_that_hide_every_record_show_the_apps_filtered_state()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <select data-graph-filter="kind" id="kind" aria-label="Kind">
              <option value="">Every kind</option><option value="printer">Printers</option>
            </select>
            """));
        await Ready(page);

        await page.Locator("#kind").SelectOptionAsync("printer");
        await State(page, "g", "filtered");
        await Assertions.Expect(page.Locator("#g .graph-empty--filtered")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#g .graph-empty:not(.graph-empty--filtered, .graph-empty--error)")).ToBeHiddenAsync();

        await page.Locator("#kind").SelectOptionAsync("");
        await State(page, "g", "ready");
        await Assertions.Expect(page.Locator("#g .graph-empty--filtered")).ToBeHiddenAsync();
        AssertQuiet();
    }

    [Fact]
    public async Task Show_muted_and_show_isolated_hide_what_they_name_while_off()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <label><input type="checkbox" data-graph-show="muted" checked id="muted"> Settled</label>
            <button class="chip" type="button" data-graph-show="isolated" aria-pressed="true" id="isolated">Unlinked</button>
            """));
        await Ready(page);

        await page.Locator("#muted").UncheckAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('cache').hasClass('hidden')");
        Assert.Equal(["api", "archive", "db", "queue", "runner", "web"], await Shown(page));

        await page.Locator("#isolated").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('archive').hasClass('hidden')");
        Assert.Equal("false", await page.Locator("#isolated").GetAttributeAsync("aria-pressed"));
        Assert.Equal(["api", "db", "queue", "runner", "web"], await Shown(page));
        AssertQuiet();
    }

    // ── Search and stats ──────────────────────────────────────────────────────

    [Fact]
    public async Task Search_marks_what_matches_without_hiding_anything_and_the_stats_say_so()
    {
        if (NoBrowser) return;
        // Search marks rather than hides: where a record sits among the others is the
        // point of a graph.
        var page = await OpenGraph(Framed("""
            <input type="search" data-graph-search aria-label="Find a record" id="find">
            <p id="stats" data-graph-stats="{nodes} records · {edges} links"
               data-graph-stats-match="{matches} of {nodes} match"></p>
            """));
        await Ready(page);
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("7 records · 5 links");

        await page.Locator("#find").FillAsync("orders");
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("2 of 7 match");
        Assert.Equal(["api", "db"], await Eval<string[]>(page, "() => cyOf('g').nodes('.match').map(n => n.id()).sort()"));
        Assert.Equal(7, (await Shown(page)).Length);

        // Every word has to be found — in the name, the id, data-meta, the kind or a tag.
        await page.Locator("#find").FillAsync("orders console");
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("1 of 7 match");
        await page.WaitForFunctionAsync("() => cyOf('g').nodes('.match').map(n => n.id()).join() === 'api'");
        // Case does not matter, and a tag is searched.
        await page.Locator("#find").FillAsync("EDGE");
        await page.WaitForFunctionAsync("() => cyOf('g').nodes('.match').map(n => n.id()).join() === 'web'");
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("1 of 7 match");

        await page.Locator("#find").FillAsync("");
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("7 records · 5 links");
        Assert.Equal(0, await Eval<int>(page, "() => cyOf('g').nodes('.match').length"));
        AssertQuiet();
    }

    [Fact]
    public async Task Search_waits_for_the_typing_to_pause_before_it_moves_the_view()
    {
        if (NoBrowser) return;
        // A view that flies to each keystroke's matches cannot be read: one search, for
        // what was typed, once the reader stops.
        var page = await OpenGraph(Framed("""<input type="search" data-graph-search aria-label="Find a record" id="find">"""));
        await Ready(page);
        await ClearLog(page);

        await page.Locator("#find").PressSequentiallyAsync("orders data", new() { Delay = 30 });
        await Heard(page, "sedna-graph-change");
        await page.WaitForTimeoutAsync(300);

        // Eleven keys: one search, or a couple on a machine slow enough to pause mid-word.
        var changes = await Log(page, "sedna-graph-change");
        Assert.InRange(changes.Count, 1, 3);
        Assert.Equal(1, changes[^1].Detail.GetProperty("matches").GetInt32());
        Assert.Equal(["db"], await Eval<string[]>(page, "() => cyOf('g').nodes('.match').map(n => n.id())"));
        AssertQuiet();
    }

    [Fact]
    public async Task Stats_outside_the_frame_are_found_by_data_graph_for_and_follow_every_filter()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(attrs: "data-graph-eager") + """
            <p id="stats" data-graph-for="g" data-graph-stats="{nodes} of {totalNodes} records, {edges} of {totalEdges} links"></p>
            <div data-graph-for="g">
              <button class="chip" type="button" data-graph-filter="kind" value="service" aria-pressed="true" id="services">Services</button>
            </div>
            """);
        await Ready(page);
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("7 of 7 records, 5 of 5 links");

        await page.Locator("#services").ClickAsync();
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("5 of 7 records, 0 of 5 links");
        AssertQuiet();
    }

    // ── Options ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_layout_option_lays_the_drawing_out_again()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <select data-graph-option="layout" id="layout" aria-label="Layout">
              <option value="force">Map</option><option value="grid">Grid</option><option value="circle">Circle</option>
            </select>
            """));
        await Ready(page);

        await page.Locator("#layout").SelectOptionAsync("grid");
        await page.WaitForFunctionAsync(
            "() => new Set(cyOf('g').nodes().map(n => Math.round(n.position('y')))).size <= 3");
        // A grid: few rows, few columns, every record on one of each.
        var (rows, columns) = await RowsAndColumns(page);
        Assert.InRange(rows, 2, 3);
        Assert.InRange(columns, 3, 4);

        await page.Locator("#layout").SelectOptionAsync("circle");
        await page.WaitForFunctionAsync("() => new Set(cyOf('g').nodes().map(n => Math.round(n.position('y')))).size > 3");
        // A circle: every record the same distance from the middle.
        var spread = await Eval<double>(page, """
            () => {
                const nodes = cyOf('g').nodes();
                const cx = nodes.reduce((s, n) => s + n.position('x'), 0) / nodes.length;
                const cy = nodes.reduce((s, n) => s + n.position('y'), 0) / nodes.length;
                const r = nodes.map(n => Math.hypot(n.position('x') - cx, n.position('y') - cy));
                return (Math.max(...r) - Math.min(...r)) / Math.max(...r);
            }
            """);
        Assert.True(spread < 0.05, $"The circle's radii differ by {spread:P0}.");
        AssertQuiet();
    }

    private static async Task<(int Rows, int Columns)> RowsAndColumns(IPage page)
    {
        var counts = await Eval<int[]>(page, """
            () => { const n = cyOf('g').nodes();
                    return [new Set(n.map(x => Math.round(x.position('y')))).size, new Set(n.map(x => Math.round(x.position('x')))).size]; }
            """);
        return (counts[0], counts[1]);
    }

    [Fact]
    public async Task The_view_options_reach_the_drawing()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <select data-graph-option="labels" id="labels" aria-label="Names"><option value="auto">Auto</option><option value="all">All</option><option value="none">None</option></select>
            <select data-graph-option="edge-labels" id="edge-labels" aria-label="Link names"><option value="hover">Hover</option><option value="always">Always</option></select>
            <select data-graph-option="curve" id="curve" aria-label="Lines"><option value="bezier">Curved</option><option value="taxi">Right angles</option></select>
            <select data-graph-option="arrows" id="arrows" aria-label="Arrows"><option value="none">None</option><option value="target">To</option></select>
            <select data-graph-option="nodes" id="nodes" aria-label="Records"><option value="dot">Dots</option><option value="box">Boxes</option></select>
            """));
        await Ready(page);

        await page.Locator("#labels").SelectOptionAsync("all");
        await page.WaitForFunctionAsync("() => cyOf('g').nodes().every(n => n.style('label') !== '')");

        await page.Locator("#labels").SelectOptionAsync("none");
        await page.WaitForFunctionAsync("() => cyOf('g').nodes().every(n => n.style('label') === '')");
        // A name still shows where the reader is looking.
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.select('db'))");
        Assert.Equal("Orders database", await Eval<string>(page, "() => cyOf('g').getElementById('db').style('label')"));

        // A link's name shows while it is lit; "always" shows the others' too.
        Assert.Equal("", await Eval<string>(page, "() => cyOf('g').getElementById('runner→api').style('label')"));
        await page.Locator("#edge-labels").SelectOptionAsync("always");
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('runner→api').style('label') === 'deploys'");

        await page.Locator("#curve").SelectOptionAsync("taxi");
        await page.WaitForFunctionAsync("() => cyOf('g').edges().every(e => e.style('curve-style') === 'taxi')");

        await page.Locator("#arrows").SelectOptionAsync("target");
        await page.WaitForFunctionAsync("() => cyOf('g').edges().every(e => e.style('target-arrow-shape') === 'triangle')");

        await page.Locator("#nodes").SelectOptionAsync("box");
        await page.WaitForFunctionAsync("() => cyOf('g').nodes().every(n => n.style('text-valign') === 'center')");
        AssertQuiet();
    }

    [Fact]
    public async Task A_colour_option_repaints_each_record_in_its_other_tone()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <select data-graph-option="colour" id="colour" aria-label="Colour by">
              <option value="tone">Kind</option><option value="team">Team</option>
            </select>
            """, inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="api" data-tone="2" data-tone-team="5">Orders API</li>
              <li data-node="db" data-tone="4">Orders database</li>
            </ul>
            """) + """<span class="graph-swatch series-2" id="s2"></span><span class="graph-swatch series-5" id="s5"></span><span class="graph-swatch series-4" id="s4"></span>""");
        await Ready(page);
        const string painted = """
            () => { const cy = cyOf('g'); const s = id => paintOf(getComputedStyle(document.getElementById(id)).color);
                    return [flat(cy.getElementById('api').style('background-color')), flat(cy.getElementById('db').style('background-color')),
                            s('s2'), s('s5'), s('s4')]; }
            """;
        var byKind = await Eval<string[]>(page, painted);
        Assert.Equal(byKind[2], byKind[0]);

        await page.Locator("#colour").SelectOptionAsync("team");
        await page.WaitForFunctionAsync("c => flat(cyOf('g').getElementById('api').style('background-color')) !== c", byKind[0]);
        var byTeam = await Eval<string[]>(page, painted);
        Assert.Equal(byTeam[3], byTeam[0]);
        // A record with no colour of that name keeps its own.
        Assert.Equal(byTeam[4], byTeam[1]);
        AssertQuiet();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private const string Tools = """
        <div class="graph-tools" role="toolbar" aria-label="View">
          <button type="button" data-graph-action="zoom-in" id="zoom-in" aria-label="Zoom in"></button>
          <button type="button" data-graph-action="zoom-out" id="zoom-out" aria-label="Zoom out"></button>
          <button type="button" data-graph-action="fit" id="fit" aria-label="Fit"></button>
          <button type="button" data-graph-action="arrange" id="arrange" aria-label="Arrange"></button>
        </div>
        """;

    private const string EveryRecordInView = """
        () => { const cy = cyOf('g'); const e = cy.extent(); return cy.nodes().every(n => {
            const p = n.position(); return p.x > e.x1 && p.x < e.x2 && p.y > e.y1 && p.y < e.y2; }); }
        """;

    [Fact]
    public async Task Zoom_in_zoom_out_and_fit_move_the_view()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Services + Tools));
        await Ready(page);
        var fitted = await Eval<double>(page, "() => cyOf('g').zoom()");

        await page.Locator("#zoom-in").ClickAsync();
        Assert.Equal(fitted * 1.3, await Eval<double>(page, "() => cyOf('g').zoom()"), 6);
        await page.Locator("#zoom-out").ClickAsync();
        await page.Locator("#zoom-out").ClickAsync();
        Assert.Equal(fitted / 1.3, await Eval<double>(page, "() => cyOf('g').zoom()"), 6);

        await page.EvaluateAsync("() => cyOf('g').panBy({ x: 300, y: -120 })");
        await page.Locator("#fit").ClickAsync();
        // Fitted again — to within what the records' own size changes with the zoom.
        Assert.InRange(await Eval<double>(page, "() => cyOf('g').zoom()"), fitted * 0.97, fitted * 1.03);
        // Fitted: every record inside the frame.
        Assert.True(await Eval<bool>(page, EveryRecordInView));
        AssertQuiet();
    }

    private static async Task PushAroundAndArrange(IPage page)
    {
        await page.EvaluateAsync("""
            () => { const cy = cyOf('g'); cy.getElementById('api').position({ x: -900, y: 700 }); cy.getElementById('web').shift({ x: 250, y: 0 }); }
            """);
        await page.Locator("#arrange").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('api').position('x') > -900");
    }

    private static void AssertSamePlaces(Dictionary<string, (double X, double Y)> expected, Dictionary<string, (double X, double Y)> actual)
    {
        foreach (var (id, at) in expected)
        {
            Assert.Equal(at.X, actual[id].X, 1);
            Assert.Equal(at.Y, actual[id].Y, 1);
        }
    }

    [Fact]
    public async Task Arrange_puts_every_record_back_where_the_first_drawing_had_it()
    {
        if (NoBrowser) return;
        // Every layout is deterministic: the drawing a reader learned is the one that
        // comes back, however they pushed it around in between.
        var page = await OpenGraph(Graph(inside: Services + Tools));
        await Ready(page);
        var laid = await Positions(page);

        await PushAroundAndArrange(page);

        AssertSamePlaces(laid, await Positions(page));
        AssertQuiet();
    }

    [Fact]
    public async Task Arrange_puts_the_records_a_reader_moved_back_where_it_laid_them()
    {
        if (NoBrowser) return;
        // A short frame fits the drawing below full size, where a record's size does not
        // change with the zoom — see LayoutDependsOnZoom for above it.
        var page = await OpenGraph(Graph(inside: Services + Tools, attrs: "data-graph-eager style=\"--graph-height: 9rem\""));
        await Ready(page);
        await page.Locator("#arrange").ClickAsync();
        var laid = await Positions(page);
        Assert.True(await Eval<double>(page, "() => cyOf('g').zoom()") < 1);

        await PushAroundAndArrange(page);

        AssertSamePlaces(laid, await Positions(page));
        AssertQuiet();
    }

    [Fact]
    public async Task Arrange_lays_the_same_records_out_the_same_way_at_any_zoom()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Services + Tools));
        await Ready(page);
        await page.Locator("#arrange").ClickAsync();
        var laid = await Positions(page);

        for (var i = 0; i < 3; i++) await page.Locator("#zoom-in").ClickAsync();
        await PushAroundAndArrange(page);

        AssertSamePlaces(laid, await Positions(page));
        AssertQuiet();
    }

    [Fact]
    public async Task Select_focus_and_unfocus_act_on_the_record_their_value_names()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <button type="button" data-graph-action="select" value="db" id="select-db">Show the database</button>
            <button type="button" data-graph-action="clear" id="clear">Clear</button>
            <button type="button" data-graph-action="focus" value="web" id="focus-web">Around the shop</button>
            <button type="button" data-graph-action="unfocus" id="unfocus">Everything</button>
            """));
        await Ready(page);
        await ClearLog(page);

        await page.Locator("#select-db").ClickAsync();
        Assert.Equal(["db"], await Eval<string[]>(page, "() => cyOf('g').$(':selected').map(n => n.id())"));
        Assert.Equal("db", (await Log(page, "sedna-graph-select")).Single().Detail.GetProperty("id").GetString());

        await page.Locator("#clear").ClickAsync();
        Assert.Equal(0, await Eval<int>(page, "() => cyOf('g').$(':selected').length"));

        // One record's neighbourhood: the web shop and what it links to.
        await page.Locator("#focus-web").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('runner').hasClass('hidden')");
        Assert.Equal(["api", "cache", "web"], await Shown(page));

        await page.Locator("#unfocus").ClickAsync();
        await page.WaitForFunctionAsync("() => !cyOf('g').getElementById('runner').hasClass('hidden')");
        Assert.Equal(7, (await Shown(page)).Length);
        AssertQuiet();
    }

    [Fact]
    public async Task A_depth_option_widens_the_focus_by_hops()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <select data-graph-option="depth" id="depth" aria-label="Hops"><option value="1">1</option><option value="2">2</option></select>
            <button type="button" data-graph-action="focus" value="db" id="focus-db">Around the database</button>
            """));
        await Ready(page);

        await page.Locator("#focus-db").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('web').hasClass('hidden')");
        Assert.Equal(["api", "db"], await Shown(page));

        await page.Locator("#depth").SelectOptionAsync("2");
        await page.WaitForFunctionAsync("() => !cyOf('g').getElementById('web').hasClass('hidden')");
        Assert.Equal(["api", "db", "queue", "runner", "web"], await Shown(page));
        AssertQuiet();
    }

    [Fact]
    public async Task Hide_takes_its_value_else_the_selection_and_show_all_brings_them_back()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <button type="button" data-graph-action="hide" value="runner" id="hide-runner">Hide the runner</button>
            <button type="button" data-graph-action="hide" id="hide">Hide the selection</button>
            <button type="button" data-graph-action="show-all" id="show-all">Show everything</button>
            """));
        await Ready(page);

        await page.Locator("#hide-runner").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('runner').hasClass('hidden')");

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.select('archive'))");
        await page.Locator("#hide").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('archive').hasClass('hidden')");
        Assert.Equal(["api", "cache", "db", "queue", "web"], await Shown(page));

        await page.Locator("#show-all").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').nodes('.hidden').length === 0");
        AssertQuiet();
    }

    [Fact]
    public async Task Open_as_an_action_dispatches_the_open_event_for_its_record()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <button type="button" data-graph-action="open" value="api" id="open-api">Open the API</button>
            """));
        await Ready(page);
        await page.EvaluateAsync("() => document.addEventListener('sedna-graph-open', e => e.preventDefault())");

        await page.Locator("#open-api").ClickAsync();
        await Heard(page, "sedna-graph-open");

        var open = (await Log(page, "sedna-graph-open")).Single();
        Assert.Equal("api", open.Detail.GetProperty("id").GetString());
        Assert.Equal("/records/api", open.Detail.GetProperty("href").GetString());
        Assert.EndsWith("/fixture.html", page.Url, StringComparison.Ordinal);
        AssertQuiet();
    }

    [Fact]
    public async Task Reset_puts_every_control_and_the_drawing_back_as_they_were_first_drawn()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <button class="chip" type="button" data-graph-filter="group" value="shop" aria-pressed="true" id="shop">Shop</button>
            <label><input type="checkbox" data-graph-show="muted" checked id="muted"> Settled</label>
            <select data-graph-filter="kind" id="kind" aria-label="Kind"><option value="">Every kind</option><option value="service">Services</option><option value="store">Stores</option></select>
            <input type="search" data-graph-search aria-label="Find" id="find">
            <button type="button" data-graph-action="hide" value="runner" id="hide-runner">Hide the runner</button>
            <button type="button" data-graph-action="reset" id="reset">Reset</button>
            <p id="stats" data-graph-stats="{nodes} records" data-graph-stats-match="{matches} match"></p>
            """));
        await Ready(page);

        await page.Locator("#shop").ClickAsync();
        await page.Locator("#muted").UncheckAsync();
        await page.Locator("#kind").SelectOptionAsync("store");
        await page.Locator("#find").FillAsync("orders");
        await page.Locator("#hide-runner").ClickAsync();
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => { g.select('db'); g.zoom(2); })");
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("1 match");

        await page.Locator("#reset").ClickAsync();
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("7 records");

        Assert.Equal("true", await page.Locator("#shop").GetAttributeAsync("aria-pressed"));
        Assert.True(await page.Locator("#muted").IsCheckedAsync());
        Assert.Equal("", await page.Locator("#kind").InputValueAsync());
        Assert.Equal("", await page.Locator("#find").InputValueAsync());
        Assert.Equal(7, (await Shown(page)).Length);
        Assert.Equal(0, await Eval<int>(page, "() => cyOf('g').$(':selected').length + cyOf('g').nodes('.match').length"));
        // And the view fitted to the whole drawing again.
        Assert.True(await Eval<bool>(page, EveryRecordInView));
        AssertQuiet();
    }

    [Fact]
    public async Task A_filter_by_call_keeps_the_focus_unless_it_names_one()
    {
        if (NoBrowser) return;
        // The focus is the reader's place; narrowing what is shown should not throw them out of it.
        var page = await OpenGraph(Framed("""
            <button type="button" data-graph-action="focus" value="web" id="focus-web">Around the shop</button>
            """));
        await Ready(page);
        await page.Locator("#focus-web").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('runner').hasClass('hidden')");

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.filter({ muted: false }))");
        Assert.Equal(["api", "web"], await Shown(page));

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.filter({ focus: 'db' }))");
        Assert.Equal(["api", "db"], await Shown(page));

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.filter({ focus: null }))");
        Assert.Equal(7, (await Shown(page)).Length);
        AssertQuiet();
    }

    [Fact]
    public async Task A_filter_set_by_call_is_what_the_controls_apply_on_top_of()
    {
        if (NoBrowser) return;
        // The app narrows by call — this reader's records — and the reader's own chips
        // narrow further; turning a chip back on does not undo the app's filter.
        var page = await OpenGraph(Framed("""
            <button class="chip" type="button" data-graph-filter="group" value="orders" aria-pressed="true" id="orders">Orders</button>
            """));
        await Ready(page);

        var stats = await page.EvaluateAsync<JsonElement>(
            "() => sednaUi.graph.get('g').then(g => g.filter({ except: { kind: ['host'] }, muted: false }))");
        Assert.Equal(5, stats.GetProperty("nodes").GetInt32());
        Assert.Equal(["api", "archive", "db", "queue", "web"], await Shown(page));

        await page.Locator("#orders").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('api').hasClass('hidden')");
        Assert.Equal(["archive", "web"], await Shown(page));

        await page.Locator("#orders").ClickAsync();
        await page.WaitForFunctionAsync("() => !cyOf('g').getElementById('api').hasClass('hidden')");
        Assert.Equal(["api", "archive", "db", "queue", "web"], await Shown(page));

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.filter({}))");
        Assert.Equal(7, (await Shown(page)).Length);
        AssertQuiet();
    }

    // ── Which graph a control drives ──────────────────────────────────────────

    [Fact]
    public async Task A_control_drives_the_graph_it_names_else_the_one_it_is_in_else_the_one_in_its_frame()
    {
        if (NoBrowser) return;
        var page = await OpenGraph($"""
            <div class="graph-frame" data-graph-frame>
              <div class="toolbar">
                <button type="button" data-graph-action="zoom-in" id="by-frame">In, by frame</button>
                <span data-graph-for="b"><button type="button" data-graph-action="zoom-in" id="named-in-frame">In, named</button></span>
              </div>
              {Graph(id: "a", inside: Services + """<div class="graph-tools"><button type="button" data-graph-action="zoom-in" id="inside">In, inside</button></div>""")}
            </div>
            <div class="toolbar" data-graph-for="b"><div><button type="button" data-graph-action="zoom-in" id="by-name">In, by name</button></div></div>
            {Graph(id: "b")}
            """);
        await Ready(page, "a");
        await Ready(page, "b");
        const string zooms = "() => [cyOf('a').zoom(), cyOf('b').zoom()]";
        var start = await Eval<double[]>(page, zooms);

        await page.Locator("#by-frame").ClickAsync();
        var afterFrame = await Eval<double[]>(page, zooms);
        Assert.Equal(start[0] * 1.3, afterFrame[0], 6);
        Assert.Equal(start[1], afterFrame[1], 6);

        await page.Locator("#inside").ClickAsync();
        var afterInside = await Eval<double[]>(page, zooms);
        Assert.Equal(afterFrame[0] * 1.3, afterInside[0], 6);
        Assert.Equal(start[1], afterInside[1], 6);

        await page.Locator("#by-name").ClickAsync();
        await page.Locator("#named-in-frame").ClickAsync();
        var afterNamed = await Eval<double[]>(page, zooms);
        Assert.Equal(afterInside[0], afterNamed[0], 6);
        Assert.Equal(start[1] * 1.3 * 1.3, afterNamed[1], 6);
        AssertQuiet();
    }

    [Fact]
    public async Task A_control_on_a_graph_that_has_not_started_starts_it()
    {
        if (NoBrowser) return;
        var page = await OpenGraph("""<div style="height: 400vh"></div>""" + Graph(attrs: "") + """
            <div style="position: fixed; top: 0; left: 0" data-graph-for="g">
              <button class="chip" type="button" data-graph-filter="kind" value="store" aria-pressed="true" id="stores">Stores</button>
            </div>
            """);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Null(await StateOf(page));

        await page.Locator("#stores").ClickAsync();
        await State(page, "g", "ready");
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('db').hasClass('hidden')");
        AssertQuiet();
    }

    // ── Reset, the colouring, the root, the stats ─────────────────────────────

    [Fact]
    public async Task Reset_puts_back_the_view_options_as_well_as_the_controls()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Framed("""
            <label><input type="radio" name="by" data-graph-option="colour" value="tone" checked id="by-tone" /> Kind</label>
            <label><input type="radio" name="by" data-graph-option="colour" value="team" id="by-team" /> Team</label>
            <label><input type="radio" name="lay" data-graph-option="layout" value="force" checked /> Map</label>
            <label><input type="radio" name="lay" data-graph-option="layout" value="grid" id="grid" /> Grid</label>
            <button type="button" data-graph-action="reset" id="reset">Reset</button>
            """));
        await Ready(page);
        Assert.Equal("tone", await page.Locator("#g").GetAttributeAsync("data-graph-colouring"));

        await page.Locator("#by-team").CheckAsync();
        await page.Locator("#grid").CheckAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('g').getAttribute('data-graph-colouring') === 'team'");

        await page.Locator("#reset").ClickAsync();
        // The radio and the drawing both: the colouring is the element's again, and so is the layout.
        await page.WaitForFunctionAsync("() => document.getElementById('g').getAttribute('data-graph-colouring') === 'tone'");
        Assert.True(await page.Locator("#by-tone").IsCheckedAsync());
        var layoutIsBack = await page.EvaluateAsync<bool>("""
            () => { const xs = new Set(cyOf('g').nodes().map(n => Math.round(n.position('x')))); return xs.size > 3; }
            """);
        Assert.True(layoutIsBack);
        AssertQuiet();
    }

    [Fact]
    public async Task A_chip_hides_the_root_of_its_kind_like_any_other_record()
    {
        if (NoBrowser) return;
        // The root is what the graph is about, but a legend key that is off means "not these".
        var page = await OpenGraph(Framed("""
            <button class="chip" type="button" data-graph-filter="kind" value="service" aria-pressed="true" id="services">Services</button>
            """, inside: Services.Replace("data-node=\"api\"", "data-root data-node=\"api\"", StringComparison.Ordinal)));
        await Ready(page);

        await page.Locator("#services").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('api').hasClass('hidden')");
        Assert.DoesNotContain("web", await Shown(page));
        AssertQuiet();
    }

    [Fact]
    public async Task Stats_inside_an_element_that_names_the_graph_are_kept_current()
    {
        if (NoBrowser) return;
        // data-graph-for on a container is the same claim for everything in it — controls,
        // stats, a panel — wherever the container is on the page.
        var page = await OpenGraph("""
            <section data-graph-for="g">
              <button class="chip" type="button" data-graph-filter="group" value="orders" aria-pressed="true" id="orders">Orders</button>
              <span data-graph-stats="{nodes} of {totalNodes}" id="stats"></span>
            </section>
            """ + Graph());
        await Ready(page);
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("7 of 7");

        await page.Locator("#orders").ClickAsync();
        await Assertions.Expect(page.Locator("#stats")).ToHaveTextAsync("4 of 7");
        AssertQuiet();
    }
}
