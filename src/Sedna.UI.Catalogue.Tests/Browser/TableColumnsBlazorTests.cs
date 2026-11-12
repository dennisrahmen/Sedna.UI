using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The columns on <c>/table-columns</c> through a real Blazor circuit: a width the reader drags
/// is kept by the component from <c>sedna-resize</c> and rendered back, a column dropped
/// elsewhere is moved by the component from <c>sedna-drop</c> with every cell of it, and the
/// chooser's checkboxes decide which columns are rendered.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class TableColumnsBlazorTests(CatalogueAppFixture app)
{
    private async Task<(IPage Page, ILocator Demo)> Open(List<string> errors)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        await page.GotoAsync(app.Url("/table-columns"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        var demo = page.Locator("section[data-example]")
            .Filter(new() { Has = page.Locator("h2, h3", new() { HasTextString = "All three from Blazor" }) })
            .Locator(".ex-demo");
        return (page, demo);
    }

    private static Task<string> Headings(ILocator demo) =>
        demo.Locator("table").EvaluateAsync<string>("t => [...t.tHead.rows[0].cells].map(c => c.textContent.trim()).join('|')");

    [Fact]
    public async Task A_dragged_width_is_kept_by_the_component_and_rendered_back()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var (page, demo) = await Open(errors);
        var heading = demo.Locator("th[data-drag-item='customer']");
        var grip = heading.Locator(".col-resize");
        await grip.ScrollIntoViewIfNeededAsync();

        var box = (await grip.BoundingBoxAsync())!;
        await page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X + box.Width / 2 + 40, box.Y + box.Height / 2, new() { Steps = 4 });
        await page.Mouse.UpAsync();

        // The component's own render: the width in the heading's style and in the line under the table.
        await Assertions.Expect(demo.Locator("p.text-muted")).ToContainTextAsync("Customer 248px");
        await Assertions.Expect(heading).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex("--col-width: 248px"));
        Assert.Equal(248, await heading.EvaluateAsync<double>("th => th.getBoundingClientRect().width"), 0.5);

        // Home puts it back to the component's default.
        await grip.FocusAsync();
        await page.Keyboard.PressAsync("Home");
        await Assertions.Expect(demo.Locator("p.text-muted")).ToContainTextAsync("Customer 208px");
        Assert.Equal(208, await heading.EvaluateAsync<double>("th => th.getBoundingClientRect().width"), 0.5);

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_column_moved_from_the_keyboard_is_moved_by_the_component_with_its_cells()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var (page, demo) = await Open(errors);
        Assert.Equal("Order|Customer|State|Ship to|Total (EUR)", await Headings(demo));

        await demo.Locator("th[data-drag-item='customer']").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("Space");

        await page.WaitForFunctionAsync(
            "t => [...t.tHead.rows[0].cells].map(c => c.textContent.trim()).join('|') === 'Order|State|Ship to|Customer|Total (EUR)'",
            await demo.Locator("table").ElementHandleAsync());
        // Every cell of the column went with its heading.
        Assert.Equal("Northwind Retail, Central Warehouse",
            await demo.Locator("tbody tr").First.Locator("td").Nth(3).TextContentAsync());
        await Assertions.Expect(demo.Locator("th[data-drag-item='customer']")).ToBeFocusedAsync();

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_chooser_decides_which_columns_are_rendered_and_stays_open()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var (page, demo) = await Open(errors);

        // Scrolled to first and left to come to rest: a menu closes when a scroll moves its
        // trigger, as a platform menu does.
        var toggle = demo.Locator("[data-menu-toggle]");
        await toggle.ScrollIntoViewIfNeededAsync();
        await Js.Settle(page);
        await toggle.ClickAsync();
        var menu = demo.Locator(".menu");
        await Assertions.Expect(menu).ToBeVisibleAsync();
        await Assertions.Expect(menu.Locator(".form-check", new() { HasTextString = "Order" }).Locator("input")).ToBeDisabledAsync();

        await menu.Locator(".form-check", new() { HasTextString = "Ship to" }).ClickAsync();
        await page.WaitForFunctionAsync(
            "t => [...t.tHead.rows[0].cells].map(c => c.textContent.trim()).join('|') === 'Order|Customer|State|Total (EUR)'",
            await demo.Locator("table").ElementHandleAsync());
        await Assertions.Expect(menu).ToBeVisibleAsync();

        await menu.Locator(".menu-item", new() { HasTextString = "Reset columns" }).ClickAsync();
        await Assertions.Expect(menu).ToBeHiddenAsync();
        await page.WaitForFunctionAsync(
            "t => t.tHead.rows[0].cells.length === 5",
            await demo.Locator("table").ElementHandleAsync());

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }
}
