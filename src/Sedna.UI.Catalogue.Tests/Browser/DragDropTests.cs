using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Drag and drop end to end, through a real Blazor circuit: the script's event reaches
/// <c>@onsedna-drop</c> with its data, and the component's own render moves the item.
/// </summary>
/// <remarks>
/// The library's own suite pins the event contract and that the script moves no node. What
/// only a running app can show is the other half — that <c>Sedna.UI.lib.module.js</c> is
/// loaded by Blazor and registers the events, so the C# handler receives a populated
/// <c>SednaDropEventArgs</c> rather than an empty one, and that the render which follows
/// is the one that puts the item in its new place.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class DragDropTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open(int width = 1280, int height = 900, bool touch = false)
    {
        var context = await app.Browser!.NewContextAsync(new()
        {
            ViewportSize = new() { Width = width, Height = height },
            HasTouch = touch,
            IsMobile = touch,
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(app.Url("/drag-drop"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    private static Task<string> Order(IPage page, string zone) =>
        page.EvaluateAsync<string>(
            $"() => [...document.querySelector('[data-drag-zone=\"{zone}\"]').children].map(el => el.getAttribute('data-drag-item')).filter(Boolean).join(',')");

    private static async Task<(float X, float Y)> Centre(ILocator locator, float fy = 0.5f)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var box = await locator.BoundingBoxAsync();
        Assert.NotNull(box);
        return (box!.X + box.Width / 2, box.Y + box.Height * fy);
    }

    [Fact]
    public async Task A_mouse_drag_reaches_the_component_and_its_render_moves_the_row()
    {
        if (app.NoBrowser) return;
        var page = await Open();
        Assert.Equal("freeze,backup,migrate,canary,promote", await Order(page, "release-steps"));

        var from = await Centre(page.Locator("[data-drag-item=backup] [data-drag-handle]"));
        var to = await Centre(page.Locator("[data-drag-item=canary]"), fy: 0.8f);
        await page.Mouse.MoveAsync(from.X, from.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(to.X, to.Y, new() { Steps = 12 });
        await page.Mouse.UpAsync();

        await page.WaitForFunctionAsync(
            "() => [...document.querySelectorAll('[data-drag-zone=\"release-steps\"] > [data-drag-item]')].map(el => el.getAttribute('data-drag-item')).join(',') === 'freeze,migrate,canary,backup,promote'");
        Assert.Equal(0, await page.Locator("[data-drag-zone=release-steps] [data-dragging], [data-drag-zone=release-steps][data-drop-state]").CountAsync());

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_keyboard_move_to_another_lane_renders_the_card_there_and_keeps_focus_on_it()
    {
        if (app.NoBrowser) return;
        var page = await Open();

        await page.Locator("[data-drag-item='ORD-4209']").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("Space");

        await page.Locator("[data-drag-zone=done] > [data-drag-item='ORD-4209']").WaitForAsync(new() { Timeout = 10_000 });
        Assert.Equal("ORD-4204,ORD-4187", await Order(page, "queued"));
        // The lane's count is rendered from the component's state, so it moved too.
        Assert.Equal("1", (await page.Locator("#lane-done .badge").TextContentAsync())?.Trim());
        // A move between lanes replaces the node, which drops focus; the script puts it back.
        await Assertions.Expect(page.Locator("[data-drag-item='ORD-4209']")).ToBeFocusedAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task At_phone_width_a_finger_held_on_a_tile_carries_it()
    {
        if (app.NoBrowser) return;
        var page = await Open(width: 390, height: 844, touch: true);
        var cdp = await page.Context.NewCDPSessionAsync(page);

        var before = await Order(page, "dashboard");
        Assert.StartsWith("orders,revenue", before, StringComparison.Ordinal);

        var from = await Centre(page.Locator("[data-drag-item=orders]"));
        var to = await Centre(page.Locator("[data-drag-item=share]"));

        async Task Touch(string type, (float X, float Y)? at) =>
            await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
            {
                ["type"] = type,
                ["touchPoints"] = at is { } p
                    ? new object[] { new Dictionary<string, object> { ["x"] = p.X, ["y"] = p.Y, ["id"] = 1 } }
                    : Array.Empty<object>(),
            });

        await Touch("touchStart", from);
        await page.WaitForTimeoutAsync(450);
        for (var i = 1; i <= 10; i++)
        {
            await Touch("touchMove", (from.X + (to.X + 30 - from.X) * i / 10, from.Y + (to.Y - from.Y) * i / 10));
        }
        await page.WaitForTimeoutAsync(50);
        await Touch("touchEnd", null);

        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-drag-zone=\"dashboard\"] > [data-drag-item]').getAttribute('data-drag-item') !== 'orders'");
        var after = await Order(page, "dashboard");
        Assert.NotEqual(before, after);
        Assert.Equal(before.Split(',').Order(), after.Split(',').Order());

        await page.Context.CloseAsync();
    }
}
