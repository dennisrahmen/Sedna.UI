using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The Replay button on the lists page plays the arrival of <c>.list--stagger</c> again. A finished
/// row animation has no fill after its end, so it is gone from <c>getAnimations()</c> and cannot
/// be restarted by playing it: the rows have to be taken out of the page and put back.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class ListReplayTests(CatalogueAppFixture app)
{
    [Fact]
    public async Task Replay_plays_the_rows_arriving_again_after_they_have_arrived()
    {
        if (app.NoBrowser) return;
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(app.Url("/list"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });

        var list = page.Locator("#stagger-list");
        await list.ScrollIntoViewIfNeededAsync();
        // The first arrival is over: every row's animation has finished and left the list.
        await page.WaitForFunctionAsync("() => document.getElementById('stagger-list').getAnimations({ subtree: true }).length === 0");

        await page.GetByRole(AriaRole.Button, new() { Name = "Replay" }).ClickAsync();

        // The rows arrive again: a row is held at its start, transparent, until its turn.
        await page.WaitForFunctionAsync("() => document.getElementById('stagger-list').getAnimations({ subtree: true }).length > 0");
        Assert.True(await list.EvaluateAsync<bool>("el => !el.hidden"), "Replay left the list hidden.");
        await context.CloseAsync();
    }
}
