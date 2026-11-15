using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The rows of a tree keep a gap, so a picked row and the one the pointer is on never touch: between
/// siblings, and between a branch's row and its first child.
/// </summary>
public class TreeRowGapTests : ScriptTestBase
{
    private const string Fixture = """
        <ul class="tree" role="tree" aria-label="Places" style="width:300px">
          <li role="treeitem" aria-selected="true" id="a" tabindex="0"><span class="tree-row"><span class="tree-toggle"></span><span class="tree-label">Aisle 1</span></span></li>
          <li role="treeitem" aria-selected="false" id="b" aria-expanded="true"><span class="tree-row"><span class="tree-toggle"></span><span class="tree-label">Aisle 2</span></span>
            <ul role="group"><li role="treeitem" id="c"><span class="tree-row"><span class="tree-toggle"></span><span class="tree-label">Bay 1</span></span></li>
              <li role="treeitem" id="d"><span class="tree-row"><span class="tree-toggle"></span><span class="tree-label">Bay 2</span></span></li></ul></li>
        </ul>
        """;

    [Fact]
    public async Task Rows_are_a_step_apart_between_siblings_and_down_into_a_branch()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        var gaps = await page.EvaluateAsync<double[]>("""
            () => {
                const row = id => document.querySelector('#' + id + ' > .tree-row').getBoundingClientRect();
                return [row('b').top - row('a').bottom, row('c').top - row('b').bottom, row('d').top - row('c').bottom];
            }
            """);
        Assert.All(gaps, g => Assert.True(g >= 2, $"Two rows are {g:0.##}px apart; the gap a menu keeps is 2px."));
        Assert.Empty(errors);
    }
}
