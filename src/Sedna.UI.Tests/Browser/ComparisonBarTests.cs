using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A bar with a previous value behind its fill, measured: the line is at the previous value,
/// it is still the topmost thing there when the fill covers the hatch, and the hatch stands
/// apart from the track and from the fill in every palette, forced colours included.
/// </summary>
public class ComparisonBarTests : ScriptTestBase
{
    private const string Fixture = """
        <div class="card" style="width:520px; padding:24px">
          <div class="bar-list">
            <div class="bar-list-row">
              <span class="bar-list-label">Lower</span>
              <span id="lower" class="bar-list-track">
                <span class="bar-previous" style="--value: 40%"></span>
                <span class="bar-list-fill" style="--value: 70%"></span>
              </span>
              <span class="bar-list-value">70%<span class="bar-list-previous">(40%)</span></span>
            </div>
            <div class="bar-list-row">
              <span class="bar-list-label">Higher</span>
              <span id="higher" class="bar-list-track">
                <span class="bar-previous" style="--value: 80%"></span>
                <span class="bar-list-fill" style="--value: 50%"></span>
              </span>
              <span class="bar-list-value">50%<span class="bar-list-previous">(80%)</span></span>
            </div>
          </div>
          <div id="meter" class="meter" style="margin-top:24px">
            <span class="bar-previous" style="--value: 30%"></span>
            <span class="meter-fill" style="--value: 60%"></span>
          </div>
          <div id="progress" class="progress" style="margin-top:24px">
            <span class="bar-previous" style="--value: 25%"></span>
            <div class="progress-bar" style="width: 45%"></div>
          </div>
        </div>
        """;

    [Theory]
    [InlineData("lower")]
    [InlineData("higher")]
    [InlineData("meter")]
    [InlineData("progress")]
    public async Task The_line_is_at_the_previous_value_and_nothing_paints_over_it(string id)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var m = await page.EvaluateAsync<double[]>("""
            id => {
                const track = document.getElementById(id);
                const previous = track.querySelector('.bar-previous');
                const t = track.getBoundingClientRect();
                const after = getComputedStyle(previous, '::after');
                const value = parseFloat(previous.style.getPropertyValue('--value')) / 100;
                const p = previous.getBoundingClientRect();
                const midY = t.top + t.height / 2;
                const hit = (x, y) => {
                    const el = document.elementFromPoint(x, y);
                    return el === previous ? 1 : el && el.matches('.bar-list-fill, .meter-fill, .progress-bar') ? 2 : 0;
                };
                return [
                    t.left + t.width * value,      // where the previous value is
                    p.right,                       // where the hatch ends
                    hit(p.right, midY),            // over the line, in the middle of the track
                    hit(p.right, t.top - 2),       // over the line, just above the track
                    hit(p.right, t.bottom + 2),    // and just below it
                    parseFloat(after.height),
                    t.height,
                ];
            }
            """, id);

        Assert.Empty(errors);
        Assert.Equal(m[0], m[1], 0.5);
        Assert.True((int)m[2] == 1, "The previous value's line is the topmost thing at its own position — over the fill as well.");
        Assert.True((int)m[3] == 1 && (int)m[4] == 1, "The line reaches past the track above and below, so it shows when the fill covers the hatch.");
        Assert.True(m[5] > m[6], $"The line is {m[5]}px tall on a {m[6]}px track.");
    }

    [Fact]
    public async Task The_fill_paints_over_the_hatch_and_the_hatch_shows_past_a_lower_fill()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);

        var hits = await page.EvaluateAsync<string[]>("""
            () => ['lower', 'higher'].map(id => {
                const t = document.getElementById(id).getBoundingClientRect();
                const at = f => document.elementFromPoint(t.left + t.width * f, t.top + t.height / 2)?.className ?? '';
                return at(0.2) + '|' + at(0.65);
            })
            """);

        // Lower previous value (40%) under a 70% fill: the fill is on top at 20%, and at 65%.
        Assert.Equal("bar-list-fill|bar-list-fill", hits[0]);
        // Higher previous value (80%) past a 50% fill: fill at 20%, hatch at 65%.
        Assert.Equal("bar-list-fill|bar-previous", hits[1]);
    }

    public static IEnumerable<object[]> Palettes() =>
    [
        ["dark", ""],
        ["light", "light"],
        ["dark, colour-blind", "cvd"],
        ["light, colour-blind", "light cvd"],
        ["dark, high contrast", "contrast"],
        ["light, high contrast", "light contrast"],
    ];

    [Theory]
    [MemberData(nameof(Palettes))]
    public async Task The_hatch_stands_apart_from_the_track_and_the_fill(string name, string flags)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);
        await SetPalette(page, flags);

        var c = await page.EvaluateAsync<string[]>("""
            () => {
                const previous = document.querySelector('#higher .bar-previous');
                const s = getComputedStyle(previous);
                return [
                    s.color,
                    s.backgroundImage,
                    getComputedStyle(document.querySelector('#higher')).backgroundColor,
                    getComputedStyle(document.querySelector('.card')).backgroundColor,
                    getComputedStyle(document.querySelector('#higher .bar-list-fill')).backgroundImage,
                ];
            }
            """);

        var stripe = Parse(c[0]);
        var card = Parse(c[3]);
        var track = Over(Parse(c[2]), card);

        Assert.Contains("repeating-linear-gradient", c[1], StringComparison.Ordinal);
        Assert.Equal("none", c[4]);

        var onTrack = Tokens.Contrast(stripe.Rgb, track.Rgb);
        var onCard = Tokens.Contrast(stripe.Rgb, card.Rgb);
        Assert.True(onTrack >= 3,
            $"{name}: the hatch is {onTrack:0.00}:1 against the track, under the 3:1 a graphical object needs. " +
            "Move --viz-previous in this palette's token block.");
        Assert.True(onCard >= 3,
            $"{name}: the hatch is {onCard:0.00}:1 against the card, under 3:1.");
    }

    [Fact]
    public async Task In_forced_colours_the_fill_the_hatch_and_the_track_are_three_colours()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);
        await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });

        var c = await page.EvaluateAsync<string[]>("""
            () => [
                getComputedStyle(document.querySelector('#higher .bar-list-fill')).backgroundColor,
                getComputedStyle(document.querySelector('#higher .bar-previous')).color,
                getComputedStyle(document.querySelector('#higher')).backgroundColor,
                getComputedStyle(document.querySelector('.card')).backgroundColor,
                getComputedStyle(document.querySelector('#higher .bar-previous')).backgroundImage,
                getComputedStyle(document.querySelector('#higher .bar-previous'), '::after').backgroundColor,
            ]
            """);

        var card = Parse(c[3]);
        var fill = Over(Parse(c[0]), card).Rgb;
        var stripe = Parse(c[1]).Rgb;
        var track = Over(Parse(c[2]), card).Rgb;
        var line = Over(Parse(c[5]), card).Rgb;

        Assert.Contains("repeating-linear-gradient", c[4], StringComparison.Ordinal);
        Assert.True(fill != track, $"The fill ({c[0]}) is the track's colour ({c[2]}) in forced colours.");
        Assert.True(stripe != track, $"The hatch ({c[1]}) is the track's colour ({c[2]}) in forced colours.");
        Assert.True(stripe != fill, $"The hatch ({c[1]}) is the fill's colour ({c[0]}) in forced colours.");
        Assert.True(line != track, $"The line ({c[5]}) is the track's colour ({c[2]}) in forced colours.");
    }

    private static async Task SetPalette(IPage page, string flags)
    {
        await page.EvaluateAsync("""
            flags => {
                const html = document.documentElement;
                html.dataset.variant = flags.includes('light') ? 'light' : 'dark';
                if (flags.includes('cvd')) html.dataset.cvd = '1'; else delete html.dataset.cvd;
                if (flags.includes('contrast')) html.dataset.contrast = 'more'; else delete html.dataset.contrast;
            }
            """, flags);
    }

    private static readonly Regex RgbFunction = new(
        @"rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)(?:\s*[,/]\s*([\d.]+))?\s*\)", RegexOptions.Compiled);

    private static ((double R, double G, double B) Rgb, double A) Parse(string css)
    {
        var m = RgbFunction.Match(css);
        Assert.True(m.Success, $"\"{css}\" is not an rgb() colour.");
        double N(int g) => double.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
        return ((N(1) / 255, N(2) / 255, N(3) / 255), m.Groups[4].Success ? N(4) : 1);
    }

    private static ((double R, double G, double B) Rgb, double A) Over(
        ((double R, double G, double B) Rgb, double A) top, ((double R, double G, double B) Rgb, double A) under) =>
        ((top.Rgb.R * top.A + under.Rgb.R * (1 - top.A),
          top.Rgb.G * top.A + under.Rgb.G * (1 - top.A),
          top.Rgb.B * top.A + under.Rgb.B * (1 - top.A)), 1);
}
