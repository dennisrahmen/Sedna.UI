using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The graph through a real Blazor circuit, on <c>/graph-blazor</c>: records sent from
/// <c>OnAfterRenderAsync</c>, the reader's clicks reaching <c>@onsedna-graph-*</c> with their
/// data, and the component's own render following what it was told.
/// </summary>
/// <remarks>
/// The library's suite pins the call shape and the wire format of <c>ISednaGraphs</c>. What only a
/// running app shows is the round trip: that <c>Sedna.UI.lib.module.js</c> registered the events,
/// that a handler's re-send reaches the drawing, and that the component renders what came back.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class GraphBlazorTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open(List<string>? errors = null)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        if (errors is not null)
        {
            page.PageError += (_, error) => errors.Add(error);
            page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        }

        await page.GotoAsync(app.Url("/graph-blazor"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        // The records arrive by call, so a deferred graph that is ready has heard from C#.
        await page.WaitForSelectorAsync("#checkout-services[data-graph-state='ready']", new() { Timeout = 15_000 });
        return page;
    }

    private static async Task Ready(IPage page, string graph)
    {
        await page.Locator("#" + graph).ScrollIntoViewIfNeededAsync();
        await page.WaitForSelectorAsync($"#{graph}[data-graph-state='ready']", new() { Timeout = 15_000 });
        // The first drawing travels into place; a click during it lands where a record was.
        await page.WaitForTimeoutAsync(600);
    }

    private static ILocator Example(IPage page, string graph) =>
        page.Locator("#" + graph).Locator("xpath=ancestor::section[@data-example][1]").Locator(".ex-demo");

    private static Task<T> Cy<T>(IPage page, string graph, string expression) =>
        page.EvaluateAsync<T>(
            $"() => {{ const cy = document.getElementById('{graph}').querySelector('.graph-canvas')._cyreg.cy; return {expression}; }}");

    private static async Task<(float X, float Y)> At(IPage page, string graph, string node)
    {
        var p = await page.EvaluateAsync<float[]>(
            """
            ([g, n]) => {
                const cy = document.getElementById(g).querySelector('.graph-canvas')._cyreg.cy;
                const at = cy.getElementById(n).renderedPosition();
                const box = cy.container().getBoundingClientRect();
                return [box.left + at.x, box.top + at.y];
            }
            """, new[] { graph, node });
        return (p[0], p[1]);
    }

    [Fact]
    public async Task Records_sent_from_the_first_render_are_drawn_and_counted_by_the_component()
    {
        if (app.NoBrowser) return;
        var page = await Open();

        Assert.Equal(7, await Cy<int>(page, "checkout-services", "cy.nodes().length"));
        // Rendered by the component from the SednaGraphStats that SetDataAsync answered.
        Assert.Contains("7 services, 7 calls", await Example(page, "checkout-services").InnerTextAsync(), StringComparison.Ordinal);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_click_and_a_double_click_reach_the_component_with_the_records_data()
    {
        if (app.NoBrowser) return;
        var page = await Open();
        await Ready(page, "owned-services");
        var demo = Example(page, "owned-services");

        var orders = await At(page, "owned-services", "orders");
        await page.Mouse.ClickAsync(orders.X, orders.Y);
        // data-owner and data-on-call, as Fields["owner"] and Fields["onCall"].
        await demo.Locator("aside", new() { HasText = "Jordan Weiss" }).WaitForAsync(new() { Timeout = 5_000 });
        Assert.Contains("Priya Nair", await demo.Locator("aside").InnerTextAsync(), StringComparison.Ordinal);

        var url = page.Url;
        var payments = await At(page, "owned-services", "payments");
        await page.Mouse.DblClickAsync(payments.X, payments.Y);
        await page.Locator(".toast", new() { HasText = "/services/payments" }).WaitForAsync(new() { Timeout = 5_000 });
        // :preventDefault reached the script, so the record's data-href was not followed.
        Assert.Equal(url, page.Url);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Lists_sent_again_move_only_the_new_record_and_its_neighbours()
    {
        if (app.NoBrowser) return;
        var page = await Open();
        await Ready(page, "fulfilment");
        var demo = Example(page, "fulfilment");
        const string positions =
            "Object.fromEntries(['orders', 'warehouse-north', 'warehouse-south'].map(id => [id, cy.getElementById(id).position()]))";

        var before = await Cy<Dictionary<string, Dictionary<string, double>>>(page, "fulfilment", positions);
        await demo.GetByRole(AriaRole.Button, new() { Name = "Open a warehouse" }).ClickAsync();
        await demo.GetByText("6 records, 7 links").WaitForAsync(new() { Timeout = 5_000 });
        await page.WaitForTimeoutAsync(600);
        var after = await Cy<Dictionary<string, Dictionary<string, double>>>(page, "fulfilment", positions);

        Assert.True(await Cy<bool>(page, "fulfilment", "cy.getElementById('warehouse-east').nonempty()"));
        foreach (var (id, at) in before)
        {
            Assert.Equal(at["x"], after[id]["x"], 0.5);
            Assert.Equal(at["y"], after[id]["y"], 0.5);
        }

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_razor_render_that_changes_the_list_changes_the_drawing()
    {
        if (app.NoBrowser) return;
        var page = await Open();
        await Ready(page, "release-pipeline");

        Assert.False(await Cy<bool>(page, "release-pipeline", "cy.getElementById('load').nonempty()"));
        await Example(page, "release-pipeline").Locator("label.switch", new() { HasText = "Load test" }).ClickAsync();

        await page.WaitForFunctionAsync(
            "() => document.getElementById('release-pipeline').querySelector('.graph-canvas')._cyreg.cy.edges('[source = \"load\"]').length === 1",
            null, new() { Timeout = 5_000 });

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_link_the_reader_draws_reaches_the_component_which_sends_it_back()
    {
        if (app.NoBrowser) return;
        var page = await Open();
        await Ready(page, "drawn-calls");
        var demo = Example(page, "drawn-calls");

        await demo.Locator("label.switch", new() { HasText = "Draw links" }).ClickAsync();
        await page.WaitForSelectorAsync("#drawn-calls[data-graph-drawing]", new() { Timeout = 5_000 });

        var from = await At(page, "drawn-calls", "orders");
        var to = await At(page, "drawn-calls", "payments");
        await page.Mouse.MoveAsync(from.X, from.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((from.X + to.X) / 2, (from.Y + to.Y) / 2, new() { Steps = 10 });
        await page.Mouse.MoveAsync(to.X, to.Y, new() { Steps = 10 });
        // The plugin snaps to a record it has hovered for a moment, not one passed over.
        await page.WaitForTimeoutAsync(300);
        await page.Mouse.UpAsync();

        await demo.Locator(".chip", new() { HasText = "Orders API calls Payments" }).WaitForAsync(new() { Timeout = 5_000 });
        Assert.True(await Cy<bool>(page, "drawn-calls", "cy.edges('[source = \"orders\"][target = \"payments\"]').length === 1"));

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Filters_search_and_focus_from_csharp_answer_what_is_on_screen()
    {
        if (app.NoBrowser) return;
        var page = await Open();

        await Ready(page, "filtered-services");
        var filters = Example(page, "filtered-services");
        await filters.Locator("select").SelectOptionAsync("checkout");
        await filters.GetByText("Showing 4 of 12 services").WaitForAsync(new() { Timeout = 5_000 });
        await filters.Locator("label.form-check", new() { HasText = "Stores" }).ClickAsync();
        await filters.GetByText("Showing 3 of 12 services").WaitForAsync(new() { Timeout = 5_000 });

        await Ready(page, "explored-services");
        var explore = Example(page, "explored-services");
        await explore.Locator("input[type=search]").FillAsync("orders");
        await explore.GetByText("2 found").WaitForAsync(new() { Timeout = 5_000 });
        await explore.Locator("select").SelectOptionAsync("payments");
        await explore.GetByText("2 of 7 services shown").WaitForAsync(new() { Timeout = 5_000 });

        await explore.GetByRole(AriaRole.Button, new() { Name = "Picture" }).ClickAsync();
        await explore.Locator("img[src^='data:image/svg+xml']").WaitForAsync(new() { Timeout = 5_000 });

        await page.Context.CloseAsync();
    }

    // ── Engine faults found while writing this page ─────────────────────────────────

    [Fact]
    public async Task A_click_on_a_record_leaves_the_keys_with_the_graph()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);
        await Ready(page, "fulfilment");

        var courier = await At(page, "fulfilment", "courier");
        await page.Mouse.ClickAsync(courier.X, courier.Y);
        await page.WaitForTimeoutAsync(400);

        // wire() focuses the graph on pointerdown "so the keys carry on from there", but the
        // engine's own mousedown handler blurs the active element straight after: focus ends on
        // <body>. The focus in between matches :focus-visible, so the keyboard's key(start())
        // runs and animates to the root, and its complete callback then shows a tooltip for a
        // record the blur already cleared — TypeError: Cannot read properties of null
        // (reading 'inside'), from 52-keyboard.js.
        Assert.Empty(errors);
        Assert.Equal("fulfilment", await page.EvaluateAsync<string?>("() => document.activeElement?.id"));

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_deferred_graphs_first_drawing_is_fitted_to_where_its_records_end()
    {
        if (app.NoBrowser) return;
        var page = await Open();
        string[] deferred = ["checkout-services", "fulfilment", "drawn-calls", "filtered-services", "explored-services"];
        var outside = new List<string>();

        // The first SetDataAsync lays the records out and animates them from where they were
        // placed, fitting the view to where they end. The ResizeObserver's first notification —
        // one is always delivered after observe() — lands inside that animation, and its
        // fitView(g, null, false) stops the animated fit and fits to the records mid-flight. Which
        // graph it catches depends on timing, so every one on the page is checked.
        foreach (var graph in deferred)
        {
            await Ready(page, graph);
            var ids = await Cy<string[]>(page, graph,
                "cy.nodes().filter(n => { const p = n.renderedPosition(); return p.x < 0 || p.y < 0 || p.x > cy.width() || p.y > cy.height(); }).map(n => n.id())");
            outside.AddRange(ids.Select(id => $"{graph}: {id}"));
        }

        Assert.Empty(outside);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_call_on_a_graph_whose_records_did_not_load_answers_instead_of_throwing()
    {
        if (app.NoBrowser) return;
        var page = await Open();

        // The bridge's contract, in ISednaGraphs and in graph-parts/CLAUDE.md: it answers null
        // and warns for anything it cannot do, because a rejection reaches the component as a
        // JSException and an unhandled one in a handler tears the circuit down. A graph whose
        // data-graph-src failed has a rejected start, and invoke() awaits it — so every call on
        // it rejects, StatsAsync as much as ReloadAsync, and reload() of a source that fails now
        // rejects the same way.
        var answers = await page.EvaluateAsync<string[]>(
            """
            async () => {
                document.body.insertAdjacentHTML('beforeend',
                    '<div class="graph graph--sm" data-graph data-graph-eager data-graph-src="/api/not-a-graph" ' +
                    'id="failed-probe" aria-label="Probe"><div class="graph-canvas"></div></div>');
                const answer = method => sednaUi.graph.invoke('failed-probe', method, [])
                    .then(r => method + ': ' + JSON.stringify(r), e => method + ': rejected, ' + e.message);
                return [await answer('stats'), await answer('reload')];
            }
            """);

        Assert.All(answers, a => Assert.DoesNotContain("rejected", a, StringComparison.Ordinal));

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_run_stepped_from_csharp_is_drawn_where_it_stands_and_the_links_follow_their_targets()
    {
        if (app.NoBrowser) return;
        var page = await Open();
        await Ready(page, "order-run");
        var demo = Example(page, "order-run");
        const string graph = "order-run";
        const string state = "id => document.getElementById('order-run').querySelector('.graph-canvas')._cyreg.cy.getElementById(id)";
        const string positions = "Object.fromEntries(cy.nodes().map(n => [n.id(), n.position()]))";

        Assert.Equal("next", await Cy<string>(page, graph, "cy.getElementById('received').data('state')"));
        var before = await Cy<Dictionary<string, Dictionary<string, double>>>(page, graph, positions);

        var step = demo.GetByRole(AriaRole.Button, new() { Name = "Step" });
        await step.ClickAsync();
        await page.WaitForFunctionAsync($"() => ({state})('received').data('state') === 'running'", null, new() { Timeout = 5_000 });
        await step.ClickAsync();
        await page.WaitForFunctionAsync($"() => ({state})('stock').data('state') === 'running'", null, new() { Timeout = 5_000 });
        await demo.GetByText("1 of 8 steps done").WaitForAsync(new() { Timeout = 5_000 });

        // The links carry no state of their own and take their targets'.
        Assert.Null(await Cy<string?>(page, graph, "cy.getElementById('received→stock').data('state')"));
        Assert.Equal("running", await Cy<string>(page, graph, "cy.getElementById('received→stock').data('run')"));
        // A step is new data, not a new drawing: nothing moved.
        var after = await Cy<Dictionary<string, Dictionary<string, double>>>(page, graph, positions);
        foreach (var (id, at) in before)
        {
            Assert.Equal(at["x"], after[id]["x"], 0.5);
            Assert.Equal(at["y"], after[id]["y"], 0.5);
        }

        await demo.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();
        await page.WaitForFunctionAsync($"() => ({state})('stock').data('state') === null", null, new() { Timeout = 5_000 });
        Assert.Equal("next", await Cy<string>(page, graph, "cy.getElementById('received').data('state')"));

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Collapsed_sent_by_a_call_starts_the_group_folded()
    {
        if (app.NoBrowser) return;
        var page = await Open();

        // The JSON SetDataAsync sends for SednaGraphNode.Collapsed = true, which GraphInteropTests
        // pins. The same records written as markup, with data-collapsed, start folded; sent by a
        // call they do not — setData() folds only the groups that were folded before it, and
        // plugins() reads `collapsed` once, from the records a graph starts with.
        var folded = await page.EvaluateAsync<string>(
            """
            async () => {
                document.body.insertAdjacentHTML('beforeend',
                    '<div class="graph graph--sm" data-graph data-graph-eager data-graph-deferred data-graph-collapse ' +
                    'id="collapse-probe" aria-label="Probe"><div class="graph-canvas"></div></div>');
                const graph = await sednaUi.graph.get('collapse-probe');
                await graph.set({
                    nodes: [
                        { id: 'checkout', label: 'Checkout', collapsed: true },
                        { id: 'basket', label: 'Basket', parent: 'checkout' },
                        { id: 'payments', label: 'Payments', parent: 'checkout' },
                        { id: 'search', label: 'Search' },
                    ],
                    edges: [{ source: 'basket', target: 'search' }],
                });
                await new Promise(r => setTimeout(r, 800));
                return graph.cy.nodes('.cy-expand-collapse-collapsed-node').map(n => n.id()).join(',');
            }
            """);

        Assert.Equal("checkout", folded);

        await page.Context.CloseAsync();
    }
}
