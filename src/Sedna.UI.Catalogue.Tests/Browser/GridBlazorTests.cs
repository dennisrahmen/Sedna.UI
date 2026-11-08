using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The keyboard grid through a real Blazor circuit, on <c>/table-editing</c>: an edit committed
/// to <c>@bind</c> by the editor's own <c>change</c>, the states rendered back from it, and a row
/// in edit mode whose re-render takes away the node that had the focus.
/// </summary>
/// <remarks>
/// The library's suite pins the keys and the focus after a render on a plain page, with the
/// render done by hand. What only a running app shows is a render Blazor makes: whether the
/// focus lands where it should once the circuit has answered.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class GridBlazorTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open(List<string> errors)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        await page.GotoAsync(app.Url("/table-editing"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    private static ILocator Example(IPage page, string title) =>
        page.Locator("section[data-example]")
            .Filter(new() { Has = page.Locator("h2", new() { HasTextString = title }) })
            .Locator(".ex-demo");

    private static Task<string> Focused(IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement.getAttribute('aria-label') || document.activeElement.textContent.trim()");

    [Fact]
    public async Task An_edit_reaches_the_binding_and_the_cell_shows_what_the_component_renders_back()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);
        var demo = Example(page, "Editing cells from Blazor");
        var cell = demo.Locator("td.col-num").First;

        await cell.ClickAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(demo.GetByLabel("Quantity, Desk lamp")).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("15");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(cell).ToBeFocusedAsync();
        await Assertions.Expect(cell).ToHaveAttributeAsync("data-dirty", "");
        await Assertions.Expect(cell).ToContainTextAsync("15");
        await Assertions.Expect(demo.GetByRole(AriaRole.Button, new() { Name = "Save changes" })).ToBeEnabledAsync();

        // A value the component rejects is rendered back as aria-invalid, on the cell and the editor.
        await page.Keyboard.PressAsync("F2");
        await page.Keyboard.PressAsync(Keys.SelectAll);
        await page.Keyboard.TypeAsync("0");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(cell).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(demo.GetByLabel("Quantity, Desk lamp")).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(demo.GetByRole(AriaRole.Button, new() { Name = "Save changes" })).ToBeDisabledAsync();
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_row_entering_and_leaving_edit_mode_keeps_the_focus_through_each_render()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);
        var demo = Example(page, "Row edit mode from Blazor");

        await demo.GetByRole(AriaRole.Gridcell, new() { Name = "Bern", Exact = true }).ClickAsync();
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("Edit the address for Alex Fischer", await Focused(page));

        // Edit: Blazor renders the row in edit mode and takes the Edit button away. The focus
        // goes to the row's first editor rather than to the page.
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(demo.GetByLabel("Recipient")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync(Keys.Tab);
        await Assertions.Expect(demo.GetByLabel("City")).ToBeFocusedAsync();

        // Emptied, the city is rejected and Save is disabled, so Enter presses nothing.
        await page.Keyboard.PressAsync(Keys.SelectAll);
        await page.Keyboard.PressAsync("Backspace");
        await Assertions.Expect(demo.GetByLabel("City")).ToHaveAttributeAsync("aria-invalid", "true");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(demo.GetByLabel("City")).ToBeFocusedAsync();

        // Enter saves: the row leaves edit mode and the focus is on the cell it was edited in.
        await page.Keyboard.TypeAsync("Thun");
        await Assertions.Expect(demo.GetByRole(AriaRole.Button, new() { Name = "Save" })).ToBeEnabledAsync();
        await page.Keyboard.PressAsync("Enter");
        var city = demo.GetByRole(AriaRole.Gridcell, new() { Name = "Thun", Exact = true });
        await Assertions.Expect(city).ToBeFocusedAsync();
        await Assertions.Expect(demo.Locator("tr[data-editing]")).ToHaveCountAsync(0);

        // Escape cancels the same way.
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(demo.GetByLabel("Recipient")).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("Someone else");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(demo.GetByRole(AriaRole.Gridcell, new() { Name = "Alex Fischer", Exact = true })).ToBeFocusedAsync();
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }
}
