using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What sweeps across a track — the indeterminate progress bar, the busy bar along a card's top edge
/// and a skeleton's band of light — is one continuous motion: it begins wholly outside the track at
/// the inline start, crosses it, and ends wholly outside at the inline end, so the track's edge is
/// what shows it arriving and leaving and nothing pops in or vanishes on the way. Read from the
/// animation itself, at its first and last frame, in either direction.
/// </summary>
public class SweepTests : ScriptTestBase
{
    private const string Fixture = """
        <div style="width: 300px; padding: 20px">
          <div class="progress progress--indeterminate" id="progress"><div class="progress-bar" id="progress-bar"></div></div>
          <div class="card" id="busy" aria-busy="true"><div class="card-body"><p>ORD-4182</p></div></div>
          <div class="skeleton skeleton-text" id="skeleton"></div>
        </div>
        """;

    /// <summary>The sweeping part's [left, right] in the track's own coordinates, and the track's width, for one moment of the animation.</summary>
    private const string Probe = """
        (args) => {
            const [kind, at] = args;
            const names = { progress: 'sedna-progress', busy: 'sedna-busy-bar', skeleton: 'sedna-skeleton' };
            const track = document.getElementById(kind);
            const animation = track.getAnimations({ subtree: true }).find(a => a.animationName === names[kind]);
            if (!animation) return null;
            animation.pause();
            const duration = animation.effect.getComputedTiming().duration;
            animation.currentTime = at === 'start' ? 0 : at === 'end' ? duration - 0.001 : duration / 2;
            const w = kind === 'progress' ? track.getBoundingClientRect().width : track.clientWidth;
            if (kind === 'progress') {
                const t = track.getBoundingClientRect(), b = document.getElementById('progress-bar').getBoundingClientRect();
                return [b.left - t.left, b.right - t.left, w];
            }
            // A background's position is a share of the track less the sweeping part, which is a share of the track.
            const first = getComputedStyle(track).backgroundPosition.split(',')[0].trim().split(' ')[0];
            const share = kind === 'busy' ? 0.3 : 0.5;
            const part = w * share;
            const left = parseFloat(first) / 100 * (w - part);
            return [left, left + part, w];
        }
        """;

    private async Task<IPage> Open(string dir, ReducedMotion motion = ReducedMotion.NoPreference)
    {
        var page = await Open(Fixture, head: StylesheetTag, reducedMotion: motion);
        await Js.Run(page, $"() => {{ document.documentElement.dir = '{dir}'; }}");
        await Js.Settle(page);
        return page;
    }

    private static async Task<double[]> At(IPage page, string kind, string moment) =>
        (await page.EvaluateAsync<JsonElement>(Probe, new object[] { kind, moment })).EnumerateArray().Select(e => e.GetDouble()).ToArray();

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var kind in new[] { "progress", "busy", "skeleton" })
            foreach (var dir in new[] { "ltr", "rtl" })
                data.Add(kind, dir);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_sweep_starts_wholly_outside_the_inline_start_and_ends_wholly_outside_the_inline_end(string kind, string dir)
    {
        if (NoBrowser) return;
        var page = await Open(dir);
        var start = await At(page, kind, "start");
        var middle = await At(page, kind, "middle");
        var end = await At(page, kind, "end");
        var width = start[2];
        const double Slack = 0.5;

        if (dir == "ltr")
        {
            Assert.True(start[1] <= Slack, $"{kind}: it starts with {start[1]:0.0}px of itself inside the track (it should be wholly outside the start, at {start[0]:0.0}…{start[1]:0.0} of {width:0}).");
            Assert.True(end[0] >= width - Slack, $"{kind}: it ends with {width - end[0]:0.0}px of itself still inside the track ({end[0]:0.0}…{end[1]:0.0} of {width:0}).");
        }
        else
        {
            Assert.True(start[0] >= width - Slack, $"{kind}: right to left, it starts with {start[0] - width:0.0}px of itself inside the track ({start[0]:0.0}…{start[1]:0.0} of {width:0}).");
            Assert.True(end[1] <= Slack, $"{kind}: right to left, it ends with {end[1]:0.0}px of itself inside the track ({end[0]:0.0}…{end[1]:0.0} of {width:0}).");
        }
        // In between it is on the track, and moving the way the page reads.
        Assert.True(middle[0] < width && middle[1] > 0, $"{kind}: the middle of the sweep is off the track ({middle[0]:0.0}…{middle[1]:0.0} of {width:0}).");
        Assert.True(dir == "ltr" ? start[0] < middle[0] && middle[0] < end[0] : start[0] > middle[0] && middle[0] > end[0],
            $"{kind}: it does not run from the inline start to the inline end.");
    }

    [Fact]
    public async Task Under_reduced_motion_nothing_sweeps()
    {
        if (NoBrowser) return;
        var page = await Open("ltr", ReducedMotion.Reduce);
        var running = await page.EvaluateAsync<string[]>("""
            () => ['progress', 'busy', 'skeleton'].flatMap(id => document.getElementById(id).getAnimations({ subtree: true }).map(a => a.animationName))
            """);
        Assert.Empty(running);
    }
}
