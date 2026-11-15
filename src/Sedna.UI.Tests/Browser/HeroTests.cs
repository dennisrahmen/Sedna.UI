using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.page-head--hero</c>: a page head with a picture. The words on it clear WCAG AA over the
/// scrim on the brightest possible photograph, in both page variants; the head is capped at the
/// content width on a very wide screen; the scrim runs from the inline start in either direction;
/// and it fits a phone.
/// </summary>
public class HeroTests : ScriptTestBase
{
    // A white 8x8 picture: the brightest thing a photograph can be, so the scrim is all there is.
    private const string White =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAD0lEQVR4nGP4jwMwDC0JALoev0Ewkwr8AAAAAElFTkSuQmCC";

    private static string Fixture(string dir = "ltr") =>
        $"""
        <div dir="{dir}" style="padding:0;margin:0">
          <header class="page-head page-head--hero" id="hero" data-variant="dark">
            <img class="page-head-image" id="img" src="{White}" alt="" width="1600" height="900">
            <div class="page-head-text" id="text">
              <p class="eyebrow" id="eyebrow">Services</p>
              <h1 id="title">What we build for the web</h1>
              <p id="lead">Sites, shops and the systems behind them, kept running by the people who built them.</p>
            </div>
            <div class="page-head-actions">
              <a class="btn btn--solid btn--lg" id="solid" href="#">Get in touch</a>
              <a class="btn btn--ghost btn--lg" id="ghost" href="#">See the work</a>
              <a class="btn btn--outline btn--lg" id="outline" href="#">Our story</a>
              <a class="btn btn--lg" id="plainbtn" href="#">Jobs</a>
            </div>
          </header>
        </div>
        """;

    private const string Contrast =
        """
        () => {
          const lum = ([r, g, b]) => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b); };
          const rgba = s => s.match(/[\d.]+/g).map(Number);
          const probe = document.createElement('i');
          document.body.append(probe);
          const hero = document.getElementById('hero');
          const read = name => { probe.style.color = ''; hero.append(probe); probe.style.color = 'var(' + name + ')'; return rgba(getComputedStyle(probe).color); };
          const strong = read('--hero-scrim-strong');
          // The brightest photograph, then the scrim over it.
          const a = strong.length > 3 ? strong[3] : 1;
          const ground = [0, 1, 2].map(i => 255 * (1 - a) + strong[i] * a);
          const L2 = lum(ground);
          const out = {};
          for (const id of ['title', 'lead', 'eyebrow', 'ghost', 'outline', 'plainbtn']) {
            const c = rgba(getComputedStyle(document.getElementById(id)).color).slice(0, 3);
            const L1 = lum(c);
            out[id] = (Math.max(L1, L2) + 0.05) / (Math.min(L1, L2) + 0.05);
          }
          probe.remove();
          return out;
        }
        """;

    private async Task<IPage> Hero(string variant, string dir = "ltr", int width = 1280, string style = "", string classes = "")
    {
        var markup = Fixture(dir).Replace("id=\"hero\"", $"id=\"hero\" style=\"{style}\"", StringComparison.Ordinal)
            .Replace("page-head--hero\"", "page-head--hero " + classes + "\"", StringComparison.Ordinal);
        var (page, _) = await OpenStyled(markup);
        await page.SetViewportSizeAsync(width, 900);
        await Js.Run(page, $"() => document.documentElement.dataset.variant = '{variant}'");
        return page;
    }

    [Theory]
    [InlineData("dark", "")]
    [InlineData("light", "")]
    [InlineData("dark", "data-cvd")]
    [InlineData("light", "data-cvd")]
    [InlineData("light", "data-contrast")]
    public async Task The_words_clear_AA_over_the_brightest_photograph(string variant, string setting)
    {
        if (NoBrowser) return;
        var page = await Hero(variant);
        if (setting.Length > 0)
            await Js.Run(page, $"() => document.documentElement.setAttribute('{setting}', '{(setting == "data-cvd" ? "1" : "more")}')");

        var ratios = await page.EvaluateAsync<Dictionary<string, double>>(Contrast);
        foreach (var (id, ratio) in ratios)
            Assert.True(ratio >= 4.5, $"#{id} measures {ratio:0.00}:1 over the scrim on a white picture in {variant}{(setting.Length > 0 ? " with " + setting : "")}; AA text is 4.5:1.");
    }

    [Fact]
    public async Task The_picture_is_an_image_that_fills_the_head_and_is_cropped_to_it()
    {
        if (NoBrowser) return;
        var page = await Hero("light");
        var report = await page.EvaluateAsync<double[]>(@"() => {
            const h = document.getElementById('hero').getBoundingClientRect();
            const i = document.getElementById('img').getBoundingClientRect();
            return [h.left - i.left, h.top - i.top, h.width - i.width, h.height - i.height, getComputedStyle(document.getElementById('img')).objectFit === 'cover' ? 0 : 1];
        }");
        Assert.All(report, d => Assert.Equal(0, d, 0.5));
    }

    [Fact]
    public async Task On_a_very_wide_screen_the_head_stops_at_the_content_width_and_is_centred()
    {
        if (NoBrowser) return;
        var page = await Hero("light", width: 2800);
        var report = await page.EvaluateAsync<double[]>(@"() => {
            const h = document.getElementById('hero').getBoundingClientRect();
            const max = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--page-max'));
            return [h.width, max, h.left, window.innerWidth - h.right];
        }");
        Assert.Equal(report[1], report[0], 0.5);
        Assert.True(report[2] > 100, "The head is not set away from the edge.");
        Assert.Equal(report[2], report[3], 1);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_scrim_is_strongest_at_the_inline_start(string dir)
    {
        if (NoBrowser) return;
        var page = await Hero("light", dir, width: 1800);
        // Hide the words so only the picture and its scrim are in the screenshot.
        await page.AddStyleTagAsync(new() { Content = "#text, .page-head-actions { visibility: hidden; }" });
        var box = (await page.Locator("#hero").BoundingBoxAsync())!;
        var png = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = new() { X = box.X, Y = box.Y, Width = box.Width, Height = box.Height } }));
        var y = png.Height / 2;
        var inlineStartIsLeft = dir == "ltr";
        var nearStart = png.Pixel(inlineStartIsLeft ? 8 : png.Width - 9, y);
        var nearEnd = png.Pixel(inlineStartIsLeft ? png.Width - 9 : 8, y);
        Assert.True(nearStart.R < nearEnd.R, $"In {dir} the scrim should be darker at the inline start: {nearStart} against {nearEnd}.");
    }

    [Fact]
    public async Task On_a_phone_it_fits_and_the_whole_head_is_the_strong_part()
    {
        if (NoBrowser) return;
        var page = await Hero("light", width: 360);
        var report = await page.EvaluateAsync<double[]>(@"() => {
            const h = document.getElementById('hero');
            const r = h.getBoundingClientRect();
            const s = getComputedStyle(h, '::after');
            return [document.documentElement.scrollWidth - document.documentElement.clientWidth, r.width, h.scrollWidth - h.clientWidth,
                    document.getElementById('title').scrollWidth - document.getElementById('title').clientWidth];
        }");
        Assert.True(report[0] <= 0, "The page scrolls sideways on a phone.");
        Assert.True(report[2] <= 0 && report[3] <= 0, "A part of the head overflows it on a phone.");
        Assert.Equal(360, report[1], 1);
    }

    [Fact]
    public async Task Forced_colours_put_the_words_on_the_users_canvas()
    {
        if (NoBrowser || !BrowserEngine.EmulatesForcedColors) return;
        var page = await Open(Fixture(), head: StylesheetTag, forcedColors: ForcedColors.Active);
        var report = await page.EvaluateAsync<string[]>(@"() => {
            const t = getComputedStyle(document.getElementById('text'));
            const probe = document.createElement('i'); probe.style.color = 'Canvas'; document.body.append(probe);
            const canvas = getComputedStyle(probe).color; probe.remove();
            return [t.backgroundColor, canvas];
        }");
        Assert.Equal(report[1], report[0]);
    }

    [Fact]
    public async Task The_text_column_paints_nothing_over_the_scrim()
    {
        if (NoBrowser) return;
        foreach (var variant in new[] { "dark", "light" })
        {
            var page = await Hero(variant);
            var bg = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('text')).backgroundColor");
            Assert.True(bg is "rgba(0, 0, 0, 0)" or "transparent",
                $"The text column paints {bg} in {variant}: a box lighter than the scrim shows behind the words.");
        }
    }

    [Fact]
    public async Task Every_hero_token_reaches_the_head()
    {
        if (NoBrowser) return;
        var page = await Hero("light", width: 2000,
            style: "--hero-radius: 0px; --hero-max: none; --hero-focus: 20% 80%; --hero-pad-block: 10px; --hero-pad-inline: 12px; --hero-gap: 7px; --hero-text-shadow: 0 1px 2px var(--hero-scrim-strong)");
        var r = await page.EvaluateAsync<string[]>(@"() => {
            const h = document.getElementById('hero'), cs = getComputedStyle(h);
            return [cs.borderTopLeftRadius, String(Math.round(h.getBoundingClientRect().width)), String(window.innerWidth - (document.documentElement.scrollWidth - document.documentElement.clientWidth)),
                    getComputedStyle(document.getElementById('img')).objectPosition, cs.paddingTop, cs.paddingLeft, cs.rowGap,
                    getComputedStyle(document.getElementById('title')).textShadow];
        }");
        Assert.Equal("0px", r[0]);
        Assert.True(double.Parse(r[1], System.Globalization.CultureInfo.InvariantCulture) > 1900, $"--hero-max: none left the head {r[1]}px wide.");
        Assert.Equal("20% 80%", r[3]);
        Assert.Equal("10px", r[4]);
        Assert.Equal("12px", r[5]);
        Assert.Equal("7px", r[6]);
        Assert.NotEqual("none", r[7]);
    }

    [Fact]
    public async Task The_scrim_holds_and_ends_where_its_tokens_say()
    {
        if (NoBrowser) return;
        var page = await Hero("light", width: 1600, style: "--hero-scrim-hold: 20%; --hero-scrim-end: 40%");
        await page.AddStyleTagAsync(new() { Content = "#text, .page-head-actions { visibility: hidden; }" });
        var box = (await page.Locator("#hero").BoundingBoxAsync())!;
        var png = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = new() { X = box.X, Y = box.Y, Width = box.Width, Height = box.Height } }));
        var y = png.Height / 2;
        int At(double share) => png.Pixel((int)(png.Width * share), y).R;
        Assert.InRange(Math.Abs(At(0.05) - At(0.18)), 0, 2);
        Assert.True(At(0.30) > At(0.18) + 10, "Between the hold and the end the scrim does not fade.");
        Assert.InRange(Math.Abs(At(0.60) - At(0.95)), 0, 2);
        Assert.True(At(0.60) > At(0.30), "Past its end the scrim is not at its soft end.");
    }

    [Fact]
    public async Task Centred_words_sit_on_a_strong_band_down_the_middle()
    {
        if (NoBrowser) return;
        var page = await Hero("light", width: 1800, classes: "page-head--hero-center page-head--hero-middle");
        var placement = await page.EvaluateAsync<double[]>(@"() => {
            const h = document.getElementById('hero').getBoundingClientRect(), t = document.getElementById('text').getBoundingClientRect();
            return [(t.left + t.right) / 2 - (h.left + h.right) / 2, (t.top + t.bottom) / 2 - (h.top + h.bottom) / 2, getComputedStyle(document.getElementById('title')).textAlign === 'center' ? 0 : 1];
        }");
        Assert.Equal(0, placement[0], 2);
        Assert.Equal(0, placement[2]);

        var ratios = await page.EvaluateAsync<Dictionary<string, double>>(Contrast);
        foreach (var (id, ratio) in ratios)
            Assert.True(ratio >= 4.5, $"#{id} measures {ratio:0.00}:1 on the centred band.");

        await page.AddStyleTagAsync(new() { Content = "#text, .page-head-actions { visibility: hidden; }" });
        var box = (await page.Locator("#hero").BoundingBoxAsync())!;
        var png = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = new() { X = box.X, Y = box.Y, Width = box.Width, Height = box.Height } }));
        var y = png.Height / 2;
        var middle = png.Pixel(png.Width / 2, y).R;
        var left = png.Pixel(8, y).R;
        var right = png.Pixel(png.Width - 9, y).R;
        Assert.True(middle < left && middle < right, $"The middle ({middle}) is not darker than the edges ({left}, {right}).");
        Assert.InRange(Math.Abs(left - right), 0, 2);
    }

    [Theory]
    [InlineData("page-head--hero-top", -1)]
    [InlineData("page-head--hero-middle", 0)]
    [InlineData("", 1)]
    public async Task The_words_sit_at_the_top_the_middle_or_the_bottom(string classes, int where)
    {
        if (NoBrowser) return;
        var page = await Hero("light", classes: classes, style: "--hero-height: 40rem");
        var r = await page.EvaluateAsync<double[]>(@"() => {
            const h = document.getElementById('hero').getBoundingClientRect(), t = document.getElementById('text').getBoundingClientRect();
            const a = document.querySelector('.page-head-actions').getBoundingClientRect();
            return [t.top - h.top, h.bottom - a.bottom];
        }");
        var (top, bottom) = (r[0], r[1]);
        switch (where)
        {
            case -1: Assert.True(top < bottom, $"--hero-top: {top:0}px above the words, {bottom:0}px below."); break;
            case 0: Assert.Equal(top, bottom, 2); break;
            default: Assert.True(bottom < top, $"Default: {top:0}px above the words, {bottom:0}px below."); break;
        }
    }
}
