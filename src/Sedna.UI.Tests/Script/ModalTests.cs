using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.modal</c> presents an app's own <c>&lt;dialog&gt;</c> and resolves with its
/// <c>returnValue</c> — the one capability a Blazor app could not reach without injecting
/// <c>IJSRuntime</c>, and what a confirmation is built from.
/// </summary>
/// <remarks>
/// <para>
/// The promise settles from the <c>open</c> attribute, never from the <c>close</c> event,
/// because a non-compositing tab applies <c>close()</c> without dispatching the event. Every
/// route out is pinned below: a dialog-method button, Escape, <c>close()</c> from anywhere,
/// and removal from the document.
/// </para>
/// <para>
/// Two older failure modes are pinned too, because both are silent in a source scan:
/// <c>showModal()</c> throws <c>InvalidStateError</c> on a dialog that is already open,
/// which a Blazor re-render calling <c>show()</c> twice reaches routinely; and a wrong id
/// must not throw at all, because an exception crossing the interop boundary from a
/// Blazor handler tears down the circuit rather than surfacing anywhere.
/// </para>
/// </remarks>
public class ModalTests : ScriptTestBase
{
    private const string Fixture = """
        <dialog id="panel" class="modal">
            <div class="modal-header"><h3>New API key</h3></div>
            <div class="modal-body"><p>A form would go here.</p></div>
        </dialog>
        <div id="not-a-dialog"></div>

        <dialog id="ask" class="modal modal-sm">
            <form method="dialog">
                <div class="modal-header"><h3>Delete the queue?</h3></div>
                <div class="modal-footer">
                    <button class="btn" id="keep" value="">Keep it</button>
                    <button class="btn btn-danger" id="delete" value="delete">Delete</button>
                </div>
            </form>
        </dialog>
        """;

    // Parked on window so the click and the await are separate steps — awaiting the
    // evaluate call itself would wait for a reader who is this test.
    private const string ShowAsk = "() => { window.__answer = sednaUi.modal.show('ask'); }";

    // Raced against a timer so a promise that never settles fails in a second with a
    // readable message, instead of stalling until Playwright's default timeout.
    private const string Answer = """
        () => Promise.race([
            window.__answer.then(v => 'settled:' + v),
            new Promise(r => setTimeout(() => r('never settled'), 1000))
        ])
        """;

    [Fact]
    public async Task Show_resolves_with_the_value_of_the_dialog_method_button_pressed()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        await page.EvaluateAsync(ShowAsk);
        await page.Locator("#delete").ClickAsync();

        Assert.Equal("settled:delete", await page.EvaluateAsync<string>(Answer));
    }

    [Fact]
    public async Task Show_resolves_null_for_a_button_with_no_value_and_for_escape()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        await page.EvaluateAsync(ShowAsk);
        await page.Locator("#keep").ClickAsync();
        Assert.Equal("settled:null", await page.EvaluateAsync<string>(Answer));

        // The platform keeps the last returnValue, so show() clears it as the dialog
        // opens — or Escape here would report the previous opening's button.
        await page.EvaluateAsync("() => { document.getElementById('ask').returnValue = 'stale'; }");
        await page.EvaluateAsync(ShowAsk);
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("settled:null", await page.EvaluateAsync<string>(Answer));
    }

    [Fact]
    public async Task Show_settles_without_the_close_event_ever_being_dispatched()
    {
        if (NoBrowser) return;
        // THE defect this guards. A non-compositing tab applies close() and never dispatches
        // the queued `close` event, so a promise hanging off the event never settles. Every
        // `close` event is swallowed here before any listener sees it; the promise must
        // settle anyway.
        var page = await Open(Fixture);

        await page.EvaluateAsync("""
            () => window.addEventListener('close', e => e.stopImmediatePropagation(), true)
            """);
        await page.EvaluateAsync(ShowAsk);
        await page.EvaluateAsync("() => document.getElementById('ask').close('delete')");

        Assert.Equal("settled:delete", await page.EvaluateAsync<string>(Answer));
    }

    [Fact]
    public async Task Show_settles_null_when_the_open_dialog_is_removed_from_the_document()
    {
        if (NoBrowser) return;
        // A page navigated away from under an open dialog must not leave its caller
        // awaiting forever.
        var page = await Open(Fixture);

        await page.EvaluateAsync(ShowAsk);
        await page.EvaluateAsync("() => document.getElementById('ask').remove()");

        Assert.Equal("settled:null", await page.EvaluateAsync<string>(Answer));
    }

    [Fact]
    public async Task A_second_show_on_an_open_dialog_joins_the_first_wait()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        var both = await page.EvaluateAsync<string>("""
            async () => {
                const first = sednaUi.modal.show('ask');
                const second = sednaUi.modal.show('ask');
                sednaUi.modal.close('ask', 'delete');
                return (first === second) + ':' + (await first) + ':' + (await second);
            }
            """);

        Assert.Equal("true:delete:delete", both);
    }

    [Fact]
    public async Task Idle_waits_for_a_closing_drawer_to_finish_sliding_out()
    {
        if (NoBrowser) return;
        // The fixture carries no stylesheet, so the drawer's closing slide is restated
        // here in the shape 63-drawer.css gives it.
        var page = await Open("""
            <style>
                dialog { transition: transform 180ms ease, display 180ms allow-discrete, overlay 180ms allow-discrete; transform: translateX(100%); }
                dialog[open] { transform: none; }
            </style>
            <dialog id="panel" class="drawer"><div class="drawer-body">Filters</div></dialog>
            """);

        var result = await page.EvaluateAsync<string>("""
            async () => {
                const d = document.getElementById('panel');
                sednaUi.modal.show('panel');
                await new Promise(r => setTimeout(r, 400));
                d.close();
                const started = performance.now();
                await sednaUi.modal.idle('panel');
                const waited = performance.now() - started;
                return (d.getAnimations().length === 0) + ':' + (waited > 50);
            }
            """);

        // --motion-mid is 0.18s. Resolving at once would let the host remove the drawer
        // mid-slide; resolving with its animation still running is the same fault.
        Assert.Equal("true:true", result);
    }

    [Fact]
    public async Task Show_puts_the_dialog_in_the_top_layer()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        await page.EvaluateAsync("() => { sednaUi.modal.show('panel'); }");

        // :modal matches only a dialog opened with showModal(), so this is the
        // assertion that the top layer, the focus trap and the inert background are
        // actually in effect — show() falling back to `open = true` would pass a
        // check on `.open` and fail this one.
        Assert.True(await page.EvaluateAsync<bool>(
            "() => document.getElementById('panel').matches(':modal')"));
    }

    [Fact]
    public async Task Show_on_an_open_dialog_is_a_no_op_rather_than_a_throw()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        var result = await page.EvaluateAsync<string>("""
            () => {
                try {
                    sednaUi.modal.show('panel');
                    sednaUi.modal.show('panel');
                    return document.getElementById('panel').matches(':modal') ? 'open' : 'closed';
                } catch (e) { return 'threw: ' + e.name; }
            }
            """);

        Assert.Equal("open", result);
    }

    [Fact]
    public async Task Close_reports_the_return_value_the_caller_passed()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        var returnValue = await page.EvaluateAsync<string>("""
            () => {
                sednaUi.modal.show('panel');
                sednaUi.modal.close('panel', 'save');
                return document.getElementById('panel').returnValue;
            }
            """);

        Assert.Equal("save", returnValue);
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('panel').open"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("not-a-dialog")]
    public async Task Neither_call_throws_on_an_id_that_is_not_an_open_dialog(string id)
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        var outcome = await page.EvaluateAsync<string>($$"""
            async () => {
                try {
                    const answer = await sednaUi.modal.show('{{id}}');
                    sednaUi.modal.close('{{id}}');
                    return 'resolved:' + answer;
                } catch (e) { return e.name; }
            }
            """);

        Assert.Equal("resolved:null", outcome);
    }
}
