using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A panel hanging off a trigger closes when a render removes the trigger, as a hint does
/// (<see cref="TipLayerTests"/>): what pointed at nothing used to stay open.
/// </summary>
public class TriggerLifecycleTests : ScriptTestBase
{
    [Fact]
    public async Task A_menu_closes_when_its_toggle_is_removed()
    {
        if (NoBrowser) return;
        var page = await Open("""
            <div class="menu-anchor">
                <button type="button" id="t" data-menu-toggle aria-expanded="false">Actions</button>
                <div class="menu" id="m" hidden><button class="menu-item" type="button">Copy</button></div>
            </div>
            """);

        await page.Locator("#t").ClickAsync();
        await Assertions.Expect(page.Locator("#m")).ToBeVisibleAsync();

        await Js.Run(page, "() => document.getElementById('t').remove()");

        await Assertions.Expect(page.Locator("#m")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task A_popover_closes_when_its_invoker_is_removed()
    {
        if (NoBrowser) return;
        var page = await Open("""
            <button type="button" id="t" popovertarget="p">Details</button>
            <div class="popover" id="p" popover>Three letters wait to be sent.</div>
            """);

        await page.Locator("#t").ClickAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('p').matches(':popover-open')");

        await Js.Run(page, "() => document.getElementById('t').remove()");

        await page.WaitForFunctionAsync("() => !document.getElementById('p').matches(':popover-open')");
    }
}
