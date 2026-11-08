using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>.rating</c>: which stars are lit, whole or half, for a shown score and for a chosen
/// radio; the row mirrored on a right-to-left page; the stars visible in forced colours; and the
/// editable row at the control height of its tier.
/// </summary>
public class RatingLayoutTests : ScriptTestBase
{
    private const string Shown = """
        <span class="rating" id="shown" role="img" aria-label="Rated 3.5 out of 5">
          <span class="rating-star rating-star--on"></span>
          <span class="rating-star rating-star--on"></span>
          <span class="rating-star rating-star--on"></span>
          <span class="rating-star rating-star--half"></span>
          <span class="rating-star"></span>
        </span>
        """;

    private static string Halves(string name = "fit")
    {
        var stars = string.Concat(Enumerable.Range(1, 5).Select(i => $"""
            <span class="rating-star">
              <label class="rating-half"><input type="radio" name="{name}" id="{name}-{i * 2 - 1}" value="{i - 1}.5" aria-label="{i - 1} and a half stars"></label>
              <label class="rating-half"><input type="radio" name="{name}" id="{name}-{i * 2}" value="{i}" aria-label="{i} stars"></label>
            </span>
            """));
        return $"""<div class="rating" id="{name}" role="radiogroup" aria-label="Rate the fit">{stars}</div>""";
    }

    /// <summary>Each star of a row: "full", "half" or "off", read from its lit drawing.</summary>
    private static Task<string[]> Lit(IPage page, string row) =>
        page.EvaluateAsync<string[]>("""
            row => [...document.querySelectorAll('#' + row + ' > .rating-star')].map(star => {
              const s = getComputedStyle(star, '::after');
              if (s.visibility !== 'visible') return 'off';
              // The half drawing starts at the foot of the star's middle, 75.1% down; engines print it to different precision.
              return /^polygon\(50% 75\./.test(s.clipPath) ? 'half' : 'full';
            })
            """, row);

    [Fact]
    public async Task A_shown_score_lights_whole_and_half_stars()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Shown);

        Assert.Equal(["full", "full", "full", "half", "off"], await Lit(page, "shown"));

        // Every star draws its outline, lit or not: that is what an empty star is.
        var outlines = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('#shown > .rating-star')].map(s => getComputedStyle(s, '::before').clipPath)
            """);
        Assert.All(outlines, o => Assert.StartsWith("polygon(evenodd", o, StringComparison.Ordinal));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_chosen_half_lights_every_star_before_it_and_half_of_its_own()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Halves());

        Assert.Equal(["off", "off", "off", "off", "off"], await Lit(page, "fit"));

        await page.ClickAsync("label:has(> #fit-7)");
        Assert.True(await page.IsCheckedAsync("#fit-7"));
        await page.Mouse.MoveAsync(0, 0);
        Assert.Equal(["full", "full", "full", "half", "off"], await Lit(page, "fit"));

        await page.ClickAsync("label:has(> #fit-10)");
        await page.Mouse.MoveAsync(0, 0);
        Assert.Equal(["full", "full", "full", "full", "full"], await Lit(page, "fit"));

        // The keyboard moves the score half a star at a time, through the radios.
        await page.FocusAsync("#fit-10");
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.True(await page.IsCheckedAsync("#fit-9"));
        Assert.Equal(["full", "full", "full", "full", "half"], await Lit(page, "fit"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Pointing_at_a_half_previews_that_score()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Halves());

        await page.ClickAsync("label:has(> #fit-10)");
        await page.HoverAsync("label:has(> #fit-3)");
        Assert.Equal(["full", "half", "off", "off", "off"], await Lit(page, "fit"));
        await page.Mouse.MoveAsync(0, 0);
        Assert.Equal(["full", "full", "full", "full", "full"], await Lit(page, "fit"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Each_half_is_half_the_star_and_the_first_is_at_the_inline_start()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Halves());

        var ltr = await Geometry(page);
        Assert.Equal(ltr.Star / 2, ltr.FirstWidth, 0.5);
        Assert.Equal(ltr.Star / 2, ltr.SecondWidth, 0.5);
        Assert.True(ltr.FirstLeft < ltr.SecondLeft, "The first half is not on the left on a left-to-right page.");
        Assert.True(ltr.Star1Left < ltr.Star5Left, "The first star is not on the left on a left-to-right page.");
        Assert.Equal("none", ltr.Transform);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_right_to_left_page_mirrors_the_row_and_the_half_lit_star()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Shown + Halves());
        await Js.Run(page, "() => document.documentElement.setAttribute('dir', 'rtl')");

        var rtl = await Geometry(page);
        Assert.True(rtl.Star1Left > rtl.Star5Left, "The first star is not on the right on a right-to-left page.");
        Assert.True(rtl.FirstLeft > rtl.SecondLeft, "The first half is not on the right on a right-to-left page.");
        // The half-lit drawing is a polygon measured from the left, so it turns round.
        Assert.Equal("matrix(-1, 0, 0, 1, 0, 0)", rtl.Transform);

        var shown = await page.EvaluateAsync<double[]>("""
            () => [...document.querySelectorAll('#shown > .rating-star')].map(s => s.getBoundingClientRect().left)
            """);
        Assert.True(shown[0] > shown[4], "A shown score does not run from the right on a right-to-left page.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task In_forced_colours_lit_and_empty_stars_stay_painted_and_apart()
    {
        // Forced colours exist in Chromium only (see BrowserEngine).
        if (NoBrowser || !BrowserEngine.EmulatesForcedColors) return;
        var (page, errors) = await OpenStyled(Shown);
        await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });

        var c = await page.EvaluateAsync<string[]>("""
            () => {
              const stars = document.querySelectorAll('#shown > .rating-star');
              return [
                getComputedStyle(stars[0], '::after').backgroundColor,
                getComputedStyle(stars[4], '::before').backgroundColor,
                getComputedStyle(document.body).backgroundColor,
                getComputedStyle(stars[0], '::after').forcedColorAdjust,
                getComputedStyle(stars[4], '::before').forcedColorAdjust,
                String(matchMedia('(forced-colors: active)').matches),
              ];
            }
            """);

        Assert.Equal("true", c[5]);
        Assert.Equal("none", c[3]);
        Assert.Equal("none", c[4]);
        Assert.NotEqual(c[2], c[0]);
        Assert.NotEqual(c[2], c[1]);
        Assert.NotEqual(c[0], c[1]);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("rating--sm", "form-input--sm", 28)]
    [InlineData("", "", 36)]
    [InlineData("rating--lg", "form-input--lg", 44)]
    public async Task An_editable_row_is_the_control_height_of_its_tier(string tier, string field, int height)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div class="sedna-row-wrap">
              <div class="rating {tier}" id="row" role="radiogroup" aria-label="Rate">
                <label class="rating-star"><input type="radio" name="r" aria-label="1 star"></label>
                <label class="rating-star"><input type="radio" name="r" aria-label="2 stars"></label>
              </div>
              <input class="form-input {field}" id="field" aria-label="Comment">
            </div>
            """);

        var h = await page.EvaluateAsync<double[]>("""
            () => ['row', 'field'].map(id => document.getElementById(id).getBoundingClientRect().height)
            """);
        Assert.Equal(height, h[0], 0.5);
        Assert.Equal(h[1], h[0], 0.5);
        Assert.Empty(errors);
    }

    private sealed record Layout(double Star, double FirstLeft, double FirstWidth, double SecondLeft, double SecondWidth,
        double Star1Left, double Star5Left, string Transform);

    private static async Task<Layout> Geometry(IPage page)
    {
        var v = await page.EvaluateAsync<string[]>("""
            () => {
              const stars = document.querySelectorAll('#fit > .rating-star');
              const halves = stars[0].querySelectorAll('.rating-half');
              const a = halves[0].getBoundingClientRect(), b = halves[1].getBoundingClientRect();
              return [stars[0].getBoundingClientRect().width, a.left, a.width, b.left, b.width,
                      stars[0].getBoundingClientRect().left, stars[4].getBoundingClientRect().left,
                      getComputedStyle(stars[0], '::after').transform].map(String);
            }
            """);
        double D(int i) => double.Parse(v[i], System.Globalization.CultureInfo.InvariantCulture);
        return new Layout(D(0), D(1), D(2), D(3), D(4), D(5), D(6), v[7]);
    }
}
