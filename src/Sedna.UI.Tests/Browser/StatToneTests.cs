using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.stat--warn</c> and <c>.stat--danger</c>: a toned tile is measured against the same
/// row with no tone at all, and must not have moved anything.
/// </summary>
/// <remarks>
/// The tint reaches past the tile by a negative margin. Every number here is the track, the
/// label, the value and the dividing rule — what a reader lines up across a row — and each
/// has to land where the untoned row put it.
/// </remarks>
public class StatToneTests : ScriptTestBase
{
    private static readonly string[] Tones = ["", "stat--warn", "stat--danger", ""];

    private static string Tiles(string tag, bool toned) =>
        string.Concat(Tones.Select((tone, i) =>
            $"""<{tag} class="stat {(toned ? tone : "")}" id="t{i}"{(tag == "a" ? " href=\"#\"" : "")}><span class="stat-label">Count {i}</span><span class="stat-value">{i * 7}</span><span class="stat-target">of {i * 10 + 40}</span></{tag}>"""));

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
        public double Rule { get; set; }
        public double RuleTop { get; set; }
        public double RuleHeight { get; set; }
        public string Background { get; set; } = "";
        public string ValueColour { get; set; } = "";
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
                const before = getComputedStyle(t, '::before');
                const ml = parseFloat(s.marginLeft), mr = parseFloat(s.marginRight);
                const mt = parseFloat(s.marginTop), mb = parseFloat(s.marginBottom);
                // The padding box is what the rule is positioned against.
                const padLeft = b.left + parseFloat(s.borderLeftWidth);
                const padTop = b.top + parseFloat(s.borderTopWidth);
                return {
                  track: [b.left - ml, b.top - mt, b.width + ml + mr, b.height + mt + mb],
                  label: rect(t.querySelector('.stat-label')),
                  value: rect(t.querySelector('.stat-value')),
                  rule: padLeft + (parseFloat(before.left) || 0),
                  ruleTop: padTop + (parseFloat(before.top) || 0),
                  ruleHeight: parseFloat(before.height) || 0,
                  background: s.backgroundColor,
                  valueColour: getComputedStyle(t.querySelector('.stat-value')).color,
                };
              });
              return { rowHeight: row.getBoundingClientRect().height, tiles };
            }
            """);

    private async Task<Layout> MeasureRow(string layout, string tag, bool toned)
    {
        var (page, errors) = await OpenStyled(Row(layout, Tiles(tag, toned)));
        var measured = await Measure(page);
        Assert.Empty(errors);
        return measured;
    }

    [Theory]
    [InlineData("plain", "div")]
    [InlineData("divided", "div")]
    [InlineData("card", "div")]
    [InlineData("plain", "a")]
    [InlineData("divided", "a")]
    [InlineData("card", "a")]
    public async Task A_toned_tile_lines_up_with_the_row_it_would_have_been_without_a_tone(string layout, string tag)
    {
        if (NoBrowser) return;

        // The reference is always a row of plain tiles: a linked tile's own box is not what a
        // toned one has to match, the neighbours a reader compares it with are.
        var plain = await MeasureRow(layout, "div", toned: false);
        var toned = await MeasureRow(layout, tag, toned: true);

        if (tag == "div") Assert.Equal(plain.RowHeight, toned.RowHeight, 0.01);
        for (var i = 0; i < Tones.Length; i++)
        {
            if (tag != "div" && Tones[i] == "") continue;
            for (var k = 0; k < 4; k++)
            {
                Assert.Equal(plain.Tiles[i].Track[k], toned.Tiles[i].Track[k], 0.01);
                Assert.Equal(plain.Tiles[i].Label[k], toned.Tiles[i].Label[k], 0.01);
                Assert.Equal(plain.Tiles[i].Value[k], toned.Tiles[i].Value[k], 0.01);
            }
            if (layout != "plain")
            {
                Assert.Equal(plain.Tiles[i].Rule, toned.Tiles[i].Rule, 0.01);
                Assert.Equal(plain.Tiles[i].RuleTop, toned.Tiles[i].RuleTop, 0.01);
                Assert.Equal(plain.Tiles[i].RuleHeight, toned.Tiles[i].RuleHeight, 0.01);
            }
        }
    }

    [Fact]
    public async Task A_toned_tile_is_tinted_and_colours_its_value()
    {
        if (NoBrowser) return;

        var row = await MeasureRow("plain", "div", toned: true);

        var (neutral, warn, danger) = (row.Tiles[0], row.Tiles[1], row.Tiles[2]);
        Assert.Equal("rgba(0, 0, 0, 0)", neutral.Background);
        Assert.NotEqual(neutral.Background, warn.Background);
        Assert.NotEqual(neutral.Background, danger.Background);
        Assert.NotEqual(warn.Background, danger.Background);
        Assert.NotEqual(neutral.ValueColour, warn.ValueColour);
        Assert.NotEqual(neutral.ValueColour, danger.ValueColour);
        Assert.NotEqual(warn.ValueColour, danger.ValueColour);
    }

    // The plain row has the narrowest gap; two tints side by side must leave some of it.
    [Fact]
    public async Task Two_toned_tiles_side_by_side_do_not_touch()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Row("plain", Tiles("div", toned: true)));

        var gap = await page.EvaluateAsync<double>("""
            () => document.getElementById('t2').getBoundingClientRect().left
                - document.getElementById('t1').getBoundingClientRect().right
            """);

        Assert.True(gap > 0, $"The warn and danger tints are {gap}px apart.");
    }
}
