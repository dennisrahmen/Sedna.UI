using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Hover hints on touch: press and hold shows one, the press does not also act,
/// and a tap is unchanged.
/// </summary>
public class TipTouchTests : ScriptTestBase
{
    private const string Body = """
        <button type="button" id="b" data-tip="Deletes the order and its reservations"
                onclick="window.clicks = (window.clicks || 0) + 1">Delete</button>
        """;

    private static Task Pointer(IPage page, string type) =>
        page.EvaluateAsync($$"""
            () => document.getElementById('b').dispatchEvent(
                new PointerEvent('{{type}}', { pointerType: 'touch', bubbles: true, cancelable: true }))
            """);

    [Fact]
    public async Task Press_and_hold_shows_the_hint_and_swallows_the_click()
    {
        if (NoBrowser) return;
        var page = await Open(Body);

        await Pointer(page, "pointerdown");
        await page.WaitForTimeoutAsync(650);
        await Assertions.Expect(page.Locator(".sedna-tip")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));
        await Assertions.Expect(page.Locator(".sedna-tip")).ToHaveTextAsync("Deletes the order and its reservations");

        // Lifting the finger fires the platform's click; the hold has already taken it.
        await Pointer(page, "pointerup");
        await page.Locator("#b").ClickAsync();
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.clicks || 0"));

        // Lingers long enough to be read, then goes.
        await Assertions.Expect(page.Locator(".sedna-tip")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));
        await Assertions.Expect(page.Locator(".sedna-tip")).Not.ToHaveClassAsync(
            new System.Text.RegularExpressions.Regex("sedna-tip--visible"), new() { Timeout = 4000 });
    }

    [Fact]
    public async Task A_tap_acts_and_shows_nothing()
    {
        if (NoBrowser) return;
        var page = await Open(Body);

        await Pointer(page, "pointerdown");
        await page.WaitForTimeoutAsync(120);
        await Pointer(page, "pointerup");

        // Nothing from the hold path: the press was too short. (Focus after the click
        // below may show the hint, as it does for a mouse — that is the focus path.)
        var visible = await page.EvaluateAsync<bool>(
            "() => !!document.querySelector('.sedna-tip--visible')");
        Assert.False(visible, "A tap showed a hint; only a hold should.");

        await page.Locator("#b").ClickAsync();
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.clicks || 0"));
    }
}
