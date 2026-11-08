using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>.kv</c> row in a narrow box wraps its value and keeps its key's words whole;
/// <c>.kv--text</c> sets a prose value in the body font.
/// </summary>
public class KeyValueTests : ScriptTestBase
{
    [Fact]
    public async Task A_narrow_row_keeps_a_short_key_on_one_line()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:180px">
              <div class="kv"><span class="k" id="k">Checksum</span><span class="v">9f86d081884c7d659a2feaa0c55ad015</span></div>
            </div>
            """);

        // The key's text, not its box: a flex item stretches to the height of the value beside it.
        var lines = await page.EvaluateAsync<double>(
            "() => { const k = document.getElementById('k'); const r = document.createRange(); r.selectNodeContents(k); return r.getBoundingClientRect().height / parseFloat(getComputedStyle(k).lineHeight); }");
        Assert.True(lines < 1.5, $"The key broke onto {lines:0.0} lines.");
    }

    [Fact]
    public async Task A_text_value_is_set_in_the_body_font_and_breaks_between_words()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:200px">
              <div class="kv kv--text"><span class="k">Role</span><span class="v" id="v">Administrator of every region</span></div>
            </div>
            <p id="body">x</p>
            """);

        var fonts = await page.EvaluateAsync<string[]>(
            "() => [getComputedStyle(document.getElementById('v')).fontFamily, getComputedStyle(document.getElementById('body')).fontFamily, getComputedStyle(document.getElementById('v')).overflowWrap]");
        Assert.Equal(fonts[1], fonts[0]);
        Assert.Equal("break-word", fonts[2]);
    }
}
