using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A button on a link: copies of the app's <c>&lt;template data-graph-edge-tool&gt;</c>, one per
/// link on screen, each held on its link's midpoint as the view and the records move, and
/// pressing one dispatching <c>sedna-graph-edge</c>.
/// </summary>
/// <remarks>
/// Where a copy is placed is read against the engine's own midpoint for the link — the copy's
/// centre on the viewport and the midpoint taken onto the viewport through the canvas's own
/// box — so nothing here assumes how the engine routes a line.
/// </remarks>
public class GraphEdgeToolTests : GraphTestBase
{
    private const string Tool = """
        <template data-graph-edge-tool>
          <span class="graph-edge-tool">
            <button class="btn btn--sm btn--icon" type="button" value="remove">
              <span class="visually-hidden">Remove <span data-graph-field="label"></span></span>
            </button>
            <button class="btn btn--sm btn--icon" type="button" value="insert">
              <span class="visually-hidden">Insert a step</span>
            </button>
          </span>
        </template>
        """;

    /// <summary>Every copy's link, and how far its centre is from that link's midpoint, in pixels.</summary>
    private const string Offsets = """
        () => {
            const cy = cyOf('g');
            const host = document.querySelector('#g .graph-canvas').getBoundingClientRect();
            const out = [];
            document.querySelectorAll('#g [data-graph-edge-id]').forEach(copy => {
                const id = copy.getAttribute('data-graph-edge-id');
                const m = cy.getElementById(id).midpoint();
                const b = copy.getBoundingClientRect();
                const x = host.left + m.x * cy.zoom() + cy.pan().x, y = host.top + m.y * cy.zoom() + cy.pan().y;
                out.push([id, copy.hidden ? -1 : Math.hypot(b.left + b.width / 2 - x, b.top + b.height / 2 - y)]);
            });
            return JSON.stringify(out);
        }
        """;

    private static async Task<Dictionary<string, double>> CopiesAt(Microsoft.Playwright.IPage page)
    {
        using var json = JsonDocument.Parse(await page.EvaluateAsync<string>(Offsets));
        return json.RootElement.EnumerateArray().ToDictionary(p => p[0].GetString()!, p => p[1].GetDouble());
    }

    [Fact]
    public async Task Every_link_on_screen_gets_a_copy_on_its_midpoint_filled_with_its_fields()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Services + Tool));
        await Ready(page);

        var at = await CopiesAt(page);
        Assert.Equal(await ShownLinks(page), at.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.All(at, p => Assert.InRange(p.Value, 0, 1.5));

        // The app's slots, filled as text; the template itself is left as it was.
        Assert.Equal("Remove reads", (await page.Locator("#g [data-graph-edge-id=\"api→db\"] button[value=remove]").TextContentAsync())!.Trim());
        Assert.Equal(1, await page.Locator("#g template[data-graph-edge-tool]").CountAsync());
        AssertQuiet();
    }

    [Fact]
    public async Task A_copy_follows_the_view_and_a_layout_and_hides_off_the_frame()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Services + Tool + """
            <div class="graph-tools"><button type="button" data-graph-action="arrange" id="arrange" aria-label="Arrange"></button></div>
            """));
        await Ready(page);

        await Js.Run(page, "() => cyOf('g').panBy({ x: 37, y: -21 })");
        await Js.Run(page, "() => cyOf('g').zoom({ level: cyOf('g').zoom() * 1.4, renderedPosition: { x: 100, y: 80 } })");
        Assert.All(await CopiesAt(page), p => Assert.True(p.Value is -1 or <= 1.5, $"{p.Key} is {p.Value}px off its link"));

        // Panned far away, every copy is hidden rather than drawn outside the frame.
        await Js.Run(page, "() => cyOf('g').panBy({ x: 5000, y: 0 })");
        Assert.All(await CopiesAt(page), p => Assert.Equal(-1, p.Value));

        // A layout that moves every record takes every copy with it.
        await page.Locator("#arrange").ClickAsync();
        await page.WaitForFunctionAsync("() => [...document.querySelectorAll('#g [data-graph-edge-id]')].some(c => !c.hidden)");
        Assert.All(await CopiesAt(page), p => Assert.True(p.Value is -1 or <= 1.5, $"{p.Key} is {p.Value}px off its link"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_link_a_filter_hides_has_no_copy_and_a_kind_named_on_the_template_limits_it()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Services + Tool.Replace("data-graph-edge-tool", "data-graph-edge-tool=\"reads calls\"", StringComparison.Ordinal) + """
            <input type="checkbox" data-graph-filter="group" value="shop" checked id="shop" aria-label="Shop" />
            """));
        await Ready(page);
        Assert.Equal(["api→db", "web→api", "web→cache"], (await CopiesAt(page)).Keys.Order(StringComparer.Ordinal));

        await page.Locator("#shop").UncheckAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('#g [data-graph-edge-id=\"web→api\"]')");
        Assert.Equal(["api→db"], (await CopiesAt(page)).Keys);
        AssertQuiet();
    }

    [Fact]
    public async Task Pressing_a_button_dispatches_the_link_and_its_value_and_the_app_keeps_its_data()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Services + Tool));
        await Ready(page);
        await ClearLog(page);

        await page.Locator("#g [data-graph-edge-id=\"api→db\"] button[value=insert]").ClickAsync();
        await Heard(page, "sedna-graph-edge");
        var e = (await Log(page, "sedna-graph-edge")).Single();
        Assert.True(e.Cancelable);
        Assert.Equal("g", e.On);
        Assert.Equal("api→db", e.Detail.GetProperty("id").GetString());
        Assert.Equal("api", e.Detail.GetProperty("source").GetString());
        Assert.Equal("db", e.Detail.GetProperty("target").GetString());
        Assert.Equal("reads", e.Detail.GetProperty("kind").GetString());
        Assert.Equal("insert", e.Detail.GetProperty("value").GetString());

        // Without data-graph-managed the drawing is the app's to change: remove changes nothing.
        await page.Locator("#g [data-graph-edge-id=\"api→db\"] button[value=remove]").ClickAsync();
        await Heard(page, "sedna-graph-edge", 2);
        Assert.Contains("api→db", await ShownLinks(page));
        AssertQuiet();
    }

    [Fact]
    public async Task A_managed_graph_removes_the_link_unless_the_event_is_cancelled()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Services + Tool, attrs: "data-graph-eager data-graph-managed"));
        await Ready(page);

        await Js.Run(page, "() => document.addEventListener('sedna-graph-edge', e => { if (e.detail.id === 'web→api') e.preventDefault(); }, { once: true })");
        await page.Locator("#g [data-graph-edge-id=\"web→api\"] button[value=remove]").ClickAsync();
        await Heard(page, "sedna-graph-edge");
        Assert.Contains("web→api", await ShownLinks(page));

        await page.Locator("#g [data-graph-edge-id=\"api→db\"] button[value=remove]").ClickAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('api→db').empty()");
        await page.WaitForFunctionAsync("() => !document.querySelector('#g [data-graph-edge-id=\"api→db\"]')");
        Assert.DoesNotContain("api→db", await ShownLinks(page));
        AssertQuiet();
    }

    [Fact]
    public async Task A_graph_taken_down_takes_its_copies_with_it()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Services + Tool));
        await Ready(page);
        Assert.True(await page.Locator("#g [data-graph-edge-id]").CountAsync() > 0);

        await Js.Run(page, "() => sednaUi.graph.get('g').then(g => g.destroy())");
        Assert.Equal(0, await page.Locator("#g [data-graph-edge-id]").CountAsync());
        AssertQuiet();
    }
}
