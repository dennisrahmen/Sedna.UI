using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The rule between tiles of a divided <c>.stat-row</c>, measured when the row wraps.
/// </summary>
/// <remarks>
/// The rule used to be dropped from <c>:first-child</c> only, so a row of four that wrapped
/// to two lines left the third tile — first on its line — indented behind a rule. Every
/// tile now draws one in the gap before it, and the row clips the one that falls outside
/// its edge; these tests find each rule and say whether it is inside the clip.
/// </remarks>
public class StatRowDividerTests : ScriptTestBase
{
    private static string Tiles(int count, string tag = "div") =>
        string.Concat(Enumerable.Range(0, count).Select(i =>
            $"""<{tag} class="stat" id="t{i}"{(tag == "a" ? " href=\"#\"" : "")}><span class="stat-label">Count {i}</span><span class="stat-value">{i * 7}</span></{tag}>"""));

    private static string InCard(int width, string tiles) =>
        $"""<div class="card" style="width:{width}px"><div class="card-body"><div class="stat-row" id="row">{tiles}</div></div></div>""";

    private static string Divided(int width, string tiles) =>
        $"""<div class="stat-row stat-row--divided" id="row" style="width:{width}px">{tiles}</div>""";

    private sealed class Tile
    {
        public double Left { get; set; }
        public double Right { get; set; }
        public double Top { get; set; }
        public double Rule { get; set; }
    }

    private sealed class Layout
    {
        public double Left { get; set; }
        public double Right { get; set; }
        public double ClipMargin { get; set; }
        public Tile[] Tiles { get; set; } = [];
    }

    private static async Task<Layout> Measure(Microsoft.Playwright.IPage page) =>
        await page.EvaluateAsync<Layout>("""
            () => {
              const row = document.getElementById('row');
              const r = row.getBoundingClientRect();
              // The track, not the box: a linked tile's box reaches past it by a negative margin.
              const tiles = [...row.children].map(t => {
                const b = t.getBoundingClientRect();
                const s = getComputedStyle(t);
                const before = getComputedStyle(t, '::before');
                return { left: b.left - parseFloat(s.marginLeft), right: b.right + parseFloat(s.marginRight),
                         top: b.top - parseFloat(s.marginTop), rule: b.left + parseFloat(before.left) };
              });
              return { left: r.left, right: r.right,
                       clipMargin: parseFloat(getComputedStyle(row).overflowClipMargin) || 0, tiles };
            }
            """);

    private static void AssertRulesFollowTheLines(Layout layout)
    {
        var clipEdge = layout.Left - layout.ClipMargin;
        for (var i = 0; i < layout.Tiles.Length; i++)
        {
            var tile = layout.Tiles[i];
            var startsALine = i == 0 || tile.Top > layout.Tiles[i - 1].Top + 0.5;
            if (startsALine)
            {
                Assert.Equal(layout.Left, tile.Left, 0.5);
                Assert.True(tile.Rule < clipEdge, $"Tile {i} starts a line and its rule at {tile.Rule} is inside the clip edge {clipEdge}.");
            }
            else
            {
                var previous = layout.Tiles[i - 1];
                Assert.InRange(tile.Rule, previous.Right, tile.Left);
                Assert.Equal((previous.Right + tile.Left) / 2, tile.Rule + 0.5, 1.0);
            }
        }
    }

    [Theory]
    [InlineData(360)]
    [InlineData(520)]
    [InlineData(760)]
    public async Task A_card_row_that_wraps_has_a_rule_between_tiles_and_none_at_a_line_start(int width)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(InCard(width, Tiles(4)));

        var layout = await Measure(page);

        AssertRulesFollowTheLines(layout);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_divided_row_on_the_page_wraps_the_same_way()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Divided(400, Tiles(4)));

        var layout = await Measure(page);

        Assert.True(layout.Tiles[2].Top > layout.Tiles[0].Top, "Four tiles in 400px should wrap.");
        AssertRulesFollowTheLines(layout);
    }

    [Fact]
    public async Task Linked_tiles_put_their_rule_where_a_plain_tile_does()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(InCard(360, Tiles(4, "a")));

        var layout = await Measure(page);

        AssertRulesFollowTheLines(layout);
    }

    // 150px a tile was the plain row's floor and the width of a divided tile, padding and
    // all; the wider gap must not make a row that fitted on one line wrap.
    [Fact]
    public async Task Four_tiles_that_fitted_on_one_line_still_do()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Divided(600, Tiles(4)));

        var layout = await Measure(page);

        Assert.All(layout.Tiles, tile => Assert.Equal(layout.Tiles[0].Top, tile.Top, 0.5));
    }
}
