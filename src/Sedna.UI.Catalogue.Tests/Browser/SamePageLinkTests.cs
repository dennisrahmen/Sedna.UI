using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// A link to somewhere on the page being read keeps the reader on it, in the running
/// Blazor app.
/// </summary>
/// <remarks>
/// The host page's <c>&lt;base href="/"&gt;</c> resolves a bare <c>#id</c> against the
/// root, and the router follows it: a skip link and a validation summary both opened
/// the landing page, and every <c>href="#"</c> placeholder in a demo did the same. The
/// library's fixture tests prove the script with a base and no Blazor; these prove it
/// against Blazor's own link interception, which is the half a fixture cannot have.
///
/// The short settle after each click is the one wait here that is not on a signal:
/// the assertion is that nothing happens, and the router answers a click it
/// intercepted within a few milliseconds.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class SamePageLinkTests(CatalogueAppFixture app)
{
    private const int Settle = 400;

    [Fact]
    public async Task The_skip_link_moves_focus_to_the_content_without_leaving_the_page()
    {
        if (app.NoBrowser) return;
        var page = await app.OpenInteractiveAsync("/button");

        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("skip-link", await page.EvaluateAsync<string>("() => document.activeElement.className"));

        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(Settle);

        Assert.Equal("/button", await page.EvaluateAsync<string>("() => location.pathname"));
        Assert.Equal("main", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        await page.CloseAsync();
    }

    [Fact]
    public async Task A_validation_summary_link_focuses_its_field()
    {
        if (app.NoBrowser) return;
        var page = await app.OpenInteractiveAsync("/alert");

        await page.Locator(".ex-demo a[href='#vs-email']").ClickAsync();
        await page.WaitForTimeoutAsync(Settle);

        Assert.Equal("/alert", await page.EvaluateAsync<string>("() => location.pathname"));
        Assert.Equal("vs-email", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        await page.CloseAsync();
    }

    [Fact]
    public async Task A_placeholder_link_in_a_demo_does_not_leave_the_page()
    {
        if (app.NoBrowser) return;

        var problems = new List<string>();
        var clicked = 0;

        // Off the landing page, because a link that goes to "/" from "/" cannot be seen
        // to have gone anywhere.
        foreach (var route in RoutedPages.All.Where(r => r != "/"))
        {
            var page = await app.OpenAsync(route);
            var count = await page.Locator(".ex-demo a[href='#']").CountAsync();
            if (count > 0)
            {
                await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
                await page.EvaluateAsync(
                    "() => document.querySelectorAll('.ex-demo a[href=\"#\"]').forEach(a => a.click())");
                await page.WaitForTimeoutAsync(Settle);

                var path = await page.EvaluateAsync<string>("() => location.pathname");
                if (path != route) problems.Add($"{route}: a placeholder link in a demo went to {path}");
                clicked += count;
            }

            await page.CloseAsync();
        }

        // The vacuity guard: a selector that stopped matching would pass every page.
        Assert.True(clicked >= 20, $"Only {clicked} placeholder links were found to click.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
