using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-graph-arrive="pop"</c>: a record added after the first drawing shows a halo that
/// fades, not under reduced motion, and the live region says it arrived either way.
/// </summary>
public class GraphArriveTests : GraphTestBase
{
    private const string Live = """<p data-graph-live aria-live="polite" data-graph-announce-arrive="{label} arrived."></p>""";

    private const string First = """
        () => sednaUi.graph.get('g').then(g => g.set({ nodes: [{ id: 'a', label: 'Orders API' }], edges: [] }))
        """;

    private const string Second = """
        () => sednaUi.graph.get('g').then(g => g.set({ nodes: [{ id: 'a', label: 'Orders API' }, { id: 'n', label: 'Note on refunds' }], edges: [{ source: 'n', target: 'a' }] }))
        """;

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task A_record_added_live_shows_a_halo_unless_motion_is_reduced_and_is_announced(bool reduce, bool halo)
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Live, attrs: "data-graph-eager data-graph-deferred data-graph-arrive=\"pop\""), reduceMotion: reduce);
        await Ready(page);
        await Js.Run(page, First);
        await Js.Run(page, Second);

        var opacity = await Eval<double>(page, "() => cyOf('g').getElementById('n').numericStyle('overlay-opacity')");
        if (halo) Assert.True(opacity > 0, "The new record shows no halo.");
        else Assert.Equal(0, opacity);
        Assert.Equal(0, await Eval<double>(page, "() => cyOf('g').getElementById('a').numericStyle('overlay-opacity')"));

        await Assertions.Expect(page.Locator("[data-graph-live]")).ToHaveTextAsync("Note on refunds arrived.");
    }
}
