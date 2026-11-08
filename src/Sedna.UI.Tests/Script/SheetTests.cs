using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A sheet with <c>data-sheet-snap</c> rests at the heights it lists as well as its full
/// height: it opens at the first, a drag lets go at the nearest, a flick goes on or closes it,
/// and the handle written as a button steps between them from the keyboard.
/// </summary>
public class SheetTests : ScriptTestBase
{
    private const string Fixture = """
        <button id="open" type="button" onclick="document.getElementById('sheet').showModal()">Open</button>
        <dialog id="sheet" class="drawer sheet" data-sheet data-sheet-snap="50%" aria-labelledby="sheet-title">
            <button class="sheet-handle" id="handle" type="button" data-sheet-expand aria-expanded="false" aria-label="Resize the sheet"></button>
            <div class="drawer-header"><h3 id="sheet-title">Route for build-runner-04</h3></div>
            <div class="drawer-body"><p>Depot 2, loading bay 4</p></div>
        </dialog>
        <dialog id="steps" class="drawer sheet" data-sheet data-sheet-snap="30%, 60%">
            <button class="sheet-handle" id="steps-handle" type="button" data-sheet-expand aria-expanded="false" aria-label="Resize the sheet"></button>
            <div class="drawer-header"><h3>Stops</h3></div>
        </dialog>
        """;

    /// <summary>How much of the sheet shows above the bottom of the viewport, as a share of its height.</summary>
    private static Task<double> Showing(IPage page, string id = "sheet") =>
        page.EvaluateAsync<double>("""
            id => {
                const r = document.getElementById(id).getBoundingClientRect();
                return (innerHeight - r.top) / r.height;
            }
            """, id);

    private static Task<string?> At(IPage page, string id = "sheet") =>
        page.Locator("#" + id).GetAttributeAsync("data-sheet-at");

    /// <summary>
    /// Drags the sheet's header by <paramref name="share"/> of its height (down is positive),
    /// as dispatched pointer events in one go — a flick, its speed set by nothing but the
    /// distance — or, given <paramref name="slowMs"/>, with at least that much of the page's
    /// own time between the press and the release, which no flick survives.
    /// </summary>
    private static Task Drag(IPage page, double share, int slowMs = 0) =>
        Js.Run(page, """
            async ([share, slowMs]) => {
                const sheet = document.getElementById('sheet');
                const grip = sheet.querySelector('.drawer-header');
                const r = grip.getBoundingClientRect();
                const x = r.left + r.width / 2, y = r.top + r.height / 2;
                const dy = share * sheet.getBoundingClientRect().height;
                const fire = (type, at) => grip.dispatchEvent(new PointerEvent(type, {
                    bubbles: true, cancelable: true, pointerId: 7, pointerType: 'mouse', button: 0, isPrimary: true,
                    clientX: x, clientY: at }));
                fire('pointerdown', y);
                if (slowMs) await new Promise(done => {
                    const until = performance.now() + slowMs;
                    const step = () => performance.now() < until ? requestAnimationFrame(step) : done();
                    step();
                });
                for (let i = 1; i <= 6; i++) fire('pointermove', y + dy * i / 6);
                fire('pointerup', y + dy);
            }
            """, new object[] { share, slowMs });

    [Fact]
    public async Task A_snapping_sheet_opens_at_its_first_height_and_is_as_tall_as_a_sheet_may_be()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await page.Locator("#open").ClickAsync();
        Assert.Equal("50%", await At(page));
        await Js.Settle(page);

        Assert.Equal(0.5, await Showing(page), 2);
        var tall = await page.EvaluateAsync<double>("() => document.getElementById('sheet').getBoundingClientRect().height / innerHeight");
        Assert.Equal(0.85, tall, 2);

        // Closed and opened again, it starts from its first height, not where it was left.
        await page.Locator("#handle").ClickAsync();
        Assert.Equal("100%", await At(page));
        await Js.Run(page, "() => document.getElementById('sheet').close()");
        await page.Locator("#open").ClickAsync();
        Assert.Equal("50%", await At(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_handle_steps_up_through_the_heights_from_the_keyboard_and_back_to_the_first()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await Js.Run(page, "() => document.getElementById('steps').showModal()");
        Assert.Equal("30%", await At(page, "steps"));

        await page.Locator("#steps-handle").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("60%", await At(page, "steps"));
        Assert.Equal("false", await page.Locator("#steps-handle").GetAttributeAsync("aria-expanded"));

        await page.Keyboard.PressAsync("Space");
        Assert.Equal("100%", await At(page, "steps"));
        Assert.Equal("true", await page.Locator("#steps-handle").GetAttributeAsync("aria-expanded"));
        await Js.Settle(page);
        Assert.Equal(1.0, await Showing(page, "steps"), 2);

        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("30%", await At(page, "steps"));
        Assert.Equal("false", await page.Locator("#steps-handle").GetAttributeAsync("aria-expanded"));
        await Js.Settle(page);
        Assert.Equal(0.3, await Showing(page, "steps"), 2);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_slow_drag_lets_go_at_the_nearest_height()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await page.Locator("#open").ClickAsync();
        await Js.Settle(page);
        // Up by four tenths of the sheet, slowly: past the middle of the way to full height.
        await Drag(page, -0.4, slowMs: 1200);

        Assert.Equal("100%", await At(page));
        Assert.Equal("", await page.EvaluateAsync<string>("() => document.getElementById('sheet').style.transform"));
        await Js.Settle(page);
        Assert.Equal(1.0, await Showing(page), 2);

        // Down by three tenths, slowly: nearer half than full, and well short of closing.
        await Drag(page, 0.3, slowMs: 1200);

        Assert.Equal("50%", await At(page));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('sheet').open"));

        // Down past most of what the lowest height shows, slowly: that closes it.
        await Js.Settle(page);
        await Drag(page, 0.45, slowMs: 1200);
        await page.WaitForFunctionAsync("() => !document.getElementById('sheet').open");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_flick_goes_on_to_the_next_height_and_from_the_lowest_closes_the_sheet()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await page.Locator("#open").ClickAsync();
        await Js.Settle(page);
        // A short flick up: not near full height, but a flick goes on to it.
        await Drag(page, -0.1);
        Assert.Equal("100%", await At(page));

        // A flick down from full height rests at half, and another from half closes it.
        await Js.Settle(page);
        await Drag(page, 0.1);
        Assert.Equal("50%", await At(page));

        await Js.Settle(page);
        await Drag(page, 0.1);
        await page.WaitForFunctionAsync("() => !document.getElementById('sheet').open");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_click_on_the_handle_after_dragging_it_is_not_a_step()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await page.Locator("#open").ClickAsync();
        await Js.Settle(page);
        var handle = await page.Locator("#handle").BoundingBoxAsync();
        Assert.NotNull(handle);
        var x = handle!.X + handle.Width / 2;
        var y = handle.Y + handle.Height / 2;

        // A drag that starts and ends on the handle still raises its click; it rests where
        // the drag put it — half, here — rather than stepping on to full.
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + 8, new() { Steps = 2 });
        await page.Mouse.MoveAsync(x, y, new() { Steps = 2 });
        await page.Mouse.UpAsync();
        Assert.Equal("50%", await At(page));

        // A plain click on it does step.
        await page.Locator("#handle").ClickAsync();
        Assert.Equal("100%", await At(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task With_reduced_motion_the_sheet_moves_between_heights_without_sliding()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);

        await page.Locator("#open").ClickAsync();
        Assert.True(await page.EvaluateAsync<bool>(
            "() => getComputedStyle(document.getElementById('sheet')).transitionDuration.split(',').every(d => parseFloat(d) === 0)"),
            "A snapping sheet still slides with reduced motion.");
        Assert.Equal(0.5, await Showing(page), 2);

        await page.Locator("#handle").ClickAsync();
        Assert.Equal(1.0, await Showing(page), 2);
    }
}
