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

    [Theory]
    [InlineData("", 1)]
    [InlineData("", 1.5)]
    [InlineData("", 0.5)]
    [InlineData("cal-event--outline", 1)]
    [InlineData("cal-event--tentative", 2)]
    public async Task A_block_shows_whole_lines_and_never_half_of_one(string variant, double span)
    {
        if (NoBrowser) return;
        // The 14:00 block that overflowed on the catalogue: a time and a title that wraps,
        // in a block too short for all of it.
        var (page, _) = await OpenStyled($$"""
            <div class="cal-week" style="--cal-days: 1; --cal-hours: 4; --cal-hour-height: 40px; width: 180px">
              <div class="cal-week-body">
                <div class="cal-hours"></div>
                <div class="cal-day-col">
                  <a class="cal-block cal-event--go {{variant}}" id="b" href="#" style="--cal-start: 0; --cal-span: {{span.ToString(System.Globalization.CultureInfo.InvariantCulture)}}"><span class="cal-event-time">14:00</span> Dispatch ORD-4209 to the north hub, with the carrier, the dock and the driver named in a title far too long for any block</a>
                </div>
              </div>
            </div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const b = document.getElementById('b');
                const s = getComputedStyle(b);
                const box = b.getBoundingClientRect();
                // The clip is the content box, and every line box is one line-height tall
                // from its top: so whole lines show exactly when that height is a multiple.
                const clipTop = box.top + parseFloat(s.borderTopWidth) + parseFloat(s.paddingTop);
                const clipBottom = box.bottom - parseFloat(s.borderBottomWidth) - parseFloat(s.paddingBottom);
                const lines = (clipBottom - clipTop) / parseFloat(s.lineHeight);
                return [Math.abs(lines - Math.round(lines)), Math.round(lines), clipBottom - box.bottom,
                        s.overflowX === 'clip' && s.overflowClipMargin.startsWith('content-box') ? 1 : 0,
                        b.scrollHeight > b.clientHeight ? 1 : 0];
            }
            """);

        Assert.True(m[0] < 0.05, $"The block's edge cuts a line {m[0]:0.00} of the way through.");
        Assert.True(m[1] >= 1, "The block shows no line at all.");
        Assert.True(m[2] <= 0, "The clip edge is outside the block.");
        Assert.Equal(1, m[3]);                 // clipped at the content box, not the padding
        Assert.Equal(1, m[4]);                 // and the fixture does overflow, or this proves nothing
    }

    [Fact]
    public async Task Week_numbers_take_a_column_of_their_own_and_the_lanes_stay_over_the_days()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div class="cal-month cal-month--weeknums" style="--cal-lanes: 2; width: 740px">
              <div class="cal-weekdays" id="head"><span class="cal-weekday"></span>{string.Concat(Enumerable.Range(1, 7).Select(i => $"<span class=\"cal-weekday\">D{i}</span>"))}</div>
              <div class="cal-row" id="row">
                <span class="cal-weeknum" id="wk">38</span>
                {Days(7)}
                <div class="cal-row-events">
                  <a class="cal-event" id="first" href="#" style="grid-column: 1; grid-row: 1">Stand-up</a>
                  <a class="cal-event" id="last" href="#" style="grid-column: 7; grid-row: 1">Rota</a>
                </div>
              </div>
            </div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const days = [...document.querySelectorAll('#row .cal-day')].map(d => d.getBoundingClientRect());
                const heads = [...document.querySelectorAll('#head .cal-weekday')].map(d => d.getBoundingClientRect());
                const r = id => document.getElementById(id).getBoundingClientRect();
                return [r('wk').width, days[0].left - r('wk').right,
                        r('first').left - days[0].left, days[6].right - r('last').right,
                        heads[1].left - days[0].left, days[0].width - days[6].width];
            }
            """);

        Assert.Equal(40, m[0], 1.0);      // --cal-weeknum-width
        Assert.Equal(0, m[1], 1.0);       // the days start where it ends
        Assert.InRange(m[2], 0, 6);       // column 1 is the first day, not the week number
        Assert.InRange(m[3], 0, 6);
        Assert.Equal(0, m[4], 1.0);       // the heading still sits over its day
        Assert.Equal(0, m[5], 1.0);
    }

    [Fact]
    public async Task A_growing_row_is_as_tall_as_its_last_lane_and_keeps_its_days_in_place()
    {
        if (NoBrowser) return;
        string Lanes(int n) => string.Concat(Enumerable.Range(1, n).Select(i =>
            $"""<a class="cal-event" href="#" style="grid-column: 3; grid-row: {i}">Event {i}</a>"""));

        var (page, _) = await OpenStyled($"""
            <div class="cal-month cal-month--grow cal-month--weeknums" style="--cal-lanes: 1; width: 740px">
              <div class="cal-row" id="busy">
                <span class="cal-weeknum">38</span>
                {Days(7)}
                <div class="cal-row-events">{Lanes(6)}<a class="cal-event" id="span" href="#" style="grid-column: 2 / span 3; grid-row: 7">Inspection</a></div>
              </div>
              <div class="cal-row" id="quiet">
                <span class="cal-weeknum">39</span>
                {Days(7)}
                <div class="cal-row-events"></div>
              </div>
            </div>
            <div class="cal-month" style="--cal-lanes: 1; width: 740px">
              <div class="cal-row" id="fixed">
                {Days(7)}
                <div class="cal-row-events">{Lanes(6)}</div>
              </div>
            </div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const r = el => el.getBoundingClientRect();
                const busy = document.getElementById('busy'), quiet = document.getElementById('quiet');
                const bd = [...busy.querySelectorAll('.cal-day')].map(r), qd = [...quiet.querySelectorAll('.cal-day')].map(r);
                const last = r(busy.querySelector('.cal-row-events').lastElementChild);
                const aligned = bd.every((d, i) => Math.abs(d.left - qd[i].left) < 1 && Math.abs(d.top - bd[0].top) < 1) ? 1 : 0;
                return [r(busy).bottom - last.bottom, r(busy).height, r(quiet).height,
                        r(document.getElementById('fixed')).height, aligned, bd[6].height - r(busy).height,
                        r(document.getElementById('span')).left - bd[1].left];
            }
            """);

        Assert.InRange(m[0], 0, 12);                          // the last lane is inside the row, near its foot
        Assert.True(m[1] > 7 * 20, $"The busy row is {m[1]}px, too short for seven lanes.");
        Assert.True(m[2] < m[1], "A quiet row grew as well.");
        Assert.True(m[3] < m[1], "The fixed-row month grew with its lanes.");
        Assert.Equal(1, m[4]);                                // days in their columns, in one row
        Assert.Equal(0, m[5], 1.0);                           // and as tall as the row
        Assert.InRange(m[6], 0, 6);                           // a span still starts on its day
    }

    [Fact]
    public async Task A_busy_day_gets_the_width_and_its_heading_follows()
    {
        if (NoBrowser) return;
        string Heads(string lanes) => string.Concat(Enumerable.Range(0, 7).Select(i =>
            $"""<span class="cal-weekday" style="--cal-lanes: {(i == 1 ? lanes : "1")}">D{i}</span>"""));
        string Cols(string lanes) => string.Concat(Enumerable.Range(0, 7).Select(i =>
            $"""<div class="cal-day-col" style="--cal-lanes: {(i == 1 ? lanes : "1")}"></div>"""));
        string Week(string id, string modifier, string style) => $"""
            <div class="cal-week {modifier}" id="{id}" style="--cal-hours: 2; {style}">
              <div class="cal-week-head"><span></span>{Heads("3")}</div>
              <div class="cal-week-body" style="overflow: hidden"><div class="cal-hours"></div>{Cols("3")}</div>
            </div>
            """;

        var (page, _) = await OpenStyled(
            Week("weighted", "cal-week--by-lanes", "width: 506px")
            + Week("floored", "cal-week--by-lanes", "--cal-day-min: 60px; width: 506px")
            + Week("even", "", "width: 506px")
            + $"""<div style="width: 300px"><div class="sedna-scroll-x">{Week("narrow", "cal-week--by-lanes", "--cal-day-min: 60px")}</div></div>""");

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const widths = id => [...document.querySelectorAll(`#${id} .cal-day-col`)].map(c => c.getBoundingClientRect().width);
                const offset = id => {
                    const h = [...document.querySelectorAll(`#${id} .cal-week-head .cal-weekday`)].map(e => e.getBoundingClientRect());
                    const c = [...document.querySelectorAll(`#${id} .cal-day-col`)].map(e => e.getBoundingClientRect());
                    return Math.max(...h.map((r, i) => Math.abs(r.left - c[i].left) + Math.abs(r.width - c[i].width)));
                };
                const w = widths('weighted'), f = widths('floored'), e = widths('even'), n = widths('narrow');
                const col = document.querySelector('#weighted .cal-day-col'), cs = getComputedStyle(col);
                const base = col.offsetWidth - col.clientWidth + parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
                return [(w[1] - base) / (w[0] - base), offset('weighted'), Math.max(...e) - Math.min(...e), Math.min(...f), Math.min(...n),
                        document.getElementById('narrow').getBoundingClientRect().width];
            }
            """);

        Assert.Equal(3, m[0], 0.05);            // three lanes, three times the width
        Assert.True(m[1] < 1, $"A heading is {m[1]}px off its column.");
        Assert.True(m[2] < 1, "Without the modifier the days are no longer even.");
        Assert.True(m[3] >= 59.5, $"A day is {m[3]}px, under --cal-day-min.");
        Assert.True(m[4] >= 59.5, $"A day in a narrow scroller is {m[4]}px, under --cal-day-min.");
        Assert.True(m[5] >= 56 + 7 * 60 - 1, "The week shrank below its days' minimum instead of scrolling.");
    }

    public static IEnumerable<object[]> FillsAndHues()
    {
        foreach (var variant in new[] { "dark", "light" })
            yield return [variant];
    }

    [Theory]
    [MemberData(nameof(FillsAndHues))]
    public async Task Every_hue_changes_every_fill_and_filled_text_is_readable(string variant)
    {
        if (NoBrowser) return;
        string[] hues = ["", "go", "warn", "danger", "info", "cyan", "orange", "teal"];
        string[] fills = ["", "filled", "outline", "tentative"];
        var body = string.Concat(fills.SelectMany(f => hues.Select(h =>
        {
            var cls = (f == "" ? "" : $" cal-event--{f}") + (h == "" ? "" : $" cal-event--{h}");
            return $"""
                <div class="cal-month" style="width: 300px"><div class="cal-row">{Days(7)}<div class="cal-row-events">
                  <a class="cal-event{cls}" data-f="{f}" data-h="{h}" href="#" style="grid-column: 1 / span 7; grid-row: 1"><span class="cal-event-time">09:00</span> Title</a>
                </div></div></div>
                """;
        })));

        var (page, _) = await OpenStyled(body);
        await page.EvaluateAsync($"() => document.documentElement.setAttribute('data-variant', '{variant}')");

        var rows = await page.EvaluateAsync<string[][]>("""
            () => {
                const rgb = c => (c.match(/[\d.]+/g) || []).map(Number);
                const lum = ([r, g, b]) => [r, g, b].map(v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; })
                    .reduce((s, v, i) => s + v * [0.2126, 0.7152, 0.0722][i], 0);
                const ratio = (a, b) => { const [x, y] = [lum(rgb(a)), lum(rgb(b))].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };
                return [...document.querySelectorAll('[data-f]')].map(e => {
                    const s = getComputedStyle(e), t = getComputedStyle(e.querySelector('.cal-event-time'));
                    return [e.dataset.f, e.dataset.h, s.backgroundColor + '|' + s.backgroundImage, s.borderInlineStartColor, s.borderTopStyle,
                            ratio(s.color, s.backgroundColor).toFixed(2), ratio(t.color, s.backgroundColor).toFixed(2)];
                });
            }
            """);

        foreach (var fill in new[] { "", "filled", "outline", "tentative" })
        {
            var set = rows.Where(r => r[0] == fill).ToList();
            // Each hue paints a different accent inside the same fill: a family is not
            // outranked by a variant.
            Assert.Equal(set.Count, set.Select(r => r[3]).Distinct().Count());
            if (fill is "" or "filled" or "tentative")
                Assert.Equal(set.Count, set.Select(r => r[2]).Distinct().Count());
        }

        Assert.All(rows.Where(r => r[0] == "outline" || r[0] == "tentative"), r => Assert.NotEqual("none", r[4]));
        Assert.All(rows.Where(r => r[0] == "tentative"), r => Assert.Equal("dashed", r[4]));
        Assert.All(rows.Where(r => r[0] == "filled"), r =>
        {
            Assert.True(double.Parse(r[5], System.Globalization.CultureInfo.InvariantCulture) >= 4.5,
                $"Filled {r[1]} title is {r[5]}:1 in {variant}.");
            Assert.True(double.Parse(r[6], System.Globalization.CultureInfo.InvariantCulture) >= 4.5,
                $"Filled {r[1]} time is {r[6]}:1 in {variant}.");
        });
    }

    [Fact]
    public async Task Avatars_keep_the_trailing_edge_and_the_title_gives_way()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div class="cal-month" style="width: 420px"><div class="cal-row">{Days(7)}<div class="cal-row-events">
              <a class="cal-event" id="e" href="#" style="grid-column: 1 / span 2; grid-row: 1">
                <span class="cal-event-dot cal-event-dot--live" id="dot"></span>
                <span class="cal-event-title" id="t">Carrier onboarding with a title far too long for two days</span>
                <span class="avatar-group cal-event-avatars" id="g"><span class="avatar">AF</span><span class="avatar">PN</span></span>
              </a>
            </div></div></div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const r = id => document.getElementById(id).getBoundingClientRect();
                const e = document.getElementById('e'), t = document.getElementById('t');
                const pad = parseFloat(getComputedStyle(e).paddingRight);
                return [r('e').right - pad - r('g').right, r('g').height, r('e').height - r('g').height,
                        t.scrollWidth > t.clientWidth ? 1 : 0, r('g').left - r('t').right, r('dot').width];
            }
            """);

        Assert.Equal(0, m[0], 1.0);          // flush with the pill's end
        Assert.True(m[2] >= 0, "The avatars are taller than the pill.");
        Assert.Equal(1, m[3]);               // the title truncates
        Assert.True(m[4] >= 0, "The title runs under the avatars.");
        Assert.True(m[5] > 0);
    }

    [Fact]
    public async Task A_holiday_name_steps_aside_for_its_hint_when_the_cell_is_narrow()
    {
        if (NoBrowser) return;
        string Cell(string id, int width) => $"""
            <div class="cal-day cal-day--holiday" style="width: {width}px">
              <span class="cal-day-num">5</span>
              <span class="cal-holiday" id="{id}" role="img" aria-label="Holiday: Autumn bank holiday" data-tip="Autumn bank holiday">
                <i class="ri-flag-2-line"></i><span class="cal-holiday-name">Autumn bank holiday</span>
              </span>
            </div>
            """;

        var (page, _) = await OpenStyled(
            $"""<div style="padding: 60px">{Cell("wide", 260)}{Cell("cut", 120)}{Cell("narrow", 64)}</div>""");

        var shown = await page.EvaluateAsync<string[]>("""
            () => ['wide', 'cut', 'narrow'].map(id => getComputedStyle(document.querySelector(`#${id} .cal-holiday-name`)).display)
            """);
        Assert.NotEqual("none", shown[0]);
        Assert.NotEqual("none", shown[1]);
        Assert.Equal("none", shown[2]);

        async Task<bool> TipFor(string id)
        {
            await page.Mouse.MoveAsync(0, 0);
            await page.WaitForTimeoutAsync(50);
            await page.HoverAsync($"#{id}");
            await page.WaitForTimeoutAsync(300);
            return await page.EvaluateAsync<bool>("() => !!document.querySelector('.sedna-tip--visible')");
        }

        Assert.False(await TipFor("wide"), "The whole name is on screen, and the hint repeated it.");
        Assert.True(await TipFor("cut"), "The name is cut short, and no hint gave it back.");
        Assert.True(await TipFor("narrow"), "The name is hidden, and no hint gave it back.");
    }
    [Fact]
    public async Task A_picker_s_time_row_and_its_levels_fit_the_panel_with_one_divider()
    {
        if (NoBrowser) return;
        var picks = string.Concat(Enumerable.Range(1, 12).Select(n => $"<button class=\"cal-pick\" type=\"button\" aria-pressed=\"{(n == 9 ? "true" : "false")}\"{(n == 9 ? " aria-current=\"date\"" : "")}>{n}</button>"));
        var (page, _) = await OpenStyled($"""
            <div class="datepicker" id="panel" style="position: static">
              <div class="cal-months" id="months">{picks}</div>
              <div class="cal-years" id="years">{picks}</div>
              <div class="datepicker-time" id="time">
                <label for="from">From</label>
                <input class="form-input form-input-sm" id="from" type="time" value="14:00">
                <span aria-hidden="true">&ndash;</span>
                <input class="form-input form-input-sm" id="to" type="time" value="15:30" aria-label="Until">
              </div>
              <div class="datepicker-actions" id="actions"><button class="btn btn-sm" type="button">Set</button></div>
            </div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const cols = id => getComputedStyle(document.getElementById(id)).gridTemplateColumns.split(' ').length;
                const from = document.getElementById('from').getBoundingClientRect();
                const to = document.getElementById('to').getBoundingClientRect();
                const panel = document.getElementById('panel').getBoundingClientRect();
                const chosenPick = getComputedStyle(document.querySelector('#months .cal-pick[aria-pressed="true"]'));
                const chosen = chosenPick.backgroundColor;
                const other = getComputedStyle(document.querySelector('#months .cal-pick[aria-pressed="false"]')).backgroundColor;
                const onSolid = getComputedStyle(document.querySelector('#months .cal-pick[aria-pressed="true"]')).color;
                const probe = document.createElement('span'); probe.style.color = 'var(--on-solid)'; document.body.append(probe);
                const expectedInk = getComputedStyle(probe).color; probe.remove();
                return [cols('months'), cols('years'), Math.abs(from.top - to.top), to.right <= panel.right ? 1 : 0,
                        parseFloat(getComputedStyle(document.getElementById('actions')).borderTopWidth),
                        chosen !== other ? 1 : 0, onSolid === expectedInk ? 1 : 0];
            }
            """);

        Assert.Equal(3, m[0]);            // twelve months in four rows of three
        Assert.Equal(4, m[1]);            // twelve years in three rows of four
        Assert.True(m[2] < 1, "A slot's two times wrapped onto two lines.");
        Assert.Equal(1, m[3]);            // and stay inside the panel
        Assert.Equal(0, m[4]);            // the actions share the time row's divider
        Assert.Equal(1, m[5]);            // a chosen month is filled
        Assert.Equal(1, m[6]);            // and this month chosen reads on-solid, not brand on brand
    }
    [Fact]
    public async Task A_timeline_pill_covers_its_columns_the_label_stays_pinned_and_now_is_at_its_column()
    {
        if (NoBrowser) return;
        string Row(string label, string events, int lanes = 0) =>
            "<div class=\"cal-timeline-row\"><span class=\"cal-timeline-label\"><span>" + label + "</span></span>"
            + "<div class=\"cal-timeline-cells\" aria-hidden=\"true\"></div>"
            + "<div class=\"cal-row-events\">" + events + "</div></div>";
        var heads = string.Concat(Enumerable.Range(1, 20).Select(n => $"<span class=\"cal-timeline-col\" id=\"c{n}\">{n}</span>"));
        var (page, _) = await OpenStyled($"""
            <div class="sedna-scroll-x" id="scroller" style="width: 600px">
              <div class="cal-timeline" id="t" style="--cal-columns: 20; --cal-lanes: 2; --cal-label-width: 120px; --cal-column-min: 40px">
                <div class="cal-timeline-head"><span class="cal-timeline-corner">Dock</span>{heads}</div>
                {Row("Dock 1", "<a class=\"cal-event\" id=\"pill\" href=\"#\" style=\"grid-column: 3 / span 4; grid-row: 1\">Inbound</a>")}
                <div class="cal-now" id="now" style="--cal-at: 2.5"></div>
              </div>
            </div>
            <div class="cal-timeline cal-timeline--grow" id="g" style="--cal-columns: 7; --cal-lanes: 1; width: 600px; margin-top: 24px">
              {Row("Busy", string.Concat(Enumerable.Range(1, 4).Select(n => $"<a class=\"cal-event\" href=\"#\" style=\"grid-column: 1 / span 2; grid-row: {n}\">E{n}</a>")))}
              {Row("Quiet", "")}
            </div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const r = id => document.getElementById(id).getBoundingClientRect();
                const pill = r('pill'), c3 = r('c3'), c6 = r('c6'), now = r('now');
                const label = () => document.querySelector('#t .cal-timeline-label').getBoundingClientRect().left;
                const before = label();
                const scroller = document.getElementById('scroller');
                scroller.scrollLeft = 300;
                const after = label();
                const rows = [...document.querySelectorAll('#g .cal-timeline-row')].map(x => x.getBoundingClientRect().height);
                const last = [...document.querySelectorAll('#g .cal-event')].at(-1).getBoundingClientRect();
                const busy = document.querySelector('#g .cal-timeline-row').getBoundingClientRect();
                return [pill.left - c3.left, c6.right - pill.right, now.left - (c3.left + c3.width / 2),
                        scroller.scrollWidth > scroller.clientWidth ? 1 : 0, after - before,
                        rows[0] - rows[1], busy.bottom - last.bottom];
            }
            """);

        Assert.InRange(m[0], 0, 6);          // the pill starts inside its first column
        Assert.InRange(m[1], 0, 6);          // and ends inside its last
        Assert.InRange(m[2], -2, 2);         // 2.5 columns is the middle of the third
        Assert.Equal(1, m[3]);               // the columns ran out of room and scroll
        Assert.InRange(m[4], -1.5, 0.5);     // and the label did not move with them — it pins at the
                                             // scroller's edge, one border-width left of where it started
        Assert.True(m[5] > 40, $"A growing row with four lanes is only {m[5]}px taller than a quiet one.");
        Assert.InRange(m[6], 0, 16);         // its last lane is inside it
    }
}
