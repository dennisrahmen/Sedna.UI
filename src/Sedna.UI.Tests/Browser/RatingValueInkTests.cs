using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The figure printed with a shown score sits on the stars' weight: its digits are centred a little
/// below the middle of a star's box, where the mass of a five-pointed star is — measured on the paint,
/// at every star size.
/// </summary>
/// <remarks>
/// A star is a point on top and two on the bottom, so its bounding box is centred on the box and its
/// mass sits at about 55% of its height. A figure centred on the box looks a pixel high beside it, which
/// is what the first example of the ratings page showed.
/// </remarks>
public class RatingValueInkTests : ScriptTestBase
{
    private static string Row(string modifier) => $"""
        <div style="padding: 20px">
          <span class="rating {modifier}" role="img" aria-label="Rated 4 out of 5">
            <span class="rating-star rating-star--on" id="star"></span>
            <span class="rating-star rating-star--on"></span>
            <span class="rating-star rating-star--on"></span>
            <span class="rating-star rating-star--on"></span>
            <span class="rating-star"></span>
            <span class="rating-value" id="value" aria-hidden="true">4.0</span>
          </span>
        </div>
        """;

    public static TheoryData<string, double> Sizes() => new()
    {
        { "rating--sm", 16 },
        { "", 20 },
        { "rating--lg", 28 },
    };

    [Theory]
    [MemberData(nameof(Sizes))]
    public async Task The_figure_is_centred_a_little_below_the_middle_of_a_star(string modifier, double size)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Row(modifier), deviceScaleFactor: Ink.Scale);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);

        var ink = await Ink.Of(page, "#star", "#value");
        var below = Ink.Centre(ink[1]) - Ink.Centre(ink[0]);

        // Between the box's middle and a tenth of the star below it; the mass is at five hundredths,
        // and a figure on the middle itself — the defect — is within a fraction of a pixel of 0.
        Assert.True(below >= size * 0.02 && below <= size * 0.1,
            $"{(modifier == "" ? "rating" : modifier)}: the figure's ink is {below:0.00}px below the star's middle; it belongs between {size * 0.02:0.00} and {size * 0.1:0.00}px.");
        Assert.Empty(errors);
    }
}
