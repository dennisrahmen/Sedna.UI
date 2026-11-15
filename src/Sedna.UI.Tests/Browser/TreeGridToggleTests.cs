using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The toggle of a tree grid row is a slot of its own: its caret or its spinner is centred on the
/// capitals of the label beside it, the slot is as wide for one as for the other, and the label
/// stands the same distance from it on every row.
/// </summary>
/// <remarks>
/// The caret used to sit on the text's baseline in an em square lowered by a guess, so it stood
/// below the capitals — more so further down the table — and the spinner that replaced it was wider
/// than the slot, closing the gap to the label on a row that was loading.
/// </remarks>
public class TreeGridToggleTests : ScriptTestBase
{
    private const double Tolerance = 1.0 + 1.0 / 16;

    private const string Table = """
        <div style="padding:24px; width:480px">
          <table class="table" role="treegrid" aria-label="Cost by team">
            <thead><tr><th>Team</th><th class="col-num">Cost</th></tr></thead>
            <tbody>
              <tr aria-level="1" aria-expanded="true" data-value="a"><td id="a"><span class="tree-toggle"></span>OPERATIONS 4</td><td class="col-num">412</td></tr>
              <tr aria-level="2" aria-expanded="false" data-value="b"><td id="b"><span class="tree-toggle"></span>NETWORK 18</td><td class="col-num">96</td></tr>
              <tr aria-level="2" aria-expanded="false" aria-busy="true" data-tree-lazy data-value="c"><td id="c"><span class="tree-toggle"></span>FINANCE 12</td><td class="col-num">88</td></tr>
              <tr aria-level="2" data-value="d"><td id="d"><span class="tree-toggle"></span>LEGAL 3</td><td class="col-num">42</td></tr>
              <tr aria-level="1" aria-expanded="false" data-value="e"><td id="e"><span class="tree-toggle"></span>SUPPORT 46</td><td class="col-num">31</td></tr>
            </tbody>
          </table>
        </div>
        """;

    [Theory]
    [InlineData("a", true)]
    [InlineData("b", true)]
    [InlineData("c", true)]
    [InlineData("e", true)]
    [InlineData("a", false)]
    public async Task The_caret_and_the_spinner_stand_on_the_labels_capitals_in_a_slot_of_one_width(string row, bool _)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Table, deviceScaleFactor: Ink.Scale);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);

        // The words alone, with the toggle hidden: inside the cell, clear of its borders.
        await page.EvaluateAsync("() => document.querySelectorAll('.tree-toggle').forEach(t => t.style.visibility = 'hidden')");
        var words = (await Ink.Extent(page, 3, $"#{row}"))[0];
        await page.EvaluateAsync("() => document.querySelectorAll('.tree-toggle').forEach(t => t.style.visibility = '')");

        var slot = await page.EvaluateAsync<double[]>($$"""
            () => { const t = document.querySelector('#{{row}} > .tree-toggle').getBoundingClientRect(); return [t.left, t.top, t.width, t.height]; }
            """);
        // The slot, widened a few pixels each way, holds the caret or the spinner and nothing else.
        var glyph = (await Ink.Extent(page, -3, $"#{row} > .tree-toggle"))[0];
        var delta = (glyph[1] + glyph[3]) / 2 - (words[1] + words[3]) / 2;

        Assert.True(Math.Abs(delta) <= Tolerance,
            $"Row {row}: the toggle is drawn {delta:+0.00;-0.00}px from the middle of the label's capitals.");
        // Inside the slot, whichever it is.
        Assert.True(glyph[0] >= slot[0] - 0.5 && glyph[2] <= slot[0] + slot[2] + 0.5,
            $"Row {row}: what the toggle draws spans {glyph[0]:0.0}–{glyph[2]:0.0} and its slot {slot[0]:0.0}–{slot[0] + slot[2]:0.0}.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_label_stands_a_gap_from_the_slot_on_every_row_whatever_the_slot_holds()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Table);
        await Js.Settle(page);

        var gaps = await page.EvaluateAsync<double[][]>("""
            () => ['a', 'b', 'c', 'd', 'e'].map(id => {
                const td = document.getElementById(id), t = td.querySelector('.tree-toggle').getBoundingClientRect();
                const range = document.createRange(); range.selectNodeContents(td.lastChild);
                const text = range.getBoundingClientRect();
                const probe = document.createElement('i'); probe.style.cssText = 'display:block;width:var(--space-3)'; document.body.append(probe);
                const gap = probe.getBoundingClientRect().width; probe.remove();
                return [text.left - t.right, gap, t.width];
            })
            """);
        foreach (var g in gaps)
        {
            Assert.Equal(g[1], g[0], 0.05);          // the library's gap between an icon and its words
            Assert.Equal(gaps[0][2], g[2], 0.05);    // one slot, on every row
        }
        Assert.Empty(errors);
    }
}
