using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The design guide's areas are links to addresses of their own. Following one shows that area's
/// goals and leaves the reader at them, wherever they had scrolled to, rather than back at the top
/// of the page above the principles.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class GuideAreaTests(CatalogueAppFixture app)
{
    [Fact]
    public async Task Choosing_an_area_shows_its_goals_and_keeps_the_reader_at_them()
    {
        if (app.NoBrowser) return;

        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(app.Url("/design-guide"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });

        var areas = page.Locator("nav[aria-label='Goal areas'] a.chip");
        await areas.Filter(new() { HasTextString = "Forms" }).ScrollIntoViewIfNeededAsync();
        await areas.Filter(new() { HasTextString = "Forms" }).ClickAsync();

        await Assertions.Expect(areas.Filter(new() { HasTextString = "Forms" }))
            .ToHaveAttributeAsync("aria-current", "page");
        await Assertions.Expect(page.Locator("#fill-in-a-form")).ToBeVisibleAsync();

        // The goals' heading is in view, not scrolled away to the top of the page.
        var top = await page.EvaluateAsync<double>("() => document.getElementById('goals').getBoundingClientRect().top");
        Assert.InRange(top, 0, 900);
        Assert.True(await page.EvaluateAsync<double>("() => document.querySelector('.page').scrollTop") > 0,
            "Choosing an area scrolled the page back to its top.");

        await context.CloseAsync();
    }
}
