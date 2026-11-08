using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The large tables on <c>/table-large</c> through a real Blazor circuit: a tree grid whose rows
/// the component renders when the script says a branch opened, and a virtualized table whose
/// spacer rows the grid and the stripe pass by while Blazor swaps the window.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class LargeTableBlazorTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open(List<string> errors)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        await page.GotoAsync(app.Url("/table-large"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    private static ILocator Example(IPage page, string title) =>
        page.Locator("section[data-example]")
            .Filter(new() { Has = page.Locator("h2", new() { HasTextString = title }) })
            .Locator(".ex-demo");

    [Fact]
    public async Task A_lazy_branch_is_rendered_by_the_component_and_its_state_kept_by_the_events()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);
        var demo = Example(page, "A tree grid from Blazor");
        var year = demo.Locator("tr[data-value='2024']");
        var cell = year.Locator("td").First;

        await cell.ClickAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(year).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(demo.Locator("tr[data-value='2024-November']")).ToBeVisibleAsync();
        await Assertions.Expect(year).Not.ToHaveAttributeAsync("aria-busy", "true");
        await Assertions.Expect(year).Not.ToHaveAttributeAsync("data-tree-lazy", "");
        await Assertions.Expect(demo.Locator("p")).ToContainTextAsync("Open: 2024");

        // Down into the rows the component rendered, back up to the branch, and closed.
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(demo.Locator("tr[data-value='2024-October'] td").First).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(cell).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(year).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(demo.Locator("tr[data-value='2024-October']")).ToHaveCountAsync(0);
        await Assertions.Expect(demo.Locator("p")).ToContainTextAsync("Open: none");
        await Assertions.Expect(cell).ToBeFocusedAsync();

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_virtualized_table_keeps_its_stripe_on_the_row_number_as_the_window_moves()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);
        var scroller = Example(page, "Virtualized rows").Locator(".sedna-scroll");

        // Far down the list, with the window Blazor renders moved along.
        await scroller.EvaluateAsync<int>("s => s.scrollTop = 120000");
        await page.WaitForFunctionAsync("""
            () => [...document.querySelectorAll('[aria-label="Parcel scans"] tbody tr[aria-rowindex]')]
                .some(r => +r.getAttribute('aria-rowindex') > 2000)
            """);

        var wrong = await scroller.EvaluateAsync<string[]>("""
            s => {
                const rows = [...s.querySelectorAll('tbody tr[aria-rowindex]')];
                const bg = r => getComputedStyle(r.cells[0]).backgroundColor;
                const clear = 'rgba(0, 0, 0, 0)';
                return rows.filter(r => (+r.getAttribute('aria-rowindex') % 2 === 1) !== (bg(r) !== clear))
                    .map(r => r.getAttribute('aria-rowindex') + ':' + bg(r));
            }
            """);
        Assert.Empty(wrong);
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }
}
