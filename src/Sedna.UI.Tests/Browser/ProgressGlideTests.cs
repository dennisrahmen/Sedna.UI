using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every determinate progress glides to a new value and lands at once under reduced motion — the
/// bar by its width, the ring by its registered <c>--progress</c> — and the ring's round ends sit on
/// its centre line, where an SVG ring's <c>stroke-linecap: round</c> would put them (41-progress.css).
/// </summary>
public class ProgressGlideTests : ScriptTestBase
{
    private const string Fixture = """
        <div class="progress" style="width:200px"><div class="progress-bar" id="bar" style="width:10%"></div></div>
        <span class="progress-ring" id="ring" style="--progress: 10%">10%</span>
        """;

    private const string Change = """
        () => {
            const bar = document.getElementById('bar'), ring = document.getElementById('ring');
            getComputedStyle(bar).width; getComputedStyle(ring).getPropertyValue('--progress');
            bar.style.width = '90%';
            ring.style.setProperty('--progress', '90%');
            return [bar.getAnimations().length, ring.getAnimations().length,
                    ring.getAnimations().map(a => a.transitionProperty).join(',')];
        }
        """;

    [Fact]
    public async Task Every_determinate_progress_glides_to_a_new_value()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: StylesheetTag, reducedMotion: ReducedMotion.NoPreference);
        var running = await page.EvaluateAsync<object[]>(Change);
        Assert.Equal(1, Convert.ToInt32(running[0], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, Convert.ToInt32(running[1], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("--progress", running[2]);
    }

    [Fact]
    public async Task Under_reduced_motion_every_progress_lands_at_once()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);
        var running = await page.EvaluateAsync<object[]>(Change);
        Assert.Equal(0, Convert.ToInt32(running[0], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0, Convert.ToInt32(running[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Blazors_load_percentage_still_drives_the_ring()
    {
        if (NoBrowser) return;
        var page = await Open("""<span class="progress-ring" id="ring" style="--progress: var(--blazor-load-percentage, 0%)"></span>""",
            head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);
        await page.EvaluateAsync("() => document.documentElement.style.setProperty('--blazor-load-percentage', '40%')");
        var value = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('ring')).getPropertyValue('--progress').trim()");
        Assert.Equal("40%", value);
    }

    [Theory]
    [InlineData("", 40, 4)]
    [InlineData("progress-ring--sm", 24, 3)]
    [InlineData("progress-ring--lg", 64, 6)]
    public async Task A_round_ring_puts_both_ends_on_its_centre_line(string size, double diameter, double width)
    {
        if (NoBrowser) return;
        var rings = string.Concat(new[] { 0, 25, 50, 62, 100 }.Select(p =>
            $"""<span class="progress-ring progress-ring--round {size}" data-p="{p}" style="--progress: {p}%"></span>"""));
        var page = await Open($"<div dir=\"rtl\">{rings}</div>", head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);

        var caps = await page.EvaluateAsync<double[][]>("""
            () => [...document.querySelectorAll('[data-p]')].map(r => {
                const s = getComputedStyle(r, '::after');
                return [+r.dataset.p, parseFloat(s.left) + parseFloat(s.width) / 2, parseFloat(s.top) + parseFloat(s.height) / 2,
                        parseFloat(s.width), getComputedStyle(r, '::before').backgroundImage.includes('radial-gradient') ? 1 : 0];
            })
            """);

        var radius = (diameter - width) / 2;
        foreach (var cap in caps)
        {
            var turn = cap[0] / 100 * 2 * Math.PI;
            Assert.Equal(diameter / 2 + radius * Math.Sin(turn), cap[1], 0.5);   // physical: a ring turns clockwise in any direction
            Assert.Equal(diameter / 2 - radius * Math.Cos(turn), cap[2], 0.5);
            Assert.Equal(width, cap[3], 0.01);
            Assert.Equal(1, cap[4]);                                            // and the start is round too
        }
    }
}
