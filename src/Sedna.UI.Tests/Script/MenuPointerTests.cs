using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A menu at the pointer, its submenu, the arrow keys in a menu, and a hover card.
/// </summary>
public class MenuPointerTests : ScriptTestBase
{
    private const string Menu = """
        <div style="padding:100px">
          <div id="row" data-menu-context="m" tabindex="0" style="width:300px;height:40px">ORD-4182</div>
        </div>
        <div class="menu" id="m" popover>
          <button class="menu-item" id="copy" type="button">Copy</button>
          <div class="menu-sub-anchor">
            <button class="menu-item" id="status" type="button" data-menu-sub aria-haspopup="menu" aria-expanded="false">Set status</button>
            <div class="menu menu-sub"><button class="menu-item" id="open" type="button">Open</button><button class="menu-item" type="button">Shipped</button></div>
          </div>
          <button class="menu-item" id="cancel" type="button">Cancel order</button>
        </div>
        """;

    [Fact]
    public async Task A_right_click_opens_the_menu_at_the_pointer_with_the_focus_on_its_first_item()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Menu);
        await page.Mouse.ClickAsync(160, 115, new() { Button = MouseButton.Right });
        await Assertions.Expect(page.Locator("#m")).ToBeVisibleAsync();

        var at = await page.EvaluateAsync<double[]>("""
            () => { const r = document.getElementById('m').getBoundingClientRect();
                    return [r.left, r.top, document.getElementById('m').matches(':popover-open') ? 1 : 0]; }
            """);
        Assert.Equal(1, at[2]);
        Assert.InRange(at[0], 158, 162);
        Assert.InRange(at[1], 113, 117);
        Assert.Equal("copy", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('row').hasAttribute('data-menu-context-target')"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_arrow_keys_move_through_the_menu_and_into_and_out_of_a_submenu()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Menu);
        await page.FocusAsync("#row");
        await page.Keyboard.PressAsync("Shift+F10");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("status", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("open", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Equal("true", await page.GetAttributeAsync("#status", "aria-expanded"));
        Assert.True(await page.IsVisibleAsync(".menu-sub"));

        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("status", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("End");
        Assert.Equal("cancel", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_hover_card_opens_after_a_rest_and_stays_while_the_pointer_is_on_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <p style="padding:80px"><span id="t" data-popover="card" data-popover-hover tabindex="0">14 Nov</span></p>
            <div class="popover" id="card" popover><p>Delivery window 09:00–11:00</p></div>
            """);
        await page.HoverAsync("#t");
        await Assertions.Expect(page.Locator("#card")).ToBeVisibleAsync();

        await page.HoverAsync("#card");
        await page.WaitForTimeoutAsync(400);
        await Assertions.Expect(page.Locator("#card")).ToBeVisibleAsync();

        await page.Mouse.MoveAsync(5, 5);
        await Assertions.Expect(page.Locator("#card")).ToBeHiddenAsync();
        Assert.Empty(errors);
    }
}
