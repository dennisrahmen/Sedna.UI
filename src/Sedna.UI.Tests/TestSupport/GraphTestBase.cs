using System.Text.Json;
using Microsoft.Playwright;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// A fixture page for the graph: the shipped stylesheet and scripts, an event log, and a
/// console that is listened to from before the page loads.
/// </summary>
/// <remarks>
/// <para>
/// The graph starts while the page loads and does most of its work in promises nobody
/// awaits, so an exception there reaches nobody but the console. Every test reads the
/// console from the first line of the page and ends by asserting it stayed quiet — a
/// graph that draws and logs an error on the way is a broken graph.
/// </para>
/// <para>
/// The engine is read where the module registers it, <c>.graph-canvas._cyreg.cy</c>, for
/// what the contract leaves to the drawing: where a record is, what colour it was painted.
/// Everything the contract does state — <c>data-graph-state</c>, events, the app's own
/// markup — is asserted as the contract states it.
/// </para>
/// <para>
/// Motion is reduced unless a test asks otherwise: a layout then lands at once, so a test
/// reads the drawing it asked for rather than a frame on the way to it.
/// </para>
/// </remarks>
public abstract class GraphTestBase : ScriptTestBase
{
    private readonly object _gate = new();
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    /// <summary>
    /// Runs in <c>&lt;head&gt;</c>, before <c>Sedna.UI.js</c>: every graph event logged with
    /// its target and whether it could be cancelled, and three helpers — the engine behind a
    /// graph, a record's point on the viewport, and a token resolved the way a canvas sees it.
    /// </summary>
    private const string Probe = """
        <script>
        window.graphLog = [];
        for (const name of ['sedna-graph-ready', 'sedna-graph-change', 'sedna-graph-select', 'sedna-graph-open',
                            'sedna-graph-hover', 'sedna-graph-context', 'sedna-graph-connect',
                            'sedna-graph-expand', 'sedna-graph-collapse'])
            document.addEventListener(name, e => graphLog.push({ name, on: e.target.id, cancelable: e.cancelable, detail: e.detail }));
        window.cyOf = id => document.querySelector('#' + id + ' .graph-canvas')._cyreg.cy;
        window.pointOf = (id, node) => {
            const host = document.querySelector('#' + id + ' .graph-canvas').getBoundingClientRect();
            const p = cyOf(id).getElementById(node).renderedPosition();
            return { x: host.left + p.x, y: host.top + p.y };
        };
        // What an element with this colour paints, as the rgb() the engine reports.
        window.paintOf = css => {
            const probe = document.createElement('span');
            probe.style.color = css;
            document.body.appendChild(probe);
            const c = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
            c.fillStyle = getComputedStyle(probe).color;
            probe.remove();
            c.fillRect(0, 0, 1, 1);
            const [r, g, b] = c.getImageData(0, 0, 1, 1).data;
            return `rgb(${r},${g},${b})`;
        };
        window.flat = colour => String(colour).replace(/\s+/g, '');
        </script>
        """;

    /// <summary>
    /// A small service map: four kinds, three groups and one record in none, a muted
    /// record, a record with no link at all, an icon, a shape, a tag list, an app field,
    /// a <c>data-href</c> and a dashed link. Nothing in it names anything real.
    /// </summary>
    protected const string Services = """
        <ul class="graph-data" data-graph-data>
          <li data-node="api" data-kind="service" data-group="orders" data-tone="2" data-icon="ri-server-line"
              data-meta="orders-console-01" data-href="/records/api" data-owner="Alex Fischer">Orders API</li>
          <li data-node="db" data-kind="store" data-group="orders" data-tone="4" data-shape="square" data-meta="src-db-14">Orders database</li>
          <li data-node="queue" data-kind="queue" data-group="orders" data-tone="5" data-shape="diamond">Order queue</li>
          <li data-node="web" data-kind="service" data-group="shop" data-tone="3" data-tags="public edge">Web shop</li>
          <li data-node="cache" data-kind="store" data-group="shop" data-muted>Page cache</li>
          <li data-node="runner" data-kind="host" data-meta="build-runner-04">Build runner</li>
          <li data-node="archive" data-kind="store" data-group="archive">Archive</li>
          <li data-edge data-source="web" data-target="api" data-kind="calls" data-label="calls">Web shop calls the orders API</li>
          <li data-edge data-source="api" data-target="db" data-kind="reads" data-label="reads">Orders API reads the orders database</li>
          <li data-edge data-source="api" data-target="queue" data-kind="writes" data-label="publishes">Orders API publishes to the order queue</li>
          <li data-edge data-source="web" data-target="cache" data-kind="reads" data-label="caches">Web shop caches its pages</li>
          <li data-edge data-source="runner" data-target="api" data-kind="deploys" data-label="deploys" data-line="dashed">The build runner deploys the orders API</li>
        </ul>
        """;

    /// <summary>
    /// A graph element around <paramref name="inside"/>, with its canvas and its wait.
    /// </summary>
    protected static string Graph(string inside = Services, string attrs = "data-graph-eager", string id = "g",
        string label = "Service dependencies") => $"""
        <div class="graph" data-graph id="{id}" aria-label="{label}" {attrs}>
          {inside}
          <div class="graph-canvas"></div>
          <div class="graph-wait skeleton skeleton-block"></div>
          <p class="graph-empty empty-state">Nothing to show yet.</p>
          <p class="graph-empty graph-empty--filtered empty-state">No record matches these filters.</p>
          <p class="graph-empty graph-empty--error empty-state">The records could not be read.</p>
        </div>
        """;

    /// <summary>The graph events a test did not ask to be cleared, oldest first.</summary>
    protected sealed record Logged(string Name, string On, bool Cancelable, JsonElement Detail);

    /// <summary>
    /// Opens a fixture with the stylesheet and the graph probe, listening to the console
    /// from before the first byte.
    /// </summary>
    protected async Task<IPage> OpenGraph(
        string body,
        string head = "",
        bool reduceMotion = true,
        ForcedColors? forcedColors = null,
        IReadOnlyDictionary<string, Served>? serve = null,
        ColorScheme colorScheme = ColorScheme.Light)
    {
        return await Open(body, head: StylesheetTag + Probe + head,
            colorScheme: colorScheme,
            reducedMotion: reduceMotion ? ReducedMotion.Reduce : ReducedMotion.NoPreference,
            forcedColors: forcedColors,
            serve: serve,
            beforeLoad: page =>
            {
                page.Console += (_, message) =>
                {
                    lock (_gate)
                    {
                        if (message.Type == "error") _errors.Add(message.Text);
                        else if (message.Type == "warning") _warnings.Add(message.Text);
                    }
                };
                page.PageError += (_, error) =>
                {
                    lock (_gate) _errors.Add(error);
                };
            });
    }

    /// <summary>Console errors and uncaught exceptions so far.</summary>
    protected IReadOnlyList<string> Errors
    {
        get { lock (_gate) return _errors.ToList(); }
    }

    /// <summary>Console warnings so far — the graph's own start with "Sedna.UI graph:".</summary>
    protected IReadOnlyList<string> Warnings
    {
        get { lock (_gate) return _warnings.ToList(); }
    }

    /// <summary>
    /// Nothing went wrong: no error, no exception, and no warning — the engine warns about
    /// a style value it cannot parse, which is a record drawn wrong in silence.
    /// </summary>
    protected void AssertQuiet(params string[] expectedWarnings)
    {
        Assert.Empty(Errors);
        var unexpected = Warnings
            .Where(w => !expectedWarnings.Any(e => w.Contains(e, StringComparison.Ordinal)))
            .ToList();
        Assert.True(unexpected.Count == 0, "Unexpected console warnings:\n" + string.Join("\n", unexpected));
    }

    /// <summary>
    /// The graph's handle, which resolves once the graph has drawn and its plugins have
    /// loaded — after <c>sedna-graph-ready</c>. Starts a graph that has not started.
    /// </summary>
    protected static Task Ready(IPage page, string id = "g") =>
        page.EvaluateAsync("id => sednaUi.graph.get(id).then(() => true)", id);

    /// <summary>Waits until a graph says it is in <paramref name="state"/>.</summary>
    protected static Task State(IPage page, string id, string state) =>
        page.WaitForFunctionAsync(
            "([id, state]) => document.getElementById(id)?.getAttribute('data-graph-state') === state",
            new[] { id, state });

    protected static Task<string?> StateOf(IPage page, string id = "g") =>
        page.Locator("#" + id).GetAttributeAsync("data-graph-state");

    protected static async Task<T> Eval<T>(IPage page, string script, object? arg = null) =>
        arg is null ? await page.EvaluateAsync<T>(script) : await page.EvaluateAsync<T>(script, arg);

    /// <summary>The ids of the records on screen — not hidden by a filter — sorted.</summary>
    protected static async Task<string[]> Shown(IPage page, string id = "g") =>
        await page.EvaluateAsync<string[]>(
            "id => cyOf(id).nodes().not('.hidden').map(n => n.id()).sort()", id);

    /// <summary>The ids of the links on screen, sorted.</summary>
    protected static async Task<string[]> ShownLinks(IPage page, string id = "g") =>
        await page.EvaluateAsync<string[]>(
            "id => cyOf(id).edges().not('.hidden').map(e => e.id()).sort()", id);

    /// <summary>Every record's position, rounded to a hundredth, by id.</summary>
    protected static async Task<Dictionary<string, (double X, double Y)>> Positions(IPage page, string id = "g")
    {
        var json = await page.EvaluateAsync<string>(
            "id => JSON.stringify(cyOf(id).nodes().map(n => [n.id(), n.position('x'), n.position('y')]))", id);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().ToDictionary(
            e => e[0].GetString()!,
            e => (Math.Round(e[1].GetDouble(), 2), Math.Round(e[2].GetDouble(), 2)));
    }

    /// <summary>A record's centre on the viewport, for the mouse.</summary>
    protected static async Task<(float X, float Y)> PointOf(IPage page, string node, string id = "g")
    {
        var p = await page.EvaluateAsync<JsonElement>("([id, node]) => pointOf(id, node)", new[] { id, node });
        return ((float)p.GetProperty("x").GetDouble(), (float)p.GetProperty("y").GetDouble());
    }

    /// <summary>The events logged so far, optionally only those named.</summary>
    protected static async Task<List<Logged>> Log(IPage page, string? name = null)
    {
        var json = await page.EvaluateAsync<string>("() => JSON.stringify(graphLog)");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray()
            .Select(e => new Logged(
                e.GetProperty("name").GetString()!,
                e.GetProperty("on").GetString() ?? "",
                e.GetProperty("cancelable").GetBoolean(),
                e.TryGetProperty("detail", out var d) ? d.Clone() : default))
            .Where(l => name is null || l.Name == name)
            .ToList();
    }

    protected static Task ClearLog(IPage page) => page.EvaluateAsync("() => { graphLog.length = 0; }");

    /// <summary>Waits until <paramref name="count"/> events of that name have been logged.</summary>
    protected static Task Heard(IPage page, string name, int count = 1) =>
        page.WaitForFunctionAsync(
            "([name, count]) => graphLog.filter(l => l.name === name).length >= count",
            new object[] { name, count });
}
