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
}
