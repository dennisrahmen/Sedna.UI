using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What a large graph costs a reader's action, counted rather than timed: how many elements
/// the engine restyles for it.
/// </summary>
/// <remarks>
/// <para>
/// A restyle is the most expensive thing the graph does, and one that touched every element
/// is what froze a graph of a few thousand records under the pointer — seconds a hover. A
/// count holds on any machine, where a time bound has to be loose enough for the slowest
/// runner to pass it. The engine announces every element it restyles with a <c>style</c>
/// event and every change to an element's data with a <c>data</c> event; these tests count
/// both, and hold each action to a small fraction of the graph.
/// </para>
/// <para>
/// <see cref="GraphPerformanceTests"/> times the one thing a count cannot stand for: laying
/// the whole drawing out.
/// </para>
/// </remarks>
public class GraphScaleTests : GraphTestBase
{
    // Thirty teams of thirty: nine hundred records, a team's head the busiest, a chain of
    // links through each team and a bridge from every tenth record to another team.
    private const string Build = """
        () => {
            let s = 0x5eda;
            const rnd = () => (s = (s * 1664525 + 1013904223) >>> 0) / 4294967296;
            const idOf = (g, i) => (i === 0 ? `team-${g}` : `t${g}-${i}`);
            const nodes = [], edges = [];
            for (let g = 0; g < 30; g++)
                for (let i = 0; i < 30; i++)
                    nodes.push({ id: idOf(g, i), label: i === 0 ? `Team ${g}` : `host-${g}-${i}`, group: `team-${g}`,
                                 kind: i % 3 ? 'service' : 'store', tone: String(1 + (g % 6)) });
            for (let g = 0; g < 30; g++) {
                for (let i = 1; i < 30; i++) edges.push({ source: idOf(g, Math.floor(rnd() * i)), target: idOf(g, i) });
                for (let i = 0; i < 27; i++) edges.push({ source: idOf(g, i), target: idOf(g, (i + 3 + Math.floor(rnd() * 20)) % 30) });
                for (let i = 0; i < 30; i += 10) edges.push({ source: idOf(g, i), target: idOf((g + 1 + Math.floor(rnd() * 28)) % 30, Math.floor(rnd() * 30)) });
            }
            return { nodes, edges };
        }
        """;

    /// <summary>A large graph, drawn, with a count of what the engine restyles from now on.</summary>
    private async Task<IPage> Large()
    {
        var page = await OpenGraph(Graph(inside: "", attrs: "data-graph-eager data-graph-deferred data-graph-layout=\"islands\""));
        await Ready(page);
        await page.EvaluateAsync($$"""
            async () => {
                const g = await sednaUi.graph.get('g');
                window.graphData = ({{Build}})();
                await g.set(window.graphData);
                window.restyled = 0;
                window.changedData = 0;
                cyOf('g').on('style', () => { window.restyled++; });
                cyOf('g').on('data', () => { window.changedData++; });
            }
            """);
        await Quiet(page);
        return page;
    }

    /// <summary>Waits out whatever the last action left on timers, then zeroes the counts.</summary>
    private static async Task Quiet(IPage page)
    {
        await page.WaitForTimeoutAsync(400);
        await page.EvaluateAsync("() => { window.restyled = 0; window.changedData = 0; }");
    }

    private static Task<int> Elements(IPage page) => page.EvaluateAsync<int>("() => cyOf('g').elements().length");

    [Fact]
    public async Task Pointing_at_a_record_restyles_its_neighbourhood_not_the_graph()
    {
        if (NoBrowser) return;
        var page = await Large();
        var all = await Elements(page);

        var (x, y) = await PointOf(page, "team-3");
        await page.Mouse.MoveAsync(x - 40, y - 40);
        await Quiet(page);
        await page.Mouse.MoveAsync(x, y, new() { Steps = 4 });
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('team-3').hasClass('lit')");
        await page.WaitForTimeoutAsync(300);
        var lit = await page.EvaluateAsync<int>("() => restyled");
        Assert.True(lit < all / 10, $"Pointing at one record restyled {lit} of {all} elements.");

        await page.Mouse.MoveAsync(2, 2);
        await page.WaitForFunctionAsync("() => cyOf('g').elements('.lit').length === 0");
        await page.WaitForTimeoutAsync(300);
        var both = await page.EvaluateAsync<int>("() => restyled");
        Assert.True(both < all / 5, $"Pointing at one record and leaving it restyled {both} of {all} elements.");
        AssertQuiet();
    }

    [Fact]
    public async Task Selecting_a_record_and_clearing_it_restyles_a_fraction_of_the_graph()
    {
        if (NoBrowser) return;
        var page = await Large();
        var all = await Elements(page);

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.select('team-7'))");
        await page.WaitForTimeoutAsync(500);
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.select(null))");
        await page.WaitForTimeoutAsync(500);
        var count = await page.EvaluateAsync<int>("() => restyled");
        Assert.True(count < all / 5, $"Selecting a record and clearing it restyled {count} of {all} elements.");
        AssertQuiet();
    }

    [Fact]
    public async Task A_page_attribute_that_changes_no_colour_restyles_nothing()
    {
        if (NoBrowser) return;
        // A dialog locking the scroll, a class an app toggles on <html>: the canvas repaints
        // only when a colour it is painted with has changed.
        var page = await Large();
        await page.EvaluateAsync("""
            () => {
                document.documentElement.classList.add('app-busy');
                document.documentElement.style.overflow = 'hidden';
            }
            """);
        await page.WaitForTimeoutAsync(400);
        Assert.Equal(0, await page.EvaluateAsync<int>("() => restyled"));

        // The theme itself still repaints it.
        await page.EvaluateAsync("() => sednaUi.settings.save('variant', 'light')");
        await page.WaitForFunctionAsync("() => restyled > 0");
        AssertQuiet();
    }

    [Fact]
    public async Task New_data_that_repeats_the_old_changes_only_what_it_adds()
    {
        if (NoBrowser) return;
        var page = await Large();
        var all = await Elements(page);

        await page.EvaluateAsync("""
            async () => {
                const g = await sednaUi.graph.get('g');
                const d = window.graphData;
                d.nodes.push({ id: 'new-1', label: 'new-host-1', group: 'team-1' });
                d.edges.push({ source: 'team-1', target: 'new-1' });
                await g.set(d);
            }
            """);
        await page.WaitForTimeoutAsync(400);
        Assert.Equal(901, (await Shown(page)).Length);
        var changed = await page.EvaluateAsync<int>("() => changedData");
        var restyled = await page.EvaluateAsync<int>("() => restyled");
        Assert.True(changed < 20, $"One more record changed the data of {changed} elements.");
        Assert.True(restyled < all / 10, $"One more record restyled {restyled} of {all} elements.");
        AssertQuiet();
    }

    [Fact]
    public async Task A_run_moving_on_restyles_the_records_it_moves_and_the_links_into_them()
    {
        if (NoBrowser) return;
        var page = await Large();
        var all = await Elements(page);
        var before = await Positions(page);

        // The same records with three states set: a run moving through a large map.
        await page.EvaluateAsync("""
            async () => {
                const g = await sednaUi.graph.get('g');
                const d = window.graphData;
                const states = { 'team-4': 'done', 't4-1': 'running', 't4-2': 'next' };
                await g.set({ nodes: d.nodes.map(n => (states[n.id] ? Object.assign({}, n, { state: states[n.id] }) : n)), edges: d.edges });
            }
            """);
        await page.WaitForTimeoutAsync(400);

        // The three records, and the links into them, which take their state.
        var expected = await page.EvaluateAsync<int>(
            "() => cyOf('g').$('#team-4, #t4-1, #t4-2').union(cyOf('g').$('#team-4, #t4-1, #t4-2').incomers('edge')).length");
        Assert.Equal(expected, await page.EvaluateAsync<int>("() => changedData"));
        var restyled = await page.EvaluateAsync<int>("() => restyled");
        Assert.True(restyled < all / 20, $"Three new states restyled {restyled} of {all} elements.");
        // Nothing was laid out again.
        var after = await Positions(page);
        Assert.All(before, p => Assert.Equal(p.Value, after[p.Key]));
        AssertQuiet();
    }

    [Fact]
    public async Task A_view_that_rests_restyles_what_is_near_it_not_what_is_far_away()
    {
        if (NoBrowser) return;
        // Zoomed in close, most of a large drawing is off screen: its names and sizes are
        // brought up to date when the view comes near them, not on every step of the zoom.
        var page = await Large();
        var all = await Elements(page);
        await page.EvaluateAsync("""
            () => {
                const cy = cyOf('g');
                cy.zoom({ level: 2.5, renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 } });
            }
            """);
        await page.WaitForTimeoutAsync(500);
        await page.EvaluateAsync("() => { restyled = 0; }");
        await page.EvaluateAsync("""
            () => {
                const cy = cyOf('g');
                cy.zoom({ level: 3, renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 } });
            }
            """);
        await page.WaitForTimeoutAsync(500);
        var count = await page.EvaluateAsync<int>("() => restyled");
        Assert.True(count < all / 3, $"A step of the zoom close in restyled {count} of {all} elements.");

        // Far off, the drawing still holds an older zoom; what the view reaches is current
        // when it gets there.
        var far = await page.EvaluateAsync<string>("""
            () => {
                const cy = cyOf('g'), e = cy.extent(), cx = (e.x1 + e.x2) / 2, cy0 = (e.y1 + e.y2) / 2;
                let far = null, most = -1;
                cy.nodes().forEach(n => { const p = n.position(), d = Math.hypot(p.x - cx, p.y - cy0); if (d > most) { most = d; far = n; } });
                return far.id();
            }
            """);
        Assert.True(await page.EvaluateAsync<bool>("""
            id => {
                const cy = cyOf('g'), e = cy.extent();
                const seen = cy.nodes().filter(n => { const p = n.position(); return p.x > e.x1 && p.x < e.x2 && p.y > e.y1 && p.y < e.y2; });
                return cy.getElementById(id).data('zoom') !== seen[0].data('zoom');
            }
            """, far), "A record far from the view was restyled for it.");
        await page.EvaluateAsync("id => cyOf('g').center(cyOf('g').getElementById(id))", far);
        await page.WaitForTimeoutAsync(500);
        Assert.True(await page.EvaluateAsync<bool>("""
            () => {
                const cy = cyOf('g'), e = cy.extent();
                const seen = cy.nodes().filter(n => { const p = n.position(); return p.x > e.x1 && p.x < e.x2 && p.y > e.y1 && p.y < e.y2; });
                const z = seen[0].data('zoom');
                return seen.nonempty() && z > 1 && seen.every(n => n.data('zoom') === z);
            }
            """), "A record the view reached still holds the zoom of an earlier view.");

        // An export draws the whole drawing, so everything is brought up to date for it first.
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.export('svg'))");
        Assert.True(await page.EvaluateAsync<bool>("""
            () => { const cy = cyOf('g'), z = cy.nodes()[0].data('zoom');
                    return z > 1 && cy.nodes().every(n => n.data('zoom') === z); }
            """), "An export drew records at the zoom of an earlier view.");
        AssertQuiet();
    }

    [Fact]
    public async Task Laying_the_drawing_out_again_draws_the_islands_it_drew_and_restyles_little()
    {
        if (NoBrowser) return;
        // An island whose records and links are unchanged is put back as it was, which is
        // what springing it again would give, so the drawing is the same — after a plain
        // re-layout, and after a group is hidden and shown again.
        var page = await Large();
        var all = await Elements(page);
        var first = await Positions(page);

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.layout())");
        await page.WaitForTimeoutAsync(600);
        var restyled = await page.EvaluateAsync<int>("() => restyled");
        Assert.True(restyled < all / 3, $"Laying the drawing out again restyled {restyled} of {all} elements.");
        SamePlaces(first, await Positions(page), "after laying it out again");

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.filter({ except: { group: ['team-3'] } }))");
        Assert.Equal(870, (await Shown(page)).Length);
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.filter({}))");
        await page.WaitForTimeoutAsync(600);
        SamePlaces(first, await Positions(page), "after a group was hidden and shown");
        AssertQuiet();
    }

    /// <summary>Every record where it was, naming the first few that moved.</summary>
    private static void SamePlaces(Dictionary<string, (double X, double Y)> expected, Dictionary<string, (double X, double Y)> actual, string when)
    {
        var moved = expected.Where(e => !actual.TryGetValue(e.Key, out var a) || a != e.Value)
            .Select(e => $"{e.Key} {e.Value} → {(actual.TryGetValue(e.Key, out var a) ? a.ToString() : "gone")}").ToList();
        Assert.True(moved.Count == 0, $"{moved.Count} records moved {when}: {string.Join("; ", moved.Take(6))}");
    }

    // ── What the review found ───────────────────────────────────────────────────

    [Fact]
    public async Task A_curve_a_control_chooses_is_drawn_over_a_layered_layouts_route()
    {
        if (NoBrowser) return;
        var page = await OpenGraph($$"""
            {{Graph(attrs: "data-graph-eager data-graph-layout=\"dagre\"")}}
            <select data-graph-option="curve" data-graph-for="g" id="curve">
              <option value="">As the layout routes it</option>
              <option value="taxi">Right angles</option>
            </select>
            """);
        await Ready(page);
        // An empty choice is the layout's own route.
        const string Curve = "cyOf('g').edges().filter(e => e.data('source') === 'api' && e.data('target') === 'db')[0].style('curve-style')";
        Assert.Equal("unbundled-bezier", await page.EvaluateAsync<string>("() => " + Curve));

        await page.Locator("#curve").SelectOptionAsync("taxi");
        await page.WaitForFunctionAsync("() => " + Curve + " === 'taxi'");
        AssertQuiet();
    }

    [Fact]
    public async Task A_field_whose_name_starts_with_tone_is_a_field_and_not_a_colouring()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="printer" data-toner="black" data-tone-status="warn">Printer</li>
            </ul>
            """));
        await Ready(page);
        var data = await page.EvaluateAsync<JsonElement>("() => { const d = cyOf('g').getElementById('printer').data(); return { fields: d.fields, tones: d.tones }; }");
        Assert.Equal("black", data.GetProperty("fields").GetProperty("toner").GetString());
        Assert.Equal("warn", data.GetProperty("tones").GetProperty("status").GetString());
        Assert.False(data.GetProperty("tones").TryGetProperty("r", out _));
        AssertQuiet();
    }

    [Fact]
    public async Task A_tone_that_names_what_every_object_has_is_refused_like_any_other()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: """
            <ul class="graph-data" data-graph-data>
              <li data-node="a" data-tone="constructor" data-shape="toString">A record</li>
            </ul>
            """));
        await Ready(page);
        var data = await page.EvaluateAsync<JsonElement>("() => { const d = cyOf('g').getElementById('a').data(); return { tone: d.tone, shape: d.shape }; }");
        Assert.Equal(JsonValueKind.Null, data.GetProperty("tone").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("shape").ValueKind);
        AssertQuiet("is not a tone", "is not a shape");
    }

    [Fact]
    public async Task Another_element_leaving_full_screen_leaves_the_view_the_reader_made()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph());
        await Ready(page);
        await page.EvaluateAsync("() => { const cy = cyOf('g'); cy.emit('dragpan'); cy.panBy({ x: 120, y: 40 }); }");
        await page.WaitForTimeoutAsync(300);
        var before = await page.EvaluateAsync<JsonElement>("() => cyOf('g').pan()");

        // A video on the page going to full screen and back: nothing of the graph's.
        await page.EvaluateAsync("() => document.dispatchEvent(new Event('fullscreenchange'))");
        await page.WaitForTimeoutAsync(300);
        var after = await page.EvaluateAsync<JsonElement>("() => cyOf('g').pan()");
        Assert.Equal(before.GetProperty("x").GetDouble(), after.GetProperty("x").GetDouble(), 3);
        Assert.Equal(before.GetProperty("y").GetDouble(), after.GetProperty("y").GetDouble(), 3);
        AssertQuiet();
    }

    [Fact]
    public async Task A_direction_control_takes_the_names_the_attribute_takes()
    {
        if (NoBrowser) return;
        // `right` is LR on the element, so it is on a control too — and a sideways tree writes
        // its names beside its records.
        var page = await OpenGraph($$"""
            {{Graph(attrs: "data-graph-eager data-graph-layout=\"tree\"")}}
            <select data-graph-option="direction" data-graph-for="g" id="direction">
              <option value="down">Down</option>
              <option value="right">Right</option>
            </select>
            """);
        await Ready(page);
        await page.Locator("#direction").SelectOptionAsync("right");
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('db').style('text-halign') === 'right'");
        AssertQuiet();
    }
}
