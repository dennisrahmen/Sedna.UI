using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.progress--route</c>: a thin bar along the top edge of the window, above the frame,
/// shown and hidden by the <c>hidden</c> attribute, faded in after a moment, and standing
/// still under reduced motion.
/// </summary>
public class RouteProgressTests : ScriptTestBase
{
    private const string Fixture = """
        <div class="progress progress--route progress--indeterminate" id="route" role="progressbar" aria-label="Loading the page" hidden>
          <div class="progress-bar" id="bar"></div>
        </div>
        <div class="layout">
          <div class="content">
            <header class="topbar" id="topbar"><span class="topbar-spacer"></span></header>
            <main class="page"><p>The page.</p></main>
          </div>
        </div>
        """;

    private sealed record Reading(string Display, double[] Box, double Viewport, string ZIndex, string PointerEvents, double[] Bar, string AtTop);

    private static async Task<Reading> Read(IPage page) =>
        System.Text.Json.JsonSerializer.Deserialize<Reading>(await page.EvaluateAsync<string>("""
            () => {
              const el = document.getElementById('route');
              const r = e => { const x = e.getBoundingClientRect(); return [x.left, x.right, x.top, x.bottom]; };
              const cs = getComputedStyle(el);
              const hit = document.elementFromPoint(window.innerWidth / 2, 1);
              return JSON.stringify({
                display: cs.display, box: r(el), viewport: window.innerWidth, zIndex: cs.zIndex,
                pointerEvents: cs.pointerEvents, bar: r(document.getElementById('bar')),
                atTop: hit && hit.closest('#topbar') ? 'topbar' : (hit ? hit.id || hit.className : ''),
              });
            }
            """), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

    [Fact]
    public async Task Hidden_it_takes_no_room_and_shown_it_runs_along_the_top_of_the_window()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        await page.SetViewportSizeAsync(1280, 700);

        Assert.Equal("none", (await Read(page)).Display);

        await Js.Run(page, "() => document.getElementById('route').hidden = false");
        await Js.Settle(page);
        var r = await Read(page);
        Assert.Equal(0, r.Box[0], 0.5);
        Assert.Equal(r.Viewport, r.Box[1], 0.5);
        Assert.Equal(0, r.Box[2], 0.5);
        Assert.Equal(3, r.Box[3] - r.Box[2], 0.5);
        Assert.Equal("1000", r.ZIndex);
        // It sits over the topbar and takes no pointer: a click there still reaches the bar beneath.
        Assert.Equal("none", r.PointerEvents);
        Assert.Equal("topbar", r.AtTop);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task It_fades_in_only_after_a_moment()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);

        await Js.Run(page, "() => document.getElementById('route').hidden = false");
        var opacity = await page.EvaluateAsync<double[]>("""
            () => {
              const a = document.getElementById('route').getAnimations().find(x => x.animationName === 'sedna-progress-route');
              a.pause();
              a.currentTime = 0;
              const start = parseFloat(getComputedStyle(a.effect.target).opacity);
              a.finish();
              return [start, parseFloat(getComputedStyle(a.effect.target).opacity)];
            }
            """);
        Assert.Equal(0, opacity[0], 2);
        Assert.Equal(1, opacity[1], 2);
    }

    [Fact]
    public async Task Under_reduced_motion_it_is_there_at_once_and_filled()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);
        await page.SetViewportSizeAsync(1280, 700);

        await Js.Run(page, "() => document.getElementById('route').hidden = false");
        await Js.Frames(page);
        Assert.Equal(0, await page.EvaluateAsync<int>("() => document.getElementById('route').getAnimations({ subtree: true }).length"));
        Assert.Equal("1", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('route')).opacity"));
        var r = await Read(page);
        Assert.Equal(r.Viewport, r.Bar[1] - r.Bar[0], 0.5);
    }
}
