using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The head follows the router: after an in-app navigation the title, the description
/// and the canonical are the new page's.
/// </summary>
/// <remarks>
/// A <c>HeadOutlet</c> with no render mode is static under a global interactive router,
/// so every page reached by a link kept the first page's title in the tab, in a
/// bookmark and in a link shared from it.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class HeadNavigationTests(CatalogueAppFixture app)
{
    [Fact]
    public async Task An_in_app_navigation_replaces_the_head()
    {
        if (app.NoBrowser) return;
        var page = await app.OpenInteractiveAsync("/badge");

        await page.Locator("a[href='/chip']").First.ClickAsync();
        await page.WaitForFunctionAsync("() => location.pathname === '/chip'");

        var chip = Assert.Single(CataloguePages.All, p => p.Route == "/chip");
        await page.WaitForFunctionAsync(
            "url => document.querySelector('link[rel=canonical]')?.href === url",
            SiteMeta.Canonical("/chip"));

        Assert.StartsWith("Chips", await page.TitleAsync(), StringComparison.Ordinal);
        Assert.Equal(SiteMeta.Description(chip),
            await page.EvaluateAsync<string>("() => document.querySelector('meta[name=description]').content"));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => document.querySelectorAll('link[rel=canonical]').length"));

        await page.CloseAsync();
    }
}
