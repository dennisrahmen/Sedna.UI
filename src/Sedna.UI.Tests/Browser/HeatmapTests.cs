using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A heatmap measured: its cells fill a column of days and then the next, at the size each
/// step offers, and the five intensity steps stay five colours in every palette and in forced
/// colours, where an empty cell is still drawn.
/// </summary>
public class HeatmapTests : ScriptTestBase
{
    private static string Fixture(string size) => $"""
        <div class="card" style="padding:24px; display:inline-block">
          <div class="heatmap {size}" id="map" role="img" aria-label="Deployments: 31 in two weeks">
            <span></span><span></span>
            <span class="heatmap-cell" id="c0" data-tip="Wed 6 May: none"></span>
            <span class="heatmap-cell heatmap-cell--1" id="c1" data-tip="Thu 7 May: 1 deployment"></span>
            <span class="heatmap-cell heatmap-cell--2" id="c2" data-tip="Fri 8 May: 3 deployments"></span>
            <span class="heatmap-cell heatmap-cell--3" id="c3" data-tip="Sat 9 May: 5 deployments"></span>
            <span class="heatmap-cell heatmap-cell--4" id="c4" data-tip="Sun 10 May: 9 deployments"></span>
            <span class="heatmap-cell heatmap-cell--2" id="c5" data-tip="Mon 11 May: 3 deployments"></span>
            <span class="heatmap-cell heatmap-cell--1" id="c6" data-tip="Tue 12 May: 1 deployment"></span>
            <span class="heatmap-cell heatmap-cell--3" id="c7" data-tip="Wed 13 May: 6 deployments"></span>
          </div>
          <div class="heatmap-legend" id="legend" aria-hidden="true">
            <span>Less</span>
            <span class="heatmap-cell"></span>
            <span class="heatmap-cell heatmap-cell--1"></span>
            <span class="heatmap-cell heatmap-cell--2"></span>
            <span class="heatmap-cell heatmap-cell--3"></span>
            <span class="heatmap-cell heatmap-cell--4"></span>
            <span>More</span>
          </div>
        </div>
        """;

    [Theory]
    [InlineData("heatmap--sm", 9)]
    [InlineData("", 12)]
    [InlineData("heatmap--lg", 16)]
    public async Task Cells_fill_a_column_of_days_and_then_the_next_week(string size, double cell)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture(size));

        var boxes = await page.EvaluateAsync<double[][]>("""
            () => {
              const map = document.getElementById('map').getBoundingClientRect();
              return [...Array(8).keys()].map(i => {
                const r = document.getElementById('c' + i).getBoundingClientRect();
                return [r.left - map.left, r.top - map.top, r.width, r.height];
              });
            }
            """);

        var step = cell + 2;   // the cell and --space-1
        for (var i = 0; i < boxes.Length; i++)
        {
            var slot = i + 2;   // two bare spans before the first day
            Assert.Equal(cell, boxes[i][2], 0.5);
            Assert.Equal(cell, boxes[i][3], 0.5);
            int week = slot / 7, day = slot % 7;
            Assert.Equal(week * step, boxes[i][0], 0.5);
            Assert.Equal(day * step, boxes[i][1], 0.5);
        }
        Assert.Empty(errors);
    }

    public static IEnumerable<object[]> Palettes() => ComparisonBarTests.Palettes();

    [Theory]
    [MemberData(nameof(Palettes))]
    public async Task The_five_steps_are_five_colours_in_every_palette(string name, string flags)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture(""));
        await SetPalette(page, flags);

        var colours = await page.EvaluateAsync<string[]>("""
            () => [...Array(5).keys()].map(i => getComputedStyle(document.getElementById('c' + i)).backgroundColor)
            """);
        Assert.True(colours.Distinct().Count() == 5, $"{name}: the steps paint {string.Join(" / ", colours)}.");
    }

    [Fact]
    public async Task In_forced_colours_the_steps_stay_ordered_and_an_empty_cell_is_still_drawn()
    {
        // Forced colours exist in Chromium only (see BrowserEngine).
        if (NoBrowser || !BrowserEngine.EmulatesForcedColors) return;
        var (page, _) = await OpenStyled(Fixture(""));
        await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });

        var c = await page.EvaluateAsync<string[]>("""
            () => [...[...Array(5).keys()].map(i => getComputedStyle(document.getElementById('c' + i)).backgroundColor),
                   getComputedStyle(document.querySelector('.card')).backgroundColor,
                   getComputedStyle(document.getElementById('c0')).outlineStyle,
                   getComputedStyle(document.getElementById('c0')).outlineColor]
            """);

        var steps = c.Take(5).Select(Luminance).ToArray();
        var canvas = Luminance(c[5]);
        Assert.True(steps.Distinct().Count() == 5, $"The steps are not five colours in forced colours: {string.Join(" / ", c.Take(5))}.");

        // Each step is further from the canvas than the one before it.
        var distance = steps.Select(l => Math.Abs(l - canvas)).ToArray();
        for (var i = 1; i < distance.Length; i++)
            Assert.True(distance[i] > distance[i - 1], $"Step {i} ({c[i]}) is no further from the canvas than step {i - 1} ({c[i - 1]}).");

        Assert.Equal("solid", c[6]);
        Assert.NotEqual(c[5], c[7]);   // the empty cell's outline is not the canvas
    }

    private static double Luminance(string css)
    {
        // A mix of two system colours serialises as color(srgb …) with channels from 0 to 1.
        var srgb = Regex.Match(css, @"color\(srgb\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)");
        var m = srgb.Success ? srgb : Regex.Match(css, @"rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)");
        Assert.True(m.Success, $"\"{css}\" is not a colour this can read.");
        var scale = srgb.Success ? 1.0 : 255;
        double N(int g) => double.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture) / scale;
        return 0.2126 * N(1) + 0.7152 * N(2) + 0.0722 * N(3);
    }

    private static Task SetPalette(IPage page, string flags) =>
        Js.Run(page, """
            flags => {
                const html = document.documentElement;
                html.dataset.variant = flags.includes('light') ? 'light' : 'dark';
                if (flags.includes('cvd')) html.dataset.cvd = '1'; else delete html.dataset.cvd;
                if (flags.includes('contrast')) html.dataset.contrast = 'more'; else delete html.dataset.contrast;
            }
            """, flags);
}
