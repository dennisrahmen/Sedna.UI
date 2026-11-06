using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A day marker's icon and its count are drawn on one centre line, and that line is the day
/// number's: measured on the ink, which is what the eye compares.
/// </summary>
public class CalendarMarkInkTests : ScriptTestBase
{
    private const string Day = """
        <div class="cal-month" style="width:740px; --cal-lanes: 1">
          <div class="cal-row">
            <div class="cal-day"><span class="cal-day-num" id="num">7</span>
              <span class="cal-marks"><button class="cal-mark" type="button"><i class="ri-checkbox-blank-fill" id="icon"></i><span id="count">09:00 +1</span></button></span>
            </div>
            <div class="cal-day"></div><div class="cal-day"></div><div class="cal-day"></div>
            <div class="cal-day"></div><div class="cal-day"></div><div class="cal-day"></div>
          </div>
        </div>
        """;

    [Fact]
    public async Task The_icon_the_count_and_the_day_number_share_one_centre_line()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Day, deviceScaleFactor: Ink.Scale);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");

        var ink = await Ink.Of(page, "#icon", "#count", "#num");
        var (icon, count, num) = (Ink.Centre(ink[0]), Ink.Centre(ink[1]), Ink.Centre(ink[2]));

        Assert.True(Math.Abs(icon - count) <= 0.5, $"The icon's ink is centred {icon - count:0.00}px from the count's.");
        Assert.True(Math.Abs(count - num) <= 0.5, $"The count's ink is centred {count - num:0.00}px from the day number's.");
        Assert.Empty(errors);
    }
}
