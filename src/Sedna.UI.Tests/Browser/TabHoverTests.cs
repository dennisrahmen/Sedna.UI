using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A tab under the pointer recolours its label and draws a hairline where the indicator goes. It
/// draws no fill, and the selected tab keeps its indicator.
/// </summary>
public class TabHoverTests : ScriptTestBase
{
    private const string Markup = """
        <div class="tabs" role="tablist">
          <button class="tab" role="tab" aria-selected="true" id="on" type="button">All</button>
          <button class="tab" role="tab" aria-selected="false" id="off" type="button">Unread</button>
          <button class="tab" role="tab" aria-selected="false" id="gone" type="button" disabled>Archived</button>
        </div>
        """;

    private static Task<string[]> Look(IPage page, string id) => page.EvaluateAsync<string[]>($$"""
        () => {
            const cs = getComputedStyle(document.getElementById('{{id}}'));
            return [cs.backgroundColor, cs.borderBottomColor, cs.color];
        }
        """);

    [Fact]
    public async Task Hover_draws_no_fill_and_a_hairline_under_the_tab()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Markup);
        var idle = await Look(page, "off");
        await page.Locator("#off").HoverAsync();
        await Js.Settle(page);
        var hover = await Look(page, "off");

        Assert.Equal("rgba(0, 0, 0, 0)", hover[0]);
        Assert.Equal("rgba(0, 0, 0, 0)", idle[1]);
        Assert.NotEqual("rgba(0, 0, 0, 0)", hover[1]);
        Assert.NotEqual(idle[2], hover[2]);
    }

    [Fact]
    public async Task The_selected_tab_keeps_its_indicator_under_the_pointer()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Markup);
        var idle = await Look(page, "on");
        await page.Locator("#on").HoverAsync();
        await Js.Settle(page);
        Assert.Equal(idle, await Look(page, "on"));
    }

    [Fact]
    public async Task A_disabled_tab_shows_no_hairline()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Markup);
        await page.Locator("#gone").HoverAsync(new() { Force = true });
        await Js.Settle(page);
        var hover = await Look(page, "gone");
        Assert.Equal("rgba(0, 0, 0, 0)", hover[0]);
        Assert.Equal("rgba(0, 0, 0, 0)", hover[1]);
    }
}
