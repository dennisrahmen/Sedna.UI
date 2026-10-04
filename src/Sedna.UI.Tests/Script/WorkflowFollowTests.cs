using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-follow</c> on a <c>.workflow</c>: the step at work is kept in the middle of the
/// diagram while the run moves on, until the reader scrolls it out of view.
/// </summary>
public class WorkflowFollowTests : ScriptTestBase
{
    private static string Diagram(int running, string attributes = "data-follow", string after = "") => $"""
        <div style="width: 600px">
            <ol class="workflow" id="wf" {attributes}>
                {string.Concat(Enumerable.Range(0, 10).Select(i => $"""
                    <li class="workflow-step" id="s{i}"{(i < running ? " data-state=\"done\"" : i == running ? " data-state=\"running\"" : "")}>
                        <article class="workflow-node" id="n{i}">
                            <header class="workflow-node-head">
                                <span class="workflow-node-icon"><i class="ri-checkbox-blank-circle-line"></i></span>
                                <span class="workflow-node-title">Step {i}</span>
                                <span class="workflow-node-meta" id="m{i}">meta</span>
                            </header>
                        </article>
                    </li>
                    """))}
            </ol>
        </div>
        {after}
        """;

    /// <summary>How far the step's node is from the middle of the diagram, in pixels.</summary>
    private static string OffCentre(int step) => $$"""
        () => {
            const w = document.getElementById('wf').getBoundingClientRect();
            const n = document.getElementById('n{{step}}').getBoundingClientRect();
            return Math.abs((n.left + n.width / 2) - (w.left + w.width / 2));
        }
        """;

    private static string Centred(int step) => $"() => ({OffCentre(step)})() < 2";

    private const string Paused = "() => document.getElementById('wf').hasAttribute('data-follow-paused')";

    private static string Move(int from, int to) => $$"""
        () => {
            document.getElementById('s{{from}}').dataset.state = 'done';
            document.getElementById('s{{to}}').dataset.state = 'running';
        }
        """;

    private async Task<IPage> OpenDiagram(string html, ReducedMotion motion = ReducedMotion.Reduce)
    {
        var page = await Open(html, head: StylesheetTag, reducedMotion: motion);
        await page.SetViewportSizeAsync(1200, 800);
        return page;
    }

    /// <summary>Scrolls the diagram the way a reader does: a wheel over it.</summary>
    private static async Task Wheel(IPage page, double deltaX)
    {
        var box = await page.Locator("#wf").BoundingBoxAsync();
        await page.Mouse.MoveAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Mouse.WheelAsync((float)deltaX, 0);
    }

    [Fact]
    public async Task A_diagram_opens_on_the_step_at_work()
    {
        if (NoBrowser) return;
        var page = await OpenDiagram(Diagram(running: 6));
        await page.WaitForFunctionAsync(Centred(6));
        Assert.False(await page.EvaluateAsync<bool>(Paused));
    }

    [Fact]
    public async Task A_new_step_at_work_is_brought_to_the_middle()
    {
        if (NoBrowser) return;
        var page = await OpenDiagram(Diagram(running: 3));
        await page.WaitForFunctionAsync(Centred(3));

        await page.EvaluateAsync(Move(3, 7));
        await page.WaitForFunctionAsync(Centred(7));
    }

    [Fact]
    public async Task A_smooth_burst_of_changes_lands_on_the_last_rather_than_past_it()
    {
        if (NoBrowser) return;
        // Smooth scrolling, and five changes inside the time one smooth scroll takes. A
        // scroll BY an amount adds onto one still under way and overshoots by the sum.
        var page = await OpenDiagram(Diagram(running: 1), ReducedMotion.NoPreference);
        await page.WaitForFunctionAsync(Centred(1));

        await page.EvaluateAsync("""
            async () => {
                for (let i = 1; i < 6; i++) {
                    document.getElementById('s' + i).dataset.state = 'done';
                    document.getElementById('s' + (i + 1)).dataset.state = 'running';
                    await new Promise(r => setTimeout(r, 60));
                }
            }
            """);
        await page.WaitForFunctionAsync(Centred(6), null, new() { Timeout = 5000 });
    }

    [Fact]
    public async Task The_reader_scrolling_away_releases_it_and_scrolling_back_takes_it_up_again()
    {
        if (NoBrowser) return;
        var page = await OpenDiagram(Diagram(running: 6));
        await page.WaitForFunctionAsync(Centred(6));
        await page.EvaluateAsync("() => { window.follows = []; document.addEventListener('sedna-follow', e => window.follows.push(e.detail.following)); }");

        await Wheel(page, -2000);
        await page.WaitForFunctionAsync(Paused);
        Assert.Equal(new[] { false }, await page.EvaluateAsync<bool[]>("() => window.follows"));

        // Released: the run moves on and the diagram stays where the reader left it.
        var left = await page.EvaluateAsync<double>("() => document.getElementById('wf').scrollLeft");
        await page.EvaluateAsync(Move(6, 7));
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(left, await page.EvaluateAsync<double>("() => document.getElementById('wf').scrollLeft"));

        // Back into view: following again.
        await Wheel(page, 2000);
        await page.WaitForFunctionAsync("() => !document.getElementById('wf').hasAttribute('data-follow-paused')");
        Assert.Equal(new[] { false, true }, await page.EvaluateAsync<bool[]>("() => window.follows"));
    }

    [Fact]
    public async Task A_scroll_the_reader_did_not_make_never_releases_it()
    {
        if (NoBrowser) return;
        // A render that changes the diagram's width under the scroll position scrolls it,
        // and so does the script itself — neither is the reader looking away.
        var page = await OpenDiagram(Diagram(running: 6));
        await page.WaitForFunctionAsync(Centred(6));

        await page.EvaluateAsync("() => { document.getElementById('wf').scrollLeft = 0; }");
        await page.EvaluateAsync("() => { for (let i = 0; i < 10; i++) document.getElementById('m' + i).textContent = 'a much longer meta line than before'; }");
        await page.WaitForTimeoutAsync(200);
        Assert.False(await page.EvaluateAsync<bool>(Paused));

        await page.EvaluateAsync(Move(6, 8));
        await page.WaitForFunctionAsync(Centred(8));
    }

    [Fact]
    public async Task A_follow_button_brings_the_step_back_and_follows_again()
    {
        if (NoBrowser) return;
        var page = await OpenDiagram(Diagram(running: 6, after:
            """<button class="btn btn-sm" id="back" type="button" data-workflow-follow aria-controls="wf">Back to the step at work</button>"""));
        await page.WaitForFunctionAsync(Centred(6));

        await Wheel(page, -2000);
        await page.WaitForFunctionAsync(Paused);

        await page.Locator("#back").ClickAsync();
        await page.WaitForFunctionAsync(Centred(6));
        Assert.False(await page.EvaluateAsync<bool>(Paused));
    }

    [Fact]
    public async Task Without_data_follow_the_diagram_stays_where_it_is()
    {
        if (NoBrowser) return;
        var page = await OpenDiagram(Diagram(running: 6, attributes: ""));
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(0, await page.EvaluateAsync<double>("() => document.getElementById('wf').scrollLeft"));

        await page.EvaluateAsync(Move(6, 8));
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(0, await page.EvaluateAsync<double>("() => document.getElementById('wf').scrollLeft"));

        // Switched on later, it goes to the step at work.
        await page.EvaluateAsync("() => document.getElementById('wf').setAttribute('data-follow', '')");
        await page.WaitForFunctionAsync(Centred(8));
    }

    [Fact]
    public async Task A_diagram_run_down_in_a_box_of_fixed_height_follows_down()
    {
        if (NoBrowser) return;
        var html = Diagram(running: 3, attributes: "data-follow style=\"max-height: 300px; overflow-y: auto\"")
            .Replace("class=\"workflow\"", "class=\"workflow workflow--down\"", StringComparison.Ordinal);
        var page = await OpenDiagram(html);
        await page.WaitForFunctionAsync(CentredDown(3));

        await page.EvaluateAsync(Move(3, 6));
        await page.WaitForFunctionAsync(CentredDown(6));
    }

    private static string CentredDown(int step) => $$"""
        () => {
            const w = document.getElementById('wf').getBoundingClientRect();
            const n = document.getElementById('n{{step}}').getBoundingClientRect();
            return Math.abs((n.top + n.height / 2) - (w.top + w.height / 2)) < 2;
        }
        """;

    [Fact]
    public async Task The_step_at_work_is_the_last_running_then_waiting_then_the_next()
    {
        if (NoBrowser) return;
        var page = await OpenDiagram(Diagram(running: -1));

        await page.EvaluateAsync("() => document.getElementById('s7').dataset.state = 'next'");
        await page.WaitForFunctionAsync(Centred(7));
        await page.EvaluateAsync("() => document.getElementById('s2').dataset.state = 'waiting'");
        await page.WaitForFunctionAsync(Centred(2));
        await page.EvaluateAsync("() => { document.getElementById('s5').dataset.state = 'running'; document.getElementById('s8').dataset.state = 'running'; }");
        await page.WaitForFunctionAsync(Centred(8));
    }
}
