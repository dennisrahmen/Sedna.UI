using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Choice tiles are a real radio group: the arrows move the choice, the chosen tile shows it,
/// and a read-only group keeps its value.
/// </summary>
public class ChoiceTileTests : ScriptTestBase
{
    private static string Tiles(string group = "") => $"""
        <div class="choice-tiles" role="radiogroup" aria-label="Layout" {group}>
          <label class="choice-tile" id="a"><input type="radio" name="l" value="a" checked><span class="choice-tile-title">Stage</span></label>
          <label class="choice-tile" id="b"><input type="radio" name="l" value="b"><span class="choice-tile-title">Grid</span></label>
        </div>
        """;

    private static Task<string> Chosen(Microsoft.Playwright.IPage page) =>
        page.EvaluateAsync<string>("() => document.querySelector('input[name=l]:checked').value");

    [Fact]
    public async Task The_arrows_move_the_choice_and_the_chosen_tile_shows_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Tiles());
        await page.FocusAsync("#a input");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("b", await Chosen(page));

        var edges = await page.EvaluateAsync<string[]>("""
            () => [getComputedStyle(document.getElementById('a')).borderTopColor, getComputedStyle(document.getElementById('b')).borderTopColor]
            """);
        Assert.NotEqual(edges[0], edges[1]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_read_only_group_keeps_its_value()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Tiles("aria-readonly=\"true\""));
        await page.FocusAsync("#a input");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("a", await Chosen(page));
        Assert.Empty(errors);
    }
}
