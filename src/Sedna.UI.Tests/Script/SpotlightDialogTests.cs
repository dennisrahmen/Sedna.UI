using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A spotlight step whose target is inside a <c>&lt;dialog&gt;</c> opened with
/// <c>showModal()</c>.
/// </summary>
/// <remarks>
/// <para>
/// The dialog is in the top layer, which no z-index reaches, and the platform makes
/// everything outside it inert — so the documented chain (modal backdrop 500 &lt;
/// spotlight 510) holds for the <c>.modal-backdrop</c> div and for nothing else. An
/// app with a guided tour that points inside its own modals could not adopt
/// <c>&lt;dialog&gt;</c> at all: it would have had to give up the top layer, the focus
/// trap, Escape and the inert background to keep its tour working.
/// </para>
/// <para>
/// Every assertion here is the browser's answer rather than the source's: whether a
/// real click reaches a button, whether focus can land on it, and — for the hole —
/// what colour a pixel of the dialog actually is, because a hole positioned perfectly
/// and painted underneath highlights nothing.
/// </para>
/// </remarks>
public class SpotlightDialogTests : ScriptTestBase
{
    /// <summary>
    /// A page with something to point at, a dialog holding the step's real target, a
    /// control the step must not let through, and a bubble with a Next button.
    /// </summary>
    private const string Stage = """
        <div id="stage" style="position:relative; padding:20px">
            <button class="btn" type="button" id="on-page" data-live="yes"
                    onclick="window.hits = (window.hits || 0) + 1; window.last = 'on-page'">On the page</button>

            <dialog class="modal" id="panel">
                <div class="modal-header">
                    <h3>New API key</h3>
                    <button class="btn" type="button" id="elsewhere"
                            onclick="window.hits = (window.hits || 0) + 1; window.last = 'elsewhere'">Docs</button>
                </div>
                <div class="modal-body">
                    <div class="form-field">
                        <label class="form-label" for="key-name">Name</label>
                        <input class="form-input" id="key-name" />
                    </div>
                    <span id="anchor">
                        <button class="btn btn-primary" type="button" id="create"
                                onclick="window.hits = (window.hits || 0) + 1; window.last = 'create'">Create</button>
                        <span data-spotlight-block>
                            <button class="btn" type="button" id="cancel"
                                    onclick="window.hits = (window.hits || 0) + 1; window.last = 'cancel'">Cancel</button>
                        </span>
                    </span>
                </div>
            </dialog>

            <div id="hole" class="spotlight-hole spotlight-ring"></div>
            <div id="tip" class="spotlight-tip">
                <button class="btn btn-sm btn-primary" type="button" id="next"
                        onclick="window.hits = (window.hits || 0) + 1; window.last = 'next'">Next</button>
            </div>
        </div>
        """;

    private static Task<int> Hits(IPage page) => page.EvaluateAsync<int>("() => window.hits || 0");

    // Which control answered last, so a forced click that landed somewhere else names
    // the element it hit rather than reporting an off-by-one.
    private static Task<string> Last(IPage page) =>
        page.EvaluateAsync<string>("() => window.last || ''");

    [Fact]
    public async Task The_bubbles_buttons_answer_over_an_open_modal_dialog()
    {
        if (NoBrowser) return;
        // Inertness, not painting: a bubble left in the page is visible under the
        // dialog's backdrop and completely dead — a real click lands on the dialog and
        // focus() on its button does nothing. Both halves are the reason the bubble is
        // moved into the dialog rather than merely raised above it.
        var (page, errors) = await OpenStyled(Stage);

        await page.EvaluateAsync("() => sednaUi.modal.show('panel')");
        await page.EvaluateAsync(
            "() => window.step = sednaUi.spotlight.follow('#hole', '#key-name', { tip: '#tip' })");

        Assert.Equal("panel", await page.EvaluateAsync<string>(
            "() => document.getElementById('tip').closest('dialog').id"));

        // A real click, not a forced one: what is being tested is hit-testing.
        await page.Locator("#next").ClickAsync(new() { Timeout = 4000 });
        Assert.Equal(1, await Hits(page));

        // And the keyboard, which inertness takes away just as completely.
        await page.Locator("#next").FocusAsync();
        Assert.Equal("next", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(2, await Hits(page));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_hole_sits_over_a_control_inside_the_dialog_and_dims_what_is_around_it()
    {
        if (NoBrowser) return;
        // Geometry is only half of "over": the hole was always positioned correctly and
        // painted underneath the dialog, which highlights nothing. So this measures the
        // paint — a pixel of the dialog's own surface darkens under the dim, and the
        // pixel inside the hole does not change at all.
        var (page, errors) = await OpenStyled(
            Stage, extraHead: "<style>:root { --motion-fast: 0s; --motion-slow: 0s }</style>");

        await page.EvaluateAsync("() => sednaUi.modal.show('panel')");

        // [surface x, surface y, target x, target y] — the header's padding, which is
        // panel and not text, and the middle of the control the step is about.
        var points = await page.EvaluateAsync<double[]>("""
            () => {
                const head = document.querySelector('#panel .modal-header').getBoundingClientRect();
                const target = document.getElementById('key-name').getBoundingClientRect();
                return [head.left + 6, head.top + head.height / 2,
                        target.left + target.width / 2, target.top + target.height / 2];
            }
            """);

        var surfaceBefore = await Pixel(page, points[0], points[1]);
        var targetBefore = await Pixel(page, points[2], points[3]);

        await page.EvaluateAsync(
            "() => window.step = sednaUi.spotlight.follow('#hole', '#key-name', { pad: 0 })");
        await page.WaitForFunctionAsync(
            "() => document.getElementById('hole').matches(':popover-open')",
            null, new() { Timeout = 4_000 });

        // The box, in the viewport both are measured in: exactly the control.
        var box = await page.EvaluateAsync<double[]>("""
            () => {
                const h = document.getElementById('hole').getBoundingClientRect();
                const t = document.getElementById('key-name').getBoundingClientRect();
                return [h.left - t.left, h.top - t.top, h.width - t.width, h.height - t.height];
            }
            """);
        Assert.All(box, d => Assert.True(Math.Abs(d) <= 1,
            $"The hole is offset from the control it is over by [{string.Join(", ", box)}]."));

        var surfaceAfter = await Pixel(page, points[0], points[1]);
        var targetAfter = await Pixel(page, points[2], points[3]);

        Assert.True(Sum(surfaceAfter) < Sum(surfaceBefore) - 20,
            $"The dialog's surface is {Sum(surfaceBefore)} before the step and {Sum(surfaceAfter)} "
            + "during it. The dim is not covering the dialog, so the hole is painting under it.");
        Assert.Equal(targetBefore, targetAfter);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Escape_and_the_focus_trap_are_left_alone()
    {
        if (NoBrowser) return;
        // The whole point of adopting <dialog> is the four things the platform gives
        // you, so a spotlight that took any of them away would be no better than the
        // backdrop div the app started with.
        var (page, errors) = await OpenStyled(Stage);

        await page.EvaluateAsync("() => sednaUi.modal.show('panel')");
        await page.EvaluateAsync(
            "() => window.step = sednaUi.spotlight.follow('#hole', '#key-name', { tip: '#tip' })");

        await page.EvaluateAsync("() => document.getElementById('key-name').focus()");
        var seen = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            seen.Add(await page.EvaluateAsync<string>("""
                () => {
                    const a = document.activeElement;
                    if (!a || a === document.body) return 'body';
                    return (document.getElementById('panel').contains(a) ? 'in:' : 'out:') + (a.id || a.tagName);
                }
                """));
        }

        // The bubble joins the dialog's cycle, and the cycle still reaches nothing
        // outside the dialog — the trap is the platform's and stays intact.
        Assert.Contains("in:next", seen);
        Assert.DoesNotContain(seen, focus => focus.StartsWith("out:", StringComparison.Ordinal));

        // Escape from the bubble closes the dialog: the close watcher is the dialog's
        // own, and nothing here registers one or swallows the key.
        await page.EvaluateAsync("() => document.getElementById('next').focus()");
        Assert.Equal("next", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Escape");

        await page.WaitForFunctionAsync("() => !document.getElementById('panel').open");
        // …and the bubble comes home rather than going with it.
        await page.WaitForFunctionAsync(
            "() => document.getElementById('tip').parentElement.id === 'stage'",
            null, new() { Timeout = 4_000 });
        Assert.False(await page.EvaluateAsync<bool>(
            "() => document.getElementById('tip').hasAttribute('popover')"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_lock_treats_the_open_dialog_as_the_page()
    {
        if (NoBrowser) return;
        // While a modal dialog is open the dialog IS the page, so the lock has to hold
        // inside it exactly as it holds outside: the live anchor and the bubble answer,
        // everything else in the dialog does not, and typing survives.
        var (page, errors) = await OpenStyled(Stage);

        await page.EvaluateAsync("() => sednaUi.modal.show('panel')");
        // Placed below the anchor, which is the bottom of the dialog: a bubble over the
        // controls this test force-clicks would take the clicks itself, and the count
        // would be right for the wrong reason.
        await page.EvaluateAsync("""
            () => window.step = sednaUi.spotlight.follow(
                '#hole', '#anchor',
                { tip: '#tip', placement: 'bottom', lock: { interactive: true } })
            """);

        // The bubble, by a real click — it is the one thing the lock must never take.
        await page.Locator("#next").ClickAsync(new() { Timeout = 4000 });
        // The live anchor. Forced, because the guard is what has to refuse or allow the
        // click, and Playwright's actionability check would not get past
        // pointer-events: none to let it try.
        await page.Locator("#create").ClickAsync(new() { Force = true });
        Assert.Equal(2, await Hits(page));

        // Another control in the same dialog is as dead as one on the page behind…
        await page.Locator("#elsewhere").ClickAsync(new() { Force = true });
        // …and so is the opt-out inside the live anchor itself.
        await page.Locator("#cancel").ClickAsync(new() { Force = true });
        Assert.Equal(2, await Hits(page));
        Assert.Equal("create", await Last(page));

        // Typing is never activation, in a dialog or out of it.
        await page.Locator("#key-name").FocusAsync();
        await page.Keyboard.TypeAsync("ci publisher");
        Assert.Equal("ci publisher", await page.InputValueAsync("#key-name"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_step_that_leaves_the_dialog_puts_everything_back_exactly_as_it_was()
    {
        if (NoBrowser) return;
        // The page case has to be untouched by all of this, which is a claim about what
        // is left behind rather than about the dialog step: the same parent, the same
        // attributes, the same containing block and the same rectangle as before the
        // tour ever reached a dialog.
        var (page, errors) = await OpenStyled(Stage);

        await page.EvaluateAsync("""
            () => window.step = sednaUi.spotlight.follow(
                '#hole', '[data-live]', { tip: '#tip', pad: 0 })
            """);

        var plain = await State(page);
        Assert.Equal(new[] { "stage", "no", "no", "absolute", "absolute" }, plain[..5]);

        // Into the dialog: the attribute moves to the control inside it, exactly as a
        // tour advancing a step does.
        await page.EvaluateAsync("""
            () => {
                document.getElementById('on-page').removeAttribute('data-live');
                document.getElementById('key-name').setAttribute('data-live', 'yes');
                sednaUi.modal.show('panel');
                window.step.update();
            }
            """);

        var raised = await State(page);
        Assert.Equal(new[] { "panel", "yes", "yes", "fixed", "fixed" }, raised[..5]);

        // And out again.
        await page.EvaluateAsync("""
            () => {
                document.getElementById('key-name').removeAttribute('data-live');
                document.getElementById('on-page').setAttribute('data-live', 'yes');
                sednaUi.modal.close('panel');
                window.step.update();
            }
            """);

        Assert.Equal(plain, await State(page));

        await page.EvaluateAsync("() => window.step.stop()");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_step_waiting_on_a_dialog_places_itself_when_the_dialog_opens()
    {
        if (NoBrowser) return;
        // Opening a dialog moves nothing in the tree, changes an attribute the observer
        // deliberately does not watch, and fires neither scroll nor resize. Without the
        // dialog's own events a step pointing into one waits for ever — and an app
        // driving this from C# would have to know to call update() from its own
        // "I opened the dialog" handler, which is exactly the coupling follow() exists
        // to remove.
        var (page, errors) = await OpenStyled(Stage);

        await page.EvaluateAsync(
            "() => window.step = sednaUi.spotlight.follow('#hole', '#key-name', { tip: '#tip', pad: 0 })");

        // Nothing visible to point at yet: the target is inside a closed dialog.
        Assert.Equal("none", await page.EvaluateAsync<string>(
            "() => document.getElementById('hole').style.display"));

        await page.EvaluateAsync("() => sednaUi.modal.show('panel')");

        await page.WaitForFunctionAsync(
            "() => document.getElementById('hole').matches(':popover-open')",
            null, new() { Timeout = 4_000 });
        Assert.NotEqual("none", await page.EvaluateAsync<string>(
            "() => document.getElementById('hole').style.display"));

        Assert.Empty(errors);
    }

    /// <summary>
    /// Where the hole and the bubble are: the bubble's parent, whether each is raised,
    /// the containing block each is placed against, and the coordinates written on
    /// them.
    /// </summary>
    /// <remarks>
    /// The written values rather than the measured box, because the hole transitions
    /// between steps — a rectangle read immediately after a placement is somewhere
    /// between the two, and the assertion would be about the animation.
    /// </remarks>
    private static Task<string[]> State(IPage page) => page.EvaluateAsync<string[]>("""
        () => {
            const hole = document.getElementById('hole'), tip = document.getElementById('tip');
            return [
                tip.parentElement.id,
                hole.hasAttribute('popover') ? 'yes' : 'no',
                tip.hasAttribute('popover') ? 'yes' : 'no',
                getComputedStyle(hole).position,
                getComputedStyle(tip).position,
                [hole.style.top, hole.style.left, hole.style.width, hole.style.height].join(' '),
                [tip.style.top, tip.style.left].join(' '),
            ];
        }
        """);

    /// <summary>
    /// The colour of one viewport pixel, as [r, g, b].
    /// </summary>
    /// <remarks>
    /// Decoded in the page rather than in .NET: the screenshot is a PNG, and the
    /// browser already has a decoder. It is the only way to ask what is actually
    /// painted — a top-layer element is above everything by promotion order, which no
    /// property of either element reports.
    /// </remarks>
    private static async Task<int[]> Pixel(IPage page, double x, double y)
    {
        var shot = Convert.ToBase64String(await page.ScreenshotAsync());

        return await page.EvaluateAsync<int[]>("""
            async (at) => {
                const image = new Image();
                image.src = 'data:image/png;base64,' + at.shot;
                await image.decode();
                const canvas = document.createElement('canvas');
                canvas.width = image.width;
                canvas.height = image.height;
                const ctx = canvas.getContext('2d');
                ctx.drawImage(image, 0, 0);
                const d = ctx.getImageData(Math.round(at.x), Math.round(at.y), 1, 1).data;
                return [d[0], d[1], d[2]];
            }
            """, new { shot, x, y });
    }

    private static int Sum(int[] rgb) => rgb[0] + rgb[1] + rgb[2];
}
