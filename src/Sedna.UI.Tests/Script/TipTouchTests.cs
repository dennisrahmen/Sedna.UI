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
        await page.WaitForTimeoutAsync(450);
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

        // The hold is 300ms of real time, so on a loaded machine the round trips between
        // the press and the release can outlast it and turn the tap into a hold. The page
        // clock is paused, so the press lasts exactly as long as the test runs it for.
        await page.Clock.InstallAsync(new() { TimeDate = new DateTime(2030, 1, 1) });
        await page.Clock.PauseAtAsync(new DateTime(2030, 1, 1, 0, 0, 1));

        await Pointer(page, "pointerdown");
        await page.Clock.RunForAsync(120);
        await Pointer(page, "pointerup");
        // Well past the hold: the release has to have cancelled it, not merely beaten it.
        await page.Clock.RunForAsync(1000);

        // Nothing from the hold path: the press was too short. (Focus after the click
        // below may show the hint, as it does for a mouse — that is the focus path.)
        var visible = await page.EvaluateAsync<bool>(
            "() => !!document.querySelector('.sedna-tip--visible')");
        Assert.False(visible, "A tap showed a hint; only a hold should.");

        await page.Locator("#b").ClickAsync();
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.clicks || 0"));
    }

    [Fact]
    public async Task Switched_off_no_hint_shows_and_the_one_showing_goes()
    {
        if (NoBrowser) return;
        // The C# form of the gate: a boolean, because a predicate does not cross into C#.
        var page = await Open(Body);

        await Pointer(page, "pointerdown");
        await page.WaitForTimeoutAsync(450);
        await Assertions.Expect(page.Locator(".sedna-tip")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));

        await page.EvaluateAsync("() => sednaUi.tips.setEnabled(false)");
        Assert.False(await page.EvaluateAsync<bool>("() => !!document.querySelector('.sedna-tip--visible')"));

        await Pointer(page, "pointerup");
        await page.Locator("#b").FocusAsync();
        await page.WaitForTimeoutAsync(300);
        Assert.False(await page.EvaluateAsync<bool>("() => !!document.querySelector('.sedna-tip--visible')"),
            "A hint showed while hints were switched off.");
    }
}
