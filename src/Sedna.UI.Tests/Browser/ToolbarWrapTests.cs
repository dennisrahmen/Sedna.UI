using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.toolbar-actions</c> is the trailing group: on a line of its own after the toolbar
/// wraps, it stays at the end edge.
/// </summary>
public class ToolbarWrapTests : ScriptTestBase
{
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
