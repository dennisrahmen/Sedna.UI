using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Toasts: one reused stack, aria-live chosen by kind, a message inserted as text, and an id
/// that C# can hold to dismiss or replace one.
/// </summary>
public class ToastTests : ScriptTestBase
{
    [Fact]
    public async Task Replace_changes_a_toast_in_place_and_keeps_its_position()
    {
        if (NoBrowser) return;
        // "Uploading…" turning into "Uploaded" must not leave and come back: same element,
        // same place in the stack, new kind and message.
        var page = await Open("<div></div>");

        var result = await page.EvaluateAsync<string[]>("""
            () => {
                const first = sednaUi.toast.show('Uploading…', { timeout: 0 });
                sednaUi.toast.show('Another', { timeout: 0 });
                const el = document.querySelector('.toast');
                const same = sednaUi.toast.replace(first, 'Uploaded', { kind: 'go', timeout: 0 });
                const now = document.querySelector('.toast');
                return [String(same === first), String(now === el), now.className,
                        now.querySelector('.toast-body').textContent];
            }
            """);

        Assert.Equal(["true", "true", "toast toast-go", "Uploaded"], result);
    }

    [Fact]
    public async Task A_gone_toast_is_not_an_error_and_replacing_it_shows_a_new_one()
    {
        if (NoBrowser) return;
        // The work can finish after the toast timed out or the reader closed it. Its outcome
        // still has to reach them.
        var page = await Open("<div></div>");

        var result = await page.EvaluateAsync<string[]>("""
            () => {
                const id = sednaUi.toast.show('Uploading…', { timeout: 0 });
                const dismissed = sednaUi.toast.dismiss(id);
                const again = sednaUi.toast.dismiss(id);
                const next = sednaUi.toast.replace(id, 'Uploaded', { timeout: 0 });
                return [String(dismissed), String(again), String(next !== id),
                        document.querySelector('.toast-body').textContent];
            }
            """);

        Assert.Equal(["true", "false", "true", "Uploaded"], result);
    }

    [Fact]
    public async Task The_close_buttons_name_is_configured_once_and_can_be_overridden()
    {
        if (NoBrowser) return;
        // The one word the library writes itself, so it has to be the app's language.
        var page = await Open("<div></div>");

        var labels = await page.EvaluateAsync<string[]>("""
            () => {
                const before = (sednaUi.toast('a', { timeout: 0 }), document.querySelector('.toast-close').getAttribute('aria-label'));
                sednaUi.configure({ toastDismissLabel: 'Schließen' });
                sednaUi.toast('b', { timeout: 0 });
                sednaUi.toast('c', { timeout: 0, dismissLabel: 'Ausblenden' });
                const all = [...document.querySelectorAll('.toast-close')].map(b => b.getAttribute('aria-label'));
                return [before, all[1], all[2]];
            }
            """);

        Assert.Equal(["Dismiss", "Schließen", "Ausblenden"], labels);
    }

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
    public async Task A_toast_shown_over_an_open_modal_is_visible_reachable_and_goes_home_after()
    {
        if (NoBrowser) return;
        // showModal() puts the dialog in the top layer, above any z-index, and makes the
        // rest of the page inert. A failed save the dialog itself reports used to paint
        // under its backdrop, where nobody saw it and nothing announced it.
        var page = await Open(
            """
            <dialog class="modal" id="dlg"><p>Editing</p><button type="button" id="save">Save</button></dialog>
            """);

        await page.EvaluateAsync("() => document.getElementById('dlg').showModal()");
        await page.EvaluateAsync("() => sednaUi.toast('Saving failed', { kind: 'danger', timeout: 0 })");

        var probe = await page.EvaluateAsync<JsonElement>("""
            () => {
                const stack = document.querySelector('[data-sedna-toasts]');
                const toast = stack.querySelector('.toast');
                const r = toast.getBoundingClientRect();
                const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
                return { open: stack.matches(':popover-open'),
                         inDialog: stack.parentElement.id === 'dlg',
                         onTop: toast.contains(hit) };
            }
            """);

        Assert.True(probe.GetProperty("open").GetBoolean(), "The stack is not in the top layer.");
        Assert.True(probe.GetProperty("inDialog").GetBoolean(),
            "The stack was left outside the modal, where it is inert and never announced.");
        Assert.True(probe.GetProperty("onTop").GetBoolean(), "The toast is painted under the dialog.");

        // Reachable, not merely painted: an inert close button would not take the click.
        await page.Locator(".toast-close").ClickAsync(new() { Timeout = 2000 });
        Assert.Equal(0, await page.Locator(".toast").CountAsync());

        // A toast outliving the dialog goes back to <body>, still above the page.
        await page.EvaluateAsync("() => sednaUi.toast('Still here', { timeout: 0 })");
        await page.EvaluateAsync("() => document.getElementById('dlg').close()");
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-sedna-toasts]').parentElement === document.body");
        Assert.True(await page.EvaluateAsync<bool>(
            "() => document.querySelector('[data-sedna-toasts]').matches(':popover-open')"));
    }

    [Fact]
    public async Task A_toast_in_a_dialog_the_app_removes_is_kept()
    {
        if (NoBrowser) return;
        // A framework removes a dialog as readily as it closes one, and removal fires no
        // close event. The toasts the stack carried in with it must not go too.
        var page = await Open("""<dialog class="modal" id="dlg"><p>Editing</p></dialog>""");

        await page.EvaluateAsync("() => document.getElementById('dlg').showModal()");
        await page.EvaluateAsync("() => sednaUi.toast('Saved with warnings', { timeout: 0 })");
        await page.EvaluateAsync("() => document.getElementById('dlg').remove()");

        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-sedna-toasts]')?.parentElement === document.body");
        Assert.Equal("Saved with warnings", await page.Locator(".toast-body").TextContentAsync());
    }

    [Fact]
    public async Task A_modal_opened_after_a_toast_takes_the_stack_with_it()
    {
        if (NoBrowser) return;
        // The top layer orders by promotion: a dialog opened later covers a stack shown
        // earlier, unless the stack follows it.
        var page = await Open("""<dialog class="modal" id="dlg"><p>Editing</p></dialog>""");

        await page.EvaluateAsync("() => sednaUi.toast('Uploading…', { timeout: 0 })");
        await page.EvaluateAsync("() => document.getElementById('dlg').showModal()");

        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-sedna-toasts]').parentElement.id === 'dlg'");
        await page.Locator(".toast-close").ClickAsync(new() { Timeout = 2000 });
        Assert.Equal(0, await page.Locator(".toast").CountAsync());
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
}
