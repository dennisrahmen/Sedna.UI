using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.sedna-span-full</c> takes the whole row of whichever grid it is in.
/// </summary>
/// <remarks>
/// It was <c>.card-span-full</c> and was documented for a card grid, so a form field
/// that needed the whole row of a <c>.field-grid</c> — a description, a checkbox line —
/// either wore a card's class or carried <c>style="grid-column:1 / -1"</c>. Measured in
/// both grids here, because "works in any grid" is the claim the rename makes.
/// </remarks>
public class GridSpanTests : ScriptTestBase
{
    private const string Fixture =
        """
        <div style="width:640px; padding:0">
          <div class="field-grid" id="fields">
            <div class="form-field" id="short">
              <label class="form-label" for="a">Queue</label>
              <input class="form-input" id="a" />
            </div>
            <div class="form-field" id="short-2">
              <label class="form-label" for="b">Owner</label>
              <input class="form-input" id="b" />
            </div>
            <div class="form-field sedna-span-full" id="long">
              <label class="form-label" for="c">What this queue is for</label>
              <textarea class="form-input" id="c" rows="2"></textarea>
            </div>
          </div>

          <div class="card-grid" id="cards">
            <div class="card" id="tile">one</div>
            <div class="card sedna-span-full" id="wide">two</div>
          </div>
        </div>
        """;

    [Fact]
    public async Task It_spans_the_row_of_a_field_grid_and_of_a_card_grid()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var width = await page.EvaluateAsync<double[]>(
            "ids => ids.map(id => Math.round(document.getElementById(id).getBoundingClientRect().width))",
            new[] { "fields", "short", "long", "cards", "tile", "wide" });

        var (fields, shortField, longField, cards, tile, wide) =
            (width[0], width[1], width[2], width[3], width[4], width[5]);

        Assert.True(shortField < fields,
            $"The two-up fields measured {shortField}px in a {fields}px grid, so the fixture is not "
            + "laid out two columns wide and the span proves nothing.");
        Assert.Equal(fields, longField);

        Assert.True(tile < cards,
            $"The card tile measured {tile}px in a {cards}px grid, so the fixture is one column wide.");
        Assert.Equal(cards, wide);
        Assert.Empty(errors);
    }
}
