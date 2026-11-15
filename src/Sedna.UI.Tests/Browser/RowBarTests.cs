using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The bar at the start of an expanded row's detail and of a selected row fills the row from the
/// line above it to the line below, pixel for pixel, at every device scale — and does not move
/// when the pointer comes onto the row.
/// </summary>
/// <remarks>
/// A collapsed table's borders belong to the table, half to each row: a bar drawn inside the cell
/// stops half a border short of each, and where the engine rounds a row's edge to a device pixel
/// that is a pixel of the page's own background between the bar and the next row. The bar is drawn
/// a border past the cell instead, so it covers the lines themselves.
/// </remarks>
public class RowBarTests : ScriptTestBase
{
    private const string Head = "<thead><tr><th>Order</th><th>Reservation</th></tr></thead>";

    private static string Table(string classes, string rows) =>
        $"""<div style="padding:16px; width:480px"><table class="table {classes}" id="t">{Head}<tbody>{rows}</tbody></table></div>""";

    public static TheoryData<string, string, string, string> Cases()
    {
        var detail = """
            <tr class="tr-clickable" id="parent"><td>ORD-4209</td><td>Reroute</td></tr>
            <tr class="tr-detail" id="row"><td colspan="2"><div class="tr-detail-body"><div class="kv"><span class="k">Reason</span><span class="v">Carrier full</span></div></div></td></tr>
            <tr><td>ORD-4204</td><td>Hold</td></tr>
            """;
        var selected = """
            <tr><td>ORD-4187</td><td>1 h</td></tr>
            <tr class="tr-clickable" aria-selected="true" id="row"><td>ORD-4204</td><td>22 min</td></tr>
            <tr><td>ORD-4209</td><td>4 min</td></tr>
            """;
        var data = new TheoryData<string, string, string, string>();
        foreach (var (name, classes, rows, bar) in new[]
        {
            ("detail row", "", detail, "tr-detail"),
            ("selected row", "", selected, "selected"),
            ("selected row, sticky header", "table--sticky", selected, "selected"),
            ("selected row, pinned column", "table--pin-start", selected, "selected"),
        })
            data.Add(name, classes, rows, bar);
        return data;
    }

    /// <summary>The first run of rows in <paramref name="column"/> whose pixel is <paramref name="colour"/>, as [first, end).</summary>
    private static (int First, int End)? Run(Ink.Png image, int column, (int R, int G, int B) colour, int from = 0)
    {
        int y = from, first = -1;
        for (; y < image.Height; y++)
        {
            var same = Ink.Same(image.Pixel(column, y), colour, 3);
            if (same && first < 0) first = y;
            if (!same && first >= 0) return (first, y);
        }
        return first >= 0 ? (first, y) : null;
    }

    private static (int R, int G, int B) Rgb(string css)
    {
        var n = System.Text.RegularExpressions.Regex.Matches(css, @"[\d.]+").Select(m => (int)Math.Round(double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        return (n[0], n[1], n[2]);
    }

    private static async Task<(int, int, int, int)> Measure(IPage page, int scale, string bar)
    {
        var info = await page.EvaluateAsync<double[]>("""
            () => { const r = document.getElementById('t').getBoundingClientRect(); return [r.left, r.top, r.width, r.height]; }
            """);
        var colours = await page.EvaluateAsync<string[]>("""
            () => { const td = document.querySelector('#row > td'); const t = getComputedStyle(td, '::before');
                    return [t.backgroundColor, getComputedStyle(td).borderBottomColor, getComputedStyle(td).backgroundColor]; }
            """);
        var shot = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = new() { X = (float)info[0], Y = (float)info[1], Width = (float)info[2], Height = (float)info[3] } }));
        var barColour = Rgb(colours[0]);
        var line = Rgb(colours[1]);

        // The bar: the run of its own colour down the middle of its 3px.
        var barRun = Run(shot, (int)(1.5 * scale), barColour);
        Assert.NotNull(barRun);

        // The lines either side of the row, read a few pixels in from the bar: the run of the line's
        // colour that sits against the row's own background above and below it.
        var inner = (int)(8 * scale);
        var bg = await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#row > td')).backgroundColor");
        var rowBg = Rgb(bg);
        // A selected row's tint is translucent over the page: read the colour that is there.
        var mid = (barRun!.Value.First + barRun.Value.End) / 2;
        var rowColour = shot.Pixel(inner, mid);
        var body = Run(shot, inner, rowColour, Math.Max(0, barRun.Value.First - 3 * scale));
        Assert.NotNull(body);
        // The lines are what lies between the row's body and the rows around it: read the colour of
        // the neighbour a few pixels off, and walk out from the body until the neighbour begins. A
        // line is a few device pixels, the last of them blended with whatever it meets.
        var neighbourAbove = shot.Pixel(inner, Math.Max(0, body!.Value.First - 5 * scale));
        var neighbourBelow = shot.Pixel(inner, Math.Min(shot.Height - 1, body.Value.End + 5 * scale));
        var above = body.Value.First;
        while (above > 0 && !Ink.Same(shot.Pixel(inner, above - 1), neighbourAbove, 1)) above--;
        var below = body.Value.End;
        while (below < shot.Height && !Ink.Same(shot.Pixel(inner, below), neighbourBelow, 1)) below++;
        return (barRun.Value.First, barRun.Value.End, above, below);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_bar_fills_the_row_from_line_to_line_and_stays_when_the_row_is_hovered(string name, string classes, string rows, string bar)
    {
        if (NoBrowser) return;
        foreach (var scale in new[] { 1, 2, 3 })
        {
            var (page, errors) = await OpenStyled(Table(classes, rows), deviceScaleFactor: scale);
            await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
            await Js.Settle(page);

            var (first, end, above, below) = await Measure(page, scale, bar);
            Assert.True(first == above && end == below,
                $"{name} at {scale}x: the bar covers device rows {first}–{end}, the lines around the row {above}–{below}.");

            // The pointer on the row, and on the one it belongs to: the bar is where it was.
            await page.HoverAsync(bar == "selected" ? "#row" : "#parent");
            await Task.Delay(400);
            var hovered = await Measure(page, scale, bar);
            // The lines are not compared: a hover tint edged on a half pixel blends the row beside it.
            Assert.True((hovered.Item1, hovered.Item2) == (first, end),
                $"{name} at {scale}x: hovering moved the bar from {first}–{end} to {hovered.Item1}–{hovered.Item2}.");
            Assert.Empty(errors);
            await page.CloseAsync();
        }
    }
}
