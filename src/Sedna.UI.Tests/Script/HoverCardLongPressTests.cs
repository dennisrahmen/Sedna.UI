using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A hover card on touch: a long press on its trigger opens it pinned without following the
/// trigger's link or running its click, a tap is unchanged, and a finger that moves is a scroll.
/// </summary>
public class HoverCardLongPressTests : ScriptTestBase
{
    private const string Body = """
        <a href="#followed" id="link" data-popover="order-card" data-popover-hover>ORD-4209</a>
        <div class="popover" id="order-card" popover>
            <strong class="popover-title">ORD-4209</strong>
            <p>Waiting on Northwind Retail since Tuesday.</p>
        </div>
        <button type="button" id="costs" data-popover="costs-card" data-popover-hover="only"
                onclick="window.opened = (window.opened || 0) + 1">Hosting costs</button>
        <div class="popover" id="costs-card" popover><p>41.20 EUR so far</p></div>
        """;

    private static Task Touch(IPage page, string id, string type, int dx = 0) =>
        Js.Run(page, $$"""
            () => {
                const el = document.getElementById('{{id}}');
                const r = el.getBoundingClientRect();
                el.dispatchEvent(new PointerEvent('{{type}}', {
                    pointerType: 'touch', pointerId: 3, isPrimary: true, bubbles: true, cancelable: true,
                    clientX: r.left + 4 + {{dx}}, clientY: r.top + 4 }));
            }
            """);

    private static Task<bool> IsOpen(IPage page, string id) =>
        page.EvaluateAsync<bool>("id => document.getElementById(id).matches(':popover-open')", id);

    [Fact]
    public async Task A_long_press_opens_the_card_and_the_link_is_not_followed()
    {
        if (NoBrowser) return;
        // The page clock is paused, so the press lasts exactly as long as the test runs it for.
        var page = await Open(Body, head: StylesheetTag, clockPausedAt: new DateTime(2030, 1, 1));

        await Touch(page, "link", "pointerdown");
        await page.Clock.RunForAsync(450);
        Assert.False(await IsOpen(page, "order-card"), "The card opened before the press was long.");
        await page.Clock.RunForAsync(100);
        Assert.True(await IsOpen(page, "order-card"));
        Assert.Equal("true", await page.Locator("#link").GetAttributeAsync("aria-expanded"));

        // Lifting the finger fires the platform's click; the press has already acted.
        await Touch(page, "link", "pointerup");
        await page.Locator("#link").DispatchEventAsync("click");
        Assert.Equal("", await page.EvaluateAsync<string>("() => location.hash"));
        Assert.True(await IsOpen(page, "order-card"));

        // The platform's own long-press menu would cover the card.
        var refused = await page.EvaluateAsync<bool>("""
            () => {
                const e = new MouseEvent('contextmenu', { bubbles: true, cancelable: true });
                document.getElementById('link').dispatchEvent(e);
                return e.defaultPrevented;
            }
            """);
        Assert.False(refused, "A contextmenu after the press was taken was refused too.");
    }

    [Fact]
    public async Task A_long_press_on_a_trigger_with_its_own_click_does_not_run_it()
    {
        if (NoBrowser) return;
        var page = await Open(Body, head: StylesheetTag, clockPausedAt: new DateTime(2030, 1, 1));

        await Touch(page, "costs", "pointerdown");
        // Refused while the finger is down: Android raises it before the release.
        var refused = await page.EvaluateAsync<bool>("""
            () => {
                const e = new MouseEvent('contextmenu', { bubbles: true, cancelable: true });
                document.getElementById('costs').dispatchEvent(e);
                return e.defaultPrevented;
            }
            """);
        Assert.True(refused);
        await page.Clock.RunForAsync(500);
        Assert.True(await IsOpen(page, "costs-card"));

        await Touch(page, "costs", "pointerup");
        await page.Locator("#costs").DispatchEventAsync("click");
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.opened || 0"));
        Assert.True(await IsOpen(page, "costs-card"));

        // The next tap is a tap again: the trigger's own action runs.
        await Touch(page, "costs", "pointerdown");
        await page.Clock.RunForAsync(80);
        await Touch(page, "costs", "pointerup");
        await page.Locator("#costs").DispatchEventAsync("click");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.opened || 0"));
    }

    [Fact]
    public async Task A_tap_or_a_finger_that_moves_opens_nothing_by_holding()
    {
        if (NoBrowser) return;
        var page = await Open(Body, head: StylesheetTag, clockPausedAt: new DateTime(2030, 1, 1));

        // A tap on an "only" trigger: too short to be a press, and the hover path ignores a finger.
        await Touch(page, "costs", "pointerover");
        await Touch(page, "costs", "pointerdown");
        await page.Clock.RunForAsync(120);
        await Touch(page, "costs", "pointerup");
        await page.Clock.RunForAsync(1000);
        Assert.False(await IsOpen(page, "costs-card"), "A tap opened the card by holding or by hover.");

        // A finger that moves is scrolling, however long it stays down.
        await Touch(page, "link", "pointerdown");
        await page.Clock.RunForAsync(200);
        await Touch(page, "link", "pointermove", dx: 30);
        await page.Clock.RunForAsync(800);
        Assert.False(await IsOpen(page, "order-card"), "A finger that moved opened the card.");
        await Touch(page, "link", "pointercancel");
    }
}
