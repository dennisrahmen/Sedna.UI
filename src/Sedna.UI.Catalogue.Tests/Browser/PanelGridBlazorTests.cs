using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// A panel grid end to end, through a real Blazor circuit: the drop and the resize reach
/// <c>@onsedna-drop</c> and <c>@onsedna-resize</c> with <c>Panels</c>, and the component's own
/// render of that arrangement is what stays on the page.
/// </summary>
/// <remarks>
/// The library's suite pins the arrangement and the events on a plain page, with the render done
/// by hand. What only a running app shows is that the initializer hands the arrangement to C#,
/// and that the preview gives way to the render Blazor makes.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class PanelGridBlazorTests(CatalogueAppFixture app)
{
    private const string Grid = ".panel-grid[data-drag-axis='cells']";

    private async Task<IPage> Open(List<string> errors)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.GotoAsync(app.Url("/panel-grid"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    /// <summary>A panel's own properties as the component rendered them, and no preview left.</summary>
    private static Task<string> Rendered(IPage page, string id) =>
        page.EvaluateAsync<string>(
            $"() => {{ const el = document.querySelector(\"{Grid} > [data-drag-item='{id}']\"); return el.getAttribute('style').replace(/\\s+/g, ''); }}");

    [Fact]
    public async Task A_keyboard_move_and_a_resize_reach_the_component_and_its_render_stays()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);

        // Returns sits under Open orders and Dispatch; one press up puts it above both.
        var grip = page.Locator($"{Grid} > [data-drag-item='returns'] [data-drag-handle]");
        await grip.ScrollIntoViewIfNeededAsync();
        await grip.FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("Space");

        await page.WaitForFunctionAsync(
            $"() => /--panel-y:\\s*0;/.test(document.querySelector(\"{Grid} > [data-drag-item='returns']\").getAttribute('style'))");
        Assert.Equal("--panel-x:0;--panel-y:0;--panel-w:8;--panel-h:3", await Rendered(page, "returns"));
        Assert.Equal("--panel-x:0;--panel-y:3;--panel-w:4;--panel-h:3", await Rendered(page, "orders"));
        // The component renders in the arrangement's reading order.
        Assert.Equal("returns", await page.EvaluateAsync<string>($"() => document.querySelector(\"{Grid} > [data-drag-item]\").getAttribute('data-drag-item')"));

        // Narrower from the keyboard: the edge's value is the component's render.
        var width = page.Locator($"{Grid} > [data-drag-item='returns'] [data-resize='--panel-w']");
        await width.FocusAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(width).ToHaveAttributeAsync("aria-valuenow", "7");
        Assert.Equal("--panel-x:0;--panel-y:0;--panel-w:7;--panel-h:3", await Rendered(page, "returns"));

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Done_takes_the_grid_out_of_arrange_mode_and_its_handles_away()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);
        var demo = page.Locator("section[data-example]").Filter(new() { Has = page.Locator(Grid) });
        await demo.Locator("button[aria-pressed]").ClickAsync();

        await Assertions.Expect(demo.Locator(Grid)).Not.ToHaveAttributeAsync("data-arrange", new System.Text.RegularExpressions.Regex(".*"));
        Assert.Equal(0, await page.EvaluateAsync<int>(
            $"() => [...document.querySelectorAll(\"{Grid} [data-drag-handle], {Grid} [data-resize]\")].filter(el => getComputedStyle(el).display !== 'none').length"));
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }
}
