using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.col-fill</c> on a <c>.table--fixed</c>: the column takes the room the others leave in place
/// of the empty end cell, the others keep the width they state, and once they outgrow the box the
/// fill column holds its <c>--col-min</c> and the box scrolls.
/// </summary>
public class TableFillColumnTests : ScriptTestBase
{
    private static string Table(int columns) =>
        $"""
        <div class="sedna-scroll-x" style="width: 700px" id="box">
          <table class="table table--fixed" style="--col-width: 120px; --col-min: 80px" id="t">
            <thead><tr>{string.Concat(Enumerable.Range(0, columns).Select(i => $"<th>Column {i}</th>"))}<th class="col-fill" id="fill">Notes</th></tr></thead>
            <tbody><tr>{string.Concat(Enumerable.Range(0, columns).Select(i => $"<td>Value {i} that is longer than its column</td>"))}<td>A note from the start list that runs on far longer than any box is wide, and on, and on</td></tr></tbody>
          </table>
        </div>
        """;

    private const string Measure = """
        () => {
          const ths = [...document.querySelectorAll('#t thead th')].map(th => th.getBoundingClientRect().width);
          const end = getComputedStyle(document.querySelector('#t tbody tr'), '::after').content;
          const box = document.getElementById('box');
          return { cols: ths, table: document.getElementById('t').getBoundingClientRect().width, box: box.clientWidth, scrolls: box.scrollWidth > box.clientWidth, end };
        }
        """;

    private sealed class Report
    {
        public double[] Cols { get; set; } = [];
        public double Table { get; set; }
        public double Box { get; set; }
        public bool Scrolls { get; set; }
        public string End { get; set; } = "";
    }

    [Fact]
    public async Task In_a_wide_box_the_fill_column_takes_the_rest()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Table(3));
        var r = await page.EvaluateAsync<Report>(Measure);
        Assert.All(r.Cols[..^1], w => Assert.Equal(120, w, 1));
        Assert.Equal(r.Box - 3 * 120, r.Cols[^1], 1.5);
        Assert.Equal(r.Box, r.Table, 1);
        Assert.False(r.Scrolls, "The box scrolls although the columns fit.");
        Assert.True(r.End is "none" or "normal", $"The end cell is still drawn: {r.End}.");
    }

    [Fact]
    public async Task Once_the_columns_outgrow_the_box_the_fill_column_holds_its_minimum_and_the_box_scrolls()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Table(7));
        var r = await page.EvaluateAsync<Report>(Measure);
        Assert.All(r.Cols[..^1], w => Assert.Equal(120, w, 1));
        Assert.Equal(80, r.Cols[^1], 1);
        Assert.True(r.Scrolls, "The columns outgrew the box and it does not scroll.");
    }

    [Fact]
    public async Task A_column_width_set_by_the_grip_moves_only_that_column()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Table(3));
        await Js.Run(page, "() => document.querySelector('#t thead th').style.setProperty('--col-width', '200px')");
        var r = await page.EvaluateAsync<Report>(Measure);
        Assert.Equal(200, r.Cols[0], 1);
        Assert.Equal(120, r.Cols[1], 1);
        Assert.Equal(r.Box - 200 - 2 * 120, r.Cols[^1], 1.5);
    }
}
