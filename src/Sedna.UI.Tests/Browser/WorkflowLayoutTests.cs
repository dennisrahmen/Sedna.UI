using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.workflow</c>: every wire, bracket and label is hung on one line through the heads of
/// the nodes, and every wire is painted from the state of the step it leads into.
/// </summary>
/// <remarks>
/// The connectors are pseudo-elements, which have no box a test can ask for. Each is
/// absolutely positioned, so its box is its containing block's padding box plus its
/// resolved <c>left</c>, <c>top</c>, <c>width</c> and <c>height</c> — which is what
/// <c>window.box</c> below adds up.
/// </remarks>
public class WorkflowLayoutTests : ScriptTestBase
{
    private static string Node(string id, string title, string body = "") => $"""
        <article class="workflow-node" id="{id}">
            <header class="workflow-node-head">
                <span class="workflow-node-icon series-2"><i class="ri-coupon-3-line"></i></span>
                <span class="workflow-node-title">{title}</span>
                <span class="workflow-node-meta">meta</span>
            </header>
            {body}
        </article>
        """;

    private static readonly string Run = $"""
        <ol class="workflow" id="wf" style="width: 3600px">
            <li class="workflow-step" id="s-start" data-state="done"><span class="workflow-end" id="start"><i class="ri-phone-line"></i>Call in</span></li>
            <li class="workflow-step" id="s-a" data-state="done">{Node("a", "Tall", "<p class=\"workflow-node-now\">A line</p><p class=\"workflow-node-now\">Another</p>")}</li>
            <li class="workflow-fork" id="par">
                <div class="workflow-branch" id="p0"><ol class="workflow-path" id="p0-path">
                    <li class="workflow-step" id="s-p0" data-state="done">{Node("p0n", "Lane 0")}</li>
                </ol></div>
                <div class="workflow-branch" id="p1"><ol class="workflow-path" id="p1-path">
                    <li class="workflow-step" id="s-p1a" data-state="done">{Node("p1a", "Lane 1a")}</li>
                    <li class="workflow-step" id="s-p1b" data-state="running">{Node("p1b", "Lane 1b")}</li>
                </ol></div>
                <div class="workflow-branch" id="p2"><ol class="workflow-path" id="p2-path">
                    <li class="workflow-step" id="s-p2">{Node("p2n", "Lane 2")}</li>
                </ol></div>
            </li>
            <li class="workflow-step" id="s-b" data-state="next">{Node("b", "Next")}</li>
            <li class="workflow-loop" id="loop">
                <ol class="workflow-path">
                    <li class="workflow-step" id="s-l1">{Node("l1", "Ask", "<p class=\"workflow-node-now\">Tall</p><p class=\"workflow-node-now\">Taller</p>")}</li>
                    <li class="workflow-step" id="s-l2">{Node("l2", "Listen")}</li>
                </ol>
                <span class="workflow-loop-count" id="count">Turn 1</span>
            </li>
            <li class="workflow-fork workflow-fork--decision" id="dec">
                <div class="workflow-branch" id="d0" data-state="skipped">
                    <span class="workflow-label" id="d0-label">Fixed</span>
                    <ol class="workflow-path"><li class="workflow-step" id="s-d0">{Node("d0n", "Close")}</li></ol>
                </div>
                <div class="workflow-branch" id="d1" data-state="taken">
                    <span class="workflow-label" id="d1-label">Not fixed</span>
                    <ol class="workflow-path"><li class="workflow-step" id="s-d1" data-state="done">{Node("d1n", "Ticket")}</li></ol>
                </div>
                <div class="workflow-branch" id="d2" data-state="skipped">
                    <span class="workflow-label">Later</span>
                    <ol class="workflow-path"><li class="workflow-step" id="s-d2">{Node("d2n", "Queue")}</li></ol>
                </div>
            </li>
        </ol>
        <span id="probe" style="position:absolute"></span>
        """;

    private const string Helpers = """
        () => {
            window.box = (el, pseudo) => {
                if (typeof el === 'string') el = document.getElementById(el);
                const cs = getComputedStyle(el, pseudo);
                if (pseudo && cs.content === 'none') return null;
                if (!pseudo) {
                    const r = el.getBoundingClientRect();
                    return { x: r.left, y: r.top, w: r.width, h: r.height, right: r.right, bottom: r.bottom, cx: r.left + r.width / 2, cy: r.top + r.height / 2 };
                }
                // A pseudo-element is placed in its containing block: its own element
                // when that is positioned, else the nearest positioned ancestor — a
                // branch's ways are placed in the fork.
                let host = el;
                while (getComputedStyle(host).position === 'static') host = host.parentElement;
                const r = host.getBoundingClientRect();
                const own = getComputedStyle(host);
                const x = r.left + parseFloat(own.borderLeftWidth) + parseFloat(cs.left);
                const y = r.top + parseFloat(own.borderTopWidth) + parseFloat(cs.top);
                let w = parseFloat(cs.width), h = parseFloat(cs.height);
                if (cs.boxSizing !== 'border-box') {
                    w += parseFloat(cs.borderLeftWidth) + parseFloat(cs.borderRightWidth) + parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
                    h += parseFloat(cs.borderTopWidth) + parseFloat(cs.borderBottomWidth) + parseFloat(cs.paddingTop) + parseFloat(cs.paddingBottom);
                }
                return { x, y, w, h, right: x + w, bottom: y + h, cx: x + w / 2, cy: y + h / 2 };
            };
            // The centre of a node's head: the middle of whatever leads it.
            window.port = id => window.box(document.querySelector('#' + id + ' .workflow-node-head > :first-child')).cy;
            // Where a piece's light field starts, as along-plus-down on the page: two
            // pieces that share a field share this number. Read with the light still.
            window.field = (el, pseudo) => {
                if (typeof el === 'string') el = document.getElementById(el);
                const b = window.box(el, pseudo);
                // The light is the second layer: over it lies the way's transition, if any.
                const at = parseFloat(getComputedStyle(el, pseudo).backgroundPosition.split(',')[1]);
                return b.x + at + b.y;
            };
            window.colour = token => {
                const p = document.getElementById('probe');
                p.style.color = `var(${token})`;
                return getComputedStyle(p).color;
            };
        }
        """;

    private async Task<IPage> OpenRun(string? html = null, bool rtl = false, ReducedMotion? motion = null)
    {
        var page = await Open(html ?? Run, head: StylesheetTag, reducedMotion: motion);
        if (rtl) await page.EvaluateAsync("() => document.documentElement.dir = 'rtl'");
        await page.SetViewportSizeAsync(1600, 900);
        await page.EvaluateAsync(Helpers);
        return page;
    }

    private static Task<double> Eval(IPage page, string js) => page.EvaluateAsync<double>(js);

    [Fact]
    public async Task Every_wire_meets_its_step_on_the_head_s_centre_line()
    {
        if (NoBrowser) return;
        var page = await OpenRun();

        // A wire is 2px; its middle is the port line, and the node's head is centred on it.
        foreach (var (step, node) in new[] { ("s-a", "a"), ("s-b", "b"), ("s-p1b", "p1b"), ("s-l2", "l2") })
        {
            var wire = await Eval(page, $"() => box('{step}', '::before').cy");
            var head = await Eval(page, $"() => port('{node}')");
            Assert.True(Math.Abs(wire - head) < 0.6, $"The wire into {step} is at {wire}, its head at {head}.");
        }

        // A tall node and a short one share the line.
        Assert.True(Math.Abs(await Eval(page, "() => port('a')") - await Eval(page, "() => port('b')")) < 0.6);

        // An end and a label sit on it too.
        var line = await Eval(page, "() => port('a')");
        Assert.True(Math.Abs(await Eval(page, "() => box('start').cy") - line) < 0.6, "The end is off the line.");
        Assert.True(Math.Abs(await Eval(page, "() => box('d0-label').cy") - line) < 0.6, "A branch's label is off the line.");
    }

    [Fact]
    public async Task A_wire_runs_from_the_step_before_to_the_node_it_leads_into()
    {
        if (NoBrowser) return;
        var page = await OpenRun();

        // After a fork, the wire starts at the join's bracket, half a gap past the fork,
        // so it runs on from the branches' tails rather than over them.
        var wire = await page.EvaluateAsync<double[]>("() => { const b = box('s-b', '::before'); return [b.x, b.right]; }");
        var join = await Eval(page, "() => box('par').right + parseFloat(getComputedStyle(document.getElementById('wf')).columnGap) / 2");
        var node = await Eval(page, "() => box('b').x");
        Assert.True(Math.Abs(wire[0] - join) < 0.6, $"The wire starts at {wire[0]}, the join is at {join}.");
        var step = await page.EvaluateAsync<double[]>("() => [box('s-a', '::before').x, box('start').right]");
        Assert.True(Math.Abs(step[0] - step[1]) < 0.6, "Between two steps, the wire does not start at the step before.");
        Assert.True(Math.Abs(wire[1] - node) < 0.6, $"The wire ends at {wire[1]}, its node starts at {node}.");

        // The first step of the run has nothing before it.
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('s-start'), '::before').content"));
    }

    [Fact]
    public async Task Each_branch_draws_its_whole_way_in_and_out_of_the_fork_round_every_corner()
    {
        if (NoBrowser) return;
        var page = await OpenRun();

        var line0 = await Eval(page, "() => port('p0n')");
        var forkX = await Eval(page, "() => box('par').x");
        var forkEnd = await Eval(page, "() => box('par').right");
        var gap = await Eval(page, "() => parseFloat(getComputedStyle(document.getElementById('wf')).columnGap)");
        var curve = await Eval(page, "() => parseFloat(getComputedStyle(document.getElementById('wf')).getPropertyValue('--workflow-curve'))");
        static string Box(string id, string pseudo) => $"() => {{ const b = box('{id}', '{pseudo}'); return [b.x, b.y, b.right, b.bottom]; }}";
        const double Wire = 2;

        // In, the first branch: the wire into the fork run straight on from the bracket.
        var first = await page.EvaluateAsync<double[]>(Box("p0", "::before"));
        Assert.True(Math.Abs(first[0] - (forkX - gap / 2)) < 0.6, "The first branch's way does not start at the bracket.");
        Assert.True(Math.Abs(first[2] - forkX) < 0.6, "The first branch's way does not reach the branch.");
        Assert.True(Math.Abs(first[1] + Wire / 2 - line0) < 0.6, "The first branch's way is off the fork's line.");

        // In, any other: a curve out of the fork's line onto the bracket half a gap out,
        // then down the bracket, round into the branch's own line, and along it.
        foreach (var (branch, node) in new[] { ("p1", "p1a"), ("p2", "p2n") })
        {
            var bend = await page.EvaluateAsync<double[]>(Box(branch, "::before"));
            var down = await page.EvaluateAsync<double[]>(Box(branch, "::after"));
            var own = await Eval(page, $"() => port('{node}')");
            Assert.True(Math.Abs(bend[1] + Wire / 2 - line0) < 0.6, $"{branch}'s curve leaves the fork's line at {bend[1] + Wire / 2}, not {line0}.");
            Assert.True(Math.Abs(bend[0] - (forkX - gap / 2 - curve)) < 0.6, $"{branch}'s curve does not start a curve before the bracket.");
            Assert.True(Math.Abs(bend[2] - Wire / 2 - (forkX - gap / 2)) < 0.6, $"{branch}'s curve does not come down on the bracket.");
            Assert.True(Math.Abs(down[1] - bend[3]) < 0.6, $"{branch}'s bracket does not run on from its curve.");
            Assert.True(Math.Abs(down[0] + Wire / 2 - (forkX - gap / 2)) < 0.6, $"{branch}'s bracket is off the curve's end.");
            Assert.True(Math.Abs(down[3] - Wire / 2 - own) < 0.6, $"{branch}'s way ends at {down[3] - Wire / 2}, its line is {own}.");
            Assert.True(Math.Abs(down[2] - forkX) < 0.6, $"{branch}'s way does not reach the branch.");
        }
        // Every corner is a curve, into a middle branch too, and each way is one shape cut
        // to its line.
        foreach (var branch in new[] { "p1", "p2" })
        {
            Assert.NotEqual("0px", await page.EvaluateAsync<string>($"() => getComputedStyle(document.getElementById('{branch}'), '::after').borderBottomLeftRadius"));
            Assert.StartsWith("shape(", await page.EvaluateAsync<string>($"() => getComputedStyle(document.getElementById('{branch}'), '::after').clipPath"), StringComparison.Ordinal);
        }

        // Out: from the branch's last step along its line to the join, half a gap past the
        // fork; the first straight, the others round and up the bracket and round into
        // the line on.
        var join = forkEnd + gap / 2;
        foreach (var (path, last, node) in new[] { ("p0-path", "p0n", "p0n"), ("p1-path", "p1b", "p1a"), ("p2-path", "p2n", "p2n") })
        {
            var way = await page.EvaluateAsync<double[]>(Box(path, "::before"));
            Assert.True(Math.Abs(way[0] - await Eval(page, $"() => box('{last}').right")) < 0.6, $"{path}'s way out does not start at its last step.");
            Assert.True(Math.Abs(way[3] - Wire / 2 - await Eval(page, $"() => port('{node}')")) < 0.6, $"{path}'s way out does not leave from its own line.");
            var end = path == "p0-path" ? join : join + Wire / 2;
            Assert.True(Math.Abs(way[2] - end) < 0.6, $"{path}'s way out ends at {way[2]}, not at the join, {end}.");
        }
        var round = await page.EvaluateAsync<double[]>(Box("p2-path", "::after"));
        Assert.True(Math.Abs(round[0] + Wire / 2 - join) < 0.6, "The curve into the line on is not on the join.");
        Assert.True(Math.Abs(round[1] + Wire / 2 - line0) < 0.6, "The curve into the line on is off the fork's line.");
        Assert.True(Math.Abs(round[3] - await Eval(page, "() => box('p2-path', '::before').y")) < 0.6, "The join's bracket does not run on into its curve.");
    }

    [Fact]
    public async Task Where_a_curve_meets_a_line_of_another_colour_the_two_fade_into_each_other()
    {
        if (NoBrowser) return;
        var page = await OpenRun($"""
            <ol class="workflow" id="wf">
                <li class="workflow-step" data-state="done">{Node("t", "Ticket")}</li>
                <li class="workflow-fork" id="ff">
                    <div class="workflow-branch" id="f0"><ol class="workflow-path"><li class="workflow-step" data-state="done">{Node("f0n", "Done first")}</li></ol></div>
                    <div class="workflow-branch" id="f1"><ol class="workflow-path"><li class="workflow-step" data-state="done">{Node("f1n", "Done too")}</li></ol></div>
                    <div class="workflow-branch" id="f2"><ol class="workflow-path"><li class="workflow-step" data-state="running">{Node("f2n", "At work")}</li></ol></div>
                    <div class="workflow-branch" id="f3"><ol class="workflow-path"><li class="workflow-step">{Node("f3n", "Not yet")}</li></ol></div>
                </li>
                <li class="workflow-step">{Node("u", "Update")}</li>
            </ol>
            <span id="probe"></span>
            """);
        var go = await page.EvaluateAsync<string>("() => colour('--go-solid')");
        string Paint(string id) => $"() => getComputedStyle(document.getElementById('{id}'), '::after').backgroundImage";

        // A finished branch over the light below it: its own line runs on down the bracket
        // and fades into the light's — a gradient, not an edge.
        Assert.Equal("1", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('f1')).getPropertyValue('--workflow-stub-in').trim()"));
        Assert.StartsWith("linear-gradient(rgba(0, 0, 0, 0), ", await page.EvaluateAsync<string>(Paint("f1")), StringComparison.Ordinal);
        // The branch at work over one not reached: the same, into the quiet line.
        Assert.Equal("1", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('f2')).getPropertyValue('--workflow-stub-in').trim()"));
        // The last branch has nothing below: no transition.
        Assert.StartsWith("conic-gradient(", await page.EvaluateAsync<string>(Paint("f3")), StringComparison.Ordinal);
        Assert.Equal("transparent", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('f3')).getPropertyValue('--workflow-under-in').trim()"));
        // Out of the fork's still, finished line, the way to the branch at work starts in
        // that line's colour and fades into its own along its curve.
        var bend = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('f2'), '::before').backgroundImage");
        Assert.StartsWith("conic-gradient(", bend, StringComparison.Ordinal);
        Assert.Contains(go, bend, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_wires_and_the_ways_light_up_the_way_the_run_went()
    {
        if (NoBrowser) return;
        var page = await OpenRun();
        var go = await page.EvaluateAsync<string>("() => colour('--go-solid')");
        var quiet = await page.EvaluateAsync<string>("() => colour('--border-strong')");

        string Background(string id, string pseudo = "::before") => $"() => getComputedStyle(document.getElementById('{id}'), '{pseudo}').backgroundImage";

        // Into a done step: the trail.
        Assert.Contains(go, await page.EvaluateAsync<string>(Background("s-a")), StringComparison.Ordinal);
        // Into the step at work: a light that moves, in the light's colour, never the agent's.
        var wire = await page.EvaluateAsync<string>(Background("s-p1b"));
        Assert.Contains(await page.EvaluateAsync<string>("() => colour('--flow-light')"), wire, StringComparison.Ordinal);
        Assert.DoesNotContain(await page.EvaluateAsync<string>("() => colour('--agent-from')"), wire, StringComparison.Ordinal);
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('s-p1b'), '::before').animationName"));
        // Into the next one: dashed.
        Assert.StartsWith("repeating-linear-gradient", await page.EvaluateAsync<string>(Background("s-b")), StringComparison.Ordinal);
        // Into a step not reached: quiet, and still.
        Assert.Contains(quiet, await page.EvaluateAsync<string>(Background("s-l2")), StringComparison.Ordinal);
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('s-l2'), '::before').animationName"));

        // A decision: the way into the taken branch is the trail, laid over the dashed
        // ways into the skipped ones where they share the bracket.
        string Edge(string id, string pseudo, string side) => $"() => {{ const s = getComputedStyle(document.getElementById('{id}'), '{pseudo}'); return s.border{side}Color + ' ' + s.border{side}Style; }}";
        Assert.EndsWith($"linear-gradient({go}, {go})", await page.EvaluateAsync<string>(Background("d1", "::after")), StringComparison.Ordinal);
        Assert.EndsWith("dashed", await page.EvaluateAsync<string>(Edge("d0", "::before", "Top")), StringComparison.Ordinal);
        Assert.EndsWith("dashed", await page.EvaluateAsync<string>(Edge("d2", "::after", "Left")), StringComparison.Ordinal);
        var z = await page.EvaluateAsync<int[]>("() => ['d1', 'd2'].map(id => +getComputedStyle(document.getElementById(id), '::after').zIndex)");
        Assert.True(z[0] > z[1], "The way the run went does not lie over the way it did not.");

        // A parallel fork's join: a finished branch's way out is the trail, one still at
        // work is quiet, and lies over one skipped.
        Assert.EndsWith($"linear-gradient({go}, {go})", await page.EvaluateAsync<string>(Background("p0-path")), StringComparison.Ordinal);
        Assert.EndsWith($"linear-gradient({quiet}, {quiet})", await page.EvaluateAsync<string>(Background("p1-path")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_skipped_branch_quiets_every_step_in_it_and_its_word()
    {
        if (NoBrowser) return;
        var page = await OpenRun();

        Assert.Equal("dashed", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('d0n')).borderTopStyle"));
        Assert.Equal(await page.EvaluateAsync<string>("() => colour('--muted')"),
            await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#d0n .workflow-node-title')).color"));
        Assert.Equal("dashed", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('d0-label')).borderTopStyle"));
        Assert.Equal(await page.EvaluateAsync<string>("() => colour('--go-fg')"),
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('d1-label')).color"));
    }

    [Fact]
    public async Task A_loop_draws_its_way_back_under_its_tallest_step()
    {
        if (NoBrowser) return;
        var page = await OpenRun();

        var back = await page.EvaluateAsync<double[]>("() => { const b = box('loop', '::after'); return [b.x, b.right, b.y, b.bottom]; }");
        var tallest = await Eval(page, "() => Math.max(box('l1').bottom, box('l2').bottom)");
        var first = await Eval(page, "() => box('l1').x");
        var last = await Eval(page, "() => box('l2').right");
        var line = await Eval(page, "() => port('l1')");

        Assert.True(back[3] > tallest, "The way back runs through a step.");
        Assert.True(back[0] < first && back[1] > last, "The way back does not go round the steps.");
        Assert.True(Math.Abs(back[2] - (line + 1)) < 0.6, "The way back does not leave from the port line.");

        // The count sits on the way back.
        var count = await Eval(page, "() => box('count').cy");
        Assert.True(Math.Abs(count - (back[3] - 1)) < 0.6, $"The count is at {count}, the way back at {back[3] - 1}.");
    }

    [Fact]
    public async Task Fill_shares_the_width_out_between_the_steps()
    {
        if (NoBrowser) return;
        var page = await OpenRun($"""
            <div style="width: 900px">
                <ol class="workflow workflow--fill" id="wf">
                    <li class="workflow-step">{Node("f1", "One")}</li>
                    <li class="workflow-step">{Node("f2", "Two")}</li>
                    <li class="workflow-step">{Node("f3", "Three")}</li>
                    <li class="workflow-step">{Node("f4", "Four")}</li>
                </ol>
            </div>
            <span id="probe"></span>
            """);

        var widths = await page.EvaluateAsync<double[]>("() => ['f1','f2','f3','f4'].map(id => box(id).w)");
        Assert.All(widths, w => Assert.True(Math.Abs(w - widths[0]) < 0.6, "The steps do not share the width."));
        Assert.False(await page.EvaluateAsync<bool>("() => { const w = document.getElementById('wf'); return w.scrollWidth > w.clientWidth; }"),
            "The overview scrolls instead of fitting.");

        // Too narrow to share out, a step keeps room for its name and the overview scrolls.
        await page.EvaluateAsync("() => document.getElementById('wf').parentElement.style.width = '360px'");
        var node = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.getElementById('wf')).getPropertyValue('--workflow-node'))");
        Assert.True(await page.EvaluateAsync<double>("() => box('f1').w") >= node * 0.75 - 0.5, "A step was squeezed below its floor.");
        Assert.True(await page.EvaluateAsync<bool>("() => { const w = document.getElementById('wf'); return w.scrollWidth > w.clientWidth; }"),
            "A squeezed overview does not scroll.");
    }

    [Fact]
    public async Task A_diagram_that_scrolls_is_reached_and_scrolled_by_the_keyboard()
    {
        if (NoBrowser) return;
        var page = await OpenRun($"""
            <div style="width: 500px">
                <ol class="workflow" id="wf" tabindex="0" aria-label="A run too wide for its box">
                    <li class="workflow-step" data-state="done">{Node("k1", "One")}</li>
                    <li class="workflow-step" data-state="running">{Node("k2", "Two")}</li>
                    <li class="workflow-step">{Node("k3", "Three")}</li>
                </ol>
            </div>
            <span id="probe"></span>
            """);

        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("wf", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        // The ring is drawn inside the diagram, which clips anything outside it.
        Assert.Contains("inset", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('wf')).boxShadow"), StringComparison.Ordinal);

        await page.Keyboard.PressAsync("ArrowRight");
        await page.WaitForFunctionAsync("() => document.getElementById('wf').scrollLeft > 0");
    }

    [Fact]
    public async Task A_state_is_drawn_on_the_node_s_mark()
    {
        if (NoBrowser) return;
        var page = await OpenRun();

        var go = await page.EvaluateAsync<string>("() => colour('--go-solid')");
        Assert.Equal(go, await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('a'), '::before').backgroundColor"));
        // The tick: two strokes of a turned corner.
        Assert.Equal("45deg", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('a'), '::after').rotate"));
        // The mark is on the port line.
        var mark = await Eval(page, "() => box('a', '::before').cy");
        Assert.True(Math.Abs(mark - await Eval(page, "() => port('a')")) < 0.6, "The mark is off the line.");
        // At work: the edge takes the light's colour and holds still, and the mark
        // turns on the compositor.
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b')).animationName"));
        Assert.Equal(await page.EvaluateAsync<string>("() => colour('--flow-light')"),
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b')).borderTopColor"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b')).backgroundImage"));
        Assert.Equal("sedna-spin", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b'), '::before').animationName"));

        // An agent's step wears the agent's two colours on its edge instead, still; the
        // same class on a step not at work does nothing.
        await page.EvaluateAsync("() => ['p1b', 'a'].forEach(id => document.getElementById(id).classList.add('workflow-node--agent'))");
        var edge = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b')).backgroundImage");
        Assert.Contains("linear-gradient(90deg", edge, StringComparison.Ordinal);
        Assert.Contains(await page.EvaluateAsync<string>("() => colour('--agent-from')"), edge, StringComparison.Ordinal);
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('a')).backgroundImage"));
        // A glow is the plain edge with a glow round it.
        await page.EvaluateAsync("() => document.getElementById('p1b').classList.replace('workflow-node--agent', 'workflow-node--glow')");
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b')).backgroundImage"));
        Assert.Contains("22px", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b')).getPropertyValue('--workflow-glow')"), StringComparison.Ordinal);
    }

    private static readonly string Ways = $"""
        <ol class="workflow" id="wf" style="width: 5200px">
            <li class="workflow-step" data-state="done">{Node("w0", "Start")}</li>
            <li class="workflow-fork" id="jf">
                <div class="workflow-branch"><ol class="workflow-path" id="j0-path"><li class="workflow-step" data-state="done">{Node("j0", "Short")}</li></ol></div>
                <div class="workflow-branch"><ol class="workflow-path" id="j1-path">
                    <li class="workflow-step" data-state="done">{Node("j1a", "Long a")}</li>
                    <li class="workflow-step" data-state="done">{Node("j1b", "Long b")}</li>
                </ol></div>
                <div class="workflow-branch"><ol class="workflow-path" id="j2-path"><li class="workflow-step" data-state="done">{Node("j2", "Third")}</li></ol></div>
                <div class="workflow-branch" data-state="skipped"><ol class="workflow-path" id="j3-path"><li class="workflow-step">{Node("j3", "Not taken")}</li></ol></div>
            </li>
            <li class="workflow-step" id="after-join" data-state="running">{Node("aj", "After the join")}</li>
            <li class="workflow-fork" id="of">
                <div class="workflow-branch"><ol class="workflow-path"><li class="workflow-step" id="first-in" data-state="running">{Node("fi", "First in a branch")}</li></ol></div>
                <div class="workflow-branch"><ol class="workflow-path"><li class="workflow-step">{Node("fo", "Other")}</li></ol></div>
            </li>
            <li class="workflow-loop" id="lp"><ol class="workflow-path" id="lp-path"><li class="workflow-step" data-state="done">{Node("lp1", "Turn")}</li></ol></li>
            <li class="workflow-step" id="after-loop" data-state="running">{Node("al", "After the loop")}</li>
            <li class="workflow-loop" id="lq"><ol class="workflow-path"><li class="workflow-step" data-state="running">{Node("lq1", "First in a loop")}</li></ol></li>
            <li class="workflow-fork" id="sf">
                <div class="workflow-branch" id="sf0"><ol class="workflow-path"><li class="workflow-step">{Node("sf0n", "Above")}</li></ol></div>
                <div class="workflow-branch" id="sf1"><ol class="workflow-path"><li class="workflow-step">{Node("sf1n", "Between")}</li></ol></div>
                <div class="workflow-branch" id="sf2"><ol class="workflow-path"><li class="workflow-step" data-state="running">{Node("sf2n", "At work")}</li></ol></div>
            </li>
            <li class="workflow-fork" id="outer">
                <div class="workflow-branch"><ol class="workflow-path">
                    <li class="workflow-fork" id="opens">
                        <div class="workflow-branch"><ol class="workflow-path"><li class="workflow-step">{Node("n1", "Opens a")}</li></ol></div>
                        <div class="workflow-branch"><ol class="workflow-path"><li class="workflow-step">{Node("n2", "Opens b")}</li></ol></div>
                    </li>
                </ol></div>
                <div class="workflow-branch"><ol class="workflow-path">
                    <li class="workflow-step">{Node("n3", "Before")}</li>
                    <li class="workflow-fork" id="ends">
                        <div class="workflow-branch"><ol class="workflow-path" id="ends0"><li class="workflow-step">{Node("n4", "Ends a")}</li></ol></div>
                        <div class="workflow-branch"><ol class="workflow-path" id="ends1"><li class="workflow-step">{Node("n5", "Ends b")}</li></ol></div>
                    </li>
                </ol></div>
            </li>
            <li class="workflow-step">{Node("z", "Joined")}</li>
        </ol>
        <span id="probe"></span>
        """;

    [Fact]
    public async Task The_light_runs_along_every_way_into_the_step_at_work_in_one_piece()
    {
        if (NoBrowser) return;
        var page = await OpenRun(Ways);
        static string Anim(string id, string pseudo) => $"() => getComputedStyle(document.getElementById('{id}'), '{pseudo}').animationName";
        static string AnimOf(string selector, string pseudo) => $"() => getComputedStyle(document.querySelector('{selector}'), '{pseudo}').animationName";

        // Into the step after a join: the way out of every branch not skipped, each from
        // its last step to the step after the fork. The wire they all run over is theirs,
        // so it carries no light of its own.
        foreach (var path in new[] { "j0-path", "j1-path", "j2-path" })
            Assert.Equal("workflow-flow", await page.EvaluateAsync<string>(Anim(path, "::before")));
        Assert.Equal("none", await page.EvaluateAsync<string>(Anim("j3-path", "::before")));
        Assert.Equal("none", await page.EvaluateAsync<string>(Anim("after-join", "::before")));
        var node = await Eval(page, "() => box('aj').x");
        Assert.True(Math.Abs(await Eval(page, "() => box('j0-path', '::before').right") - node) < 0.6, "The first branch's way does not run on to the step after the join.");
        foreach (var (path, pseudo) in new[] { ("j0-path", "::before"), ("j1-path", "::before"), ("j1-path", "::after"), ("j2-path", "::after") })
            Assert.Equal("workflow-flow", await page.EvaluateAsync<string>(Anim(path, pseudo)));
        // The light shows on the line alone: one path cuts the box to it.
        Assert.Equal("rgba(0, 0, 0, 0)", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('j1-path'), '::before').borderBottomColor"));
        Assert.StartsWith("shape(", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('j1-path'), '::before').clipPath"), StringComparison.Ordinal);

        // Into the first step of a branch: the wire into the fork and the branch's way in.
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>(Anim("of", "::before")));
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>(AnimOf("#of > .workflow-branch:first-child", "::before")));
        Assert.Equal("none", await page.EvaluateAsync<string>(AnimOf("#of > .workflow-branch:last-child", "::before")));
        // Into the last of three: its way down the bracket, and no other's.
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>(Anim("sf", "::before")));
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>(Anim("sf2", "::before")));
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>(Anim("sf2", "::after")));
        Assert.Equal("none", await page.EvaluateAsync<string>(Anim("sf0", "::before")));
        Assert.Equal("none", await page.EvaluateAsync<string>(Anim("sf1", "::before")));
        // Out of a loop into the step at work, and into a loop whose first step is at
        // work: the wire runs on over the loop's edge to the node, in one piece.
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>(Anim("after-loop", "::before")));
        Assert.True(Math.Abs(await Eval(page, "() => box('after-loop', '::before').x") - await Eval(page, "() => box('lp1').right")) < 0.6, "The wire out of a loop does not start at its last step.");
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>(Anim("lq", "::before")));
        Assert.True(Math.Abs(await Eval(page, "() => box('lq', '::before').right") - await Eval(page, "() => box('lq1').x")) < 0.6, "The wire into a loop does not reach its first step.");

        // One field: the wire into a fork and the way into the branch at work start their
        // light at the same place, the step before the fork, so a streak runs from one
        // into the other without a joint.
        await page.EvaluateAsync("() => document.getAnimations().forEach(a => { a.pause(); a.currentTime = 0; })");
        var trunk = await Eval(page, "() => field('sf', '::before')");
        foreach (var pseudo in new[] { "::before", "::after" })
        {
            var way = await Eval(page, $"() => field('sf2', '{pseudo}')");
            Assert.True(Math.Abs(trunk - way) < 0.6, $"The wire into the fork's light starts at {trunk}, the branch's {pseudo} at {way}.");
        }

        // One speed: every piece moves one tile, of one size, in one duration.
        var pieces = await page.EvaluateAsync<string[]>("""
            () => [['sf', '::before'], ['sf2', '::after'], ['j1-path', '::before'], ['after-loop', '::before']]
                .map(([id, p]) => { const s = getComputedStyle(document.getElementById(id), p); return s.backgroundSize.split(',')[1].trim() + ' ' + s.animationDuration; })
            """);
        Assert.All(pieces, piece => Assert.Equal(pieces[0], piece));
        Assert.StartsWith("240px 240px", pieces[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fork_can_open_a_branch_and_one_that_ends_a_branch_joins_with_its_fork()
    {
        if (NoBrowser) return;
        var page = await OpenRun(Ways);

        // Opening a branch: no wire of its own, and its bracket falls on the outer one,
        // running on down from under the line of the branch it opens.
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('opens'), '::before').content"));
        var inner = await Eval(page, "() => box(document.querySelector('#opens > .workflow-branch:last-child'), '::after').x");
        var outer = await Eval(page, "() => box(document.querySelector('#outer > .workflow-branch:last-child'), '::after').x");
        Assert.True(Math.Abs(inner - outer) < 0.6, $"The inner bracket is at {inner}, the outer at {outer}.");
        Assert.True(Math.Abs(await Eval(page, "() => box(document.querySelector('#opens > .workflow-branch:last-child'), '::after').y")
            - await Eval(page, "() => box(document.querySelector('#outer > .workflow-branch:first-child'), '::before').bottom")) < 0.6,
            "The inner bracket does not start under the line of the branch it opens.");

        // Ending a branch of a fork that joins: its own branches run their ways out to a join.
        Assert.NotEqual("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('ends0'), '::before').content"));
        Assert.NotEqual("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('ends1'), '::before').content"));
    }

    [Theory]
    [InlineData("workflow--sm")]
    [InlineData("")]
    [InlineData("workflow--lg")]
    [InlineData("workflow--xl")]
    public async Task Every_size_hangs_its_wires_on_the_head_s_centre_line(string size)
    {
        if (NoBrowser) return;
        var page = await OpenRun($"""
            <ol class="workflow {size}" id="wf">
                <li class="workflow-step" data-state="done">{Node("s1", "One")}</li>
                <li class="workflow-step" id="s2-step" data-state="running"><span class="workflow-label" id="lbl">On the wire</span>{Node("s2", "Two")}</li>
                <li class="workflow-step" id="s3-step"><span class="workflow-end" id="end">End</span></li>
            </ol>
            <span id="probe"></span>
            """);

        var line = await Eval(page, "() => port('s2')");
        foreach (var js in new[] { "() => box('s2-step', '::before').cy", "() => box('lbl').cy", "() => box('end').cy", "() => box('s2', '::before').cy", "() => port('s1')" })
            Assert.True(Math.Abs(await Eval(page, js) - line) < 0.6, $"{js} is off the line at {line} for '{size}'.");
    }

    [Fact]
    public async Task Focus_opens_only_the_step_at_work()
    {
        if (NoBrowser) return;
        var page = await OpenRun($"""
            <ol class="workflow workflow--focus" id="wf">
                <li class="workflow-step" data-state="done">{Node("f-done", "Done", "<p class='workflow-node-now' id='done-body'>Detail</p>")}</li>
                <li class="workflow-step" data-state="running">{Node("f-run", "At work", "<p class='workflow-node-now' id='run-body'>Detail</p>")}</li>
                <li class="workflow-step">{Node("f-next", "Ahead", "<p class='workflow-node-now' id='next-body'>Detail</p>")}</li>
            </ol>
            <span id="probe"></span>
            """);

        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('done-body')).display"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('next-body')).display"));
        Assert.NotEqual("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('run-body')).display"));
        var widths = await page.EvaluateAsync<double[]>("() => [box('f-done').w, box('f-run').w]");
        Assert.True(Math.Abs(widths[1] - widths[0] * 1.5) < 0.6, $"The step at work is {widths[1]} wide beside {widths[0]}.");
    }

    [Fact]
    public async Task The_trail_and_the_light_take_the_app_s_colours()
    {
        if (NoBrowser) return;
        var page = await OpenRun($"""
            <ol class="workflow" id="wf" style="--workflow-trail: var(--info-solid); --workflow-flow: var(--brand)">
                <li class="workflow-step" data-state="done">{Node("c1", "One")}</li>
                <li class="workflow-step" id="c2" data-state="done">{Node("c2n", "Two")}</li>
                <li class="workflow-step" id="c3" data-state="running">{Node("c3n", "Three")}</li>
            </ol>
            <span id="probe"></span>
            """);

        Assert.Contains(await page.EvaluateAsync<string>("() => colour('--info-solid')"),
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('c2'), '::before').backgroundImage"), StringComparison.Ordinal);
        Assert.Contains(await page.EvaluateAsync<string>("() => colour('--brand')"),
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('c3'), '::before').backgroundImage"), StringComparison.Ordinal);
    }

    private static string Down(string modifier) => $"""
        <ol class="workflow {modifier}" id="wf">
            <li class="workflow-step" data-state="done">{Node("da", "First")}</li>
            <li class="workflow-step" id="db-step" data-state="running">{Node("db", "Second")}</li>
            <li class="workflow-fork" id="df">
                <div class="workflow-branch" id="dl0"><span class="workflow-label" id="dl0-label">Known</span><ol class="workflow-path" id="dl0-path"><li class="workflow-step">{Node("dn0", "Lane one")}</li></ol></div>
                <div class="workflow-branch" id="dl1"><ol class="workflow-path" id="dl1-path"><li class="workflow-step" id="dn1-step">{Node("dn1", "Lane two")}</li></ol></div>
            </li>
            <li class="workflow-loop" id="dloop">
                <ol class="workflow-path"><li class="workflow-step">{Node("dq1", "Ask")}</li><li class="workflow-step">{Node("dq2", "Listen")}</li></ol>
                <span class="workflow-loop-count" id="dcount">Turn 1</span>
            </li>
            <li class="workflow-step"><span class="workflow-end" id="dend">End</span></li>
        </ol>
        <span id="probe"></span>
        """;

    /// <summary>The centre of a node's icon, across: where the rail runs down.</summary>
    private const string RailX = "id => window.box(document.querySelector('#' + id + ' .workflow-node-head > :first-child')).cx";

    [Fact]
    public async Task Top_to_bottom_hangs_every_wire_on_a_rail_through_the_icons()
    {
        if (NoBrowser) return;
        var page = await OpenRun($"<div style=\"width: 380px\">{Down("workflow--down")}</div>");
        await page.EvaluateAsync($"() => {{ window.railX = {RailX}; }}");

        // The steps stack, and the wire between two runs down the rail between them.
        Assert.True(await Eval(page, "() => box('db').y") > await Eval(page, "() => box('da').bottom"), "The steps do not stack.");
        var wire = await page.EvaluateAsync<double[]>("() => { const b = box('db-step', '::before'); return [b.cx, b.y, b.bottom, b.w]; }");
        Assert.True(Math.Abs(wire[0] - await Eval(page, "() => railX('db')")) < 0.6, "The wire is off the rail.");
        Assert.True(Math.Abs(wire[1] - await Eval(page, "() => box('da').bottom")) < 0.6, "The wire does not start at the step above.");
        Assert.True(Math.Abs(wire[2] - await Eval(page, "() => box('db').y")) < 0.6, "The wire does not reach the step below.");
        Assert.Equal(2, wire[3], 1);

        // A fork: the rail runs on down past its branches, which hang off it, indented.
        var rail = await Eval(page, "() => railX('da')");
        // Each branch's way in comes down it from the wire into the fork and turns along
        // the branch's line into its first node.
        Assert.True(await Eval(page, "() => box('dn1').x") > await Eval(page, "() => box('da').x") + 20, "A branch is not indented.");
        var way = await page.EvaluateAsync<double[]>("() => { const b = box('dl1', '::before'); return [b.x, b.right, b.y, b.bottom]; }");
        Assert.True(Math.Abs(way[0] + 1 - rail) < 0.6, $"The way into a branch comes down at {way[0] + 1}, the rail is at {rail}.");
        Assert.True(Math.Abs(way[1] - await Eval(page, "() => box('dn1').x")) < 0.6, "The way into a branch does not reach its node.");
        Assert.True(Math.Abs(way[3] - 1 - await Eval(page, "() => port('dn1')")) < 0.6, "The way into a branch is off its head's line.");
        Assert.True(Math.Abs(way[2] - await Eval(page, "() => box('df', '::before').bottom")) < 0.6, "The way into a branch does not start at the wire into the fork.");
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('dn1-step'), '::before').content"));
        // A branch's word sits on that line.
        Assert.True(Math.Abs(await Eval(page, "() => box('dl0', '::before').bottom") - 1 - await Eval(page, "() => box('dl0-label').cy")) < 0.6, "A branch's word is off its line.");
        // Nothing of the join across is drawn.
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('dl0-path'), '::after').content"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('dl1-path'), '::before').content"));

        // A loop draws its way back up its right-hand side.
        var back = await page.EvaluateAsync<double[]>("() => { const b = box('dloop', '::after'); return [b.x, b.y, b.bottom]; }");
        Assert.True(Math.Abs(back[0] - await Eval(page, "() => box('dq1').right")) < 0.6, "The way back does not leave the steps' right edge.");
        Assert.True(Math.Abs(back[1] + 1 - await Eval(page, "() => port('dq1')")) < 0.6, "The way back does not meet the first step's line.");
        Assert.True(back[2] > await Eval(page, "() => box('dq2').y"), "The way back does not reach the last step.");

        // The light runs down the wire into the step at work.
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('db-step'), '::before').animationName"));
    }

    [Fact]
    public async Task Auto_runs_across_on_a_wide_screen_and_down_on_a_narrow_one()
    {
        if (NoBrowser) return;
        var page = await OpenRun(Down("workflow--auto"));

        await page.SetViewportSizeAsync(1600, 900);
        Assert.True(await Eval(page, "() => box('db').x") > await Eval(page, "() => box('da').right"), "Wide, the steps do not run across.");

        await page.SetViewportSizeAsync(375, 800);
        await page.WaitForFunctionAsync("() => box('db').y > box('da').bottom");
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('dl0-path'), '::after').content"));
    }

    /// <summary>A fork of three run down, with the step at work in branch <paramref name="running"/>, after the fork at 3, or none with the step after it next at 4.</summary>
    private static string DownFork(int running) => $"""
        <div style="width: 380px">
            <ol class="workflow workflow--down" id="wf">
                <li class="workflow-step" data-state="done">{Node("fa", "Before")}</li>
                <li class="workflow-fork" id="df">
                    {string.Concat(Enumerable.Range(0, 3).Select(i => $"""<div class="workflow-branch" id="b{i}"><ol class="workflow-path"><li class="workflow-step" id="b{i}-step"{(i == running ? " data-state=\"running\"" : "")}>{Node($"bn{i}", $"Branch {i}")}</li></ol></div>"""))}
                </li>
                <li class="workflow-step" id="fz-step"{(running == 3 ? " data-state=\"running\"" : running == 4 ? " data-state=\"next\"" : "")}>{Node("fz", "After")}</li>
            </ol>
        </div>
        <span id="probe"></span>
        """;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Top_to_bottom_the_light_comes_down_the_rail_in_one_piece_and_turns_into_the_branch_at_work(int running)
    {
        if (NoBrowser) return;
        var page = await OpenRun(DownFork(running));

        // The branch at work's way runs from the wire into the fork down the rail and
        // along its line to its node, in one piece, and moves; no other branch's does.
        var way = await page.EvaluateAsync<double[]>($"() => {{ const b = box('b{running}', '::before'); return [b.y, b.bottom, b.right]; }}");
        Assert.True(Math.Abs(way[0] - await Eval(page, "() => box('df', '::before').bottom")) < 0.6, "The way does not start at the wire into the fork.");
        Assert.True(Math.Abs(way[1] - 1 - await Eval(page, $"() => port('bn{running}')")) < 0.6, "The way does not turn on its branch's line.");
        Assert.True(Math.Abs(way[2] - await Eval(page, $"() => box('bn{running}').x")) < 0.6, "The way does not reach its branch's node.");
        var moving = await page.EvaluateAsync<string[]>("""
            () => ['b0', 'b1', 'b2'].flatMap(id => ['::before', '::after'].map(p => [id, p]))
                .filter(([id, p]) => {
                    const s = getComputedStyle(document.getElementById(id), p);
                    return s.content !== 'none' && s.animationName !== 'none';
                })
                .map(([id, p]) => id + p)
            """);
        Assert.Equal([$"b{running}::before"], moving);
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('df'), '::before').animationName"));

        // And the wire into the fork shares its field, so the light comes down without a joint.
        await page.EvaluateAsync("() => document.getAnimations().forEach(a => { a.pause(); a.currentTime = 0; })");
        var trunk = await Eval(page, "() => field('df', '::before')");
        var branch = await Eval(page, $"() => field('b{running}', '::before')");
        Assert.True(Math.Abs(trunk - branch) < 0.6, $"The wire into the fork's light starts at {trunk}, the branch's at {branch}.");
    }

    [Fact]
    public async Task Top_to_bottom_the_rail_after_a_fork_is_the_wire_into_the_step_after_it()
    {
        if (NoBrowser) return;
        // At work: the rail from the last branch's line on to the step moves, as one piece,
        // and the step draws no wire of its own.
        var page = await OpenRun(DownFork(3));
        var rail = await page.EvaluateAsync<double[]>("() => { const b = box('b2', '::after'); return [b.y, b.bottom]; }");
        var curve = await Eval(page, "() => parseFloat(getComputedStyle(document.getElementById('wf')).getPropertyValue('--workflow-curve'))");
        Assert.True(Math.Abs(rail[0] - (await Eval(page, "() => port('bn2')") - curve)) < 0.6, "The rail on does not start where the last branch's curve leaves it.");
        Assert.True(Math.Abs(rail[1] - await Eval(page, "() => box('fz').y")) < 0.6, "The rail on does not reach the step after the fork.");
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('b2'), '::after').animationName"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('fz-step'), '::before').content"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('df'), '::before').animationName"));

        // Next: dashed the whole way, as the wire into a step that is next always is.
        page = await OpenRun(DownFork(4));
        Assert.StartsWith("repeating-linear-gradient", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('b2'), '::after').backgroundImage"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Once_a_branch_has_finished_the_light_starts_where_its_way_leaves_the_finished_one()
    {
        if (NoBrowser) return;
        var page = await OpenRun($"""
            <ol class="workflow" id="wf">
                <li class="workflow-step" data-state="done">{Node("t", "Ticket")}</li>
                <li class="workflow-fork" id="ff">
                    <div class="workflow-branch" id="f0"><ol class="workflow-path"><li class="workflow-step" data-state="done">{Node("f0n", "Done first")}</li></ol></div>
                    <div class="workflow-branch" id="f1"><ol class="workflow-path"><li class="workflow-step" data-state="done">{Node("f1n", "Done too")}</li></ol></div>
                    <div class="workflow-branch" id="f2"><ol class="workflow-path"><li class="workflow-step" data-state="running">{Node("f2n", "At work")}</li></ol></div>
                </li>
                <li class="workflow-step">{Node("u", "Update")}</li>
            </ol>
            <span id="probe"></span>
            """);
        var go = await page.EvaluateAsync<string>("() => colour('--go-solid')");

        // The wire into the fork is the trail and holds still; the way to the branch at
        // work still carries the light, under the finished ways where they share the bracket.
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('ff'), '::before').animationName"));
        Assert.Contains(go, await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('ff'), '::before').backgroundImage"), StringComparison.Ordinal);
        Assert.Equal("workflow-flow", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('f2'), '::after').animationName"));
        var z = await page.EvaluateAsync<int[]>("() => [['f1', '::after'], ['f2', '::after'], ['ff', '::before']].map(([id, p]) => +getComputedStyle(document.getElementById(id), p).zIndex)");
        Assert.True(z[0] > z[1], "The finished way does not lie over the light.");
        Assert.True(z[2] > z[1], "The wire into the fork does not lie over the start of the light's curve.");
    }

    [Fact]
    public async Task A_meta_line_keeps_its_descenders()
    {
        if (NoBrowser) return;
        var page = await OpenRun($"""
            <ol class="workflow workflow--sm" id="wf"><li class="workflow-step">{Node("m", "Meta")}</li></ol>
            <span id="probe"></span>
            """);
        // Clipped across for the ellipsis, never down.
        Assert.Equal("visible", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#m .workflow-node-meta')).overflowY"));
        Assert.Equal("clip", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#m .workflow-node-meta')).overflowX"));
    }

    [Fact]
    public async Task Flow_off_stills_the_light_to_a_lit_line()
    {
        if (NoBrowser) return;
        var page = await OpenRun(Ways);
        const string Play = "(id, p) => getComputedStyle(document.getElementById(id), p).animationPlayState";
        var light = await page.EvaluateAsync<string>("() => colour('--flow-light')");
        Assert.Equal("running", await page.EvaluateAsync<string>($"() => ({Play})('j1-path', '::before')"));

        await page.EvaluateAsync("() => document.getElementById('wf').classList.add('workflow--flow-off')");
        foreach (var (id, pseudo) in new[] { ("j1-path", "::before"), ("sf", "::before"), ("sf2", "::before"), ("after-loop", "::before") })
            Assert.Equal("paused", await page.EvaluateAsync<string>($"() => ({Play})('{id}', '{pseudo}')"));
        // No streak, and the line under it is the light's own colour.
        var paint = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('sf2'), '::before').backgroundImage");
        Assert.DoesNotContain(light + " ", paint.Split("linear-gradient(" + light)[0], StringComparison.Ordinal);
        Assert.EndsWith($"linear-gradient({light}, {light})", paint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Speed_changes_the_pace_and_packets_the_spacing_at_the_same_pace()
    {
        if (NoBrowser) return;
        var page = await OpenRun(Ways);
        const string Wire = """
            () => {
                const s = getComputedStyle(document.getElementById('j1-path'), '::before');
                return [parseFloat(s.animationDuration), parseFloat(s.backgroundSize.split(',')[1])];
            }
            """;
        async Task<double> Pace()
        {
            var w = await page.EvaluateAsync<double[]>(Wire);
            return w[1] / w[0];
        }
        var normal = await Pace();
        Assert.Equal(240, (await page.EvaluateAsync<double[]>(Wire))[1], 1);

        await page.EvaluateAsync("() => document.getElementById('wf').style.setProperty('--workflow-flow-speed', '0.5')");
        Assert.Equal(normal / 2, await Pace(), 1);
        Assert.Equal(240, (await page.EvaluateAsync<double[]>(Wire))[1], 1);

        await page.EvaluateAsync("() => { const wf = document.getElementById('wf'); wf.style.removeProperty('--workflow-flow-speed'); wf.style.setProperty('--workflow-flow-packets', '2'); }");
        Assert.Equal(120, (await page.EvaluateAsync<double[]>(Wire))[1], 1);
        Assert.Equal(normal, await Pace(), 1);

        // The ways keep step with the wires.
        Assert.Equal(
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('after-loop'), '::before').animationDuration"),
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('sf2'), '::before').animationDuration"));
    }

    [Fact]
    public async Task Under_reduced_motion_nothing_moves_and_nothing_is_lost()
    {
        if (NoBrowser) return;
        var page = await OpenRun(motion: ReducedMotion.Reduce);

        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('s-p1b'), '::before').animationName"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b')).animationName"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b'), '::before').animationName"));
        // The way in is still a lit line, with no streak stopped on it, and the edge still lit.
        var light = await page.EvaluateAsync<string>("() => colour('--flow-light')");
        var wire = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('s-p1b'), '::before').backgroundImage");
        Assert.DoesNotContain(light + " ", wire.Split("linear-gradient(" + light)[0], StringComparison.Ordinal);
        Assert.EndsWith($"linear-gradient({light}, {light})", wire, StringComparison.Ordinal);
        Assert.Equal(light, await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('p1b')).borderTopColor"));
    }

    [Fact]
    public async Task Right_to_left_runs_the_diagram_from_the_right_and_turns_every_wire()
    {
        if (NoBrowser) return;
        var page = await OpenRun(rtl: true);

        Assert.True(await Eval(page, "() => box('b').x") < await Eval(page, "() => box('a').x"), "The run does not go right to left.");
        var wire = await page.EvaluateAsync<double[]>("() => { const b = box('s-b', '::before'); return [b.x, b.right]; }");
        Assert.True(Math.Abs(wire[0] - await Eval(page, "() => box('b').right")) < 0.6, "The wire does not meet its node on the node's right.");
        // The light runs towards the inline end: down and to the left, placed from the right.
        Assert.Equal("225deg", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('s-p1b'), '::before').getPropertyValue('--workflow-flow-ahead').trim()"));
        Assert.Equal("right", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('s-p1b'), '::before').getPropertyValue('--workflow-start-side').trim()"));

        // The bracket is mirrored too: half a gap to the right of the branches.
        var gap = await Eval(page, "() => parseFloat(getComputedStyle(document.getElementById('wf')).columnGap)");
        var curve = await Eval(page, "() => parseFloat(getComputedStyle(document.getElementById('wf')).getPropertyValue('--workflow-curve'))");
        var bend = await Eval(page, "() => box('p2', '::before').right");
        var down = await Eval(page, "() => box('p2', '::after').x");
        var fork = await Eval(page, "() => box('par').right");
        Assert.True(Math.Abs(down - fork) < 0.6, "The way into a branch does not meet it on its right.");
        Assert.True(Math.Abs(bend - curve - (fork + gap / 2)) < 0.6, "The bracket is not mirrored.");
    }
}
