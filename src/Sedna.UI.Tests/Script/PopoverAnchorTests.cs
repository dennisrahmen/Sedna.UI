using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-popover</c>: a popover opened from an element that cannot be a
/// <c>popovertarget</c> button is anchored to it, opens and closes from it, and keeps the
/// link around it from following.
/// </summary>
public class PopoverAnchorTests : ScriptTestBase
{
    private const string Markup = """
        <div style="padding:200px 300px">
          <a href="#elsewhere" id="link">Outbox <span id="count" class="nav-count" data-popover="detail" role="button" tabindex="0">5</span></a>
        </div>
        <div class="popover" id="detail" popover><p>3 invoices and 2 delivery notes.</p></div>
        """;

    private static Task<double[]> Geometry(Microsoft.Playwright.IPage page) =>
        page.EvaluateAsync<double[]>("""
            () => {
              const t = document.getElementById('count').getBoundingClientRect();
              const p = document.getElementById('detail').getBoundingClientRect();
              return [p.top - t.bottom, p.left - t.left, document.getElementById('detail').matches(':popover-open') ? 1 : 0];
            }
            """);

    [Fact]
    public async Task A_click_opens_it_beside_its_trigger_and_a_second_click_closes_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Markup);

        await page.ClickAsync("#count");
        var open = await Geometry(page);
        Assert.Equal(1, open[2]);
        Assert.InRange(open[0], 0, 12);     // under the count, by the panel's own gap
        Assert.InRange(open[1], -1, 1);     // and starting at its inline edge
        await Assertions.Expect(page.Locator("#count")).ToHaveAttributeAsync("aria-expanded", "true");
        Assert.DoesNotContain("#elsewhere", page.Url, StringComparison.Ordinal);

        await page.ClickAsync("#count");
        Assert.Equal(0, (await Geometry(page))[2]);
        await Assertions.Expect(page.Locator("#count")).ToHaveAttributeAsync("aria-expanded", "false");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Enter_opens_it_from_the_keyboard()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Markup);

        await page.FocusAsync("#count");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(1, (await Geometry(page))[2]);
        Assert.DoesNotContain("#elsewhere", page.Url, StringComparison.Ordinal);
        Assert.Empty(errors);
    }
}
