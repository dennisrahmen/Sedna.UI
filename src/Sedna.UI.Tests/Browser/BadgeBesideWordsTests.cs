using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A badge beside the words of a table cell — "Olten" and its "Yours" — is centred on the same line
/// as the words, in a flex row, in every size the badge offers.
/// </summary>
/// <remarks>
/// Set in the same line of text, a badge is centred on the middle of the line's x-height, which is
/// lower than the middle of its capitals, so it stood a pixel under the word beside it. A row that
/// centres the two boxes lines up the capitals of both, since each is trimmed to its own.
/// </remarks>
public class BadgeBesideWordsTests : ScriptTestBase
{
    private const double Tolerance = 1.0 + 1.0 / 16;

    [Theory]
    [InlineData("badge")]
    [InlineData("badge badge--sm")]
    [InlineData("badge badge--lg")]
    [InlineData("badge badge--sm badge--brand")]
    public async Task A_badge_in_a_row_with_the_words_beside_it_stands_on_their_capitals(string classes)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div style="padding:24px; width:480px"><table class="table"><tbody><tr>
              <td><div class="sedna-row"><span id="w">OLTEN 49</span><span class="{classes}" id="b">YOURS 42</span></div></td>
            </tr></tbody></table></div>
            """, deviceScaleFactor: Ink.Scale);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);

        var word = (await Ink.Extent(page, 0, "#w"))[0];
        // The badge's words, a pixel in from its border.
        var label = (await Ink.Extent(page, 3, "#b"))[0];
        var delta = (label[1] + label[3]) / 2 - (word[1] + word[3]) / 2;

        Assert.True(Math.Abs(delta) <= Tolerance,
            $".{classes}: the badge's words stand {delta:+0.00;-0.00}px from the middle of the word beside it.");
        Assert.Empty(errors);
    }
}
