using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A drawer or a sheet leaves the way it came: the panel slides out over the same duration, from
/// where it rests, and the page's dimming fades out with it, so the two finish together — by the
/// close button, Escape, a click on the backdrop, <c>close()</c> and the presenter, in every
/// engine on the floor.
/// </summary>
/// <remarks>
/// Chromium keeps the closed dialog and its backdrop displayed through <c>overlay</c>, which the
/// stylesheet transitions; WebKit has no <c>overlay</c>, so <c>42-modal-exit.js</c> holds the
/// dialog and dims the page with its own shadow. The dim is read from whichever draws it, frame
/// by frame, and the same assertions hold for both.
/// </remarks>
public class DrawerExitTests : ScriptTestBase
{
    private const string Panel = """
        <div class="drawer-header"><h3>Order</h3><button id="x" class="modal-close" type="button" aria-label="Close" onclick="this.closest('dialog').close()">×</button></div>
        <div class="drawer-body"><button id="f" type="button">Reassign</button></div>
        """;

    public static TheoryData<string> Dialogs => new()
    {
        $"""<dialog id="d" class="drawer">{Panel}</dialog>""",
        $"""<dialog id="d" class="drawer drawer--start">{Panel}</dialog>""",
        $"""<dialog id="d" class="drawer sheet" data-sheet><div class="sheet-handle"></div>{Panel}</dialog>""",
        $"""<dialog id="d" class="drawer sheet" data-sheet data-sheet-snap="50%"><div class="sheet-handle"></div>{Panel}</dialog>""",
    };

    public static TheoryData<string, string> Routes
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var markup in new[]
            {
                $"""<dialog id="d" class="drawer">{Panel}</dialog>""",
                $"""<dialog id="d" class="drawer sheet" data-sheet data-sheet-snap="50%"><div class="sheet-handle"></div>{Panel}</dialog>""",
            })
            {
                foreach (var route in new[] { "button", "escape", "backdrop", "close", "presenter" })
                    data.Add(markup, route);
            }
            return data;
        }
    }

    private const string Sampler = """
        () => {
          const d = document.getElementById('d');
          const alpha = s => {
            const all = [...s.matchAll(/(?:\/\s*|,\s*)([0-9.]+)\)/g)];
            return all.length ? parseFloat(all[all.length - 1][1]) : 0;
          };
          const move = s => {
            const m = s.match(/matrix\(([^)]+)\)/);
            if (!m) return 0;
            const v = m[1].split(',').map(Number);
            return Math.abs(v[4]) + Math.abs(v[5]);
          };
          window.samples = [];
          const t0 = performance.now();
          const step = () => {
            const cs = getComputedStyle(d);
            const held = d.hasAttribute('data-sedna-closing');
            window.samples.push({
              t: Math.round(performance.now() - t0),
              open: d.open,
              shown: cs.display !== 'none',
              move: move(cs.transform),
              dim: alpha(held ? cs.boxShadow : getComputedStyle(d, '::backdrop').backgroundColor),
            });
            if (performance.now() - t0 < 900) requestAnimationFrame(step);
          };
          requestAnimationFrame(step);
        }
        """;

    private record Sample(int T, bool Open, bool Shown, double Move, double Dim);

    private async Task<(IPage Page, List<string> Errors, double RestMove, double RestDim, double Extent)> Opened(string markup)
    {
        var (page, errors) = await OpenStyled(markup);
        await Js.Run(page, "() => { sednaUi.modal.show('d'); }");
        await Js.Settle(page);
        var rest = await page.EvaluateAsync<double[]>("""
            () => {
              const d = document.getElementById('d');
              const cs = getComputedStyle(d);
              const m = cs.transform.match(/matrix\(([^)]+)\)/);
              const v = m ? m[1].split(',').map(Number) : [0, 0, 0, 0, 0, 0];
              const a = getComputedStyle(d, '::backdrop').backgroundColor.match(/(?:\/\s*|,\s*)([0-9.]+)\)/);
              const r = d.getBoundingClientRect();
              return [Math.abs(v[4]) + Math.abs(v[5]), a ? parseFloat(a[1]) : 0, d.classList.contains('sheet') ? r.height : r.width];
            }
            """);
        return (page, errors, rest[0], rest[1], rest[2]);
    }

    private static async Task<List<Sample>> Read(IPage page)
    {
        await Js.Elapse(page, 900);
        var json = await page.EvaluateAsync<string>("() => JSON.stringify(window.samples)");
        return JsonSerializer.Deserialize<List<Sample>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    /// <summary>The frames after the dialog closed in which it was still on screen.</summary>
    private static List<Sample> Leaving(List<Sample> samples) => samples.Where(s => !s.Open && s.Shown).ToList();

    private static void AssertLeavesTogether(List<Sample> samples, double restMove, double restDim, double extent)
    {
        var leaving = Leaving(samples);
        Assert.True(leaving.Count >= 2, "The panel slid out over more than one frame, so it was displayed after it closed.");

        // From where it rests, never beyond: a half-height sheet must not jump to full height first.
        Assert.True(leaving[0].Move >= restMove - 2, $"First frame out at {leaving[0].Move}px, rest was {restMove}px.");
        for (var i = 1; i < leaving.Count; i++)
            Assert.True(leaving[i].Move >= leaving[i - 1].Move - 1, "The panel only moves outwards.");

        // The backdrop is still there while the panel starts to leave, and fades as it goes.
        Assert.True(restDim > 0.2, $"The open dialog dims the page ({restDim}).");
        Assert.True(leaving[0].Dim >= restDim * 0.5, $"The dim is still {leaving[0].Dim} as the panel starts out; open was {restDim}.");
        for (var i = 1; i < leaving.Count; i++)
            Assert.True(leaving[i].Dim <= leaving[i - 1].Dim + 0.01, "The dim only fades.");

        // And both are done when the dialog is taken away.
        var last = leaving[^1];
        Assert.True(last.Dim <= restDim * 0.2, $"The panel left with the page still dimmed to {last.Dim}.");
        Assert.True(last.Move >= extent * 0.8, $"The dim was gone with the panel only {last.Move} of {extent}px out.");
        Assert.True(samples.Last().Shown == false && samples.Last().Dim <= 0.02, "Nothing is left on screen.");
    }

    private static async Task Close(IPage page, string route)
    {
        switch (route)
        {
            case "button":
                await page.Locator("#x").ClickAsync();
                break;
            case "escape":
                await page.Locator("#f").FocusAsync();
                await page.Keyboard.PressAsync("Escape");
                break;
            case "backdrop":
                var at = await page.EvaluateAsync<double[]>("""
                    () => {
                      const r = document.getElementById('d').getBoundingClientRect();
                      const room = [r.left, innerWidth - r.right, r.top, innerHeight - r.bottom];
                      const spots = [[r.left / 2, innerHeight / 2], [(r.right + innerWidth) / 2, innerHeight / 2],
                                     [innerWidth / 2, r.top / 2], [innerWidth / 2, (r.bottom + innerHeight) / 2]];
                      return spots[room.indexOf(Math.max(...room))];
                    }
                    """);
                await page.Mouse.ClickAsync((float)at[0], (float)at[1]);
                break;
            case "close":
                await Js.Run(page, "() => document.getElementById('d').close()");
                break;
            default:
                await Js.Run(page, "() => sednaUi.modal.close('d')");
                break;
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task The_panel_and_its_backdrop_leave_together_by_every_route(string markup, string route)
    {
        if (NoBrowser) return;
        var (page, errors, restMove, restDim, extent) = await Opened(markup);

        await Js.Run(page, Sampler);
        await Close(page, route);
        var samples = await Read(page);

        AssertLeavesTogether(samples, restMove, restDim, extent);
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(Dialogs))]
    public async Task Every_drawer_and_sheet_slides_out_from_where_it_rests(string markup)
    {
        if (NoBrowser) return;
        var (page, errors, restMove, restDim, extent) = await Opened(markup);

        await Js.Run(page, Sampler);
        await Close(page, "button");
        var samples = await Read(page);

        AssertLeavesTogether(samples, restMove, restDim, extent);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Reduced_motion_closes_it_at_once()
    {
        if (NoBrowser) return;
        var page = await Open(
            $"""<dialog id="d" class="drawer sheet" data-sheet data-sheet-snap="50%">{Panel}</dialog>""",
            head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);
        await Js.Run(page, "() => { sednaUi.modal.show('d'); }");
        await Js.Settle(page);

        await Js.Run(page, "() => document.getElementById('d').close()");
        await Js.Frames(page, 1);

        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('d').hasAttribute('data-sedna-closing')"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('d')).display"));
    }
}
