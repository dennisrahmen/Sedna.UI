using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A stat that rolls up to its figure: the count is drawn over the app's own text while it runs,
/// the text is what a screen reader reads throughout, the text is what is left when it ends, and
/// under reduced motion there is no count at all (54-stat.css).
/// </summary>
public class RollingNumberTests : ScriptTestBase
{
    private const string Fixture = """
        <div class="stat" id="tile">
          <span class="stat-label">Orders today</span>
          <span class="stat-value stat-value--roll" id="v" style="--roll-to: 1240">1 240<span class="stat-unit" id="unit"> orders</span></span>
        </div>
        """;

    private const string Read = """
        () => {
          const v = document.getElementById('v');
          const after = getComputedStyle(v, '::after');
          return [getComputedStyle(v).getPropertyValue('--stat-roll').trim(),
                  after.opacity,
                  getComputedStyle(v).webkitTextFillColor,
                  getComputedStyle(document.getElementById('unit')).webkitTextFillColor,
                  v.textContent,
                  String(v.getAnimations().length),
                  after.width];
        }
        """;

    private const string Transparent = "rgba(0, 0, 0, 0)";

    private static double Px(string css) =>
        double.Parse(css.Replace("px", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task While_it_rolls_the_count_is_drawn_over_the_text_and_the_text_is_what_is_read()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: StylesheetTag, reducedMotion: ReducedMotion.NoPreference);

        // At the start and halfway, held still: the animation's own clock, not the test runner's.
        const string At = """
            f => { const a = document.getElementById('v').getAnimations()[0];
                   a.pause(); a.currentTime = a.effect.getComputedTiming().duration * f; }
            """;
        await Js.Run(page, At, 0);
        var start = await page.EvaluateAsync<string[]>(Read);
        await Js.Run(page, At, 0.5);
        var mid = await page.EvaluateAsync<string[]>(Read);

        // WebKit reports the registered integer mid-interpolation as a fraction; the count rounds it.
        var count = (int)Math.Round(double.Parse(mid[0], System.Globalization.CultureInfo.InvariantCulture));
        Assert.InRange(count, 1, 1239);
        // What is drawn is that count: one digit at the start, three or four digits halfway.
        var digit = Px(start[6]);
        Assert.InRange(Px(mid[6]) / digit, 2.5, 4.5);
        Assert.Equal("1", mid[1]);                 // the count is shown
        Assert.Equal(Transparent, mid[2]);         // over a text whose fill is transparent
        Assert.NotEqual(Transparent, mid[3]);      // a unit keeps its ink
        Assert.Equal("1 240 orders", mid[4]);      // and the text never changed

        var spoken = await page.Locator("#tile").AriaSnapshotAsync();
        Assert.Contains("1 240", spoken, StringComparison.Ordinal);
        Assert.DoesNotContain(count.ToString(System.Globalization.CultureInfo.InvariantCulture), spoken.Replace("1 240", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_it_ends_the_text_is_what_is_left()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: StylesheetTag, reducedMotion: ReducedMotion.NoPreference);
        await Js.Settle(page);
        var end = await page.EvaluateAsync<string[]>(Read);

        Assert.Equal("0", end[1]);
        Assert.NotEqual(Transparent, end[2]);
        Assert.Equal("0", end[5]);
    }

    [Fact]
    public async Task Under_reduced_motion_the_figure_is_simply_there()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);
        var still = await page.EvaluateAsync<string[]>(Read);

        Assert.Equal("0", still[5]);               // nothing runs
        Assert.Equal("0", still[1]);               // no count is drawn
        Assert.NotEqual(Transparent, still[2]);    // and the text is painted
    }
}
