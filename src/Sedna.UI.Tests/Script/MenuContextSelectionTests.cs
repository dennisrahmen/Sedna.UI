using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A right-click on an element with a menu at the pointer does not select the word under it,
/// and nothing else about text selection changes.
/// </summary>
/// <remarks>
/// macOS selects the word on the secondary press, before the contextmenu event, so the text
/// flashed selected and the menu opened over it. The press is cancelled, and that is the one
/// thing asserted: whether the platform would select is not something a test can rely on, but
/// whether the press was cancelled is the same everywhere.
/// </remarks>
public class MenuContextSelectionTests : ScriptTestBase
{
    private const string Page = """
        <div style="padding:60px">
          <p id="plain" style="width:300px">Selectable text outside any menu</p>
          <div id="row" data-menu-context="m" tabindex="0" style="width:300px;height:40px">ORD-4182 Northwind Retail</div>
          <div id="stray" data-menu-context="missing" style="width:300px;height:40px">No such menu</div>
        </div>
        <div class="menu" id="m" popover><button class="menu-item" type="button">Copy</button></div>
        <script>
          window.prevented = [];
          addEventListener('mousedown', e => prevented.push([e.target.id, e.button, e.defaultPrevented]));
        </script>
        """;

    private static async Task<bool[]> Presses(IPage page) =>
        await page.EvaluateAsync<bool[]>("() => prevented.map(p => p[2])");

    [Fact]
    public async Task A_secondary_press_on_an_element_with_a_menu_is_cancelled_so_no_word_is_selected()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Page);
        var box = (await page.Locator("#row").BoundingBoxAsync())!;

        await page.Mouse.ClickAsync(box.X + 30, box.Y + 20, new() { Button = MouseButton.Right });

        Assert.Equal(new[] { true }, await Presses(page));
        Assert.Equal("", await page.EvaluateAsync<string>("() => String(getSelection())"));
        await Assertions.Expect(page.Locator("#m")).ToBeVisibleAsync();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Selecting_text_with_the_primary_button_is_untouched_everywhere()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Page);
        var box = (await page.Locator("#row").BoundingBoxAsync())!;

        await page.Mouse.DblClickAsync(box.X + 30, box.Y + 20);

        Assert.All(await Presses(page), prevented => Assert.False(prevented));
    }

    [Fact]
    public async Task A_secondary_press_elsewhere_or_on_an_element_whose_menu_is_missing_is_left_alone()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Page);
        var plain = (await page.Locator("#plain").BoundingBoxAsync())!;
        var stray = (await page.Locator("#stray").BoundingBoxAsync())!;

        await page.Mouse.ClickAsync(plain.X + 20, plain.Y + 8, new() { Button = MouseButton.Right });
        await page.Mouse.ClickAsync(stray.X + 20, stray.Y + 20, new() { Button = MouseButton.Right });

        Assert.All(await Presses(page), prevented => Assert.False(prevented));
    }
}
