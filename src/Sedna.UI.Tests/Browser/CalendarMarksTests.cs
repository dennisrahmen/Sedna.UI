using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A week of the month marks now with the timeline's line, and a day's head carries markers
/// that open their list.
/// </summary>
public class CalendarMarksTests : ScriptTestBase
{
    private static string Row(string weeknums) => $"""
        <div class="cal-month {weeknums}" style="width:740px; --cal-lanes: 1">
          <div class="cal-row" id="row">
            {(weeknums.Length > 0 ? "<span class=\"cal-weeknum\">45</span>" : "")}
            <div class="cal-day" id="d1"><span class="cal-day-num">6</span></div>
            <div class="cal-day" id="d2"><span class="cal-day-num">7</span></div>
            <div class="cal-day" id="d3"><span class="cal-day-num">8</span></div>
            <div class="cal-day"><span class="cal-day-num">9</span></div>
            <div class="cal-day"><span class="cal-day-num">10</span></div>
            <div class="cal-day"><span class="cal-day-num">11</span></div>
            <div class="cal-day"><span class="cal-day-num">12</span></div>
            <div class="cal-now" id="now" style="--cal-at: 2.5"></div>
          </div>
        </div>
        """;

    [Theory]
    [InlineData("")]
    [InlineData("cal-month--weeknums")]
    public async Task Now_is_a_line_through_the_week_at_its_point_in_the_days(string weeknums)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Row(weeknums));

        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const r = id => document.getElementById(id).getBoundingClientRect();
              const now = r('now'), row = r('row'), d3 = r('d3');
              return [now.left - (d3.left + d3.width / 2), now.top - row.top, row.bottom - now.bottom, now.width];
            }
            """);

        Assert.InRange(m[0], -1.5, 1.5);    // half way through the third day
        Assert.InRange(m[1], -0.5, 0.5);    // the whole height of the row
        Assert.InRange(m[2], -0.5, 0.5);
        Assert.Equal(2, m[3], 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_marker_sits_at_the_end_of_the_head_and_opens_its_list()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="cal-month" style="width:740px; --cal-lanes: 1">
              <div class="cal-row">
                <div class="cal-day" id="day"><span class="cal-day-num" id="num">6</span>
                  <span class="cal-marks"><button class="cal-mark" id="mark" type="button" data-popover="due" aria-label="3 tasks due, show them"><i class="ri-checkbox-circle-line"></i>3</button></span>
                </div>
                <div class="cal-day"></div><div class="cal-day"></div><div class="cal-day"></div>
                <div class="cal-day"></div><div class="cal-day"></div><div class="cal-day"></div>
              </div>
            </div>
            <div class="popover" id="due" popover><p>Three tasks</p></div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const r = id => document.getElementById(id).getBoundingClientRect();
              const day = r('day'), mark = r('mark'), num = r('num');
              return [day.right - mark.right, Math.abs(mark.top - num.top), mark.height];
            }
            """);
        Assert.InRange(m[0], 0, 12);        // at the end of the head, inside the cell's padding
        Assert.InRange(m[1], 0, 4);         // on the day number's line
        Assert.InRange(m[2], 19, 21);

        await page.ClickAsync("#mark");
        await Assertions.Expect(page.Locator("#due")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#mark")).ToHaveAttributeAsync("aria-expanded", "true");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_day_head_takes_its_tone_whole_and_its_column_stays_plain()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="cal-week cal-week--day-tones" style="--cal-days: 4; --cal-hours: 1; width: 600px">
              <div class="cal-week-head">
                <span></span>
                <span class="cal-weekday" id="plain">Fri 2</span>
                <span class="cal-weekday cal-day--weekend" id="weekend">Sat 3</span>
                <span class="cal-weekday cal-day--weekend cal-day--holiday" id="both">Sun 4</span>
                <span class="cal-weekday cal-day--holiday" id="holiday">Mon 5</span>
              </div>
              <div class="cal-week-body">
                <div class="cal-hours"><span class="cal-hour">09:00</span></div>
                <div class="cal-day-col" id="col"></div><div class="cal-day-col"></div><div class="cal-day-col"></div><div class="cal-day-col"></div>
              </div>
            </div>
            <span id="info" style="background: var(--info-bg); color: var(--info-fg)"></span>
            <span id="danger" style="background: var(--danger-bg); color: var(--danger-fg)"></span>
            """);

        var c = await page.EvaluateAsync<string[]>("""
            () => ['plain', 'weekend', 'both', 'holiday', 'info', 'danger'].map(id => {
                const s = getComputedStyle(document.getElementById(id));
                return s.backgroundColor + ' / ' + s.color;
            })
            """);
        Assert.NotEqual(c[4], c[0]);
        Assert.Equal(c[4], c[1]);    // a weekend in the info tone
        Assert.Equal(c[5], c[2]);    // a holiday on a weekend reads as a holiday
        Assert.Equal(c[5], c[3]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Markers_in_a_column_head_follow_its_date()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="cal-week" style="--cal-days: 1; --cal-hours: 1; width: 400px">
              <div class="cal-week-head">
                <span></span>
                <span class="cal-weekday" id="head"><span id="date">Fri 2</span>
                  <span class="cal-marks"><span class="cal-mark" id="mark" role="img" aria-label="Deadline"><i class="ri-flag-line"></i></span></span>
                </span>
              </div>
            </div>
            """);

        var gap = await page.EvaluateAsync<double>("""
            () => document.getElementById('mark').getBoundingClientRect().left - document.getElementById('date').getBoundingClientRect().right
            """);
        Assert.InRange(gap, 0, 12);
    }
}
