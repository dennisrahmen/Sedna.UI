using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Editing a calendar end to end, through a real Blazor circuit: a drop on a day or a time
/// reaches <c>@onsedna-drop</c> with <c>FromSlot</c>, a grip's <c>@onsedna-resize</c> carries
/// its key, and the component's own render puts the event where the reader put it.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class CalendarEditTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open()
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(app.Url("/calendar-agenda"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    private static async Task<(float X, float Y)> Centre(ILocator locator, float fx = 0.5f)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var box = await locator.BoundingBoxAsync();
        Assert.NotNull(box);
        return (box!.X + box.Width * fx, box.Y + box.Height / 2);
    }

    [Fact]
    public async Task A_pill_dragged_to_another_day_is_rendered_on_that_day()
    {
        if (app.NoBrowser) return;
        var page = await Open();
        var pill = page.Locator(".cal-month--grow [data-drag-item=e1]");
        Assert.Contains("grid-column: 1 / span 1", await pill.GetAttributeAsync("style") ?? "", StringComparison.Ordinal);

        var from = await Centre(pill);
        var to = await Centre(page.Locator(".cal-day[data-drag-zone='2026-09-17']"));
        await page.Mouse.MoveAsync(from.X, from.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(to.X, to.Y, new() { Steps = 12 });
        await page.Mouse.UpAsync();

        // Monday the 14th to Thursday the 17th: the fourth column.
        await page.WaitForFunctionAsync(
            "() => /grid-column: 4 \\/ span 1/.test(document.querySelector('.cal-month--grow [data-drag-item=e1]').getAttribute('style'))");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_block_moved_from_the_keyboard_and_stretched_by_its_grip_is_rendered_there()
    {
        if (app.NoBrowser) return;
        var page = await Open();

        // Dispatch, Tuesday 09:00: an hour later and a day on.
        await page.Locator(".cal-week [data-drag-item=b3]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("Space");

        await page.Locator("[data-drag-zone='2026-09-16'] > [data-drag-item=b3]").WaitForAsync();
        Assert.Contains("--cal-start: 3;", await page.Locator("[data-drag-item=b3]").GetAttributeAsync("style") ?? "", StringComparison.Ordinal);

        // Its end, half an hour later: the render gives it the new span and the grip's
        // pixel height goes with the old style.
        await page.Locator("[data-drag-item=b3] .cal-resize").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await page.WaitForFunctionAsync(
            "() => { const s = document.querySelector('[data-drag-item=b3]').getAttribute('style'); return s.includes('--cal-span: 1.5') && !s.includes('--cal-height'); }");
        Assert.Equal("Ends 11:30", await page.Locator("[data-drag-item=b3] .cal-resize").GetAttributeAsync("aria-valuetext"));
        await page.Context.CloseAsync();
    }
}
