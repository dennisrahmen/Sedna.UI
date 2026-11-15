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

    // Every chip is the same gesture and must feel the same: the goals' heading lands at one offset
    // whatever the area's length, and the page never goes via the top on the way. A short area could
    // not scroll its heading up, and the layout reset the scroll before the router moved to the
    // fragment, so a chip showed the principles for a frame.
    [Fact]
    public async Task Every_area_lands_the_goals_heading_at_the_same_offset_without_visiting_the_top()
    {
        if (app.NoBrowser) return;

        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 1100 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(app.Url("/design-guide"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });

        var areas = page.Locator("nav[aria-label='Goal areas'] a.chip");
        var names = await areas.EvaluateAllAsync<string[]>("els => els.map(e => e.textContent.split(' \u00b7 ')[0].trim())");
        Assert.True(names.Length > 3, "Too few areas to compare.");

        // Samples the page's scroll on every frame, from the first click on.
        await page.EvaluateAsync("""
            () => {
                const p = document.getElementById('goals').closest('.page');
                window.__tops = [];
                (function tick() { window.__tops.push(p.scrollTop); requestAnimationFrame(tick); })();
            }
            """);

        // Starts scrolled, as a reader who has been reading the principles is.
        await page.EvaluateAsync("() => document.getElementById('goals').scrollIntoView()");

        var offsets = new List<(string Area, double Offset)>();
        foreach (var name in names)
        {
            await page.EvaluateAsync("() => { window.__tops.length = 0; }");
            var chip = areas.Filter(new() { HasTextString = name + " \u00b7" });
            await chip.ClickAsync();
            await Assertions.Expect(chip).ToHaveAttributeAsync("aria-current", "page");
            await page.WaitForTimeoutAsync(400);

            var tops = await page.EvaluateAsync<double[]>("() => window.__tops");
            Assert.True(tops.All(t => t > 0), $"{name}: the page was at its top for a frame on the way to the goals.");

            offsets.Add((name, await page.EvaluateAsync<double>(
                "() => document.getElementById('goals').getBoundingClientRect().top - document.getElementById('goals').closest('.page').getBoundingClientRect().top")));
        }

        var reference = offsets[0].Offset;
        foreach (var (area, offset) in offsets)
            Assert.True(Math.Abs(offset - reference) <= 1, $"{area}: the goals heading is {offset}px from the top, {reference}px for {offsets[0].Area}.");

        await context.CloseAsync();
    }
}
