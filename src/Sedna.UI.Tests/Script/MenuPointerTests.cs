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
    public async Task The_app_opens_a_menu_at_a_point_by_id_and_hears_whether_it_opened()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Menu);
        var opened = await page.EvaluateAsync<bool[]>("""
            () => [sednaUi.menu.openAt('m', 160, 115, 'row'), sednaUi.menu.openAt('no-such-menu', 0, 0)]
            """);
        Assert.Equal([true, false], opened);
        await Assertions.Expect(page.Locator("#m")).ToBeVisibleAsync();
        Assert.Equal("copy", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('row').hasAttribute('data-menu-context-target')"));

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("#m")).ToBeHiddenAsync();
        // The toggle event that clears it is a task of its own, after the panel hides.
        await page.WaitForFunctionAsync("() => !document.getElementById('row').hasAttribute('data-menu-context-target')");
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("'row'")]
    [InlineData("null")]
    public async Task A_menu_at_a_point_closes_when_the_page_scrolls_from_under_it(string about)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div id="scroller" style="height:300px; overflow:auto">
              <div style="height:1200px; padding-top:100px">
                <div id="row" style="width:300px;height:40px">ORD-4182</div>
              </div>
            </div>
            <div id="other" style="height:100px; overflow:auto"><div style="height:400px"></div></div>
            <div class="menu" id="m" popover style="max-height:80px; overflow:auto">
              <button class="menu-item" type="button">Copy</button><button class="menu-item" type="button">Open</button>
              <button class="menu-item" type="button">Pin</button><button class="menu-item" type="button">Cancel order</button>
            </div>
            """);
        async Task<bool> Open() => await page.EvaluateAsync<bool>("() => document.getElementById('m').matches(':popover-open')");
        async Task Scroll(string id, string by) =>
            await page.EvaluateAsync($"() => {{ document.getElementById('{id}').scrollTop += {by}; }}");

        await page.EvaluateAsync($"() => sednaUi.menu.openAt('m', 160, 115, {about})");
        Assert.True(await Open());

        await Scroll("m", "40");          // the menu's own list, never
        await page.WaitForTimeoutAsync(50);
        Assert.True(await Open());

        if (about != "null")
        {
            await Scroll("other", "40");  // a scroller that does not hold what it is about
            await page.WaitForTimeoutAsync(50);
            Assert.True(await Open());
        }

        await Scroll("scroller", "60");
        await page.WaitForTimeoutAsync(50);
        Assert.False(await Open());
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

    [Fact]
    public async Task A_second_click_on_the_trigger_closes_a_pinned_hover_card()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <p style="padding:80px"><button class="btn" id="t" type="button" data-popover="card" data-popover-hover>Open items</button></p>
            <div class="popover" id="card" popover><p>Three items due in the next 14 days</p></div>
            """);

        await page.ClickAsync("#t");                                   // pins it
        await Assertions.Expect(page.Locator("#card")).ToBeVisibleAsync();
        await page.Mouse.MoveAsync(5, 5);
        await page.WaitForTimeoutAsync(400);
        await Assertions.Expect(page.Locator("#card")).ToBeVisibleAsync();   // pinned: leaving does not close it

        await page.ClickAsync("#t");                                   // and the trigger again closes it
        await Assertions.Expect(page.Locator("#card")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#t")).ToHaveAttributeAsync("aria-expanded", "false");

        await page.ClickAsync("#t");                                   // a third pins it again
        await Assertions.Expect(page.Locator("#card")).ToBeVisibleAsync();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_hover_only_card_opens_on_hover_and_focus_and_leaves_the_click_to_the_trigger()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <p style="padding:80px"><button class="btn" id="t" type="button" data-popover="card" data-popover-hover="only"
                onclick="window.clicked = (window.clicked || 0) + 1">Hosting costs</button></p>
            <div class="popover" id="card" popover><p>This month so far: 41.20 EUR</p></div>
            """);

        await page.HoverAsync("#t");
        await Assertions.Expect(page.Locator("#card")).ToBeVisibleAsync();

        await page.ClickAsync("#t");
        await Assertions.Expect(page.Locator("#card")).ToBeHiddenAsync();      // out of the way of what the click opens
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.clicked"));  // the trigger's own action ran
        await page.Mouse.MoveAsync(5, 5);
        await page.WaitForTimeoutAsync(400);
        await Assertions.Expect(page.Locator("#card")).ToBeHiddenAsync();      // and nothing pinned it

        // Chromium focuses a clicked button and WebKit does not, so start from no focus at all.
        await page.EvaluateAsync("() => document.activeElement && document.activeElement.blur()");
        await page.FocusAsync("#t");
        await Assertions.Expect(page.Locator("#card")).ToBeVisibleAsync();    // focus opens it, as hover does
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("#card")).ToBeHiddenAsync();
        Assert.Equal(2, await page.EvaluateAsync<int>("() => window.clicked"));
        Assert.Empty(errors);
    }
}
