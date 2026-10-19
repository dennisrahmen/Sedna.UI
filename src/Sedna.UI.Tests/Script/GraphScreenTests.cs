using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A graph on a screen read from across a room: sharp when the app scales its frame with CSS
/// <c>zoom</c>, filling the frame with a handful of records when the app asks, and no focus
/// ring round the whole graph after a finger taps it.
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
