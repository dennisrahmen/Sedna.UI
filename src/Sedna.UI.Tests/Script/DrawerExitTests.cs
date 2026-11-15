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
/// dialog and dims the page with its own shadow. The dim is read from whichever draws it, at
/// twenty stated points of the exit, and the same assertions hold for both.
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

    /// <summary>
    /// The slide at a length no machine reaches the end of while the test is reading it. The exit is
    /// not watched as it plays: every animation it runs is paused and set to a stated fraction of
    /// its own length, so what is read is what the exit looks like at that point however many
    /// frames the browser managed to draw. Every assertion below is about order and end points,
    /// which a slide of any length leaves as they are.
    /// </summary>
    private const string Slow = "<style>:root { --motion-mid: 60s; }</style>";

    /// <summary>
    /// Run after the dialog closed, in the same task as the close where the engine allows. Waits
    /// until the exit is running, then steps every finite animation on the page through the
    /// fractions of its length in turn and reads the dialog and its dim at each, by the animation
    /// clock. Chromium runs the exit as transitions of the dialog and its backdrop; WebKit as one
    /// animation the library plays on a held dialog, whose shadow is the dim. Both are in
    /// <c>document.getAnimations()</c>.
    /// </summary>
    private const string Sampler = """
        async () => {
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
          const running = () => document.getAnimations().filter(a => a.effect?.getComputedTiming().endTime !== Infinity
            && a.effect.getComputedTiming().endTime > 0 && a.playState !== 'finished');
          // The exit is started by the close: in the next style update, or in WebKit by the close event.
          // A frame count, not a time, and a limit: a close that starts no exit must fail the
          // assertions below and not leave this call waiting for ever.
          for (let frames = 0; running().length === 0; frames++) {
            if (frames > 600) return [];
            await new Promise(r => requestAnimationFrame(r));
          }
          const exit = running();
          exit.forEach(a => a.pause());
          const read = at => {
            exit.forEach(a => { a.currentTime = a.effect.getComputedTiming().endTime * at; });
            const cs = getComputedStyle(d);
            const held = d.hasAttribute('data-sedna-closing');
            return {
              t: Math.round(at * 100),
              open: d.open,
              shown: cs.display !== 'none',
              move: move(cs.transform),
              dim: alpha(held ? cs.boxShadow : getComputedStyle(d, '::backdrop').backgroundColor),
            };
          };
          const samples = [];
          for (let i = 0; i < 20; i++) samples.push(read(i / 20));
          // Where it ends: every animation run to its end, and the dialog taken away.
          exit.forEach(a => a.finish());
          await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
          const cs = getComputedStyle(d);
          samples.push({
            t: 100, open: d.open, shown: cs.display !== 'none', move: move(cs.transform),
            dim: alpha(d.hasAttribute('data-sedna-closing') ? cs.boxShadow : getComputedStyle(d, '::backdrop').backgroundColor),
          });
          return samples;
        }
        """;

    private record Sample(int T, bool Open, bool Shown, double Move, double Dim);

    private async Task<(IPage Page, List<string> Errors, double RestMove, double RestDim, double Extent)> Opened(string markup)
    {
        var (page, errors) = await OpenStyled(Slow + markup);
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
        var json = await page.EvaluateAsync<string>("async () => JSON.stringify(await (" + Sampler + ")())");
        return JsonSerializer.Deserialize<List<Sample>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    /// <summary>The points of the exit at which the dialog had closed and was still on screen.</summary>
    private static List<Sample> Leaving(List<Sample> samples) => samples.Where(s => !s.Open && s.Shown).ToList();

    private static void AssertLeavesTogether(List<Sample> samples, double restMove, double restDim, double extent)
    {
        var leaving = Leaving(samples);
        Assert.True(leaving.Count >= 2, "The panel was on screen at fewer than two points of its exit: it vanished instead of sliding out.");

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
        Assert.True(!samples.Last().Shown && samples.Last().Dim <= 0.02, "Nothing is left on screen.");
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
