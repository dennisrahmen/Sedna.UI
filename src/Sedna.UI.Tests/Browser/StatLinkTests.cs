using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>a.stat</c>: a linked tile is measured against the same row of plain <c>div.stat</c>
/// tiles, and must sit where a plain tile would.
/// </summary>
/// <remarks>
/// The linked tile's hover box reaches past its track by a negative margin that its padding
/// gives back. A context that changes the padding and not the margin — the card's
/// <c>padding-block</c> — moves the label and the value off their neighbours'.
/// </remarks>
public class StatLinkTests : ScriptTestBase
{
    // A linked tile between plain ones, so the row a reader compares it with is in the same page.
    private static readonly bool[] Linked = [false, true, false, true];

    private static string Tiles(bool mixed) =>
        string.Concat(Linked.Select((linked, i) =>
        {
            var tag = mixed && linked ? "a" : "div";
            return $"""<{tag} class="stat" id="t{i}"{(tag == "a" ? " href=\"#\"" : "")}><span class="stat-label">Count {i}</span><span class="stat-value">{i * 7}</span><span class="stat-target">of {i * 10 + 40}</span></{tag}>""";
        }));

    private static string Row(string layout, string tiles) => layout switch
    {
        "card" => $"""<div class="card" style="width:640px"><div class="card-body"><div class="stat-row" id="row">{tiles}</div></div></div>""",
        "divided" => $"""<div class="stat-row stat-row--divided" id="row" style="width:640px">{tiles}</div>""",
        _ => $"""<div class="stat-row" id="row" style="width:640px">{tiles}</div>""",
    };

    private sealed class Box
    {
        public double[] Track { get; set; } = [];
        public double[] Label { get; set; } = [];
        public double[] Value { get; set; } = [];
    }

    private sealed class Layout
    {
        public double RowHeight { get; set; }
        public Box[] Tiles { get; set; } = [];
    }

    private static async Task<Layout> Measure(Microsoft.Playwright.IPage page) =>
        await page.EvaluateAsync<Layout>("""
            () => {
              const row = document.getElementById('row');
              const rect = el => { const b = el.getBoundingClientRect(); return [b.left, b.top, b.width, b.height]; };
              const tiles = [...row.children].map(t => {
                const b = t.getBoundingClientRect();
                const s = getComputedStyle(t);
                const ml = parseFloat(s.marginLeft), mr = parseFloat(s.marginRight);
                const mt = parseFloat(s.marginTop), mb = parseFloat(s.marginBottom);
                return {
                  track: [b.left - ml, b.top - mt, b.width + ml + mr, b.height + mt + mb],
                  label: rect(t.querySelector('.stat-label')),
                  value: rect(t.querySelector('.stat-value')),
                };
              });
              return { rowHeight: row.getBoundingClientRect().height, tiles };
            }
            """);

    private async Task<Layout> MeasureRow(string layout, bool mixed)
    {
        var (page, errors) = await OpenStyled(Row(layout, Tiles(mixed)));
        var measured = await Measure(page);
        Assert.Empty(errors);
        return measured;
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("divided")]
    [InlineData("card")]
    public async Task A_linked_tile_lines_up_with_a_plain_tile_in_the_same_row(string layout)
    {
        if (NoBrowser) return;

        var plain = await MeasureRow(layout, mixed: false);
        var mixed = await MeasureRow(layout, mixed: true);

        Assert.Equal(plain.RowHeight, mixed.RowHeight, 0.01);
        for (var i = 0; i < Linked.Length; i++)
        {
            for (var k = 0; k < 4; k++)
            {
                Assert.Equal(plain.Tiles[i].Track[k], mixed.Tiles[i].Track[k], 0.01);
                Assert.Equal(plain.Tiles[i].Label[k], mixed.Tiles[i].Label[k], 0.01);
                Assert.Equal(plain.Tiles[i].Value[k], mixed.Tiles[i].Value[k], 0.01);
            }
        }
    }
}
