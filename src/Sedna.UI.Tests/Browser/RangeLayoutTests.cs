using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What a <c>.form-range-box</c> paints, read from screenshots: each thumb is centred on the
/// track the box draws, at every size and on end, and stands where its value says; the ticks
/// and the bubbles stand over the same points; and only the thumbs of a span take the pointer,
/// so two thumbs that meet can be pulled apart again.
/// </summary>
/// <remarks>
/// The thumbs are an engine's own pseudo-elements, which no layout API reports, so they are
/// measured on the paint. WebKit hangs a thumb from the track's edge, and Chromium lays a vertical
/// range with a <c>list</c> out in a box of its own size — both of which put a thumb off a track
/// the box draws while every element box is where it should be.
/// </remarks>
public class RangeLayoutTests : ScriptTestBase
{
    private static string Box(string size, bool vertical) => $"""
        <div style="padding:40px; display:flex">
          <div class="form-range-box{(vertical ? " form-range-box--vertical" : "")}" id="box" style="{(vertical ? "--range-length:300px" : "width:400px")}">
            <output class="form-range-bubble" for="lo" id="blo" data-range-value>250</output>
            <output class="form-range-bubble" for="hi" id="bhi" data-range-value>750</output>
            <input class="form-range {size}" id="lo" type="range" min="0" max="1000" step="250" value="250" aria-label="Lowest" list="marks">
            <input class="form-range {size}" id="hi" type="range" min="0" max="1000" step="250" value="750" aria-label="Highest" list="marks">
            <datalist class="form-range-ticks" id="marks">
              <option value="0">0</option><option value="250">250</option><option value="500">500</option>
              <option value="750">750</option><option value="1000">1000</option>
            </datalist>
          </div>
        </div>
        """;

    /// <summary>The paint of one screenshot, with where the shot starts on the page.</summary>
    private sealed record Shot(Ink.Png Image, double Left, double Top, (int R, int G, int B) On)
    {
        private bool IsOn(int px, int py)
        {
            var p = Image.Pixel(px, py);
            return Math.Abs(p.R - On.R) + Math.Abs(p.G - On.G) + Math.Abs(p.B - On.B) < 90;
        }

        /// <summary>
        /// [first, last] page pixel painted in the thumb's colour along a line: across x at a
        /// fixed y when <paramref name="horizontal"/>, else across y at a fixed x.
        /// </summary>
        public double[]? Run(bool horizontal, double at, double from, double to)
        {
            var s = Ink.Scale;
            var hits = new List<int>();
            for (var i = (int)Math.Ceiling(from * s); i < (int)Math.Floor(to * s); i++)
            {
                var (px, py) = horizontal ? (i - (int)Math.Round(Left * s), (int)Math.Round((at - Top) * s))
                                          : ((int)Math.Round((at - Left) * s), i - (int)Math.Round(Top * s));
                if (px < 0 || py < 0 || px >= Image.Width || py >= Image.Height) continue;
                if (IsOn(px, py)) hits.Add(i);
            }
            return hits.Count == 0 ? null : [hits.Min() / (double)s, (hits.Max() + 1) / (double)s];
        }
    }

    private static async Task<Shot> Take(IPage page)
    {
        var box = (await page.Locator("#box").BoundingBoxAsync())!;
        var (left, top) = (box.X - 20, box.Y - 20);
        var png = await page.ScreenshotAsync(new() { Clip = new() { X = left, Y = top, Width = box.Width + 40, Height = box.Height + 40 } });
        var on = await page.EvaluateAsync<int[]>("""
            () => { const m = getComputedStyle(document.getElementById('lo')).getPropertyValue('--range-on');
                    const p = document.createElement('i'); p.style.color = m; document.body.append(p);
                    const c = getComputedStyle(p).color.match(/\d+/g).map(Number); p.remove(); return c.slice(0, 3); }
            """);
        return new(Ink.Png.Decode(png), left, top, (on[0], on[1], on[2]));
    }

    /// <summary>The first range's box, the thumb's size and the track's thickness, in page pixels.</summary>
    private sealed record Geo(double Left, double Top, double Width, double Height, double Thumb, double Track);

    private static async Task<Geo> Geometry(IPage page)
    {
        var m = await page.EvaluateAsync<double[]>("""
            () => { const cs = getComputedStyle(document.getElementById('box'));
                    const t = document.getElementById('lo').getBoundingClientRect();
                    return [t.left, t.top, t.width, t.height,
                            parseFloat(cs.getPropertyValue('--range-thumb')), parseFloat(cs.getPropertyValue('--range-track'))]; }
            """);
        return new(m[0], m[1], m[2], m[3], m[4], m[5]);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("form-range--sm", false)]
    [InlineData("form-range--lg", false)]
    [InlineData("", true)]
    [InlineData("form-range--sm", true)]
    [InlineData("form-range--lg", true)]
    public async Task Each_thumb_is_centred_on_the_track_where_its_value_is(string size, bool vertical)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Box(size, vertical), deviceScaleFactor: Ink.Scale);
        var g = await Geometry(page);
        var shot = await Take(page);
        var horizontal = !vertical;

        // The track's centre line, read off the fill halfway between the thumbs.
        var length = horizontal ? g.Width : g.Height;
        double Along(double ratio) => horizontal
            ? g.Left + g.Thumb / 2 + ratio * (length - g.Thumb)
            : g.Top + g.Height - g.Thumb / 2 - ratio * (length - g.Thumb);
        var (crossFrom, crossTo) = horizontal ? (g.Top - 10, g.Top + g.Height + 10) : (g.Left - 10, g.Left + g.Width + 10);
        var fill = shot.Run(!horizontal, Along(0.5), crossFrom, crossTo)
            ?? throw new Xunit.Sdk.XunitException("No fill is painted between the thumbs.");
        var line = (fill[0] + fill[1]) / 2;
        Assert.True(Math.Abs(fill[1] - fill[0] - g.Track) <= 0.75, $"The track is {fill[1] - fill[0]:0.00}px thick, not {g.Track}.");

        foreach (var ratio in new[] { 0.25, 0.75 })
        {
            var across = shot.Run(!horizontal, Along(ratio), crossFrom, crossTo)!;
            var crossCentre = (across[0] + across[1]) / 2;
            Assert.True(Math.Abs(crossCentre - line) <= 0.5,
                $"{Engine}: the thumb at {ratio} is centred {crossCentre - line:+0.00;-0.00}px off the track's centre line.");
            Assert.True(Math.Abs(across[1] - across[0] - g.Thumb) <= 0.75, $"The thumb is {across[1] - across[0]:0.00}px, not {g.Thumb}.");

            // Along the track, on a line clear of the track itself but through the thumb.
            var off = line + g.Thumb / 2 - 2;
            var (from, to) = (Along(ratio) - g.Thumb, Along(ratio) + g.Thumb);
            var along = shot.Run(horizontal, off, Math.Min(from, to), Math.Max(from, to))!;
            var alongCentre = (along[0] + along[1]) / 2;
            Assert.True(Math.Abs(alongCentre - Along(ratio)) <= 0.75,
                $"{Engine}: the thumb at {ratio} is drawn at {alongCentre:0.00}, its value puts it at {Along(ratio):0.00}.");
        }
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_ticks_and_the_bubbles_stand_over_their_values(bool vertical)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Box("", vertical));
        var g = await Geometry(page);
        var length = vertical ? g.Height : g.Width;
        double Along(double ratio) => vertical
            ? g.Top + g.Height - g.Thumb / 2 - ratio * (length - g.Thumb)
            : g.Left + g.Thumb / 2 + ratio * (length - g.Thumb);

        var marks = await page.EvaluateAsync<double[]>("""
            v => [...document.querySelectorAll('#marks option')].map(o => { const r = o.getBoundingClientRect();
                   return v ? r.top + r.height / 2 : r.left + r.width / 2; })
            """, vertical);
        for (var i = 0; i < marks.Length; i++)
            Assert.True(Math.Abs(marks[i] - Along(i / 4.0)) <= 0.5, $"Mark {i} is at {marks[i]:0.00}, its value at {Along(i / 4.0):0.00}.");

        var bubbles = await page.EvaluateAsync<double[]>("""
            v => ['blo', 'bhi'].map(id => { const r = document.getElementById(id).getBoundingClientRect();
                   return v ? r.top + r.height / 2 : r.left + r.width / 2; })
            """, vertical);
        Assert.True(Math.Abs(bubbles[0] - Along(0.25)) <= 0.5, $"The low bubble is at {bubbles[0]:0.00}, its thumb at {Along(0.25):0.00}.");
        Assert.True(Math.Abs(bubbles[1] - Along(0.75)) <= 0.5, $"The high bubble is at {bubbles[1]:0.00}, its thumb at {Along(0.75):0.00}.");

        // The bubbles stand off the track, never over it.
        var clear = await page.EvaluateAsync<bool>("""
            v => { const t = document.getElementById('lo').getBoundingClientRect();
                   return ['blo', 'bhi'].every(id => { const b = document.getElementById(id).getBoundingClientRect();
                     return v ? b.right <= t.left + 0.5 : b.bottom <= t.top + 0.5; }); }
            """, vertical);
        Assert.True(clear, "A bubble overlaps the track.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Only_the_thumbs_of_a_span_take_the_pointer()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Box("", false).Replace("step=\"250\"", "step=\"10\"", StringComparison.Ordinal));
        var g = await Geometry(page);
        float X(double ratio) => (float)(g.Left + g.Thumb / 2 + ratio * (g.Width - g.Thumb));
        var y = (float)(g.Top + g.Height / 2);
        async Task<string[]> Values() => await page.EvaluateAsync<string[]>("() => [lo.value, hi.value]");

        // A press on the track between the thumbs moves neither.
        await page.Mouse.ClickAsync(X(0.5), y);
        Assert.Equal(["250", "750"], await Values());

        // The low thumb moves the low end, and stops at the high one.
        await page.Mouse.MoveAsync(X(0.25), y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(X(0.4), y, new() { Steps = 4 });
        await page.Mouse.MoveAsync(X(0.9), y, new() { Steps = 8 });
        await page.Mouse.UpAsync();
        Assert.Equal(["750", "750"], await Values());

        // Met at the top of the track, the pair is pulled apart from below.
        await Js.Run(page, "() => { for (const i of [hi, lo]) { i.value = 1000; i.dispatchEvent(new Event('input', { bubbles: true })); } }");
        await page.Mouse.MoveAsync(X(1), y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(X(0.6), y, new() { Steps = 8 });
        await page.Mouse.UpAsync();
        var apart = await Values();
        Assert.True(int.Parse(apart[0]) < 1000 && apart[1] == "1000", $"{Engine}: met at the top, a drag down left [{string.Join(", ", apart)}].");

        // Met at the bottom, they are pulled apart from above.
        await Js.Run(page, "() => { for (const i of [lo, hi]) { i.value = 0; i.dispatchEvent(new Event('input', { bubbles: true })); } }");
        await page.Mouse.MoveAsync(X(0), y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(X(0.4), y, new() { Steps = 8 });
        await page.Mouse.UpAsync();
        apart = await Values();
        Assert.True(apart[0] == "0" && int.Parse(apart[1]) > 0, $"{Engine}: met at the bottom, a drag up left [{string.Join(", ", apart)}].");
        Assert.Empty(errors);
    }
}
