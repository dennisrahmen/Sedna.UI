using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>closedby="any"</c> closes every dialog the library styles on a click beside it, in every
/// engine on the floor: a <c>.modal</c>, a <c>.drawer</c>, a <c>.drawer.sheet</c>, a drawer opened without
/// a modal, and a <c>.lightbox</c>, which fills the screen and so is closed by a click on its stage
/// beside the picture. A drag that starts inside and ends beside it closes nothing, and
/// <c>data-close-guard</c> turns the click into a <c>cancel</c> the app answers, every time.
/// </summary>
/// <remarks>
/// The modal, the drawer and the sheet are the platform's own light dismiss, pinned here so the day
/// an engine stops doing it is a failure; the lightbox is <c>42-modal.js</c>. A dialog family added
/// to the list below is held to the same three rules.
/// </remarks>
public class DialogClosedByTests : ScriptTestBase
{
    public static TheoryData<string> Families => new()
    {
        """<dialog id="d" class="modal" closedby="any" {0}><div class="modal-body"><input id="f" aria-label="Note" value="Waiting on the carrier"></div></dialog>""",
        """<dialog id="d" class="drawer" closedby="any" {0}><div class="drawer-body"><input id="f" aria-label="Note" value="Waiting on the carrier"></div></dialog>""",
        """<dialog id="d" class="drawer sheet" closedby="any" {0}><div class="drawer-body"><input id="f" aria-label="Note" value="Waiting on the carrier"></div></dialog>""",
        """<dialog id="d" class="drawer" closedby="any" data-modeless {0}><div class="drawer-body"><input id="f" aria-label="Note" value="Waiting on the carrier"></div></dialog>""",
        """
        <dialog id="d" class="lightbox" closedby="any" {0}>
          <div class="lightbox-stage"><input id="f" aria-label="Note" value="Waiting on the carrier" style="width:200px;height:120px"></div>
        </dialog>
        """,
    };

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
    public async Task A_click_beside_it_closes_it(string markup)
    {
        if (NoBrowser) return;
        var (page, errors) = await Opened(string.Format(markup, ""));
        Assert.True(await IsOpen(page));

        var at = await Beside(page);
        await page.Mouse.ClickAsync((float)at[0], (float)at[1]);
        await Js.Frames(page);

        Assert.False(await IsOpen(page));
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(Families))]
    public async Task A_drag_from_a_field_out_beside_it_closes_nothing(string markup)
    {
        if (NoBrowser) return;
        var (page, errors) = await Opened(string.Format(markup, ""));
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
    public async Task A_guarded_dialog_asks_the_app_on_every_click_beside_it(string markup)
    {
        if (NoBrowser) return;
        var (page, errors) = await Opened(string.Format(markup, "data-close-guard"));
        var at = await Beside(page);

        for (var i = 0; i < 6; i++) await page.Mouse.ClickAsync((float)at[0], (float)at[1]);
        await Js.Frames(page);

        Assert.True(await IsOpen(page));
        Assert.Equal(6, await page.EvaluateAsync<int>("() => window.cancels"));
        Assert.Empty(errors);
    }
}
