using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A graph on a screen read from across a room: sharp when the app scales its frame with CSS
/// <c>zoom</c>, filling the frame with a handful of records when the app asks, the same drawing
/// bigger at <c>data-graph-scale</c>, and no focus ring round the whole graph after a finger taps it.
/// </summary>
public class GraphScreenTests : GraphTestBase
{
    // Three records close together: a box far smaller than any frame, so a fit stops at its
    // ceiling rather than at the frame's edge.
    private const string Few = """
        <ul class="graph-data" data-graph-data>
          <li data-node="a" data-x="0" data-y="0">Orders API</li>
          <li data-node="b" data-x="60" data-y="0">Orders database</li>
          <li data-node="c" data-x="0" data-y="60">Order queue</li>
          <li data-edge data-source="a" data-target="b">reads</li>
          <li data-edge data-source="a" data-target="c">publishes</li>
        </ul>
        """;

    private const string Preset = "data-graph-eager data-graph-layout=\"preset\"";

    /// <summary>Device pixels per CSS pixel of the graph's drawing canvas.</summary>
    private static Task<double> CanvasRatio(IPage page) =>
        Eval<double>(page, """
            () => {
                const c = document.querySelector('#g .graph-canvas canvas');
                return c.width / c.getBoundingClientRect().width * c.currentCSSZoom;
            }
            """);

    private static Task WaitForRatio(IPage page, double ratio) =>
        page.WaitForFunctionAsync("""
            ratio => {
                const c = document.querySelector('#g .graph-canvas canvas');
                return Math.abs(c.width / c.getBoundingClientRect().width * c.currentCSSZoom - ratio) < 0.02;
            }
            """, ratio);

    [Fact]
    public async Task A_frame_scaled_with_css_zoom_draws_its_canvas_at_the_scaled_resolution()
    {
        if (NoBrowser) return;
        var page = await OpenGraph($"""<div id="frame" style="zoom: 2">{Graph(inside: Few, attrs: Preset)}</div>""");
        await Ready(page);

        // Twice as many device pixels as the screen's own ratio of one.
        Assert.Equal(2, await CanvasRatio(page), 2);
        var zoom = await Eval<double>(page, "() => cyOf('g').zoom()");
        var name = await Eval<double>(page, "() => cyOf('g').getElementById('a').numericStyle('font-size') * cyOf('g').zoom()");

        // The app changes its scale with nothing resized: the canvas follows.
        await page.EvalOnSelectorAsync("#frame", "f => f.style.zoom = '1.35'");
        await WaitForRatio(page, 1.35);
        await page.EvalOnSelectorAsync("#frame", "f => f.style.zoom = '1'");
        await WaitForRatio(page, 1);

        // The drawing itself is unchanged: the same view, names at the same size.
        Assert.Equal(zoom, await Eval<double>(page, "() => cyOf('g').zoom()"), 6);
        Assert.Equal(name, await Eval<double>(page, "() => cyOf('g').getElementById('a').numericStyle('font-size') * cyOf('g').zoom()"), 6);
        AssertQuiet();
    }

    [Fact]
    public async Task A_records_point_under_css_zoom_is_where_a_click_lands()
    {
        if (NoBrowser) return;
        var page = await OpenGraph($"""<div style="zoom: 1.5">{Graph(inside: Few, attrs: Preset)}</div>""");
        await Ready(page);
        await ClearLog(page);

        // The engine's own point, scaled to the viewport the pointer lives in.
        var p = await Eval<double[]>(page, """
            () => {
                const host = document.querySelector('#g .graph-canvas');
                const r = host.getBoundingClientRect(), k = r.width / host.clientWidth;
                const n = cyOf('g').getElementById('b').renderedPosition();
                return [r.left + n.x * k, r.top + n.y * k];
            }
            """);
        await page.Mouse.ClickAsync((float)p[0], (float)p[1]);

        Assert.Equal(new[] { "b" }, await Eval<string[]>(page, "() => cyOf('g').$(':selected').map(n => n.id())"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_fit_stops_a_little_past_full_size_unless_the_app_raises_the_ceiling()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(
            Graph(inside: Few, attrs: Preset + " style=\"--graph-height: 40rem\"")
            + Graph(inside: Few, attrs: Preset + " data-graph-fit-max=\"3\" style=\"--graph-height: 40rem\"", id: "raised")
            + Graph(inside: Few, attrs: Preset + " data-graph-nodes=\"box\" data-graph-fit-max=\"20\" style=\"--graph-height: 40rem\"", id: "boxes"));
        await Ready(page);
        await Ready(page, "raised");
        await Ready(page, "boxes");

        Assert.Equal(1.25, await Eval<double>(page, "() => cyOf('g').zoom()"), 6);
        Assert.Equal(3, await Eval<double>(page, "() => cyOf('raised').zoom()"), 6);
        // Never past the engine's own limit for boxes.
        Assert.Equal(4, await Eval<double>(page, "() => cyOf('boxes').zoom()"), 6);

        // Fit again after the reader zoomed out: back to the raised ceiling.
        await Eval<bool>(page, "() => { cyOf('raised').zoom(0.5); return true; }");
        await page.EvaluateAsync("() => sednaUi.graph.get('raised').then(h => h.fit())");
        await page.WaitForFunctionAsync("() => Math.abs(cyOf('raised').zoom() - 3) < 1e-6");
        AssertQuiet();
    }

    [Fact]
    public async Task A_fit_max_that_is_not_a_zoom_keeps_the_ceiling_and_says_so()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Few, attrs: Preset + " data-graph-fit-max=\"large\" style=\"--graph-height: 40rem\""));
        await Ready(page);

        Assert.Equal(1.25, await Eval<double>(page, "() => cyOf('g').zoom()"), 6);
        Assert.Contains(Warnings, w => w.Contains("data-graph-fit-max \"large\"", StringComparison.Ordinal));
        AssertQuiet("data-graph-fit-max");
    }

    /// <summary>What one record, its name and one link measure on screen, in CSS pixels.</summary>
    private static Task<double[]> OnScreen(IPage page, string id) =>
        Eval<double[]>(page, """
            id => {
                const cy = cyOf(id), z = cy.zoom(), n = cy.getElementById('a');
                const e = cy.edges().filter(e => e.target().id() === 'b')[0];
                const b = cy.getElementById('b').position(), a = n.position();
                return [n.numericStyle('width') * z, n.numericStyle('font-size') * z, e.numericStyle('width') * z,
                    n.numericStyle('text-max-width') * z, (b.x - a.x) * z];
            }
            """, id);

    [Fact]
    public async Task A_scale_draws_the_same_drawing_bigger_names_dots_lines_and_gaps_together()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(
            Graph(inside: Few, attrs: Preset + " style=\"--graph-height: 40rem\"")
            + Graph(inside: Few, attrs: Preset + " data-graph-scale=\"2\" style=\"--graph-height: 40rem\"", id: "tv"));
        await Ready(page);
        await Ready(page, "tv");

        // The fit's ceiling is the drawing's own, so the scale raises it too.
        Assert.Equal(2.5, await Eval<double>(page, "() => cyOf('tv').zoom()"), 6);
        var own = await OnScreen(page, "g");
        var big = await OnScreen(page, "tv");
        // A dot, its name, a line, the room a name may take and the gap between two records: twice each.
        for (var i = 0; i < own.Length; i++) Assert.Equal(own[i] * 2, big[i], 3);

        // Zoomed in by the reader, the scaled drawing is still the same drawing, twice the size.
        await page.EvaluateAsync("() => Promise.all(['g', 'tv'].map(id => sednaUi.graph.get(id).then(h => h.zoom(2.4))))");
        await page.WaitForFunctionAsync("() => cyOf('g').nodes()[0].data('zoom') > 2 && cyOf('tv').nodes()[0].data('zoom') > 2");
        own = await OnScreen(page, "g");
        big = await OnScreen(page, "tv");
        for (var i = 0; i < own.Length; i++) Assert.Equal(own[i] * 2, big[i], 3);
        AssertQuiet();
    }

    // A row of records closer together than their names are long.
    private const string Row = """
        <ul class="graph-data" data-graph-data>
          <li data-node="r01" data-x="40" data-y="0">orders-console-01</li>
          <li data-node="r02" data-x="80" data-y="0">orders-console-02</li>
          <li data-node="r03" data-x="120" data-y="0">orders-console-03</li>
          <li data-node="r04" data-x="160" data-y="0">orders-console-04</li>
          <li data-node="r05" data-x="200" data-y="0">orders-console-05</li>
          <li data-node="r06" data-x="240" data-y="0">orders-console-06</li>
          <li data-node="r07" data-x="280" data-y="0">orders-console-07</li>
          <li data-node="r08" data-x="320" data-y="0">orders-console-08</li>
          <li data-node="r09" data-x="360" data-y="0">orders-console-09</li>
          <li data-node="r10" data-x="400" data-y="0">orders-console-10</li>
          <li data-node="r11" data-x="440" data-y="0">orders-console-11</li>
          <li data-node="r12" data-x="480" data-y="0">orders-console-12</li>
        </ul>
        """;

    /// <summary>The names drawn, and every pair of them whose boxes cross, as "a/b".</summary>
    private static Task<string[][]> Names(IPage page, string id) =>
        Eval<string[][]>(page, """
            id => {
                const cy = cyOf(id);
                const named = cy.nodes().filter(n => n.style('label') !== '' && !n.hasClass('unlabelled')).toArray();
                const box = n => n.boundingBox({ includeNodes: false, includeEdges: false, includeLabels: true, includeOverlays: false });
                const hit = (a, b) => a.x1 < b.x2 - 0.5 && b.x1 < a.x2 - 0.5 && a.y1 < b.y2 - 0.5 && b.y1 < a.y2 - 0.5;
                const crossed = [];
                for (let i = 0; i < named.length; i++) for (let j = i + 1; j < named.length; j++) {
                    if (hit(box(named[i]), box(named[j]))) crossed.push(named[i].id() + '/' + named[j].id());
                }
                return [named.map(n => n.id()), crossed];
            }
            """, id);

    [Fact]
    public async Task A_scaled_drawing_keeps_its_names_apart_by_their_scaled_size()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(
            Graph(inside: Row, attrs: Preset + " data-graph-scale=\"2\" style=\"--graph-height: 30rem\"")
            + Graph(inside: Row, attrs: Preset + " style=\"--graph-height: 30rem\"", id: "own"));
        await Ready(page);
        await Ready(page, "own");

        // The names are twice the size and the records are not twice as far apart, so fewer fit
        // — and none of those that do is written over another.
        var names = await Names(page, "g");
        var own = await Names(page, "own");
        Assert.Empty(names[1]);
        Assert.Empty(own[1]);
        Assert.NotEmpty(names[0]);
        Assert.True(names[0].Length < 12, "Every name was drawn, which a row this close cannot hold.");
        AssertQuiet();
    }

    [Fact]
    public async Task Hold_keeps_names_at_their_size_however_far_the_drawing_is_zoomed_out()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(
            Graph(inside: Few, attrs: Preset + " data-graph-labels=\"all\"")
            + Graph(inside: Few, attrs: Preset + " data-graph-labels=\"all\" data-graph-hold", id: "held"));
        await Ready(page);
        await Ready(page, "held");
        const string Name = "id => cyOf(id).getElementById('a').numericStyle('font-size') * cyOf(id).zoom()";
        var size = await Eval<double>(page, Name, "held");

        await page.EvaluateAsync("() => Promise.all(['g', 'held'].map(id => sednaUi.graph.get(id).then(h => h.zoom(0.08))))");
        await page.WaitForFunctionAsync("() => ['g', 'held'].every(id => cyOf(id).getElementById('a').data('zoom') < 0.5)");

        // Without it a name shrinks with the drawing below the floor; with it, it does not —
        // to within the few percent of a step of the zoom the style is worked out at.
        Assert.InRange(await Eval<double>(page, Name, "held"), size * 0.95, size * 1.05);
        Assert.True(await Eval<double>(page, Name, "g") < size / 3);
        AssertQuiet();
    }

    [Fact]
    public async Task A_scale_that_is_not_a_number_draws_the_graph_at_its_own_size_and_says_so()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Few, attrs: Preset + " data-graph-scale=\"huge\" style=\"--graph-height: 40rem\""));
        await Ready(page);

        Assert.Equal(1.25, await Eval<double>(page, "() => cyOf('g').zoom()"), 6);
        Assert.Contains(Warnings, w => w.Contains("data-graph-scale \"huge\"", StringComparison.Ordinal));
        AssertQuiet("data-graph-scale");
    }

    /// <summary>Whether the graph's focus ring is drawn — the frame's inset shadow.</summary>
    private static Task<bool> Ringed(IPage page) =>
        Eval<bool>(page, "() => getComputedStyle(document.getElementById('g'), '::after').boxShadow !== 'none'");

    [Fact]
    public async Task A_tap_on_the_canvas_focuses_the_graph_without_its_ring_until_a_key_is_pressed()
    {
        if (NoBrowser) return;
        // The engine takes the touch for itself, so the browser calls the focus a keyboard's.
        var page = await OpenGraph(Graph(inside: Few, attrs: Preset), hasTouch: true);
        await Ready(page);

        var box = await page.Locator("#g .graph-canvas").BoundingBoxAsync();
        await page.Touchscreen.TapAsync(box!.X + 6, box.Y + 6);

        await page.WaitForFunctionAsync("() => document.activeElement?.id === 'g'");
        Assert.False(await Ringed(page));

        // A key says the keyboard is in use now: the ring comes back.
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.True(await Ringed(page));
        AssertQuiet();
    }

    [Fact]
    public async Task A_click_on_the_canvas_focuses_the_graph_without_its_ring_and_tabbing_back_rings_it()
    {
        if (NoBrowser) return;
        var page = await OpenGraph("""<button type="button" id="before">Before</button>""" + Graph(inside: Few, attrs: Preset));
        await Ready(page);

        var box = await page.Locator("#g .graph-canvas").BoundingBoxAsync();
        await page.Mouse.ClickAsync(box!.X + 6, box.Y + 6);
        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));
        Assert.False(await Ringed(page));

        // Away and back by the keyboard: a keyboard's focus, ringed.
        await page.Keyboard.PressAsync("Shift+Tab");
        Assert.Equal("before", await Eval<string>(page, "() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));
        Assert.True(await Ringed(page));
        AssertQuiet();
    }
}
