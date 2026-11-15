using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The column-span example labels every cell with its span, and a one-column cell is narrower
/// than a card's gutter: the label is still centred in its box.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class GridSpanExampleTests(CatalogueAppFixture app)
{
    [Fact]
    public async Task Every_cell_of_the_span_example_holds_its_label_in_the_middle()
    {
        if (app.NoBrowser) return;
        // Wide enough that the grid is past its 48rem collapse and a one-column cell is one column.
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1700, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(app.Url("/grid"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });

        var off = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('.sedna-grid-24 > .card:has(> .card-body > code)')].filter(card => /^\d+$/.test(card.querySelector('code').textContent)).map(card => {
              const box = card.getBoundingClientRect(), label = card.querySelector('code').getBoundingClientRect();
              const gap = Math.abs((box.left + box.right) / 2 - (label.left + label.right) / 2);
              return gap > 1 ? card.querySelector('code').textContent + ' is ' + gap.toFixed(1) + 'px off' : '';
            }).filter(Boolean)
            """);
        Assert.Empty(off);
        Assert.True(await page.Locator(".sedna-grid-24 > .card code").CountAsync() >= 25, "The span example was not found.");
        await context.CloseAsync();
    }
}
