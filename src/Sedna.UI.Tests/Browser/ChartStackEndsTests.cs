using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// In a stacked column or bar chart only the outer end of the whole stack is rounded: the segments
/// join square and the baseline end is square, with negative values stacked the other way and
/// their own outer end rounded — measured on the paint of each segment's corners.
/// </summary>
/// <remarks>
/// Every segment used to be a rounded rectangle, so a stack read as a column of pills with a notch
/// at each join. A corner is read a third of a pixel inside the segment's box: painted when it is
/// square, and not when a radius has taken it.
/// </remarks>
public class ChartStackEndsTests : ScriptTestBase
{
    private const string Table = """
        <table class="chart-data" data-chart-data>
          <thead><tr><th>Month</th><th>Received</th><th>Held</th><th>Returned</th></tr></thead>
          <tbody>
            <tr><th>Jan</th><td>30</td><td>20</td><td>10</td></tr>
            <tr><th>Feb</th><td>25</td><td>-15</td><td>10</td></tr>
            <tr><th>Mar</th><td>20</td><td></td><td>15</td></tr>
            <tr><th>Apr</th><td>40</td><td></td><td></td></tr>
            <tr><th>May</th><td>-12</td><td>-8</td><td>14</td></tr>
          </tbody>
        </table>
        """;

    private sealed class Bar
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }
    }

    [Theory]
    [InlineData("column", "ltr")]
    [InlineData("bar", "ltr")]
    [InlineData("bar", "rtl")]
    public async Task Only_the_outer_end_of_a_stack_is_rounded(string type, string dir)
    {
        if (NoBrowser) return;
        var page = await Open($"""
            <div dir="{dir}" style="width:560px; padding:16px">
              <div class="chart" id="c" data-chart="{type}" data-chart-stack data-chart-eager aria-label="Orders per month">
                {Table}
                <div class="chart-plot"></div>
              </div>
            </div>
            """, head: StylesheetTag, reducedMotion: ReducedMotion.Reduce, deviceScaleFactor: Ink.Scale);
        await page.WaitForFunctionAsync("() => document.querySelector('#c .chart-svg .chart-marks')");
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);

        var bars = await page.EvaluateAsync<Bar[]>("""
            () => [...document.querySelectorAll('#c .chart-bar')].map(e => { const r = e.getBoundingClientRect(); return { left: r.left, top: r.top, right: r.right, bottom: r.bottom }; })
            """);
        var box = await page.EvaluateAsync<double[]>("() => { const r = document.getElementById('c').getBoundingClientRect(); return [r.left, r.top, r.width, r.height]; }");
        var shot = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = new() { X = (float)box[0], Y = (float)box[1], Width = (float)box[2], Height = (float)box[3] } }));

        // A corner is painted when it is the segment's own colour — not the ground, and not a rule of the plot.
        (int R, int G, int B) At(double x, double y) =>
            shot.Pixel((int)Math.Floor((x - box[0]) * Ink.Scale), (int)Math.Floor((y - box[1]) * Ink.Scale));
        bool Painted(Bar bar, double x, double y)
        {
            var own = At((bar.Left + bar.Right) / 2, (bar.Top + bar.Bottom) / 2);
            var (r, g, b) = At(x, y);
            return Math.Abs(r - own.R) + Math.Abs(g - own.G) + Math.Abs(b - own.B) < 60;
        }
        const double In = 0.3;
        bool[] Corners(Bar b) =>
        [
            Painted(b, b.Left + In, b.Top + In), Painted(b, b.Right - In, b.Top + In),
            Painted(b, b.Left + In, b.Bottom - In), Painted(b, b.Right - In, b.Bottom - In),
        ];

        // A segment is in a stack by its position across the value axis: the same column or row.
        var column = type == "column";
        var groups = bars.GroupBy(b => Math.Round(((column ? (b.Left + b.Right) / 2 : (b.Top + b.Bottom) / 2)) / 8)).OrderBy(g => g.Key).ToList();
        Assert.True(groups.Count == 5, $"{groups.Count} stacks drawn: {string.Join(' ', groups.Select(g => g.Key + ':' + g.Count()))}");

        // The baseline is the foot of the stack that is all above it: the first month.
        var jan = groups[0].ToList();
        var rtl = dir == "rtl";
        var baseline = column ? jan.Max(b => b.Bottom) : rtl ? jan.Max(b => b.Right) : jan.Min(b => b.Left);

        foreach (var group in groups)
        {
            // A value of zero is a hairline at the junction: no end of the stack, and nothing to round.
            var segments = group.Where(b => (column ? b.Bottom - b.Top : b.Right - b.Left) > 1.5).ToList();
            foreach (var s in segments)
            {
                // [top-left, top-right, bottom-left, bottom-right]: painted = square.
                var painted = Corners(s);
                var expected = new[] { true, true, true, true };
                if (column)
                {
                    var outerUp = s == segments.MinBy(b => b.Top);
                    var outerDown = s == segments.MaxBy(b => b.Bottom) && s.Bottom > baseline + 1;
                    if (outerUp && s.Top < baseline - 1) { expected[0] = false; expected[1] = false; }
                    if (outerDown) { expected[2] = false; expected[3] = false; }
                }
                else
                {
                    var outerRight = s == segments.MaxBy(b => b.Right);
                    var outerLeft = s == segments.MinBy(b => b.Left);
                    // The end that goes past the baseline in the direction of positive values, and the one past it the other way.
                    var positiveSide = rtl ? outerLeft && s.Left < baseline - 1 : outerRight && s.Right > baseline + 1;
                    var negativeSide = rtl ? outerRight && s.Right > baseline + 1 : outerLeft && s.Left < baseline - 1;
                    if (positiveSide && !rtl || negativeSide && rtl) { expected[1] = false; expected[3] = false; }
                    if (positiveSide && rtl || negativeSide && !rtl) { expected[0] = false; expected[2] = false; }
                }
                Assert.True(painted.SequenceEqual(expected),
                    $"{type} {dir}, segment at {s.Left:0.0},{s.Top:0.0}–{s.Right:0.0},{s.Bottom:0.0}: corners (TL,TR,BL,BR) painted {string.Join(",", painted)}, expected {string.Join(",", expected)}.");
            }
        }
    }
}
