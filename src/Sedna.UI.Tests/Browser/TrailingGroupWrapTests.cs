using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A trailing group — <c>.toolbar-actions</c>, <c>.page-head-actions</c> — stays at the end
/// edge on a line of its own after its row wraps.
/// </summary>
public class TrailingGroupWrapTests : ScriptTestBase
{
    [Fact]
    public async Task A_page_heads_actions_keep_to_the_end_edge_when_they_wrap()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="page-head" id="head" style="width:360px">
              <div class="page-head-text" id="text"><h1>Approval queue</h1></div>
              <div class="page-head-actions" id="actions"><button class="btn" type="button">Refresh</button></div>
            </div>
            """);

        var r = await page.EvaluateAsync<double[]>("""
            () => {
                const box = id => document.getElementById(id).getBoundingClientRect();
                return [box('actions').top - box('text').bottom, box('head').right - box('actions').right];
            }
            """);
        Assert.True(r[0] >= 0, "The fixture did not wrap.");
        Assert.True(r[1] < 1, $"The wrapped actions stop {r[1]:0.0}px short of the end edge.");
    }

    [Fact]
    public async Task The_trailing_group_keeps_to_the_end_edge_when_the_toolbar_wraps()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="toolbar" id="bar" style="width:360px">
              <div class="toolbar-filters" id="filters">
                <input class="form-input form-input--sm" style="width:300px" aria-label="Search">
              </div>
              <div class="toolbar-actions" id="actions"><span>12 shown</span></div>
            </div>
            """);

        var r = await page.EvaluateAsync<double[]>("""
            () => {
                const box = id => document.getElementById(id).getBoundingClientRect();
                return [box('actions').top - box('filters').bottom, box('bar').right - box('actions').right];
            }
            """);
        Assert.True(r[0] >= 0, "The fixture did not wrap.");
        Assert.True(r[1] < 1, $"The wrapped trailing group stops {r[1]:0.0}px short of the end edge.");
    }

    [Fact]
    public async Task On_one_line_the_two_groups_sit_at_either_end_as_before()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="toolbar" id="bar" style="width:800px">
              <div class="toolbar-filters" id="filters"><input class="form-input form-input--sm" aria-label="Search"></div>
              <div class="toolbar-actions" id="actions"><span>12 shown</span></div>
            </div>
            """);

        var r = await page.EvaluateAsync<double[]>("""
            () => {
                const box = id => document.getElementById(id).getBoundingClientRect();
                return [box('filters').left - box('bar').left, box('bar').right - box('actions').right, box('actions').top - box('filters').top];
            }
            """);
        Assert.True(r[0] < 1 && r[1] < 1 && Math.Abs(r[2]) < 8, $"start {r[0]:0.0}, end {r[1]:0.0}, line {r[2]:0.0}");
    }
}
