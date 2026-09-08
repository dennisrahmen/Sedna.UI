using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.modal</c> opens an app's own <c>&lt;dialog class="modal"&gt;</c> — the one
/// capability a Blazor app could not reach without injecting <c>IJSRuntime</c>.
/// </summary>
/// <remarks>
/// The two failure modes are what this pins, because both are silent in a source scan:
/// <c>showModal()</c> throws <c>InvalidStateError</c> on a dialog that is already open,
/// which a Blazor re-render calling <c>show()</c> twice reaches routinely; and a wrong id
/// must not throw at all, because an exception crossing the interop boundary from a
/// Blazor handler tears down the circuit rather than surfacing anywhere.
/// </remarks>
public class ModalTests : ScriptTestBase
{
    private const string Fixture = """
        <dialog id="panel" class="modal">
            <div class="modal-header"><h3>New API key</h3></div>
            <div class="modal-body"><p>A form would go here.</p></div>
        </dialog>
        <div id="not-a-dialog"></div>
        """;

    [Fact]
    public async Task Show_puts_the_dialog_in_the_top_layer()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        await page.EvaluateAsync("() => sednaUi.modal.show('panel')");

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

        var thrown = await page.EvaluateAsync<string>($$"""
            () => {
                try {
                    sednaUi.modal.show('{{id}}');
                    sednaUi.modal.close('{{id}}');
                    return '';
                } catch (e) { return e.name; }
            }
            """);

        Assert.Equal(string.Empty, thrown);
    }
}
