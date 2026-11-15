using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A day head in a row of days: one that is a date is set as words, not as the weekday label's
/// capitals, and the weekend and holiday tones reach any head in the row — the library's
/// <c>.cal-weekday</c> or an element of the app's own.
/// </summary>
public class CalendarDayHeadTests : ScriptTestBase
{
    private const string Row = """
        <div class="cal-month cal-month--day-tones" style="width: 700px">
          <div class="cal-weekdays">
            <span class="cal-weekday" id="label">Mon</span>
            <span class="cal-weekday cal-weekday--date" id="date">Tue 17 Nov</span>
            <span class="cal-weekday cal-weekday--date cal-day--weekend" id="weekend">Sat 21 Nov</span>
            <div class="cal-day--holiday" id="own">Sun 22 Nov</div>
            <span class="cal-weekday">Wed</span><span class="cal-weekday">Thu</span><span class="cal-weekday">Fri</span>
          </div>
        </div>
        """;

    [Fact]
    public async Task A_date_head_drops_the_label_typography_and_keeps_its_tone()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Row);
        var r = await page.EvaluateAsync<string[]>(@"() => {
            const cs = id => getComputedStyle(document.getElementById(id));
            const probe = (name) => { const i = document.createElement('i'); i.style.color = 'var(' + name + ')'; document.querySelector('.cal-month').append(i); const c = getComputedStyle(i).color; i.remove(); return c; };
            const bg = (name) => { const i = document.createElement('i'); i.style.backgroundColor = 'var(' + name + ')'; document.querySelector('.cal-month').append(i); const c = getComputedStyle(i).backgroundColor; i.remove(); return c; };
            return [cs('label').textTransform, cs('date').textTransform, cs('date').letterSpacing,
                    cs('weekend').backgroundColor, bg('--cal-weekend-tone-bg'), cs('weekend').color, probe('--cal-weekend-tone-fg'),
                    cs('own').backgroundColor, bg('--cal-holiday-tone-bg')];
        }");
        Assert.Equal("uppercase", r[0]);
        Assert.Equal("none", r[1]);
        Assert.True(r[2] is "normal" or "0px", $"A date head keeps the label's letter spacing: {r[2]}.");
        Assert.Equal(r[4], r[3]);
        Assert.Equal(r[6], r[5]);
        Assert.Equal(r[8], r[7]);
    }
}
