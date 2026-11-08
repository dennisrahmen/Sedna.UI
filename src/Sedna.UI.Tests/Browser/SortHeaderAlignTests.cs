using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A sortable header stands over its column: in a <c>.col-num</c> column the label ends
/// where the digits end.
/// </summary>
public class SortHeaderAlignTests : ScriptTestBase
{
    [Theory]
    [InlineData("none")]
    [InlineData("ascending")]
    public async Task A_sortable_numeric_header_ends_its_label_over_the_digits(string sort)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <table class="table" style="width:400px">
              <thead><tr><th>Host</th><th class="col-num" aria-sort="{sort}"><button class="th-sort" type="button"><span id="label">Tasks</span></button></th></tr></thead>
              <tbody><tr><td>src-db-14</td><td class="col-num"><span id="digits">1,204</span></td></tr></tbody>
            </table>
            """);

        var offset = await page.EvaluateAsync<double>(
            "() => document.getElementById('digits').getBoundingClientRect().right - document.getElementById('label').getBoundingClientRect().right");
        Assert.True(Math.Abs(offset) < 1, $"The header's label ends {offset:0.0}px away from the column's digits.");
    }
}
