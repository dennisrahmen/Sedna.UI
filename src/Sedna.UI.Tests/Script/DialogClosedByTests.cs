using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A click on the backdrop closes every modal <c>.modal</c>, <c>.drawer</c> and <c>.drawer.sheet</c>
/// that says nothing about it, in every engine on the floor; <c>closedby="any"</c> says the same
/// on those, and on a drawer opened without a modal and a <c>.lightbox</c>, which fills the screen
/// and so is closed by a click on its stage beside the picture. <c>closedby="closerequest"</c> and
/// <c>"none"</c> opt a dialog out. A drag that starts inside and ends beside it closes nothing,
/// and <c>data-close-guard</c> turns the click into a <c>cancel</c> the app answers, every time.
/// </summary>
/// <remarks>
/// The default is <c>42-modal.js</c>'s, the same in every engine; where an engine implements
/// <c>closedby</c> an explicit <c>any</c> is the platform's own light dismiss, pinned here so the
/// day an engine stops doing it is a failure. A dialog family added to the lists below is held to
/// the same rules.
/// </remarks>
public class DialogClosedByTests : ScriptTestBase
{
    private const string Field = """<input id="f" aria-label="Note" value="Waiting on the carrier">""";

    private const string Modal = """<dialog id="d" class="modal" {0}><div class="modal-body">""" + Field + "</div></dialog>";
    private const string Drawer = """<dialog id="d" class="drawer" {0}><div class="drawer-body">""" + Field + "</div></dialog>";
    private const string Sheet = """<dialog id="d" class="drawer sheet" {0}><div class="drawer-body">""" + Field + "</div></dialog>";
    private const string Modeless = """<dialog id="d" class="drawer" data-modeless {0}><div class="drawer-body">""" + Field + "</div></dialog>";
    private const string Lightbox = """
        <dialog id="d" class="lightbox" {0}>
          <div class="lightbox-stage"><input id="f" aria-label="Note" value="Waiting on the carrier" style="width:200px;height:120px"></div>
        </dialog>
        """;

    /// <summary>A modal family says nothing about it — the default — or says <c>any</c>.</summary>
    public static TheoryData<string, string> Families
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var markup in new[] { Modal, Drawer, Sheet })
            {
                data.Add(markup, "");
                data.Add(markup, "closedby=\"any\"");
            }
            data.Add(Modeless, "closedby=\"any\"");
            data.Add(Lightbox, "closedby=\"any\"");
            return data;
        }
    }

    /// <summary>The modal families, which close on a click on their backdrop unless they say otherwise.</summary>
    public static TheoryData<string> Modals => new() { Modal, Drawer, Sheet };

    private async Task<(Microsoft.Playwright.IPage Page, List<string> Errors)> Opened(string markup)
    {
        var (page, errors) = await OpenStyled(markup);
        await Js.Run(page, """
            () => {
              const d = document.getElementById('d');
              window.cancels = 0;
              d.addEventListener('cancel', () => window.cancels++);
              d.hasAttribute('data-modeless') ? d.show() : d.showModal();
            }
            """);
        await Js.Settle(page);
        return (page, errors);
    }

    // A point beside the dialog's box — or, for a lightbox, on its stage beside the picture.
    private static Task<double[]> Beside(Microsoft.Playwright.IPage page) =>
        page.EvaluateAsync<double[]>("""
            () => {
              const d = document.getElementById('d');
              if (d.classList.contains('lightbox')) return [8, innerHeight / 2];
              const r = d.getBoundingClientRect();
              const spots = [[r.left / 2, innerHeight / 2], [(r.right + innerWidth) / 2, innerHeight / 2],
                             [innerWidth / 2, r.top / 2], [innerWidth / 2, (r.bottom + innerHeight) / 2]];
              const room = [r.left, innerWidth - r.right, r.top, innerHeight - r.bottom];
              return spots[room.indexOf(Math.max(...room))];
            }
            """);

    private static Task<bool> IsOpen(Microsoft.Playwright.IPage page) =>
        page.EvaluateAsync<bool>("() => document.getElementById('d').open");

    [Theory]
    [MemberData(nameof(Families))]
    public async Task A_click_beside_it_closes_it(string markup, string closedBy)
    {
        if (NoBrowser) return;
        var (page, errors) = await Opened(string.Format(markup, closedBy));
        Assert.True(await IsOpen(page));

        var at = await Beside(page);
        await page.Mouse.ClickAsync((float)at[0], (float)at[1]);
        await Js.Frames(page);

        Assert.False(await IsOpen(page));
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(Families))]
    public async Task A_drag_from_a_field_out_beside_it_closes_nothing(string markup, string closedBy)
    {
        if (NoBrowser) return;
        var (page, errors) = await Opened(string.Format(markup, closedBy));
        var field = (await page.Locator("#f").BoundingBoxAsync())!;
        var at = await Beside(page);

        await page.Mouse.MoveAsync(field.X + 6, field.Y + field.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)at[0], (float)at[1], new() { Steps = 6 });
        await page.Mouse.UpAsync();
        await Js.Frames(page);
        Assert.True(await IsOpen(page));

        // And the other way round: pressed beside it, released inside.
        await page.Mouse.MoveAsync((float)at[0], (float)at[1]);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(field.X + 6, field.Y + field.Height / 2, new() { Steps = 6 });
        await page.Mouse.UpAsync();
        await Js.Frames(page);
        Assert.True(await IsOpen(page));
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(Families))]
    public async Task A_guarded_dialog_asks_the_app_on_every_click_beside_it(string markup, string closedBy)
    {
        if (NoBrowser) return;
        var (page, errors) = await Opened(string.Format(markup, "data-close-guard " + closedBy));
        var at = await Beside(page);

        for (var i = 0; i < 6; i++) await page.Mouse.ClickAsync((float)at[0], (float)at[1]);
        await Js.Frames(page);

        Assert.True(await IsOpen(page));
        Assert.Equal(6, await page.EvaluateAsync<int>("() => window.cancels"));
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(Modals))]
    public async Task A_backdrop_close_completes_the_presenter_the_way_Escape_does(string markup)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(string.Format(markup, ""));

        // The presenter resolves with the returnValue, null for a close that has none.
        async Task<string> Outcome(Func<Task> close)
        {
            await Js.Run(page, "() => { window.waiting = sednaUi.modal.show('d'); }");
            await Js.Settle(page);
            await close();
            await Js.Frames(page);
            return await page.EvaluateAsync<string>("async () => String(await window.waiting)");
        }

        var byBackdrop = await Outcome(async () =>
        {
            var at = await Beside(page);
            await page.Mouse.ClickAsync((float)at[0], (float)at[1]);
        });
        var byEscape = await Outcome(async () =>
        {
            await page.Locator("#f").FocusAsync();
            await page.Keyboard.PressAsync("Escape");
        });

        Assert.Equal("null", byEscape);
        Assert.Equal(byEscape, byBackdrop);
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(Modals))]
    public async Task Closedby_closerequest_and_none_opt_out_of_the_backdrop(string markup)
    {
        if (NoBrowser) return;
        foreach (var attribute in new[] { "closedby=\"closerequest\"", "closedby=\"none\"" })
        {
            var (page, errors) = await Opened(string.Format(markup, attribute));
            var at = await Beside(page);

            for (var i = 0; i < 3; i++) await page.Mouse.ClickAsync((float)at[0], (float)at[1]);
            await Js.Frames(page);

            Assert.True(await IsOpen(page));
            Assert.Equal(0, await page.EvaluateAsync<int>("() => window.cancels"));
            Assert.Empty(errors);
        }
    }

    [Theory]
    [MemberData(nameof(Modals))]
    public async Task Escape_is_unchanged_by_the_backdrop_default(string markup)
    {
        if (NoBrowser) return;
        var (page, _) = await Opened(string.Format(markup, ""));
        await page.Locator("#f").FocusAsync();
        await page.Keyboard.PressAsync("Escape");
        await Js.Frames(page);
        Assert.False(await IsOpen(page));

        // closerequest keeps Escape, which is the point of it; none gives it up.
        var (kept, _) = await Opened(string.Format(markup, "closedby=\"closerequest\""));
        await kept.Locator("#f").FocusAsync();
        await kept.Keyboard.PressAsync("Escape");
        await Js.Frames(kept);
        Assert.False(await IsOpen(kept));
    }

    [Fact]
    public async Task A_dialog_opened_without_a_modal_is_not_closed_by_a_click_on_the_page()
    {
        if (NoBrowser) return;
        var (page, errors) = await Opened(string.Format(Modeless, ""));
        var at = await Beside(page);

        await page.Mouse.ClickAsync((float)at[0], (float)at[1]);
        await Js.Frames(page);

        Assert.True(await IsOpen(page));
        Assert.Empty(errors);
    }
}
