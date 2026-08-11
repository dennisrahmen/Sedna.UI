using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// confirm() settles on every route a user can take, not only on the dialog's close event — which a non-compositing tab never dispatches.
/// </summary>
public class ConfirmTests : ScriptTestBase
{
    [Theory]
    [InlineData(".btn-primary", true)]     // Confirm
    [InlineData(".btn", false)]            // Cancel — first .btn in the footer
    public async Task Confirm_settles_from_the_button_the_user_pressed(string selector, bool expected)
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        // The promise is parked on window so the click and the await are separate
        // steps — awaiting the evaluate call itself would deadlock on the dialog.
        await page.EvaluateAsync(
            "() => { window.__answer = sednaUi.confirm({ title: 'Delete?' }); }");
        await page.Locator($"dialog.modal .modal-footer {selector}").First.ClickAsync();

        Assert.Equal(expected, await page.EvaluateAsync<bool>("() => window.__answer"));
        // The dialog is removed, not just closed — otherwise they accumulate.
        Assert.Equal(0, await page.Locator("dialog.modal").CountAsync());
    }

    [Fact]
    public async Task Confirm_settles_when_escape_is_pressed()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        await page.EvaluateAsync("() => { window.__answer = sednaUi.confirm({ title: 'Delete?' }); }");
        await page.Locator("dialog.modal").WaitForAsync();
        await page.Keyboard.PressAsync("Escape");

        Assert.False(await page.EvaluateAsync<bool>("() => window.__answer"));
        Assert.Equal(0, await page.Locator("dialog.modal").CountAsync());
    }

    [Fact]
    public async Task Confirm_settles_from_the_cancel_route_alone_and_not_only_from_close()
    {
        if (NoBrowser) return;
        // THE defect this guards. Resolution used to hang off the `close` event alone,
        // and a non-compositing tab applies close() without ever dispatching the queued
        // event — so `await confirm()` never returned and, in a Blazor handler, the
        // action silently stopped working.
        //
        // Pressing Escape cannot prove the fix: it fires `cancel` AND `close`, so the
        // test passes with the cancel route removed. Verified — gutting the cancel
        // listener leaves the Escape test above green. Dispatching `cancel` on its own
        // is what isolates the route: an untrusted event runs no default action, so the
        // dialog does not close and `close` never arrives to cover for it.
        var page = await Open("<div></div>");

        await page.EvaluateAsync("() => { window.__answer = sednaUi.confirm({ title: 'Delete?' }); }");
        await page.Locator("dialog.modal").WaitForAsync();

        // Raced against a timer so a hung promise fails in a second with a readable
        // message, instead of stalling until Playwright's default timeout.
        var outcome = await page.EvaluateAsync<string>("""
            () => {
                document.querySelector('dialog.modal').dispatchEvent(new Event('cancel'));
                return Promise.race([
                    window.__answer.then(v => 'settled:' + v),
                    new Promise(r => setTimeout(() => r('never settled'), 1000))
                ]);
            }
            """);

        Assert.Equal("settled:false", outcome);
    }

    [Fact]
    public async Task Confirm_focuses_the_safe_choice_for_a_destructive_action()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        await page.EvaluateAsync(
            "() => { window.__answer = sednaUi.confirm({ title: 'Delete?', danger: true }); }");
        await page.Locator("dialog.modal").WaitForAsync();

        var focused = await page.EvaluateAsync<string>("() => document.activeElement.textContent");
        Assert.Equal("Cancel", focused);
        // And the confirm button carries the destructive styling.
        Assert.Equal(1, await page.Locator("dialog.modal .btn-danger").CountAsync());
    }

    // ── delegated menus ─────────────────────────────────────────────────────
}
