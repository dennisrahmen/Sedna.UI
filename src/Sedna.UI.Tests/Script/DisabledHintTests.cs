using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A hint on a disabled button shows on hover, because it is what says why the button
/// cannot be used.
/// </summary>
public class DisabledHintTests : ScriptTestBase
{
    [Theory]
    [InlineData("disabled")]
    [InlineData("aria-disabled=\"true\"")]
    public async Task A_disabled_button_shows_its_hint(string disabled)
    {
        if (NoBrowser) return;
        var page = await Open($"""
            <div style="padding: 80px">
              <button class="btn" type="button" id="b" {disabled} data-tip="Locked until a rider is chosen">Fault +4</button>
            </div>
            """, head: StylesheetTag);

        var box = await page.Locator("#b").BoundingBoxAsync();
        await page.Mouse.MoveAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);

        await Assertions.Expect(page.Locator(".sedna-tip"))
            .ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));
        Assert.Equal("Locked until a rider is chosen", await page.Locator(".sedna-tip").TextContentAsync());
    }

    [Fact]
    public async Task A_click_on_a_disabled_button_still_does_nothing()
    {
        if (NoBrowser) return;
        var page = await Open("""
            <button class="btn" type="button" id="b" disabled onclick="window.clicked = true">Fault +4</button>
            """, head: StylesheetTag);

        var box = await page.Locator("#b").BoundingBoxAsync();
        await page.Mouse.ClickAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);

        Assert.False(await page.EvaluateAsync<bool>("() => window.clicked === true"));
    }
}
