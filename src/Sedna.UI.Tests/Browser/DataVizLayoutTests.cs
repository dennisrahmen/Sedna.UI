using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Bars and meters measured: every row of a bar list draws its bar on one track length
/// whatever its value's width, and two meter marks close together keep their labels apart.
/// </summary>
public class DataVizLayoutTests : ScriptTestBase
{
    [Fact]
    public async Task Every_bar_list_row_has_the_same_track_length_whatever_its_value()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="bar-list" style="width:480px">
              <div class="bar-list-row">
                <span class="bar-list-label">EU-Central</span>
                <span class="bar-list-track"><span class="bar-list-fill" style="--value: 100%"></span></span>
                <span class="bar-list-value">1 240 000</span>
              </div>
              <div class="bar-list-row">
                <span class="bar-list-label">EU-West</span>
                <span class="bar-list-track"><span class="bar-list-fill" style="--value: 40%"></span></span>
                <span class="bar-list-value">12</span>
              </div>
            </div>
            """);

        var widths = await page.EvaluateAsync<double[]>("""
            () => [...document.querySelectorAll('.bar-list-track')].map(t => t.getBoundingClientRect().width)
            """);

        Assert.True(widths[0] > 100, $"The track is {widths[0]}px wide.");
        Assert.Equal(widths[0], widths[1], 0.01);
    }

    [Fact]
    public async Task A_mark_label_moved_above_the_meter_does_not_overlap_the_one_below()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:320px; padding:40px 0">
              <div id="meter" class="meter meter--labelled">
                <span class="meter-fill" style="--value: 71%"></span>
                <span class="meter-mark" style="--at: 80%"><span class="meter-mark-label">alert 80%</span></span>
                <span class="meter-mark meter-mark--above" style="--at: 90%"><span class="meter-mark-label">critical 90%</span></span>
              </div>
            </div>
            """);

        var boxes = await page.EvaluateAsync<double[]>("""
            () => {
                const m = document.getElementById('meter').getBoundingClientRect();
                const [below, above] = [...document.querySelectorAll('.meter-mark-label')].map(l => l.getBoundingClientRect());
                return [below.top, m.bottom, above.bottom, m.top, parseFloat(getComputedStyle(document.getElementById('meter')).marginTop)];
            }
            """);

        Assert.True(boxes[0] >= boxes[1], "The first label sits under the track.");
        Assert.True(boxes[2] <= boxes[3], "The moved label sits over the track.");
        Assert.True(boxes[4] > 0, "--labelled reserves the line above once a mark asks for it.");
    }

    // A row in parts keeps its length against the widest, and its segments split that length.
    [Fact]
    public async Task A_bar_list_row_in_parts_keeps_its_length_and_its_segments_split_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="bar-list" style="width:480px">
              <div class="bar-list-row">
                <span class="bar-list-label">Alex Fischer</span>
                <span class="bar-list-track" id="track"><span class="bar-list-fill" id="fill" style="--value: 50%">
                  <span class="stack-bar-segment series-info" id="a" style="--value: 75%"></span>
                  <span class="stack-bar-segment series-danger" id="b" style="--value: 25%"></span>
                </span></span>
                <span class="bar-list-value">9</span>
              </div>
            </div>
            """);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const w = id => document.getElementById(id).getBoundingClientRect().width;
              const bg = id => getComputedStyle(document.getElementById(id)).backgroundColor;
              return [w('fill') / w('track'), w('a') / w('fill'), w('b') / w('fill'),
                      bg('a') === bg('b') ? 1 : 0, bg('fill') === 'rgba(0, 0, 0, 0)' ? 1 : 0];
            }
            """);

        Assert.InRange(m[0], 0.49, 0.51);   // the row is half the widest
        Assert.InRange(m[1], 0.70, 0.76);   // three quarters of it, less the gap
        Assert.InRange(m[2], 0.20, 0.26);
        Assert.Equal(0, m[3]);              // two colours
        Assert.Equal(1, m[4]);              // the fill paints nothing of its own
        Assert.Empty(errors);
    }

    // ── A stack bar with marks and labels ─────────────────────────────────────

    private static string StackBarFigure(string barClass) => $"""
        <div class="stack-bar-figure" id="figure" style="width:400px; margin:40px">
          <div class="stack-bar {barClass}" id="bar" role="img" aria-label="128 orders">
            <span class="stack-bar-segment series-go" style="--value: 62%"></span>
            <span class="stack-bar-segment series-warn" style="--value: 28%"></span>
            <span class="stack-bar-segment series-muted" style="--value: 10%"></span>
          </div>
          <span class="meter-mark" id="mark" style="--at: 85%" data-tip="Target: 85% decided by Friday"><span class="meter-mark-label" id="mark-label">target</span></span>
          <div class="stack-bar-labels" id="labels" aria-hidden="true">
            <span class="stack-bar-label" id="l1" style="--at: 62%">79</span>
            <span class="stack-bar-label" id="l2" style="--at: 90%">115</span>
            <span class="stack-bar-total" id="total">128</span>
          </div>
        </div>
        """;

    [Theory]
    [InlineData("ltr", "stack-bar--sm")]
    [InlineData("ltr", "")]
    [InlineData("ltr", "stack-bar--lg")]
    [InlineData("rtl", "")]
    public async Task A_mark_on_a_stack_bar_spans_the_bar_at_its_value_and_the_labels_sit_on_their_boundaries(string dir, string barClass)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div dir="{dir}">{StackBarFigure(barClass)}</div>""");

        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const r = id => document.getElementById(id).getBoundingClientRect();
              const bar = r('bar'), mark = r('mark'), label = r('mark-label'), labels = r('labels');
              const rtl = getComputedStyle(document.getElementById('figure')).direction === 'rtl';
              const at = f => rtl ? bar.right - bar.width * f : bar.left + bar.width * f;
              const centre = b => b.left + b.width / 2;
              const hit = document.elementFromPoint(centre(mark) + 5, bar.top + bar.height / 2);
              return [
                centre(mark) - at(0.85),                  // the mark is at its value
                bar.top - mark.top, mark.bottom - bar.bottom,   // and reaches past the bar both ways
                bar.top - label.bottom,                   // its label is over the bar
                labels.top - bar.bottom,                  // the line of labels is under it
                centre(r('l1')) - at(0.62),               // each label on its boundary
                centre(r('l2')) - at(0.90),
                rtl ? bar.left - r('total').left : r('total').right - bar.right,   // the total closes the line
                hit === document.getElementById('mark') ? 1 : 0,   // the mark is a target wider than its line
                parseFloat(getComputedStyle(document.getElementById('figure')).marginTop) - (bar.top - label.top),   // the figure reserved the line over the bar
              ];
            }
            """);

        Assert.InRange(m[0], -1, 1);
        Assert.Equal(m[1], m[2], 0.5);
        Assert.True(m[1] >= 2, $"The mark reaches {m[1]}px past the bar.");
        Assert.True(m[3] >= 0, "The mark's label sits over the bar, leaving the line under it to the labels.");
        Assert.True(m[4] >= 0, "The labels sit under the bar.");
        Assert.InRange(m[5], -1, 1);
        Assert.InRange(m[6], -1, 1);
        Assert.InRange(m[7], -0.5, 0.5);
        Assert.Equal(1, m[8]);
        Assert.True(m[9] >= -0.5, $"The mark's label reaches {-m[9]}px past the line the figure reserves for it.");
        Assert.Empty(errors);
    }

    // ── Tracker ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("tracker--sm", 16)]
    [InlineData("", 28)]
    [InlineData("tracker--lg", 40)]
    public async Task A_tracker_divides_its_row_evenly_and_dims_the_blocks_a_hint_is_not_about(string size, double height)
    {
        if (NoBrowser) return;
        var blocks = string.Concat(Enumerable.Range(0, 30).Select(i =>
            $"""<span class="tracker-block {(i == 12 ? "series-danger" : i % 9 == 4 ? "series-warn" : "series-go")}" data-tip="Day {i + 1}"></span>"""));
        var (page, errors) = await OpenStyled($"""
            <div class="tracker {size}" id="tracker" style="width:420px; margin:40px" role="img" aria-label="Last 30 days">{blocks}</div>
            """);

        var layout = await page.EvaluateAsync<double[]>("""
            () => {
              const t = document.getElementById('tracker').getBoundingClientRect();
              const b = [...document.querySelectorAll('.tracker-block')].map(e => e.getBoundingClientRect());
              const widths = b.map(r => r.width);
              const colour = i => getComputedStyle(document.querySelectorAll('.tracker-block')[i]).backgroundColor;
              return [t.height, Math.min(...widths), Math.max(...widths), b[0].left - t.left, t.right - b[29].right,
                      colour(0) !== colour(12) ? 1 : 0, colour(0) !== colour(4) ? 1 : 0];
            }
            """);

        Assert.Equal(height, layout[0], 0.5);
        Assert.Equal(layout[1], layout[2], 0.5);          // every block the same width
        Assert.InRange(layout[3], -0.5, 0.5);             // and the row fills the tracker
        Assert.InRange(layout[4], -0.5, 0.5);
        Assert.Equal(1, layout[5]);
        Assert.Equal(1, layout[6]);

        await page.HoverAsync(".tracker-block:nth-child(13)");
        await Js.Settle(page);
        var opacity = await page.EvaluateAsync<double[]>("""
            () => [...document.querySelectorAll('.tracker-block')].map(e => parseFloat(getComputedStyle(e).opacity))
            """);
        Assert.Equal(1, opacity[12]);
        Assert.All(opacity.Where((_, i) => i != 12), o => Assert.True(o < 0.5, $"A block the hint is not about is at opacity {o}."));
        Assert.Empty(errors);
    }

    // ── Gauge ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("gauge--sm", 80, false)]
    [InlineData("", 120, false)]
    [InlineData("gauge--lg", 180, false)]
    [InlineData("gauge--sm gauge--arc", 80, true)]
    [InlineData("gauge--arc", 120, true)]
    [InlineData("gauge--lg gauge--arc", 180, true)]
    public async Task A_gauge_fills_its_sweep_to_the_value_and_keeps_its_figure_inside(string classes, double size, bool arc)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div class="card" style="padding:24px; display:inline-block">
              <div class="gauge series-danger {classes}" id="g" style="--value: 50%" role="img" aria-label="Score 50 of 100">
                <span class="gauge-value" id="v">50</span>
                <span class="gauge-label" id="l">score</span>
              </div>
            </div>
            """, deviceScaleFactor: 1);
        await Js.Settle(page);

        var box = await page.EvaluateAsync<double[]>("""
            () => {
              const g = document.getElementById('g').getBoundingClientRect();
              const v = document.getElementById('v').getBoundingClientRect();
              const l = document.getElementById('l').getBoundingClientRect();
              return [g.left, g.top, g.width, g.height, v.left, v.right, v.top, l.bottom,
                      parseFloat(getComputedStyle(document.getElementById('g'), '::before').height)];
            }
            """);

        Assert.Equal(size, box[2], 0.5);
        Assert.Equal(arc ? size : size / 2, box[3], 0.5);   // a half circle is half as tall as it is wide
        Assert.Equal(size, box[8], 0.5);                    // the dial is the whole circle either way

        // The figure and its caption sit inside the ring's opening.
        var width = size switch { 80 => 6, 120 => 12, _ => 16 };
        var inner = size / 2 - width;
        var cx = box[0] + size / 2;
        var cy = box[1] + size / 2;
        foreach (var x in new[] { box[4], box[5] })
            foreach (var y in new[] { box[6], box[7] })
                Assert.True(Math.Sqrt((x - cx) * (x - cx) + Math.Pow(Math.Min(y, cy) - cy, 2)) < inner + 0.5,
                    $"A corner of the figure ({x - cx:0.0}, {y - cy:0.0} from the centre) is outside the ring's opening, radius {inner}.");

        // Paint, on the ring's centre line: a half value fills the sweep up to the top.
        var image = Ink.Png.Decode(await page.ScreenshotAsync(new() { FullPage = true }));
        var radius = size / 2 - width / 2.0;
        (int R, int G, int B) At(double degrees)
        {
            var turn = degrees * Math.PI / 180;
            return image.Pixel((int)Math.Round(cx + radius * Math.Sin(turn)), (int)Math.Round(cy - radius * Math.Cos(turn)));
        }
        var fill = At(-45);
        var track = At(45);
        var card = image.Pixel((int)Math.Round(box[0] - 8), (int)Math.Round(box[1] + 4));
        Assert.True(fill != track, $"The filled half ({fill}) and the rest of the sweep ({track}) are one colour.");
        Assert.True(track != card, $"The rest of the sweep ({track}) is not drawn against the card ({card}).");
        Assert.Equal(fill, At(arc ? -120 : -80));
        if (arc) Assert.Equal(card, At(180));   // the opening at the foot of an arc is the card
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_gauge_glides_to_a_new_value_and_lands_at_once_under_reduced_motion()
    {
        if (NoBrowser) return;
        const string Fixture = """<div class="gauge" id="g" style="--value: 10%"><span class="gauge-value">10%</span></div>""";
        const string Change = """
            () => {
              const g = document.getElementById('g');
              getComputedStyle(g).getPropertyValue('--gauge-fill');
              g.style.setProperty('--value', '90%');
              return g.getAnimations().map(a => a.transitionProperty).join(',');
            }
            """;

        var moving = await Open(Fixture, head: StylesheetTag, reducedMotion: Microsoft.Playwright.ReducedMotion.NoPreference);
        Assert.Equal("--gauge-fill", await moving.EvaluateAsync<string>(Change));

        var still = await Open(Fixture, head: StylesheetTag, reducedMotion: Microsoft.Playwright.ReducedMotion.Reduce);
        Assert.Equal("", await still.EvaluateAsync<string>(Change));
        Assert.Equal("90%", await still.EvaluateAsync<string>(
            "() => getComputedStyle(document.getElementById('g')).getPropertyValue('--gauge-fill').trim()"));
    }
}
