using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A checkbox in a table cell stands on the middle line of its row, beside the words of the cells
/// next to it — measured on the paint.
/// </summary>
/// <remarks>
/// A <c>.form-check</c> is an inline-flex box: in a cell it sat on the text baseline with the
/// strut's descent under it, a few pixels above the middle the row's words are set on.
/// </remarks>
public class TableCheckAlignTests : ScriptTestBase
{
    private const double Tolerance = 1.0 + 1.0 / 16;

    public static TheoryData<string, string, string> Cases() => new()
    {
        { "in a fitted cell", "table", """<td class="col-fit"><label class="form-check"><input type="checkbox" id="c" aria-label="Gift wrap"></label></td>""" },
        { "in a numeric cell", "table", """<td class="col-num"><label class="form-check"><input type="checkbox" id="c" aria-label="Gift wrap"></label></td>""" },
        { "with its label", "table", """<td><label class="form-check"><input type="checkbox" id="c"> <span>Gift wrap</span></label></td>""" },
        { "in a keyboard grid", "table\" role=\"grid\" aria-label=\"Lines", """<td class="col-fit"><label class="form-check"><input type="checkbox" id="c" aria-label="Gift wrap"></label></td>""" },
        { "in a sticky-header table", "table table--sticky", """<td class="col-fit"><label class="form-check"><input type="checkbox" id="c" aria-label="Gift wrap"></label></td>""" },
        { "in a denser table", "table table--dense", """<td class="col-fit"><label class="form-check"><input type="checkbox" id="c" aria-label="Gift wrap"></label></td>""" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_checkbox_stands_on_the_line_the_words_beside_it_are_set_on(string name, string classes, string cell)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div style="padding:24px; width:480px">
              <table class="{classes}">
                <thead><tr><th>Order</th><th>Gift</th></tr></thead>
                <tbody><tr><td id="t">ORD 4187</td>{cell}</tr></tbody>
              </table>
            </div>
            """, deviceScaleFactor: Ink.Scale);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);

        // Taken in from the cell's edge: its own bottom line is not its words.
        var words = (await Ink.Extent(page, 3, "#t"))[0];
        var text = (words[1] + words[3]) / 2;
        var box = await page.EvaluateAsync<double>("() => { const r = document.getElementById('c').getBoundingClientRect(); return (r.top + r.bottom) / 2; }");

        Assert.True(Math.Abs(box - text) <= Tolerance,
            $"{name}: the checkbox is centred {box - text:+0.00;-0.00}px from the middle of the words in its row.");
        Assert.Empty(errors);
    }
}
