using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>--row-hover-accent</c> draws a strip on every clickable row under the pointer, a
/// selected row keeps its own bar, and nothing is drawn while the token is transparent.
/// </summary>
public class RowHoverAccentTests : ScriptTestBase
{
    private const string Rows = """
        <div id="wrap" style="width:480px">
          <table class="table"><tbody>
            <tr class="tr-clickable" id="row"><td>ORD-4182</td><td>Shipped</td></tr>
            <tr class="tr-clickable" id="sel" aria-selected="true"><td>ORD-4183</td><td>Open</td></tr>
          </tbody></table>
          <ul class="list"><li><a class="list-row" href="#" id="link"><span class="list-main"><span class="list-title">Invoices</span></span></a></li></ul>
        </div>
        """;

    private static Task<string> Strip(Microsoft.Playwright.IPage page, string cell) =>
        page.EvaluateAsync<string>("c => { const s = getComputedStyle(document.querySelector(c), '::after'); return s.content + '|' + s.backgroundColor + '|' + s.width; }", cell);

    [Theory]
    [InlineData("#row", "#row > td:first-child")]
    [InlineData("#link", "#link")]
    public async Task A_clickable_row_under_the_pointer_shows_the_strip(string hover, string cell)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Rows);
        await page.HoverAsync(hover);
        Assert.EndsWith("|rgba(0, 0, 0, 0)|3px", await Strip(page, cell), StringComparison.Ordinal);

        await page.EvaluateAsync("() => document.getElementById('wrap').style.setProperty('--row-hover-accent', 'rgb(255, 0, 0)')");
        Assert.EndsWith("|rgb(255, 0, 0)|3px", await Strip(page, cell), StringComparison.Ordinal);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_selected_row_keeps_its_own_bar()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Rows);
        await page.EvaluateAsync("() => document.getElementById('wrap').style.setProperty('--row-hover-accent', 'rgb(255, 0, 0)')");
        await page.HoverAsync("#sel");
        Assert.StartsWith("none", await Strip(page, "#sel > td:first-child"), StringComparison.Ordinal);
    }
}
