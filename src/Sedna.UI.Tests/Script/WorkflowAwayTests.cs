using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The parts of a <c>.workflow</c> well out of view are not rendered, so a run of hundreds
/// of steps costs what a short one does while its light moves.
/// </summary>
public class WorkflowAwayTests : ScriptTestBase
{
    private static string Long(int steps) => $"""
        <ol class="workflow" id="wf" style="width: 600px" tabindex="0">
            {string.Concat(Enumerable.Range(0, steps).Select(i => $"""<li class="workflow-step" id="s{i}"{(i == 0 ? " data-state=\"running\"" : "")}><article class="workflow-node"><header class="workflow-node-head"><span class="workflow-node-icon"><i class="ri-checkbox-blank-circle-line"></i></span><span class="workflow-node-title">Step {i}</span><span class="workflow-node-meta">meta</span></header></article></li>"""))}
        </ol>
        """;

    private static string Away(int step) => $"() => document.getElementById('s{step}').hasAttribute('data-workflow-away')";

    private static string Visibility(int step) => $"() => getComputedStyle(document.getElementById('s{step}')).contentVisibility";

    private async Task<IPage> OpenLong(int steps)
    {
        var page = await Open(Long(steps), head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);
        await page.SetViewportSizeAsync(1200, 800);
        return page;
    }

    [Fact]
    public async Task A_part_far_out_of_view_is_not_rendered_and_one_in_view_is_never_contained()
    {
        if (NoBrowser) return;
        var page = await OpenLong(40);
        await page.WaitForFunctionAsync(Away(39));

        // In view: drawn as written, with no containment, so a menu opened from a node
        // is not clipped to it.
        Assert.False(await page.EvaluateAsync<bool>(Away(0)));
        Assert.Equal("visible", await page.EvaluateAsync<string>(Visibility(0)));
        // Far out: skipped by the browser, at the size it had, so nothing around it moves.
        Assert.Equal("auto", await page.EvaluateAsync<string>(Visibility(39)));
        Assert.Equal(
            await page.EvaluateAsync<double>("() => document.getElementById('s0').getBoundingClientRect().width"),
            await page.EvaluateAsync<double>("() => document.getElementById('s39').getBoundingClientRect().width"));
        // The edge is never away: what is drawn reaches half the view past it.
        Assert.False(await page.EvaluateAsync<bool>(Away(3)));
    }

    [Fact]
    public async Task Scrolling_the_diagram_brings_the_far_end_back_and_lets_the_start_go()
    {
        if (NoBrowser) return;
        var page = await OpenLong(40);
        await page.WaitForFunctionAsync(Away(39));

        await page.EvaluateAsync("() => { const wf = document.getElementById('wf'); wf.scrollLeft = wf.scrollWidth; }");
        await page.WaitForFunctionAsync($"() => !({Away(39)})() && ({Away(0)})()");
    }

    [Fact]
    public async Task A_part_added_out_of_view_is_marked_and_a_page_scrolled_away_lets_all_go()
    {
        if (NoBrowser) return;
        var page = await OpenLong(10);

        await page.EvaluateAsync("""
            () => {
                const wf = document.getElementById('wf');
                for (let i = 10; i < 40; i++) {
                    const li = document.getElementById('s1').cloneNode(true);
                    li.id = 's' + i;
                    wf.append(li);
                }
            }
            """);
        await page.WaitForFunctionAsync(Away(39));

        // The page scrolled far past the diagram: every part goes, and comes back with it.
        await page.EvaluateAsync("() => { document.body.style.paddingBottom = '6000px'; window.scrollTo(0, 5000); }");
        await page.WaitForFunctionAsync(Away(0));
        await page.EvaluateAsync("() => window.scrollTo(0, 0)");
        await page.WaitForFunctionAsync($"() => !({Away(0)})()");
    }
}
