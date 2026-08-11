using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Delegated dropdowns: hidden is the closed state, and Escape returns focus to the trigger.
/// </summary>
public class MenuTests : ScriptTestBase
{
    private const string MenuFixture = """
        <div class="menu-anchor">
            <button type="button" data-menu-toggle aria-expanded="false" id="trigger">Actions</button>
            <div class="menu" hidden>
                <a class="menu-item" href="#one" id="item">Rename</a>
            </div>
        </div>
        <button type="button" id="outside">Elsewhere</button>
        """;

    [Fact]
    public async Task A_menu_opens_on_its_toggle_and_closes_on_an_outside_click()
    {
        if (NoBrowser) return;
        var page = await Open(MenuFixture);

        await page.Locator("#trigger").ClickAsync();
        Assert.Equal("true", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));
        Assert.True(await page.Locator(".menu").IsVisibleAsync());

        await page.Locator("#outside").ClickAsync();
        Assert.Equal("false", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));
        // `hidden` is the closed state, not a class — so the items leave the tab order.
        Assert.True(await page.EvaluateAsync<bool>("() => document.querySelector('.menu').hidden"));
    }

    [Fact]
    public async Task Escape_closes_a_menu_and_gives_focus_back_to_the_control_that_opened_it()
    {
        if (NoBrowser) return;
        // Without the focus return, focus is left on a node that has just been hidden
        // and the next Tab starts from the top of the document.
        var page = await Open(MenuFixture);

        await page.Locator("#trigger").ClickAsync();
        await page.Locator("#item").FocusAsync();
        await page.Keyboard.PressAsync("Escape");

        Assert.Equal("false", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));
        Assert.Equal("trigger", await page.EvaluateAsync<string>("() => document.activeElement.id"));
    }

    [Fact]
    public async Task Clicking_a_menu_item_closes_the_menu_and_closeAll_closes_every_one()
    {
        if (NoBrowser) return;
        var page = await Open(MenuFixture);

        await page.Locator("#trigger").ClickAsync();
        await page.Locator("#item").ClickAsync();
        Assert.Equal("false", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));

        await page.Locator("#trigger").ClickAsync();
        await page.EvaluateAsync("() => sednaUi.menu.closeAll()");
        Assert.Equal("false", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));
    }

    // ── delegated tabs ──────────────────────────────────────────────────────
}
