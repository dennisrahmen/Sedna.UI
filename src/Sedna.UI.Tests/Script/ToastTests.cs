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

        Assert.Equal(["true", "true", "toast toast--go", "Uploaded"], result);
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
                <div class="toast toast--info" id="mine"><span class="toast-body">Rendered by the app</span></div>
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
    public async Task The_live_region_is_in_the_page_before_its_first_toast_shows()
    {
        if (NoBrowser) return;
        // A region created with its content already in it is read by many screen readers
        // as nothing, so the first toast of a new stack comes a moment after the stack.
        var page = await Open("<p>Orders</p>");

        var first = await page.EvaluateAsync<JsonElement>("""
            () => {
                sednaUi.toast('Saved', { timeout: 0 });
                const stack = document.querySelector('[data-sedna-toasts]');
                return { region: stack && stack.getAttribute('aria-live'), shown: !stack.querySelector('.toast').hidden };
            }
            """);
        Assert.Equal("polite", first.GetProperty("region").GetString());
        Assert.False(first.GetProperty("shown").GetBoolean(), "The first toast went in with its region.");

        await page.WaitForFunctionAsync("() => !document.querySelector('[data-sedna-toasts] .toast').hidden");

        // A second toast into a region that is already there goes in at once.
        Assert.True(await page.EvaluateAsync<bool>("""
            () => { sednaUi.toast('Saved again', { timeout: 0 }); return !document.querySelectorAll('[data-sedna-toasts] .toast')[1].hidden; }
            """));
    }

    [Fact]
    public async Task A_toast_waits_while_the_pointer_is_on_it()
    {
        if (NoBrowser) return;
        var page = await Open("<p>Orders</p>", clockPausedAt: new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc));

        await Js.Run(page, "() => sednaUi.toast('Saved', { timeout: 4000 })");
        await page.Clock.RunForAsync(200);
        await page.Locator(".toast").HoverAsync();
        await page.Clock.RunForAsync(10000);
        Assert.Equal(1, await page.Locator(".toast").CountAsync());

        await page.Mouse.MoveAsync(0, 0);
        await page.Clock.RunForAsync(5000);
        Assert.Equal(0, await page.Locator(".toast").CountAsync());
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

        await Js.Run(page, "() => document.getElementById('dlg').showModal()");
        await Js.Run(page, "() => sednaUi.toast('Saving failed', { kind: 'danger', timeout: 0 })");
        await page.WaitForFunctionAsync("() => { const t = document.querySelector('[data-sedna-toasts] .toast'); return t && !t.hidden; }");

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
        await page.Locator(".toast-close").ClickAsync();
        Assert.Equal(0, await page.Locator(".toast").CountAsync());

        // A toast outliving the dialog goes back to <body>, still above the page.
        await Js.Run(page, "() => sednaUi.toast('Still here', { timeout: 0 })");
        await Js.Run(page, "() => document.getElementById('dlg').close()");
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

        await Js.Run(page, "() => document.getElementById('dlg').showModal()");
        await Js.Run(page, "() => sednaUi.toast('Saved with warnings', { timeout: 0 })");
        await Js.Run(page, "() => document.getElementById('dlg').remove()");

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

        await Js.Run(page, "() => sednaUi.toast('Uploading…', { timeout: 0 })");
        await Js.Run(page, "() => document.getElementById('dlg').showModal()");

        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-sedna-toasts]').parentElement.id === 'dlg'");
        await page.Locator(".toast-close").ClickAsync();
        Assert.Equal(0, await page.Locator(".toast").CountAsync());
    }

    [Fact]
    public async Task Toast_with_a_zero_timeout_stays_until_it_is_dismissed()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>", clockPausedAt: new DateTime(2030, 1, 1));

        await Js.Run(page, "() => sednaUi.toast('Read me', { timeout: 0 })");
        // Well past the 4000ms default, so a default-timeout regression fails here.
        await page.Clock.RunForAsync(4300);
        Assert.Equal(1, await page.Locator(".toast").CountAsync());

        await page.Locator(".toast-close").ClickAsync();
        Assert.Equal(0, await page.Locator(".toast").CountAsync());
    }

    // ── The pile, the limit and the swipe ────────────────────────────────────────

    /// <summary>The stylesheet, with transitions off so a measurement reads where a toast lands.</summary>
    private Task<IPage> OpenStack(string dir = "ltr", DateTime? clockPausedAt = null) =>
        Open($"<p>Orders</p><script>document.documentElement.dir = '{dir}';</script>", head: StylesheetTag,
            reducedMotion: ReducedMotion.Reduce, clockPausedAt: clockPausedAt);

    private static Task ShowAll(IPage page) => page.WaitForFunctionAsync(
        "() => [...document.querySelectorAll('[data-sedna-toasts] > .toast')].every(t => !t.hidden)");

    private static Task<double[][]> Boxes(IPage page) => page.EvaluateAsync<double[][]>("""
        () => [...document.querySelectorAll('[data-sedna-toasts] > .toast')].map(t => {
            const r = t.getBoundingClientRect();
            return [r.top, r.bottom, r.left, r.right];
        })
        """);

    [Fact]
    public async Task A_pile_puts_the_newest_in_front_and_opens_under_the_pointer()
    {
        if (NoBrowser) return;
        var page = await OpenStack();

        await Js.Run(page, """
            () => {
                sednaUi.configure({ toastPile: true });
                sednaUi.toast('ORD-4182 is on its way.', { title: 'Order dispatched', timeout: 0 });
                sednaUi.toast('Two of five lines are awaiting stock, and the rest is packed and waiting at the dock.', { title: 'Partially fulfilled', timeout: 0 });
                sednaUi.toast('ORD-4209 is on its way.', { title: 'Order dispatched', timeout: 0 });
            }
            """);
        await ShowAll(page);
        Assert.True(await page.EvaluateAsync<bool>(
            "() => document.querySelector('[data-sedna-toasts]').classList.contains('toast-stack--pile')"));

        // Piled: the newest in front, each one before it peeking out above it and no taller
        // than it, so a long toast behind a short one does not stand out of the pile.
        var piled = await Boxes(page);
        var front = piled[2];
        var stack = await page.Locator("[data-sedna-toasts]").BoundingBoxAsync();
        Assert.True(Math.Abs(stack!.Height - (front[1] - front[0])) < 1,
            $"A pile is as tall as its front toast; the stack is {stack.Height}px for a {front[1] - front[0]}px toast.");
        Assert.True(piled[1][0] < front[0] - 2 && piled[1][1] < front[1], "The second toast does not peek out above the front one.");
        Assert.True(piled[0][0] < piled[1][0] - 2, "The third toast does not peek out above the second.");
        Assert.True(piled[1][1] - piled[1][0] <= front[1] - front[0] + 1, "A toast behind the front one is taller than it.");

        // Opened: the plain column, each toast in its own place, the newest still nearest the corner.
        await page.Locator("[data-sedna-toasts] > .toast").Last.HoverAsync();
        var open = await Boxes(page);
        Assert.True(open[0][1] <= open[1][0] + 0.5 && open[1][1] <= open[2][0] + 0.5,
            "Opened, the toasts still overlap: " + string.Join(" | ", open.Select(b => $"{b[0]:0}-{b[1]:0}")));
        Assert.True(Math.Abs(open[2][1] - front[1]) < 1, "Opening the pile moved the front toast out from under the pointer.");

        // The pointer gone, it piles up again.
        await page.Mouse.MoveAsync(0, 0);
        var again = await Boxes(page);
        Assert.True(again[1][0] < again[2][0] && again[1][1] < again[2][1], "The pile did not close again.");
    }

    [Fact]
    public async Task An_open_pile_holds_every_toast_in_it()
    {
        if (NoBrowser) return;
        // Opened out, the pile is being read: a toast the pointer is not on must not go from
        // under the reader while they read it.
        var page = await OpenStack(clockPausedAt: new DateTime(2030, 1, 1));

        await Js.Run(page, """
            () => {
                sednaUi.configure({ toastPile: true });
                ['First', 'Second', 'Third'].forEach(m => sednaUi.toast(m, { timeout: 4000 }));
            }
            """);
        await page.Clock.RunForAsync(200);
        await page.Locator("[data-sedna-toasts] > .toast").Last.HoverAsync();
        await page.Clock.RunForAsync(10000);
        Assert.Equal(3, await page.Locator(".toast").CountAsync());

        await page.Mouse.MoveAsync(0, 0);
        await page.Clock.RunForAsync(6000);
        Assert.Equal(0, await page.Locator(".toast").CountAsync());
    }

    [Fact]
    public async Task The_limit_shows_the_newest_and_the_rest_wait_their_turn()
    {
        if (NoBrowser) return;
        var page = await OpenStack(clockPausedAt: new DateTime(2030, 1, 1));

        await Js.Run(page, """
            () => {
                sednaUi.configure({ toastLimit: 2 });
                ['First', 'Second', 'Third', 'Fourth'].forEach(m => sednaUi.toast(m, { timeout: 4000 }));
            }
            """);
        await page.Clock.RunForAsync(200);

        const string Showing = """
            () => [...document.querySelectorAll('[data-sedna-toasts] > .toast')]
                .filter(t => getComputedStyle(t).display !== 'none')
                .map(t => t.querySelector('.toast-body').textContent)
            """;
        Assert.Equal(["Third", "Fourth"], await page.EvaluateAsync<string[]>(Showing));
        Assert.Equal(2, await page.Locator("[data-toast-queued]").CountAsync());

        // The two in front time out; the two that waited show, with their whole time still
        // ahead of them — they were never on screen to be read.
        await page.Clock.RunForAsync(4000);
        Assert.Equal(["First", "Second"], await page.EvaluateAsync<string[]>(Showing));
        Assert.Equal(0, await page.Locator("[data-toast-queued]").CountAsync());
        await page.Clock.RunForAsync(3000);
        Assert.Equal(2, await page.Locator(".toast").CountAsync());
        await page.Clock.RunForAsync(1500);
        Assert.Equal(0, await page.Locator(".toast").CountAsync());
    }

    /// <summary>Drags from the middle of the newest toast by <paramref name="dx"/> pixels.</summary>
    private static async Task Swipe(IPage page, double dx)
    {
        var box = (await page.Locator("[data-sedna-toasts] > .toast").Last.BoundingBoxAsync())!;
        var (x, y) = (box.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Mouse.MoveAsync((float)x, (float)y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(x + dx), (float)y, new() { Steps = 10 });
        await page.Mouse.UpAsync();
    }

    [Theory]
    [InlineData("ltr", 200)]
    [InlineData("rtl", -200)]
    public async Task A_swipe_toward_the_stacks_edge_dismisses_a_toast(string dir, double toward)
    {
        if (NoBrowser) return;
        var page = await OpenStack(dir);

        await Js.Run(page, """
            () => { const id = sednaUi.toast.show('Saved', { timeout: 0 }); sednaUi.toast.result(id).then(v => { window.__ended = v; }); }
            """);
        await ShowAll(page);

        // The other way, it gives a little and springs back.
        await Swipe(page, -toward);
        Assert.Equal(1, await page.Locator(".toast").CountAsync());
        Assert.False(await page.EvaluateAsync<bool>("() => document.querySelector('.toast').hasAttribute('data-toast-swiping')"));

        // A short swipe the right way springs back too, to where it was.
        var before = await Boxes(page);
        await Swipe(page, toward / 10);
        Assert.Equal(1, await page.Locator(".toast").CountAsync());
        var after = await Boxes(page);
        Assert.True(Math.Abs(after[0][2] - before[0][2]) < 0.5, $"A short swipe left the toast {after[0][2] - before[0][2]}px out of place.");

        // Far enough, and it goes as its close button would take it.
        await Swipe(page, toward);
        Assert.Equal(0, await page.Locator(".toast").CountAsync());
        await page.WaitForFunctionAsync("() => window.__ended === null");
    }

    [Fact]
    public async Task A_swipe_that_springs_back_does_not_follow_the_link_it_started_on()
    {
        if (NoBrowser) return;
        // A notification card's body is a link. Dragging it sideways and letting go short of
        // dismissing it is not a click on it.
        var page = await Open(
            """
            <p>Orders</p>
            <template id="card">
              <div class="toast toast--info">
                <a class="toast-body" data-href data-toast-value="open"><strong data-title></strong></a>
                <button class="toast-close" type="button" aria-label="Dismiss">x</button>
              </div>
            </template>
            """, head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);
        await Js.Run(page, """
            () => { const id = sednaUi.toast.present('card', { title: 'Assigned to you', href: '#followed' }, { timeout: 0 }); sednaUi.toast.result(id).then(v => { window.__ended = v; }); }
            """);
        await ShowAll(page);

        await Swipe(page, 20);
        await Js.Frames(page);
        Assert.Equal(1, await page.Locator(".toast").CountAsync());
        Assert.Equal("", await page.EvaluateAsync<string>("() => location.hash"));
        Assert.True(await page.EvaluateAsync<bool>("() => window.__ended === undefined"), "The swipe ended the toast with a value.");

        // A real click still opens it.
        await page.Locator(".toast-body").ClickAsync();
        await page.WaitForFunctionAsync("() => window.__ended === 'open'");
    }

    [Fact]
    public async Task A_toast_that_cannot_be_closed_cannot_be_swiped_away()
    {
        if (NoBrowser) return;
        // "Uploading…" with no close button is waiting for its outcome; a swipe is a close.
        var page = await OpenStack();
        await Js.Run(page, "() => sednaUi.toast('Uploading…', { timeout: 0, dismissible: false })");
        await ShowAll(page);

        await Swipe(page, 200);
        Assert.Equal(1, await page.Locator(".toast").CountAsync());
    }

    [Fact]
    public async Task The_swipe_moves_the_toast_with_the_pointer()
    {
        if (NoBrowser) return;
        var page = await OpenStack();
        await Js.Run(page, "() => sednaUi.toast('Saved', { timeout: 0 })");
        await ShowAll(page);

        var box = (await page.Locator(".toast").BoundingBoxAsync())!;
        var (x, y) = ((float)(box.X + box.Width / 2), (float)(box.Y + box.Height / 2));
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 60, y, new() { Steps = 6 });

        var moved = (await page.Locator(".toast").BoundingBoxAsync())!;
        Assert.True(moved.X > box.X + 30, $"Mid-swipe the toast is at {moved.X}, it started at {box.X}.");
        Assert.True(await page.EvaluateAsync<bool>("() => document.querySelector('.toast').hasAttribute('data-toast-swiping')"));
        await page.Mouse.UpAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_pile_and_the_spring_back_move_at_once_with_reduced_motion(bool reduce)
    {
        if (NoBrowser) return;
        var page = await Open("<p>Orders</p>", head: StylesheetTag,
            reducedMotion: reduce ? ReducedMotion.Reduce : ReducedMotion.NoPreference);
        await Js.Run(page, """
            () => {
                sednaUi.configure({ toastPile: true });
                sednaUi.toast('First', { timeout: 0 });
                sednaUi.toast('Second', { timeout: 0 });
            }
            """);

        var durations = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('[data-sedna-toasts] > .toast')].map(t => getComputedStyle(t).transitionDuration)
            """);
        Assert.NotEmpty(durations);
        foreach (var d in durations)
        {
            var moving = d.Split(',').Any(v => double.Parse(v.Trim().TrimEnd('s'), System.Globalization.CultureInfo.InvariantCulture) > 0);
            Assert.True(moving != reduce, reduce
                ? $"With reduced motion a toast still transitions: {d}."
                : $"Without reduced motion the pile has no transition at all: {d}; the measurement is not reading the rule.");
        }
    }
}
