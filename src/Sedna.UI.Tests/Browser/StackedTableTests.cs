using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Below 640px a <c>.table--stack</c> row is a card and each cell a label beside one
/// value. A cell holding several elements is still one value, and a group row is the
/// heading of the cards under it.
/// </summary>
public class StackedTableTests : ScriptTestBase
{
    private const string Table = """
        <table class="table table--stack" id="t">
          <thead><tr><th>Agent</th><th>State</th><th></th></tr></thead>
          <tbody>
            <tr class="tr-group"><th colspan="3" scope="rowgroup" id="group"><div class="tr-group-body">Waiting on you</div></th></tr>
            <tr>
              <td data-label="Agent" id="multi"><a href="#" id="link">build-runner-04</a><div class="text-muted" id="sub">Linux · seen 2m ago</div></td>
              <td data-label="State" id="icon"><i class="ri-checkbox-circle-line" id="i"></i> <span id="word">Online</span></td>
              <td id="actions"><button class="btn btn--sm" type="button" id="open">Open</button></td>
            </tr>
          </tbody>
        </table>
        """;

    [Fact]
    public async Task A_cell_with_several_elements_lays_them_out_as_one_value_at_the_end_edge()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Table);
        await page.SetViewportSizeAsync(375, 700);

        var r = await page.EvaluateAsync<double[]>("""
            () => {
                const box = id => document.getElementById(id).getBoundingClientRect();
                const cell = document.getElementById('multi');
                const pad = parseFloat(getComputedStyle(cell).paddingInlineEnd);
                const end = cell.getBoundingClientRect().right - pad;
                return [box('link').bottom, box('sub').top, end - box('link').right, end - box('sub').right,
                        box('i').top, box('word').top, end - box('word').right];
            }
            """);

        Assert.True(r[1] >= r[0] - 0.5, $"The line under the link sits beside it (link bottom {r[0]}, line top {r[1]}).");
        Assert.True(r[2] < 1 && r[3] < 1, $"The value is not at the end edge (link {r[2]:0.0}px, line {r[3]:0.0}px short).");
        Assert.True(Math.Abs(r[4] - r[5]) < 8, "An icon and its word were split onto lines of their own.");
        Assert.True(r[6] < 1, $"An icon and its word are not at the end edge ({r[6]:0.0}px short).");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_cell_with_no_label_keeps_its_content_at_the_start()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Table);
        await page.SetViewportSizeAsync(375, 700);

        var gap = await page.EvaluateAsync<double>("""
            () => {
                const cell = document.getElementById('actions');
                const start = cell.getBoundingClientRect().left + parseFloat(getComputedStyle(cell).paddingInlineStart);
                return document.getElementById('open').getBoundingClientRect().left - start;
            }
            """);
        Assert.True(Math.Abs(gap) < 1, $"The actions cell's button is {gap:0.0}px from its start.");
    }

    [Fact]
    public async Task A_group_row_spans_the_table_on_a_phone()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Table);
        await page.SetViewportSizeAsync(375, 700);

        var widths = await page.EvaluateAsync<double[]>(
            "() => [document.getElementById('group').getBoundingClientRect().width, document.getElementById('t').getBoundingClientRect().width]");
        Assert.True(widths[0] >= widths[1] - 1, $"The group row is {widths[0]:0}px wide in a {widths[1]:0}px table.");
    }

    [Fact]
    public async Task A_value_that_cannot_wrap_in_a_wide_cell_wraps_in_a_card()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width: 341px" id="box">
              <table class="table table--stack" id="t">
                <thead><tr><th>Customer</th><th>Address</th><th>Site</th></tr></thead>
                <tbody><tr>
                  <td data-label="Customer">Northwind Retail</td>
                  <td data-label="Address" class="col-muted">Obere Hauptstrasse 114, 3rd floor, rear building, 12345 Example Town</td>
                  <td data-label="Site" class="col-fit">https://www.example.com/a/very/long/path/that/never/ends</td>
                </tr></tbody>
              </table>
            </div>
            """);
        await page.SetViewportSizeAsync(375, 700);

        var widths = await page.EvaluateAsync<double[]>(
            "() => [document.getElementById('t').getBoundingClientRect().width, document.getElementById('box').getBoundingClientRect().width]");
        Assert.True(widths[0] <= widths[1] + 0.5, $"The stacked table is {widths[0]:0}px wide in a {widths[1]:0}px column.");
    }
}
