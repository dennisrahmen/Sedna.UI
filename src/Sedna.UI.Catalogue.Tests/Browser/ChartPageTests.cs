using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The charts page through a real Blazor circuit: every chart drawn once, in the markup the
/// interactive render left, a point chosen reaching a C# handler as <c>SednaChartEventArgs</c>,
/// and a re-rendered table redrawing its chart with nothing called.
/// </summary>
/// <remarks>
/// The library's suite pins the chart in a plain page. What only a running app shows is that a
/// prerendered page hands its charts to the drawing once the circuit is up, and that
/// <c>@onsedna-chart-select</c> is registered with its data.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class ChartPageTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open(List<string> errors)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        await page.GotoAsync(app.Url("/chart"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    [Fact]
    public async Task Every_chart_on_the_page_draws_once()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);

        await Js.Run(page, "() => sednaUi.chart.init()");
        await page.WaitForFunctionAsync("""
            () => [...document.querySelectorAll('[data-chart]')].every(c => c.hasAttribute('data-chart-state'))
            """);
        var svgs = await page.EvaluateAsync<int[]>("() => [...document.querySelectorAll('[data-chart]')].map(c => c.querySelectorAll('.chart-svg').length)");
        Assert.NotEmpty(svgs);
        Assert.All(svgs, n => Assert.Equal(1, n));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_chosen_column_reaches_the_component_and_a_new_row_is_drawn()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);
        var chart = page.Locator("#chart-returns");
        await chart.ScrollIntoViewIfNeededAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#chart-returns .chart-bar').length === 4");

        await page.Locator("#chart-returns .chart-bar").Nth(1).ClickAsync();
        await Assertions.Expect(page.Locator("#chart-returns-chosen")).ToContainTextAsync("Week 42: 44 returns.");

        await page.GetByRole(AriaRole.Button, new() { Name = "Add a week" }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#chart-returns .chart-bar').length === 5");
        Assert.Empty(errors);
    }
}
