using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What a stat tile holds besides its words sits where the tile puts it: every drawing a
/// step below the line above it, and the value's placeholder as tall as the figure.
/// </summary>
public class StatChildrenTests : ScriptTestBase
{
    /// <summary>Every drawing the library has that a stat can hold under its words.</summary>
    public static TheoryData<string> Drawings() => new()
    {
        """<span class="progress" data-drawing><span class="progress-bar" style="width:62%"></span></span>""",
        """<div class="meter" data-drawing><span class="meter-fill" style="--value: 62%"></span></div>""",
        """<div class="stack-bar" data-drawing><span class="stack-bar-segment series-go" style="--value: 62%"></span></div>""",
        """<div class="tracker" data-drawing><span class="tracker-block series-go"></span></div>""",
        """<svg class="sparkline" data-drawing viewBox="0 0 100 32" aria-hidden="true"><polyline class="sparkline-line" points="0,24 50,18 100,6" /></svg>""",
    };

    [Theory]
    [MemberData(nameof(Drawings))]
    public async Task A_drawing_stands_a_step_below_the_words(string drawing)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            $"""
            <div class="stat" style="width:240px">
              <span class="stat-label">Fulfilment rate</span>
              <span class="stat-value">62%</span>
              <span class="stat-target" id="above">62 of 80 lines reserved</span>
              {drawing}
            </div>
            """);

        var gaps = await page.EvaluateAsync<double[]>(
            """
            () => {
              const top = el => el.getBoundingClientRect();
              const above = top(document.getElementById('above')), drawing = top(document.querySelector('[data-drawing]'));
              const value = top(document.querySelector('.stat-value'));
              return [drawing.top - above.bottom, above.top - value.bottom];
            }
            """);

        // The words stand --space-1 apart; a drawing stands --space-1 plus --space-2 below them.
        Assert.True(gaps[0] > gaps[1] + 3,
            $"The drawing is {gaps[0]}px under the line above it, no further than the lines of words stand apart ({gaps[1]}px).");
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("stat")]
    [InlineData("stat stat--sm")]
    [InlineData("stat stat--lg")]
    public async Task The_values_placeholder_is_as_tall_as_the_figure(string classes)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            $"""
            <div class="{classes}"><span class="stat-label">Orders today</span><span class="stat-value" id="value">1,284</span></div>
            <div class="{classes}"><span class="skeleton skeleton-text"></span><span class="skeleton skeleton-title" id="placeholder"></span></div>
            """);

        var heights = await page.EvaluateAsync<double[]>(
            "() => ['value', 'placeholder'].map(id => document.getElementById(id).getBoundingClientRect().height)");

        Assert.Equal(heights[0], heights[1], 0.5);
        Assert.Empty(errors);
    }
}
