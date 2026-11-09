using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// <c>/theme-builder</c> through a real circuit: picking a colour changes the printed
/// <c>brand.css</c> and the page's own tokens together, because the page applies what it prints.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class ThemeBuilderBrowserTests(CatalogueAppFixture app)
{
    [Fact]
    public async Task Picking_a_brand_colour_rethemes_the_page_and_changes_the_brand_css()
    {
        if (app.NoBrowser) return;
        var page = await app.OpenInteractiveAsync("/theme-builder");

        const string Coral600 = "getComputedStyle(document.documentElement).getPropertyValue('--coral-600').trim()";
        Assert.Equal("#d73f1a", await page.EvaluateAsync<string>("() => " + Coral600));
        var before = await page.Locator("#theme-css pre").InnerTextAsync();

        await page.Locator("#theme-brand").FillAsync("#16a34a");

        await page.WaitForFunctionAsync($"() => {Coral600} !== '#d73f1a'");
        var applied = await page.EvaluateAsync<string>("() => " + Coral600);
        await page.WaitForFunctionAsync(
            "v => document.querySelector('#theme-css pre').textContent.includes('--coral-600: ' + v + ';')", applied);

        var after = await page.Locator("#theme-css pre").InnerTextAsync();
        Assert.NotEqual(before, after);
        Assert.Contains("brand #16a34a", after, StringComparison.Ordinal);
        Assert.Equal("#16a34a", await page.Locator("#theme-brand-hex").InputValueAsync());

        // --brand follows its ramp step, on the library's own primary button. Waited on rather
        // than read once: the button transitions its background.
        await page.WaitForFunctionAsync("""
            v => {
                const probe = document.createElement('i');
                probe.style.color = v;
                document.body.append(probe);
                const expected = getComputedStyle(probe).color;
                probe.remove();
                return getComputedStyle(document.querySelector('.btn--brand.btn--solid')).backgroundColor === expected;
            }
            """, applied);

        await page.CloseAsync();
    }
}
