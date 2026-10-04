using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A run on a graph: <c>data-state</c> on records and links, drawn in its tokens' colours around a
/// record that keeps its own, followed by the links into it, restyled only where it changed, and
/// moving only where motion is welcome — asserted against <c>docs/graph.md</c>, "A run".
/// </summary>
public class GraphRunTests : GraphTestBase
{
    /// <summary>
    /// An automation flow mid-run: every state once, a record not reached yet, a record with a
    /// state the model does not know, links that follow their targets, one that leaves a skipped
    /// record, and one that says its own.
    /// </summary>
    private const string Run = """
        <ul class="graph-data" data-graph-data>
          <li data-node="webhook" data-tone="3" data-icon="ri-webhook-line" data-state="done">Webhook received</li>
          <li data-node="parse" data-tone="3" data-state="running">Parse the payload</li>
          <li data-node="lookup" data-tone="3" data-state="next">Look up the caller</li>
          <li data-node="chat" data-tone="3" data-state="skipped">Join the chat transcript</li>
          <li data-node="reply" data-tone="3" data-state="waiting">Wait for the caller's reply</li>
          <li data-node="ticket" data-tone="3" data-state="failed">Create a ticket</li>
          <li data-node="notify" data-tone="3">Page the on-call engineer</li>
          <li data-node="odd" data-tone="3" data-state="paused">Hold the queue</li>
          <li data-edge data-source="webhook" data-target="parse">The webhook is parsed</li>
          <li data-edge data-source="parse" data-target="lookup">The payload names the caller</li>
          <li data-edge data-source="lookup" data-target="chat">A chat joins its transcript</li>
          <li data-edge data-source="lookup" data-target="reply">The caller is asked for the order number</li>
          <li data-edge data-source="reply" data-target="ticket">The reply goes on the ticket</li>
          <li data-edge data-source="ticket" data-target="notify">An urgent ticket pages the on-call engineer</li>
          <li data-edge="chat-ticket" data-source="chat" data-target="ticket">A chat transcript goes on the ticket</li>
          <li data-edge="webhook-notify" data-source="webhook" data-target="notify" data-state="done">The webhook is acknowledged</li>
          <li data-edge data-source="ticket" data-target="odd">A ticket can hold the queue</li>
        </ul>
        """;

    private const string Flow =
        "data-graph-eager data-graph-layout=\"dagre\" data-graph-direction=\"LR\" data-graph-nodes=\"box\" data-graph-arrows=\"target\"";

    private const string UnknownState = "\"paused\" is not a run state";

    private static void AssertSamePlaces(Dictionary<string, (double X, double Y)> before, Dictionary<string, (double X, double Y)> after)
    {
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var (id, at) in before) Assert.True(at == after[id], $"{id} moved from {at} to {after[id]}.");
    }

    [Fact]
    public async Task Data_state_reaches_the_model_and_a_link_takes_its_targets_unless_it_leaves_a_skip_or_has_its_own()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Run, attrs: Flow));
        await Ready(page);

        var model = JsonSerializer.Deserialize<Dictionary<string, string?>>(await page.EvaluateAsync<string>("""
            () => {
                const cy = cyOf('g');
                const out = {};
                cy.nodes().forEach(n => { out[n.id()] = n.data('state'); });
                cy.edges().forEach(e => { out[e.id() + ':own'] = e.data('state'); out[e.id()] = e.data('run'); });
                return JSON.stringify(out);
            }
            """))!;

        Assert.Equal("done", model["webhook"]);
        Assert.Equal("running", model["parse"]);
        Assert.Equal("next", model["lookup"]);
        Assert.Equal("skipped", model["chat"]);
        Assert.Equal("waiting", model["reply"]);
        Assert.Equal("failed", model["ticket"]);
        // Not reached yet, and a word that is no state: drawn as not reached, with a warning.
        Assert.Null(model["notify"]);
        Assert.Null(model["odd"]);

        // A link follows its target...
        Assert.Equal("running", model["webhook→parse"]);
        Assert.Equal("next", model["parse→lookup"]);
        Assert.Equal("skipped", model["lookup→chat"]);
        Assert.Equal("waiting", model["lookup→reply"]);
        Assert.Equal("failed", model["reply→ticket"]);
        Assert.Null(model["ticket→notify"]);
        Assert.Null(model["ticket→notify:own"]);
        // ...except out of a skipped record, which nothing left: that link is skipped too...
        Assert.Equal("skipped", model["chat-ticket"]);
        Assert.Null(model["chat-ticket:own"]);
        // ...and its own state wins over all of that.
        Assert.Equal("done", model["webhook-notify"]);

        Assert.Contains(Warnings, w => w.Contains(UnknownState, StringComparison.Ordinal));
        AssertQuiet(UnknownState);
    }

    [Fact]
    public async Task Each_state_is_drawn_around_the_record_in_its_tokens_colour_and_never_by_colour_alone()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Run, attrs: Flow));
        await Ready(page);

        var drawn = await page.EvaluateAsync<JsonElement>(Drawn);
        string Of(string id, string what) => drawn.GetProperty(id).GetProperty(what).GetString()!;
        double Num(string id, string what) => drawn.GetProperty(id).GetProperty(what).GetDouble();
        var tokens = drawn.GetProperty("tokens");
        string Token(string name) => tokens.GetProperty(name).GetString()!;

        // A ring outside the record, in the state's token...
        Assert.Equal(Token("go"), Of("webhook", "ring"));
        Assert.Equal(Token("agent"), Of("parse", "ring"));
        Assert.Equal(Token("warn"), Of("reply", "ring"));
        Assert.Equal(Token("danger"), Of("ticket", "ring"));
        Assert.All(new[] { "webhook", "parse", "reply", "ticket" }, id => Assert.True(Num(id, "ringWidth") > 0, id));
        // ...while the record keeps its own tone, on its border.
        Assert.All(new[] { "webhook", "parse", "reply", "ticket", "notify" }, id => Assert.Equal(Token("tone"), Of(id, "border")));
        // Not reached: no ring at all.
        Assert.Equal(0, Num("notify", "ringWidth"));

        // Never by colour alone: running breathes, waiting is dashed, failed is heavier...
        Assert.Equal(Token("agent"), Of("parse", "halo"));
        Assert.True(Num("parse", "haloOpacity") > 0);
        Assert.Equal("dashed", Of("reply", "ringStyle"));
        Assert.Equal("solid", Of("webhook", "ringStyle"));
        Assert.True(Num("ticket", "ringWidth") > Num("webhook", "ringWidth"));
        // ...and next and skipped are dashed borders: the brand's, and a quieter record's.
        Assert.Equal("dashed", Of("lookup", "borderStyle"));
        Assert.Equal(Token("brand"), Of("lookup", "border"));
        Assert.Equal("dashed", Of("chat", "borderStyle"));
        Assert.True(Num("chat", "fill") < 1);

        // Links: the way the run went in the go colour, data flowing in the agent's, dashed.
        Assert.Equal(Token("agent"), Of("webhook→parse", "line"));
        Assert.Equal("dashed", Of("webhook→parse", "lineStyle"));
        Assert.Equal(Token("agent"), Of("webhook→parse", "arrow"));
        Assert.Equal(Token("go"), Of("lookup→reply", "line"));
        Assert.Equal("solid", Of("lookup→reply", "lineStyle"));
        Assert.Equal(Token("danger"), Of("reply→ticket", "line"));
        Assert.Equal(Token("brand"), Of("parse→lookup", "line"));
        Assert.Equal("dashed", Of("parse→lookup", "lineStyle"));
        Assert.Equal("dashed", Of("chat-ticket", "lineStyle"));
        Assert.Equal(Token("line"), Of("chat-ticket", "line"));
        Assert.Equal("solid", Of("ticket→notify", "lineStyle"));
        Assert.Equal(Token("line"), Of("ticket→notify", "line"));
        AssertQuiet(UnknownState);
    }

    /// <summary>What every record and link was drawn with, and the tokens, as the canvas resolves them.</summary>
    private const string Drawn = """
        () => {
            const cy = cyOf('g');
            const out = {
                tokens: {
                    go: paintOf('var(--go-solid)'), warn: paintOf('var(--warn-solid)'), danger: paintOf('var(--danger-solid)'),
                    agent: paintOf('var(--agent-to)'), brand: paintOf('var(--brand)'), line: paintOf('var(--border-strong)'),
                    tone: paintOf('var(--viz-3)'),
                },
            };
            cy.nodes().forEach(n => {
                out[n.id()] = {
                    ring: flat(n.style('outline-color')), ringWidth: n.numericStyle('outline-width'), ringStyle: n.style('outline-style'),
                    border: flat(n.style('border-color')), borderStyle: n.style('border-style'),
                    halo: flat(n.style('underlay-color')), haloOpacity: n.numericStyle('underlay-opacity'),
                    fill: n.numericStyle('background-opacity'),
                };
            });
            cy.edges().forEach(e => {
                out[e.id()] = { line: flat(e.style('line-color')), lineStyle: e.style('line-style'), arrow: flat(e.style('target-arrow-color')) };
            });
            return out;
        }
        """;

    [Fact]
    public async Task A_runs_colours_follow_the_theme()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Run, attrs: Flow));
        await Ready(page);
        var dark = await Eval<string>(page, "() => flat(cyOf('g').getElementById('parse').style('outline-color'))");
        Assert.Equal(await Eval<string>(page, "() => paintOf('var(--agent-to)')"), dark);

        // The light variant moves the agent colour a step darker; the ring and the flowing link go with it.
        await page.EvaluateAsync("() => sednaUi.settings.save('variant', 'light')");
        await page.WaitForFunctionAsync("c => flat(cyOf('g').getElementById('parse').style('outline-color')) !== c", dark);

        var light = await Eval<string[]>(page, """
            () => [flat(cyOf('g').getElementById('parse').style('outline-color')),
                   flat(cyOf('g').getElementById('webhook→parse').style('line-color')),
                   paintOf('var(--agent-to)')]
            """);
        Assert.Equal(light[2], light[0]);
        Assert.Equal(light[2], light[1]);
        AssertQuiet(UnknownState);
    }

    [Fact]
    public async Task Forced_colours_draw_a_run_in_the_readers_system_colours()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Run, attrs: Flow), forcedColors: ForcedColors.Active);
        await Ready(page);

        var painted = await Eval<string[]>(page, """
            () => {
                const cy = cyOf('g');
                return [flat(cy.getElementById('webhook').style('outline-color')), paintOf('CanvasText'),
                        flat(cy.getElementById('parse').style('outline-color')), flat(cy.getElementById('webhook→parse').style('line-color')),
                        paintOf('Highlight')];
            }
            """);
        // The trail is the reader's text colour; the step at work and the data flowing into it
        // are their highlight, as the agent's colours are mapped in forced colours.
        Assert.Equal(painted[1], painted[0]);
        Assert.Equal(painted[4], painted[2]);
        Assert.Equal(painted[4], painted[3]);
        AssertQuiet(UnknownState);
    }

    // ── New states ───────────────────────────────────────────────────────────

    /// <summary>A chain of steps with a branch, as a call hands it over, every step in a state.</summary>
    private const string Steps = """
        (states) => {
            const ids = ['trigger', 'parse', 'lookup', 'route', 'email', 'chat', 'classify', 'summarise', 'merge', 'ticket', 'notify', 'queue'];
            const nodes = ids.map(id => ({ id, label: id, state: states[id] || undefined }));
            const pairs = [['trigger', 'parse'], ['parse', 'lookup'], ['lookup', 'route'], ['route', 'email'], ['route', 'chat'],
                           ['email', 'classify'], ['email', 'summarise'], ['chat', 'classify'], ['classify', 'merge'],
                           ['summarise', 'merge'], ['merge', 'ticket'], ['ticket', 'notify'], ['ticket', 'queue']];
            const edges = pairs.map(([source, target]) => ({ source, target, state: states[source + '>' + target] || undefined }));
            return { nodes, edges };
        }
        """;

    private static Task SetStates(IPage page, object states) =>
        page.EvaluateAsync($"states => sednaUi.graph.get('g').then(g => g.set(({Steps})(states)))", states);

    [Fact]
    public async Task New_states_restyle_only_the_records_and_links_they_change_and_lay_nothing_out_again()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: "", attrs: Flow + " data-graph-deferred"));
        await Ready(page);
        await SetStates(page, new Dictionary<string, string>
        {
            ["trigger"] = "done", ["parse"] = "done", ["lookup"] = "running", ["chat>classify"] = "skipped",
        });
        var before = await Positions(page);
        // The first drawing's frames restyle what it marked; counting starts after them.
        await page.WaitForTimeoutAsync(400);
        await page.EvaluateAsync("""
            () => {
                window.restyledIds = new Set();
                window.dataIds = new Set();
                cyOf('g').on('style', e => restyledIds.add(e.target.id()));
                cyOf('g').on('data', e => dataIds.add(e.target.id()));
            }
            """);

        // The run moves on: the look-up is done, the route runs, the chat branch is skipped.
        await SetStates(page, new Dictionary<string, string>
        {
            ["trigger"] = "done", ["parse"] = "done", ["lookup"] = "done", ["route"] = "running", ["chat"] = "skipped",
            ["chat>classify"] = "skipped",
        });
        await page.WaitForTimeoutAsync(300);

        // Nothing moved: a state is not a reason to lay a flow out again.
        AssertSamePlaces(before, await Positions(page));
        // Exactly what changed was given new data: three records and the links into them. The link
        // out of the skipped chat into classify says its own state, which did not change.
        Assert.Equal(["chat", "lookup", "lookup→route", "parse→lookup", "route", "route→chat"],
            (await Eval<string[]>(page, "() => [...dataIds].sort()")));
        // And the engine restyled those, and what is drawn with them — nothing further off.
        var restyled = await Eval<string[]>(page, "() => [...restyledIds].sort()");
        var near = await Eval<string[]>(page,
            "() => cyOf('g').$('#chat, #lookup, #route').closedNeighborhood().map(e => e.id())");
        Assert.Equal("", string.Join(" ", restyled.Except(near)));
        Assert.Equal("done", await Eval<string>(page, "() => cyOf('g').getElementById('parse→lookup').data('run')"));
        Assert.Equal("running", await Eval<string>(page, "() => cyOf('g').getElementById('lookup→route').data('run')"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_state_changed_in_the_markup_is_drawn_where_the_record_stands()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Run, attrs: Flow));
        await Ready(page);
        var before = await Positions(page);

        await page.EvaluateAsync("() => document.querySelector('[data-node=\"lookup\"]').setAttribute('data-state', 'running')");
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('parse→lookup').data('run') === 'running'");

        Assert.Equal("running", await Eval<string>(page, "() => cyOf('g').getElementById('lookup').data('state')"));
        AssertSamePlaces(before, await Positions(page));
        AssertQuiet(UnknownState);
    }

    // ── What moves ───────────────────────────────────────────────────────────

    private const string Moving = """
        <ul class="graph-data" data-graph-data>
          <li data-node="parse" data-state="done">Parse the payload</li>
          <li data-node="classify" data-state="running">Classify with AI</li>
          <li data-node="summarise" data-state="done">Summarise with AI</li>
          <li data-node="merge">Merge</li>
          <li data-edge data-source="parse" data-target="classify">The payload is classified</li>
          <li data-edge data-source="parse" data-target="summarise">The payload is summarised</li>
          <li data-edge data-source="classify" data-target="merge">The class is merged</li>
          <li data-edge data-source="summarise" data-target="merge">The summary is merged</li>
        </ul>
        """;

    /// <summary>
    /// Waits out what the last change left for the engine's next frames — it restyles what a change
    /// marked when it draws it — and counts every restyle from then on, by element.
    /// </summary>
    private static async Task CountRestyles(IPage page)
    {
        await page.WaitForTimeoutAsync(400);
        await page.EvaluateAsync("""
            () => {
                window.restyledIds = new Set();
                window.restyled = 0;
                if (!window.counting) cyOf('g').on('style', e => { restyledIds.add(e.target.id()); restyled++; });
                window.counting = true;
            }
            """);
    }

    [Fact]
    public async Task Under_reduced_motion_a_run_stands_still_and_still_reads()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Moving, attrs: Flow));
        await Ready(page);
        await CountRestyles(page);
        await page.WaitForTimeoutAsync(500);

        Assert.Equal("", await Eval<string>(page, "() => [...restyledIds].sort().join(' ')"));
        var still = await page.EvaluateAsync<JsonElement>("""
            () => {
                const cy = cyOf('g');
                const e = cy.getElementById('parse→classify'), n = cy.getElementById('classify');
                return { dash: e.data('dash') ?? null, breath: n.data('breath') ?? null, style: e.style('line-style'),
                         line: flat(e.style('line-color')), agent: paintOf('var(--agent-to)'), halo: n.numericStyle('underlay-opacity') };
            }
            """);
        // Nothing has moved it...
        Assert.Equal(JsonValueKind.Null, still.GetProperty("dash").ValueKind);
        Assert.Equal(JsonValueKind.Null, still.GetProperty("breath").ValueKind);
        // ...and it still says "running": dashed in the agent colour, the halo lit.
        Assert.Equal("dashed", still.GetProperty("style").GetString());
        Assert.Equal(still.GetProperty("agent").GetString(), still.GetProperty("line").GetString());
        Assert.True(still.GetProperty("halo").GetDouble() > 0);
        AssertQuiet();
    }

    [Fact]
    public async Task A_run_moves_only_what_is_running_and_stops_for_a_hidden_tab_and_for_reduced_motion()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Moving, attrs: Flow), reduceMotion: false);
        await Ready(page);
        await CountRestyles(page);

        // The dashes march along the link into the running record, and its halo breathes...
        var first = await Eval<double>(page, "() => cyOf('g').getElementById('parse→classify').numericStyle('line-dash-offset')");
        await page.WaitForFunctionAsync(
            "d => cyOf('g').getElementById('parse→classify').numericStyle('line-dash-offset') !== d", first);
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('classify').data('breath') !== undefined");
        await page.WaitForTimeoutAsync(300);
        // ...and nothing else is restyled for it: what is not running and not joined to what is
        // stays as it was drawn.
        var restyled = await Eval<string[]>(page, "() => [...restyledIds].sort()");
        Assert.Contains("classify", restyled);
        Assert.Contains("parse→classify", restyled);
        Assert.Equal("", string.Join(" ", restyled.Intersect(["parse", "summarise", "merge", "parse→summarise", "summarise→merge"])));

        // A tab nobody is looking at gets nothing.
        await page.EvaluateAsync("""
            () => {
                Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' });
                document.dispatchEvent(new Event('visibilitychange'));
            }
            """);
        await CountRestyles(page);
        await page.WaitForTimeoutAsync(400);
        Assert.Equal(0, await Eval<int>(page, "() => restyled"));

        // Seen again, it moves again.
        await page.EvaluateAsync("""
            () => {
                Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'visible' });
                document.dispatchEvent(new Event('visibilitychange'));
            }
            """);
        await page.WaitForFunctionAsync("() => restyled > 0");

        // A reader who asks for less motion gets it at once, and the run is left at rest.
        await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('parse→classify').data('dash') === undefined");
        // At rest at once, where the dashes started: removing the data alone could leave the
        // link drawn mid-dash, on a busy machine for good.
        Assert.Equal(0, await Eval<double>(page, "() => cyOf('g').getElementById('parse→classify').numericStyle('line-dash-offset')"));
        await CountRestyles(page);
        await page.WaitForTimeoutAsync(400);
        Assert.Equal(0, await Eval<int>(page, "() => restyled"));
        Assert.Equal(0, await Eval<double>(page, "() => cyOf('g').getElementById('parse→classify').numericStyle('line-dash-offset')"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_run_that_finishes_stops_moving()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Moving, attrs: Flow), reduceMotion: false);
        await Ready(page);
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('classify').data('breath') !== undefined");

        await page.EvaluateAsync("() => document.querySelector('[data-node=\"classify\"]').setAttribute('data-state', 'done')");
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('parse→classify').data('run') === 'done'");
        await CountRestyles(page);
        await page.WaitForTimeoutAsync(400);

        Assert.Equal(0, await Eval<int>(page, "() => restyled"));
        Assert.True(await Eval<bool>(page,
            "() => cyOf('g').getElementById('classify').data('breath') === undefined && cyOf('g').getElementById('parse→classify').data('dash') === undefined"));
        AssertQuiet();
    }

    // ── What comes back ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_record_in_an_event_carries_its_state()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Run, attrs: Flow));
        await Ready(page);

        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.select('reply'))");
        await page.EvaluateAsync("() => sednaUi.graph.get('g').then(g => g.select('notify'))");
        await Heard(page, "sedna-graph-select", 2);

        var selected = await Log(page, "sedna-graph-select");
        Assert.Equal("waiting", selected[0].Detail.GetProperty("state").GetString());
        // A step not reached says so as null, as every other field the record lacks does.
        Assert.Equal(JsonValueKind.Null, selected[1].Detail.GetProperty("state").ValueKind);
        AssertQuiet(UnknownState);
    }

    [Fact]
    public async Task The_svg_export_keeps_a_runs_rings_and_dashes()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: Run, attrs: Flow));
        await Ready(page);

        var svg = await page.EvaluateAsync<JsonElement>("""
            async () => {
                const text = await (await sednaUi.graph.get('g').then(g => g.export('svg'))).text();
                const doc = new DOMParser().parseFromString(text, 'image/svg+xml');
                const record = name => [...doc.querySelectorAll('g.graph-record')].find(g => g.querySelector('title').textContent === name);
                const link = name => [...doc.querySelectorAll('g.graph-link')].find(g => g.querySelector('title').textContent === name);
                return {
                    broken: doc.getElementsByTagName('parsererror').length,
                    doneRing: record('Webhook received').querySelector('.graph-ring')?.getAttribute('stroke'),
                    waitingRing: record("Wait for the caller's reply").querySelector('.graph-ring')?.getAttribute('stroke-dasharray'),
                    pending: record('Page the on-call engineer').querySelectorAll('.graph-ring').length,
                    next: record('Look up the caller').querySelector(':not(.graph-ring)[stroke-dasharray]')?.getAttribute('stroke-dasharray'),
                    flowing: link('The webhook is parsed').querySelector('path').getAttribute('stroke-dasharray'),
                    go: paintOf('var(--go-solid)'),
                };
            }
            """);
        Assert.Equal(0, svg.GetProperty("broken").GetInt32());
        Assert.Equal(svg.GetProperty("go").GetString(), Flat(svg.GetProperty("doneRing").GetString()));
        Assert.Equal("4 2", svg.GetProperty("waitingRing").GetString());
        Assert.Equal(0, svg.GetProperty("pending").GetInt32());
        Assert.Equal("5 3", svg.GetProperty("next").GetString());
        Assert.Equal("6 4", svg.GetProperty("flowing").GetString());
        AssertQuiet(UnknownState);

        static string Flat(string? s) => (s ?? "").Replace(" ", "", StringComparison.Ordinal);
    }

    // ── The legend ───────────────────────────────────────────────────────────

    private const string Legend = """
        <div class="graph-legend">
          <span class="graph-key"><span class="graph-line graph-line--flow graph-line--arrow" id="flow"></span> Running</span>
        </div>
        """;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_legends_key_for_a_running_link_is_dashed_in_the_agent_colour_and_moves_only_where_motion_is_welcome(bool reduce)
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Legend, reduceMotion: reduce);

        var key = await page.EvaluateAsync<JsonElement>("""
            () => {
                const el = document.getElementById('flow');
                const dashes = getComputedStyle(el, '::before');
                return { colour: paintOf(getComputedStyle(el).color), agent: paintOf('var(--agent-to)'),
                         dashes: paintOf(dashes.borderTopColor), mask: dashes.maskImage || dashes.webkitMaskImage,
                         animation: dashes.animationName, arrow: paintOf(getComputedStyle(el, '::after').borderLeftColor) };
            }
            """);
        var agent = key.GetProperty("agent").GetString();
        Assert.Equal(agent, key.GetProperty("colour").GetString());
        Assert.Equal(agent, key.GetProperty("dashes").GetString());
        Assert.Equal(agent, key.GetProperty("arrow").GetString());
        Assert.StartsWith("repeating-linear-gradient", key.GetProperty("mask").GetString(), StringComparison.Ordinal);
        Assert.Equal(reduce ? "none" : "sedna-graph-flow", key.GetProperty("animation").GetString());
        AssertQuiet();
    }
}
