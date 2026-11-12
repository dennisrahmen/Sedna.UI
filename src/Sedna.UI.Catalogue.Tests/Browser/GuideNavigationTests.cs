using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The "Goes well with" section follows the router: after an in-app navigation it is the
/// new page's entry, or gone on a page with none.
/// </summary>
/// <remarks>
/// The layout survives an in-app navigation, and a child with unchanged parameters is not
/// rendered again, so a section that read the route itself kept the first page's entry —
/// none, from a reference page — until a reload, while the contents already listed it.
/// </remarks>
[Trait(Scope.Name, Scope.Site)]
[Collection(CatalogueAppCollection.Name)]
public class GuideNavigationTests(CatalogueAppFixture app)
{
    [Fact]
    public async Task An_in_app_navigation_replaces_the_guide_section()
    {
        if (app.NoBrowser) return;
        var page = await app.OpenInteractiveAsync("/getting-started");
        Assert.Equal(0, await page.Locator(".cat-guide").CountAsync());

        await page.Locator("a[href='/badge']").First.ClickAsync();
        await page.WaitForFunctionAsync("() => location.pathname === '/badge'");
        await page.WaitForSelectorAsync("#goes-well-with");

        await page.GoBackAsync();
        await page.WaitForFunctionAsync("() => location.pathname === '/getting-started'");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cat-guide')");

        await page.CloseAsync();
    }
}
