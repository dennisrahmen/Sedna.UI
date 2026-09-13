using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The calendar's geometry is all custom properties and grid lines the app writes, so what
/// has to be proved is that they land where they say: a pill over exactly its days, a
/// continuing pill flush with the week's edge, "+N more" below the lanes, and a block at its
/// hour and in its lane.
/// </summary>
public class CalendarLayoutTests : ScriptTestBase
{
    private static string Days(int count, string extra = "") =>
        string.Concat(Enumerable.Range(1, count).Select(i => $"""<div class="cal-day"{extra}><span class="cal-day-num">{i}</span></div>"""));

    [Fact]
    public async Task A_pill_covers_exactly_its_days_and_more_sits_below_the_lanes()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div class="cal-month" style="--cal-lanes: 2; width: 700px">
              <div class="cal-row" id="row">
                {Days(7)}
                <div class="cal-row-events">
                  <a class="cal-event" id="span" href="#" style="grid-column: 2 / span 3; grid-row: 1">Stocktake</a>
                  <a class="cal-event cal-event--continues-end" id="cont" href="#" style="grid-column: 6 / span 2; grid-row: 2">Rehearsal</a>
                  <span class="cal-more" id="more" style="grid-column: 1">+3 more</span>
                </div>
              </div>
            </div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const days = [...document.querySelectorAll('#row .cal-day')].map(d => d.getBoundingClientRect());
                const r = id => document.getElementById(id).getBoundingClientRect();
                const row = document.getElementById('row').getBoundingClientRect();
                return [r('span').left - days[1].left, days[3].right - r('span').right,
                        row.right - r('cont').right,
                        r('more').top - r('cont').bottom,
                        days[0].bottom - r('more').bottom];
            }
            """);

        // Inset by its margin on both ends, and no further: it covers days 2–4.
        Assert.InRange(m[0], 0, 6);
        Assert.InRange(m[1], 0, 6);
        // The continuing edge has no margin: flush with the week's edge.
        Assert.Equal(0, m[2], 1.0);
        // "+3 more" is in the row after the last lane, and still inside the day.
        Assert.True(m[3] >= 0, $"+N more overlaps the last lane by {-m[3]}px.");
        Assert.True(m[4] >= 0, $"+N more hangs {-m[4]}px out of the day.");
    }

    [Fact]
    public async Task A_block_sits_at_its_hour_and_side_by_side_in_its_lane()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="cal-week" style="--cal-days: 1; --cal-hours: 8; --cal-hour-height: 40px; width: 400px">
              <div class="cal-week-body">
                <div class="cal-hours"></div>
                <div class="cal-day-col" id="col">
                  <div class="cal-block" id="a" style="--cal-start: 2.5; --cal-span: 1.5; --cal-lane: 0; --cal-lanes: 2">A</div>
                  <div class="cal-block" id="b" style="--cal-start: 3; --cal-span: 1; --cal-lane: 1; --cal-lanes: 2">B</div>
                  <div class="cal-now" id="now" style="--cal-at: 1.25"></div>
                </div>
              </div>
            </div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const col = document.getElementById('col').getBoundingClientRect();
                const r = id => document.getElementById(id).getBoundingClientRect();
                return [r('a').top - col.top, r('a').height, r('b').top - col.top,
                        r('b').left - col.left, col.width, r('a').right <= r('b').left ? 1 : 0,
                        r('now').top - col.top, col.height];
            }
            """);

        Assert.Equal(100, m[0], 1.0);            // 2.5 h × 40 px
        Assert.InRange(m[1], 56, 60);          // 1.5 h, less the gap between blocks
        Assert.Equal(120, m[2], 1.0);
        Assert.Equal(m[4] / 2, m[3], 1.0);       // lane 1 of 2 starts half way
        Assert.Equal(1, m[5]);                 // and the two do not overlap
        Assert.Equal(50, m[6], 1.0);             // the now line at 1.25 h
        Assert.Equal(320, m[7], 1.0);            // 8 hours of axis
    }

    [Fact]
    public async Task A_range_is_solid_at_its_ends_and_a_band_between()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="cal-month cal-month--mini" style="width: 280px">
              <div class="cal-row">
                <button class="cal-day cal-day--range-start" id="s" type="button" aria-label="Monday 21 September 2026" aria-pressed="true">21</button>
                <button class="cal-day cal-day--in-range" id="i" type="button" aria-label="Tuesday 22 September 2026" aria-pressed="true">22</button>
                <button class="cal-day cal-day--range-end" id="e" type="button" aria-label="Wednesday 23 September 2026" aria-pressed="true">23</button>
                <button class="cal-day" id="n" type="button" aria-label="Thursday 24 September 2026" aria-pressed="false">24</button>
                <button class="cal-day" id="d" type="button" aria-label="Friday 25 September 2026" disabled>25</button>
                <span class="cal-day"></span><span class="cal-day"></span>
              </div>
            </div>
            """);

        var s = await page.EvaluateAsync<string[]>("""
            () => ['s', 'i', 'e', 'n'].map(id => getComputedStyle(document.getElementById(id)).backgroundColor)
                .concat([getComputedStyle(document.getElementById('i')).borderTopLeftRadius,
                         getComputedStyle(document.getElementById('d')).textDecorationLine,
                         String(document.getElementById('s').getBoundingClientRect().width === document.getElementById('s').getBoundingClientRect().height)])
            """);

        Assert.Equal(s[0], s[2]);                  // both ends are the same solid
        Assert.NotEqual(s[0], s[1]);               // the band is a different, lighter fill
        Assert.Equal("rgba(0, 0, 0, 0)", s[3]);    // an unchosen day has none
        Assert.Equal("0px", s[4]);                 // the band runs edge to edge
        Assert.Equal("line-through", s[5]);        // an unavailable day is struck, not only faded
        Assert.Equal("True", s[6], ignoreCase: true); // square days
    }
}
