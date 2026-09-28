using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What the drawing looks like once it has settled: groups that do not overlap, names where
/// a reader looks for them, links along their routes, and marks that read on every tone.
/// </summary>
/// <remarks>
/// Each of these was a drawing that came out wrong on a catalogue page. They measure the
/// engine's own boxes rather than pixels, so they hold whatever the fonts on the machine.
/// </remarks>
public class GraphDrawingTests : GraphTestBase
{
    private const string Sites = """
        <ul class="graph-data" data-graph-data>
          <li data-node="north" data-tone="1">North site</li>
          <li data-node="south" data-tone="2">South site</li>
          <li data-node="rack-a1" data-parent="north">Rack A1</li>
          <li data-node="rack-a2" data-parent="north">Rack A2</li>
          <li data-node="rack-b1" data-parent="south">Rack B1</li>
          <li data-node="rack-b2" data-parent="south">Rack B2</li>
          <li data-node="web-01" data-parent="rack-a1">orders-console-01</li>
          <li data-node="api-01" data-parent="rack-a1">orders-api-01</li>
          <li data-node="db-14" data-parent="rack-a2">src-db-14</li>
          <li data-node="cache-01" data-parent="rack-a2">cache-01</li>
          <li data-node="web-03" data-parent="rack-b1">orders-console-03</li>
          <li data-node="api-02" data-parent="rack-b1">orders-api-02</li>
          <li data-node="runner-04" data-parent="rack-b1">build-runner-04</li>
          <li data-node="db-15" data-parent="rack-b2">src-db-15</li>
          <li data-node="backup-01" data-parent="rack-b2">backup-store-01</li>
          <li data-node="lb-01">lb-01</li>
          <li data-edge data-source="lb-01" data-target="web-01"></li>
          <li data-edge data-source="lb-01" data-target="web-03"></li>
          <li data-edge data-source="web-01" data-target="api-01"></li>
          <li data-edge data-source="api-01" data-target="db-14"></li>
          <li data-edge data-source="api-01" data-target="cache-01"></li>
          <li data-edge data-source="web-03" data-target="api-02"></li>
          <li data-edge data-source="api-02" data-target="db-15"></li>
          <li data-edge data-source="db-14" data-target="db-15" data-line="dashed"></li>
          <li data-edge data-source="db-15" data-target="backup-01"></li>
          <li data-edge data-source="runner-04" data-target="api-02" data-line="dotted"></li>
        </ul>
        """;

    /// <summary>
    /// Sibling groups whose boxes cross, and records lying on a group they are not in —
    /// each as "a/b", sorted. Boxes without the groups' own titles, with their records' names.
    /// </summary>
    private static Task<string[]> Overlaps(IPage page) => page.EvaluateAsync<string[]>("""
        () => {
            const cy = cyOf('g');
            const shown = cy.nodes().not('.hidden');
            const groups = shown.filter(n => n.isParent()).toArray();
            const records = shown.filter(n => !n.isParent()).toArray();
            const box = n => n.boundingBox({ includeLabels: false });
            const hit = (a, b) => a.x1 < b.x2 && b.x1 < a.x2 && a.y1 < b.y2 && b.y1 < a.y2;
            const out = [];
            for (let i = 0; i < groups.length; i++) for (let j = i + 1; j < groups.length; j++) {
                const a = groups[i], b = groups[j];
                if (a.ancestors().contains(b) || b.ancestors().contains(a)) continue;
                if (hit(box(a), box(b))) out.push(a.id() + '/' + b.id());
            }
            for (const g of groups) for (const r of records) {
                if (!r.ancestors().contains(g) && hit(box(g), box(r))) out.push(g.id() + '/' + r.id());
            }
            return out.sort();
        }
        """);

    [Theory]
    [InlineData("force")]
    [InlineData("fcose")]
    public async Task Groups_inside_groups_come_out_clear_of_each_other_and_of_the_records_outside(string layout)
    {
        if (NoBrowser) return;
        // A group is drawn around its records' names, which are held at their screen size —
        // so a layout measured at one zoom and seen at another grew its groups into each other.
        var page = await OpenGraph(Graph(inside: Sites, attrs: $"data-graph-eager data-graph-layout=\"{layout}\""));
        await Ready(page);

        Assert.Empty(await Overlaps(page));

        // Laid out again, and again after the view has moved: the same.
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.zoom(0.6))");
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.layout())");
        Assert.Empty(await Overlaps(page));
        AssertQuiet();
    }

    private const string Catalogue = """
        <ul class="graph-data" data-graph-data>
          <li data-node="shop" data-root>Northwind Retail</li>
          <li data-node="drinks">Beverages</li>
          <li data-node="sauces">Condiments</li>
          <li data-node="chai">Chai</li>
          <li data-node="chang">Chang</li>
          <li data-node="syrup">Aniseed Syrup</li>
          <li data-node="maple">Maple Syrup</li>
          <li data-edge data-source="shop" data-target="drinks"></li>
          <li data-edge data-source="shop" data-target="sauces"></li>
          <li data-edge data-source="drinks" data-target="chai"></li>
          <li data-edge data-source="drinks" data-target="chang"></li>
          <li data-edge data-source="sauces" data-target="syrup"></li>
          <li data-edge data-source="sauces" data-target="maple"></li>
        </ul>
        """;

    [Theory]
    [InlineData("TB", "bottom", "center")]
    [InlineData("LR", "center", "right")]
    [InlineData("RL", "center", "left")]
    public async Task A_hierarchy_that_runs_sideways_writes_each_name_beside_its_record(string direction, string valign, string halign)
    {
        if (NoBrowser) return;
        // A level of a sideways tree is a column: a name beneath each record would make the
        // column as tall as its records and their names, and the drawing a narrow band.
        var page = await OpenGraph(Graph(inside: Catalogue,
            attrs: $"data-graph-eager data-graph-layout=\"tree\" data-graph-direction=\"{direction}\""));
        await Ready(page);

        var placed = await page.EvaluateAsync<string[]>(
            "() => { const n = cyOf('g').getElementById('chai'); return [n.style('text-valign'), n.style('text-halign')]; }");
        Assert.Equal([valign, halign], placed);

        // The levels run the way asked: the root first, its children next, their children last.
        var at = await Positions(page);
        Func<(double X, double Y), double> along = direction is "LR" ? p => p.X : direction is "RL" ? p => -p.X : p => p.Y;
        Assert.True(along(at["shop"]) < along(at["drinks"]) && along(at["drinks"]) < along(at["chai"]));
        AssertQuiet();
    }

    private const string Flow = """
        <ul class="graph-data" data-graph-data>
          <li data-node="order">Order placed</li>
          <li data-node="paid">Paid</li>
          <li data-node="packed">Packed</li>
          <li data-node="shipped">Shipped</li>
          <li data-edge data-source="order" data-target="paid"></li>
          <li data-edge data-source="paid" data-target="packed"></li>
          <li data-edge data-source="packed" data-target="shipped"></li>
          <li data-edge data-source="order" data-target="shipped">cancelled and refunded</li>
        </ul>
        """;

    [Fact]
    public async Task A_layered_layout_draws_each_link_along_its_route_unless_the_app_chose_a_curve()
    {
        if (NoBrowser) return;
        // A link that skips a rank drawn straight goes through the records in the ranks it
        // skips; the layout found a way round them, and that is what is drawn.
        var page = await OpenGraph(Graph(inside: Flow, attrs: "data-graph-eager data-graph-layout=\"dagre\" data-graph-nodes=\"box\""));
        await Ready(page);
        var routed = await page.EvaluateAsync<JsonElement>("""
            () => {
                const e = cyOf('g').edges().filter(e => e.source().id() === 'order' && e.target().id() === 'shipped');
                return { curve: e.style('curve-style'), points: (e.scratch('controlPointDistances') || []).length };
            }
            """);
        Assert.Equal("unbundled-bezier", routed.GetProperty("curve").GetString());
        Assert.True(routed.GetProperty("points").GetInt32() > 0);

        var chosen = await OpenGraph(Graph(inside: Flow,
            attrs: "data-graph-eager data-graph-layout=\"dagre\" data-graph-nodes=\"box\" data-graph-curve=\"taxi\""));
        await Ready(chosen);
        Assert.Equal("taxi", await chosen.EvaluateAsync<string>("() => cyOf('g').edges().first().style('curve-style')"));
        AssertQuiet();
    }

    [Fact]
    public async Task Link_names_written_always_keep_their_size_on_screen_when_zoomed_out()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(attrs: "data-graph-eager data-graph-edge-labels=\"always\""));
        await Ready(page);
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => { g.cy.zoom(0.5); g.cy.emit('zoom'); })");
        // The view settles a moment after the wheel rests.
        await page.WaitForFunctionAsync("() => cyOf('g').edges().first().data('zoom') < 0.6");
        var onScreen = await page.EvaluateAsync<double>(
            "() => { const cy = cyOf('g'); const e = cy.edges().filter(e => e.data('label')).first(); return e.numericStyle('font-size') * cy.zoom(); }");
        Assert.InRange(onScreen, 9, 11);
        AssertQuiet();
    }

    [Fact]
    public async Task A_search_match_is_ringed_in_the_text_colour_inside_a_halo_so_it_reads_on_every_tone()
    {
        if (NoBrowser) return;
        // An accent ring alone disappears on a record whose tone is the accent's hue.
        var page = await OpenGraph(Graph(inside: Services + """
            <input type="search" data-graph-search id="find" aria-label="Find" />
            """));
        await Ready(page);
        await page.Locator("#find").FillAsync("orders api");
        await page.WaitForFunctionAsync("() => cyOf('g').nodes('.match').length === 1");

        var mark = await page.EvaluateAsync<JsonElement>("""
            () => {
                const n = cyOf('g').getElementById('api');
                const probe = document.createElement('span');
                probe.style.color = 'var(--fg)';
                document.body.appendChild(probe);
                const fg = getComputedStyle(probe).color;
                probe.remove();
                return { border: n.style('border-color'), fg, halo: n.numericStyle('underlay-opacity') };
            }
            """);
        Assert.Equal(Normalise(mark.GetProperty("fg").GetString()!), Normalise(mark.GetProperty("border").GetString()!));
        Assert.True(mark.GetProperty("halo").GetDouble() > 0);
        AssertQuiet();

        static string Normalise(string rgb) => rgb.Replace(" ", "", StringComparison.Ordinal);
    }
}
