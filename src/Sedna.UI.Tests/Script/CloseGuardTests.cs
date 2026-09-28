using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-close-guard</c>: a dialog holding unsaved work does not close on a close
/// request. Escape and a swipe down on a sheet raise <c>cancel</c>, the app asks, and the
/// app closes it.
/// </summary>
/// <remarks>
/// <para>
/// The script refuses the <c>cancel</c> because Razor cannot: it renders
/// <c>@oncancel:preventDefault</c> as a literal attribute and the render throws. The app
/// writes only <c>@oncancel</c>, which the fixture stands in for by counting.
/// </para>
/// <para>
/// Refusing is not enough on its own. Chromium honours a refused close request only so
/// often without a click or a keypress between — Escape is not one — and then dispatches a
/// <c>cancel</c> that cannot be cancelled and closes the dialog. The first test pins that
/// platform behaviour, so the day it stops being true is a visible failure and the Escape
/// half of the guard can be reconsidered.
/// </para>
/// </remarks>
public class CloseGuardTests : ScriptTestBase
{
    private const string Fixture = """
        <button id="open-plain" type="button" onclick="document.getElementById('plain').showModal()">Plain</button>
        <button id="open-guarded" type="button" onclick="document.getElementById('guarded').showModal()">Guarded</button>

        <dialog id="plain" class="modal"><div class="modal-body"><input aria-label="Message"></div></dialog>
        <dialog id="guarded" class="modal" data-close-guard>
            <div class="modal-body">
                <input id="guarded-input" aria-label="Message">
                <input id="owns-escape" aria-label="Filter">
                <button id="request-close" type="button" commandfor="guarded" command="request-close">Close</button>
            </div>
        </dialog>

        <script>
            window.asked = {};
            // An app refusing on its own, which is what the guard exists to replace.
            document.getElementById('plain').addEventListener('cancel', e => {
                e.preventDefault();
                asked.plain = (asked.plain || 0) + 1;
            });
            // The app's @oncancel: it only hears the request. The script has refused it.
            document.getElementById('guarded').addEventListener('cancel', e => {
                if (e.defaultPrevented) asked.guarded = (asked.guarded || 0) + 1;
            });
            // A control inside the dialog that handles Escape itself, as a combo does.
            document.getElementById('owns-escape').addEventListener('keydown', e => {
                if (e.key === 'Escape') e.preventDefault();
            });
        </script>
        """;

    private static Task<bool> IsOpen(IPage page, string id) =>
        page.EvaluateAsync<bool>($"() => document.getElementById('{id}').open");

    private static Task<int> Asked(IPage page, string id) =>
        page.EvaluateAsync<int>($"() => asked['{id}'] || 0");

    [Fact]
    public async Task Without_the_guard_the_platform_closes_the_dialog_after_a_few_refused_escapes()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        await page.Locator("#open-plain").ClickAsync();
        for (var i = 0; i < 6 && await IsOpen(page, "plain"); i++)
            await page.Keyboard.PressAsync("Escape");

        Assert.False(await IsOpen(page, "plain"));
    }

    [Fact]
    public async Task A_guarded_dialog_is_asked_on_every_escape_and_stays_open()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        await page.Locator("#open-guarded").ClickAsync();
        for (var i = 0; i < 6; i++) await page.Keyboard.PressAsync("Escape");

        Assert.True(await IsOpen(page, "guarded"));
        Assert.Equal(6, await Asked(page, "guarded"));
    }

    [Fact]
    public async Task Escape_with_focus_left_on_the_body_still_reaches_the_guard()
    {
        if (NoBrowser) return;
        // A re-render removing the focused control leaves focus on the body, so the
        // keydown's target is outside every dialog.
        var page = await Open(Fixture);

        await page.Locator("#open-guarded").ClickAsync();
        await page.Locator("#guarded-input").FocusAsync();
        await page.EvaluateAsync("() => document.getElementById('guarded-input').remove()");
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement === document.body"));
        for (var i = 0; i < 6; i++) await page.Keyboard.PressAsync("Escape");

        Assert.True(await IsOpen(page, "guarded"));
        Assert.Equal(6, await Asked(page, "guarded"));
    }

    [Fact]
    public async Task Escape_a_control_inside_already_handled_is_left_to_it()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        await page.Locator("#open-guarded").ClickAsync();
        await page.Locator("#owns-escape").FocusAsync();
        await page.Keyboard.PressAsync("Escape");

        Assert.True(await IsOpen(page, "guarded"));
        Assert.Equal(0, await Asked(page, "guarded"));
    }

    [Fact]
    public async Task A_request_close_button_is_asked_too_and_close_from_the_app_still_closes()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture);

        await page.Locator("#open-guarded").ClickAsync();
        await page.Locator("#request-close").ClickAsync();

        Assert.True(await IsOpen(page, "guarded"));
        Assert.Equal(1, await Asked(page, "guarded"));

        // What Overlay.CancelAsync() does once the reader has said yes.
        await page.EvaluateAsync("() => sednaUi.modal.close('guarded')");
        Assert.False(await IsOpen(page, "guarded"));
    }

    private const string SheetFixture = """
        <button id="open" type="button" onclick="document.getElementById('sheet').showModal()">Open</button>
        <dialog id="sheet" class="drawer sheet" data-sheet data-close-guard>
            <div class="sheet-handle"></div>
            <div class="drawer-header"><h3>Reply on ORD-4209</h3></div>
            <div class="drawer-body"><p>Message to Alex Fischer</p></div>
        </dialog>
        <script>
            window.asked = 0;
            document.getElementById('sheet').addEventListener('cancel', () => asked++);
        </script>
        """;

    private static async Task SwipeDown(IPage page)
    {
        var box = await page.Locator("#sheet .drawer-header").BoundingBoxAsync();
        Assert.NotNull(box);
        var x = box!.X + box.Width / 2;
        var y = box.Y + box.Height / 2;
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + 400, new() { Steps = 8 });
        await page.Mouse.UpAsync();
    }

    [Fact]
    public async Task A_swipe_down_is_a_close_request_a_guarded_sheet_springs_back_from()
    {
        if (NoBrowser) return;
        var page = await Open(SheetFixture);

        await page.Locator("#open").ClickAsync();
        await SwipeDown(page);

        Assert.True(await IsOpen(page, "sheet"));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => asked"));
        // Nothing is left holding the sheet where the finger let go.
        Assert.Equal("", await page.EvaluateAsync<string>("() => document.getElementById('sheet').style.transform"));
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('sheet').classList.contains('sheet--dragging')"));

        // Unguarded, the same swipe closes it.
        await page.EvaluateAsync("() => document.getElementById('sheet').removeAttribute('data-close-guard')");
        await SwipeDown(page);

        Assert.False(await IsOpen(page, "sheet"));
    }
}
