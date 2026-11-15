using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// An open dropdown and its submenus are in the top layer, so the panel they sit in cannot clip
/// them or move them — an inbox, a card that scrolls, a drawer with a transform.
/// </summary>
public class MenuTopLayerTests : ScriptTestBase
{
    // A box that clips and holds a transform, which makes it the containing block of everything
    // fixed inside it: the worst panel a menu can sit in. It is narrower than the menu.
    private const string Panel = """
        <div id="clip" style="width: 90px; height: 120px; margin: 60px; overflow: hidden; transform: translateZ(0); border: 1px solid var(--border); border-radius: 8px">
          <div class="menu-anchor">
            <button class="btn" type="button" id="toggle" data-menu-toggle aria-expanded="false">Actions</button>
            <div class="menu" id="menu" hidden>
              <button class="menu-item" type="button" id="first">Mark as read</button>
              <div class="menu-sub-anchor">
                <button class="menu-item" type="button" id="snooze" data-menu-sub aria-haspopup="menu" aria-expanded="false"><span>Snooze</span> <i class="ri-arrow-right-s-line"></i></button>
                <div class="menu menu-sub" id="sub">
                  <button class="menu-item" type="button" id="hour">For an hour</button>
                  <button class="menu-item" type="button" id="monday">Until Monday</button>
                </div>
              </div>
              <button class="menu-item" type="button" id="archive">Archive</button>
            </div>
          </div>
        </div>
        <button class="btn" type="button" id="elsewhere">Elsewhere</button>
        """;

    private async Task<IPage> Open(int width = 1100)
    {
        var (page, _) = await OpenStyled(Panel);
        await page.SetViewportSizeAsync(width, 700);
        return page;
    }

    private static Task<bool> Reaches(IPage page, string id) => page.EvaluateAsync<bool>($$"""
        () => {
            const el = document.getElementById('{{id}}');
            const r = el.getBoundingClientRect();
            const at = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
            return !!at && (at === el || el.contains(at));
        }
        """);

    [Fact]
    public async Task A_dropdown_and_its_submenu_open_past_the_edge_of_the_box_they_sit_in()
    {
        if (NoBrowser) return;
        var page = await Open();
        await page.Locator("#toggle").ClickAsync();
        await Assertions.Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await page.Locator("#snooze").HoverAsync();
        await Assertions.Expect(page.Locator("#sub")).ToBeVisibleAsync();

        var clip = (await page.Locator("#clip").BoundingBoxAsync())!;
        var menu = (await page.Locator("#menu").BoundingBoxAsync())!;
        var sub = (await page.Locator("#sub").BoundingBoxAsync())!;
        // Either edge: which side a dropdown hangs off its trigger is the menu's own business.
        bool Beyond(LocatorBoundingBoxResult b) => b.X < clip.X || b.X + b.Width > clip.X + clip.Width;
        Assert.True(Beyond(menu), "The dropdown is meant to be wider than the box it sits in.");
        Assert.True(Beyond(sub), "The submenu is meant to open beyond the box it sits in.");

        // What is painted at the far end of each is the panel's own item, not the box's edge or the page.
        Assert.True(await Reaches(page, "archive"), "The dropdown is clipped by the box it sits in.");
        Assert.True(await Reaches(page, "monday"), "The submenu is clipped by the box it sits in.");
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('menu').matches(':popover-open') && document.getElementById('sub').matches(':popover-open')"),
            "An open dropdown and submenu are in the top layer.");
    }

    [Fact]
    public async Task The_panels_stand_where_the_trigger_puts_them_whatever_the_box_does()
    {
        if (NoBrowser) return;
        var page = await Open();
        await page.Locator("#toggle").ClickAsync();
        await page.Locator("#snooze").HoverAsync();
        await Assertions.Expect(page.Locator("#sub")).ToBeVisibleAsync();

        var toggle = (await page.Locator("#toggle").BoundingBoxAsync())!;
        var menu = (await page.Locator("#menu").BoundingBoxAsync())!;
        var item = (await page.Locator("#snooze").BoundingBoxAsync())!;
        var sub = (await page.Locator("#sub").BoundingBoxAsync())!;
        // The dropdown is under its trigger, in the viewport's own coordinates, not the box's.
        Assert.True(Math.Abs(menu.Y - (toggle.Y + toggle.Height)) < 12, $"The dropdown is {menu.Y - toggle.Y - toggle.Height:0}px under its trigger.");
        // The submenu opens beside the item that names it, at its height.
        Assert.True(Math.Abs(sub.Y - item.Y) < 12, $"The submenu is {sub.Y - item.Y:0}px from its item's top.");
        Assert.True(Math.Abs(sub.X - (menu.X + menu.Width)) < 12, $"The submenu is {sub.X - menu.X - menu.Width:0}px from the dropdown's end.");
    }

    [Fact]
    public async Task Leaving_the_item_closes_the_submenu_and_Escape_closes_the_dropdown()
    {
        if (NoBrowser) return;
        var page = await Open();
        await page.Locator("#toggle").ClickAsync();
        await page.Locator("#snooze").HoverAsync();
        await Assertions.Expect(page.Locator("#sub")).ToBeVisibleAsync();

        // Onto a sibling item: the submenu goes, the dropdown stays.
        await page.Locator("#first").HoverAsync();
        await Assertions.Expect(page.Locator("#sub")).ToBeHiddenAsync();
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('sub').matches(':popover-open')"), "A closed submenu is still in the top layer.");
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('menu').matches(':popover-open')"));

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('menu').matches(':popover-open')"), "A closed dropdown is still in the top layer.");
    }

    [Fact]
    public async Task The_keyboard_opens_the_submenu_into_the_top_layer_and_closing_the_dropdown_takes_it_down()
    {
        if (NoBrowser) return;
        var page = await Open();
        await page.Locator("#toggle").ClickAsync();
        await page.Locator("#snooze").FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("hour", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('sub').matches(':popover-open')"));

        await page.Locator("#elsewhere").ClickAsync();
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('sub').matches(':popover-open')"), "The submenu outlived its dropdown.");
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('menu').matches(':popover-open')"));
    }

    [Fact]
    public async Task On_a_phone_the_submenu_stays_in_the_sheet_s_own_flow()
    {
        if (NoBrowser) return;
        var page = await Open(375);
        await page.Locator("#toggle").ClickAsync();
        await page.Locator("#snooze").ClickAsync();
        var sub = await page.EvaluateAsync<string[]>("""
            () => { const s = document.getElementById('sub'); return [getComputedStyle(s).position, String(s.hasAttribute('popover'))]; }
            """);
        Assert.Equal(["static", "false"], sub);
    }
}
