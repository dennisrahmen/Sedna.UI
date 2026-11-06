using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The one address rule (<c>ui._.safeHref</c>) and the Markdown links that use it.
/// </summary>
public class AddressRuleTests : ScriptTestBase
{
    [Theory]
    [InlineData("https://example.com/a_b", "https://example.com/a_b")]
    [InlineData("mailto:alex.fischer@example.com", "mailto:alex.fischer@example.com")]
    [InlineData("/orders/4?x=1", "/orders/4?x=1")]
    [InlineData("orders/4", "orders/4")]
    [InlineData("#top", "#top")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData(" JavaScript:alert(1)", null)]
    [InlineData("data:text/html,x", null)]
    public async Task Only_safe_addresses_pass(string url, string? expected)
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>");
        Assert.Equal(expected, await page.EvaluateAsync<string?>("u => sednaUi._.safeHref(u)", url));
    }

    [Fact]
    public async Task A_markdown_link_keeps_its_underscores_and_its_text_keeps_its_emphasis()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>");
        var html = await page.EvaluateAsync<string>(
            "() => sednaUi.md.render('[the _new_ file](https://a.example/my_file_name.pdf) and **done**')");

        Assert.Contains("href=\"https://a.example/my_file_name.pdf\"", html, StringComparison.Ordinal);
        Assert.Contains("<em>new</em>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>done</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_search_result_with_a_script_address_gets_no_href()
    {
        if (NoBrowser) return;
        var page = await Open("""
            <div class="search"><input class="search-input" type="search" data-search id="s" aria-label="Search"></div>
            <div class="search-panel" data-search-panel hidden>
                <div data-search-list aria-label="Results"></div>
                <template data-search-item><a class="search-item"><span data-title></span></a></template>
            </div>
            """);
        await Js.Run(page, "() => sednaUi.search.register([{ title: 'Orders', href: 'javascript:alert(1)' }, { title: 'Ordering', href: '/orders' }])");
        await page.Locator("#s").FillAsync("Order");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.search-item').length === 2");

        var hrefs = await page.EvaluateAsync<string?[]>("() => [...document.querySelectorAll('.search-item')].map(a => a.getAttribute('href'))");
        Assert.DoesNotContain(hrefs, h => h != null && h.StartsWith("javascript", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("/orders", hrefs);
    }
}
