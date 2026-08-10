using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Toasts: one reused stack, aria-live chosen by kind, and a message inserted as text.
/// </summary>
public class ToastTests : ScriptTestBase
{
    [Fact]
    public async Task Toast_creates_and_reuses_one_stack_and_removes_it_when_empty()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        var result = await page.EvaluateAsync<JsonElement>("""
            () => {
                const remove1 = sednaUi.toast('First', { timeout: 0 });
                const afterFirst = document.querySelectorAll('.toast-stack').length;
                sednaUi.toast('Second', { timeout: 0 });
                const afterSecond = document.querySelectorAll('.toast-stack').length;
                const toasts = document.querySelectorAll('.toast').length;
                const role = document.querySelector('.toast-stack').getAttribute('role');
                remove1();
                const left = document.querySelectorAll('.toast').length;
                return { afterFirst, afterSecond, toasts, role, left,
                         stackGone: !document.querySelector('.toast-stack') };
            }
            """);

        Assert.Equal(1, result.GetProperty("afterFirst").GetInt32());
        Assert.Equal(1, result.GetProperty("afterSecond").GetInt32());   // reused, not a second stack
        Assert.Equal(2, result.GetProperty("toasts").GetInt32());
        Assert.Equal("status", result.GetProperty("role").GetString());
        Assert.Equal(1, result.GetProperty("left").GetInt32());          // the returned remover works
        Assert.False(result.GetProperty("stackGone").GetBoolean());       // one toast left, stack stays
    }

    [Fact]
    public async Task Toast_cuts_in_for_a_failure_and_waits_its_turn_for_a_success()
    {
        if (NoBrowser) return;
        // A failure is worth interrupting a screen reader for; a confirmation is not.
        var page = await Open("<div></div>");

        var polite = await page.EvaluateAsync<string>(
            "() => { sednaUi.toast('Saved', { kind: 'go', timeout: 0 }); "
            + "return document.querySelector('.toast-stack').getAttribute('aria-live'); }");
        Assert.Equal("polite", polite);

        var assertive = await page.EvaluateAsync<string>(
            "() => { sednaUi.toast('Transfer failed', { kind: 'danger', timeout: 0 }); "
            + "return document.querySelector('.toast-stack').getAttribute('aria-live'); }");
        Assert.Equal("assertive", assertive);
    }

    [Fact]
    public async Task Toast_inserts_its_message_as_text_so_a_server_value_cannot_execute()
    {
        if (NoBrowser) return;
        // The message routinely carries a value from the server, and this is the one
        // place an app hands the library one.
        var page = await Open("<div></div>");

        var probe = await page.EvaluateAsync<JsonElement>("""
            () => {
                window.__pwned = false;
                sednaUi.toast('<img src=x onerror="window.__pwned=true">', { timeout: 0 });
                const body = document.querySelector('.toast-body');
                return { pwned: window.__pwned, imgs: body.querySelectorAll('img').length,
                         text: body.textContent };
            }
            """);

        Assert.False(probe.GetProperty("pwned").GetBoolean());
        Assert.Equal(0, probe.GetProperty("imgs").GetInt32());
        Assert.Contains("<img", probe.GetProperty("text").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Toast_leaves_a_stack_the_app_wrote_alone()
    {
        if (NoBrowser) return;
        // The stack used to be found by `.toast-stack`, which matched anything the app
        // had written for its own reasons — a server-rendered stack, or the markup shown
        // on a documentation page. Toasts then appeared inside it, wherever on the page
        // that was; its aria-live was overwritten; and dismissing the last one DELETED
        // the app's own element. The library does not remove markup it did not create.
        var page = await Open(
            """
            <div id="host">
              <div class="toast-stack" id="theirs" aria-live="off">
                <div class="toast toast-info" id="mine"><span class="toast-body">Rendered by the app</span></div>
              </div>
            </div>
            """);

        var probe = await page.EvaluateAsync<JsonElement>("""
            () => {
                const theirs = document.getElementById('theirs');
                const remove = sednaUi.toast('From the library', { timeout: 0 });
                const ours = document.querySelector('[data-sedna-toasts]');
                const before = {
                    separate: ours !== theirs,
                    theirChildren: theirs.children.length,
                    theirLive: theirs.getAttribute('aria-live'),
                    ourParent: ours.parentElement.tagName,
                };
                remove();
                return { ...before, theirsStillThere: !!document.getElementById('theirs'),
                         theirChildrenAfter: document.getElementById('theirs').children.length,
                         oursGone: !document.querySelector('[data-sedna-toasts]') };
            }
            """);

        Assert.True(probe.GetProperty("separate").GetBoolean(),
            "The library appended to the app's own stack instead of creating its own.");
        Assert.Equal(1, probe.GetProperty("theirChildren").GetInt32());
        Assert.Equal("off", probe.GetProperty("theirLive").GetString());
        Assert.Equal("BODY", probe.GetProperty("ourParent").GetString());

        Assert.True(probe.GetProperty("theirsStillThere").GetBoolean(),
            "Dismissing a toast deleted the app's own stack.");
        Assert.Equal(1, probe.GetProperty("theirChildrenAfter").GetInt32());
        Assert.True(probe.GetProperty("oursGone").GetBoolean(),
            "The library's own stack should go when its last toast does.");
    }

    [Fact]
    public async Task Toast_with_a_zero_timeout_stays_until_it_is_dismissed()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        await page.EvaluateAsync("() => sednaUi.toast('Read me', { timeout: 0 })");
        // Well past the 4000ms default, so a default-timeout regression fails here.
        await page.WaitForTimeoutAsync(4300);
        Assert.Equal(1, await page.Locator(".toast").CountAsync());

        await page.Locator(".toast-close").ClickAsync();
        Assert.Equal(0, await page.Locator(".toast").CountAsync());
    }

    // ── confirm ─────────────────────────────────────────────────────────────
}
