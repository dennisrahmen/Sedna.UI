using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// "Replay" on the arriving-rows example plays the arrival again, after it has finished.
/// </summary>
/// <remarks>
/// The button used to ask the table for its animations and restart each. An arrival that has run its
/// course is not on the table any more — its fill is <c>backwards</c>, so nothing holds it once it is
/// over — and the loop had nothing to restart.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class ReplayTests(CatalogueAppFixture app)
{
    [Fact]
    public async Task Replay_on_the_tables_page_starts_the_arrival_again_once_it_is_over()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 }, ReducedMotion = ReducedMotion.NoPreference });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(app.Url("/table"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });

        var running = "() => document.getElementById('stagger-orders').getAnimations({ subtree: true }).length";
        await page.Locator("#stagger-orders").ScrollIntoViewIfNeededAsync();
        // The arrival, run to its end: nothing is left on the table.
        await page.WaitForFunctionAsync($"() => ({running})() === 0");

        await page.GetByRole(AriaRole.Button, new() { Name = "Replay" }).First.ClickAsync();
        var started = await page.EvaluateAsync<int>(running);
        Assert.True(started > 0, "Replay did not start the arrival: the table has no animation running.");
        // And it ends the same way, ready to be played again.
        await page.WaitForFunctionAsync($"() => ({running})() === 0");
        Assert.Equal("table table--stagger", await page.Locator("#stagger-orders").GetAttributeAsync("class"));
        Assert.Null(await page.Locator("#stagger-orders").GetAttributeAsync("hidden"));
        Assert.Empty(errors);
        await context.CloseAsync();
    }
}
