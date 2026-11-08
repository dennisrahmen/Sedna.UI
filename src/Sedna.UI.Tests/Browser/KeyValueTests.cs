using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>.kv</c> row in a narrow box wraps its value and keeps its key's words whole;
/// <c>.kv--text</c> sets a prose value in the body font.
/// </summary>
public class KeyValueTests : ScriptTestBase
{
    [Fact]
    public async Task A_narrow_row_keeps_a_short_key_on_one_line()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:180px">
              <div class="kv"><span class="k" id="k">Checksum</span><span class="v">9f86d081884c7d659a2feaa0c55ad015</span></div>
            </div>
            """);

        // The key's text, not its box: a flex item stretches to the height of the value beside it.
        var lines = await page.EvaluateAsync<double>(
            "() => { const k = document.getElementById('k'); const r = document.createRange(); r.selectNodeContents(k); return r.getBoundingClientRect().height / parseFloat(getComputedStyle(k).lineHeight); }");
        Assert.True(lines < 1.5, $"The key broke onto {lines:0.0} lines.");
    }

    [Fact]
    public async Task A_text_value_is_set_in_the_body_font_and_breaks_between_words()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:200px">
              <div class="kv kv--text"><span class="k">Role</span><span class="v" id="v">Administrator of every region</span></div>
            </div>
            <p id="body">x</p>
            """);

        var fonts = await page.EvaluateAsync<string[]>(
            "() => [getComputedStyle(document.getElementById('v')).fontFamily, getComputedStyle(document.getElementById('body')).fontFamily, getComputedStyle(document.getElementById('v')).overflowWrap]");
        Assert.Equal(fonts[1], fonts[0]);
        Assert.Equal("break-word", fonts[2]);
    }

    /// <summary>
    /// A check-answers list: three rows, the middle one with nothing to change, and a
    /// zero-height inline-block at the start of each value and action whose top is the
    /// line's baseline.
    /// </summary>
    private static string Answers(string width, string dir = "ltr") =>
        $$"""
        <div dir="{{dir}}" style="width:{{width}}">
          <dl id="dl">
            <div class="kv kv--text" id="r1">
              <dt class="k" id="k1">Name</dt>
              <dd class="v" id="v1"><span class="bl" id="v1b"></span>Alex Fischer</dd>
              <dd class="kv-action" id="a1"><span class="bl" id="a1b"></span><a href="#">Change<span class="visually-hidden"> name</span></a></dd>
            </div>
            <div class="kv kv--text" id="r2">
              <dt class="k" id="k2">Customer number</dt>
              <dd class="v" id="v2">TRD-20418</dd>
            </div>
            <div class="kv kv--text" id="r3">
              <dt class="k" id="k3">Registered address</dt>
              <dd class="v" id="v3"><span class="bl" id="v3b"></span>12 Harbour Road<br>Unit 4<br>Example Town</dd>
              <dd class="kv-action" id="a3"><span class="bl" id="a3b"></span><a href="#">Change<span class="visually-hidden"> address</span></a></dd>
            </div>
          </dl>
        </div>
        <style>.bl { display: inline-block; width: 0; height: 0; }</style>
        """;

    private const string Measure =
        """
        () => {
          const r = id => document.getElementById(id).getBoundingClientRect();
          const cs = id => getComputedStyle(document.getElementById(id));
          return {
            row: [r('r1').left, r('r1').right, r('r3').left, r('r3').right],
            key: [r('k1').left, r('k1').right, r('k1').bottom, r('k3').left, r('k3').right, r('k3').width],
            value: [r('v1').left, r('v1').right, r('v2').left, r('v2').right, r('v3').left, r('v3').right, r('v1').top, r('v3').top],
            action: [r('a1').left, r('a1').right, r('a3').left, r('a3').right, r('a1').top, r('a3').top],
            baseline: [r('v1b').top, r('a1b').top, r('v3b').top, r('a3b').top],
            margin: [cs('v1').marginInlineStart, cs('a1').marginInlineEnd, cs('k1').marginInlineStart],
            divider: [cs('r1').borderTopWidth, cs('r2').borderTopWidth, cs('r3').borderTopWidth],
          };
        }
        """;

    private sealed record Geometry(double[] Row, double[] Key, double[] Value, double[] Action, double[] Baseline, string[] Margin, string[] Divider);

    [Fact]
    public async Task An_action_column_lines_every_value_up_and_stands_on_the_first_line()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Answers("640px"));
        var g = await Read<Geometry>(page, Measure);

        // A <dd> loses the platform's indent.
        Assert.All(g.Margin, m => Assert.Equal("0px", m));
        // Every value starts on one edge, the row with no action included.
        Assert.InRange(Math.Abs(g.Value[0] - g.Value[2]), 0, 0.5);
        Assert.InRange(Math.Abs(g.Value[0] - g.Value[4]), 0, 0.5);
        // The key takes --kv-key-width, 30% of the row.
        var rowWidth = g.Row[1] - g.Row[0];
        Assert.InRange(g.Key[1] - g.Key[0], rowWidth * 0.3 - 0.5, rowWidth * 0.3 + 0.5);
        // The actions stand at the row's inline end, one above the other.
        Assert.InRange(Math.Abs(g.Action[1] - g.Row[1]), 0, 0.5);
        Assert.InRange(Math.Abs(g.Action[3] - g.Row[3]), 0, 0.5);
        // The value without an action takes the action's column too: it runs to the end.
        Assert.InRange(Math.Abs(g.Value[3] - g.Row[1]), 0, 0.5);
        // "Change" sits on the first line of the value beside it, an address of three lines included.
        Assert.InRange(Math.Abs(g.Baseline[0] - g.Baseline[1]), 0, 0.5);
        Assert.InRange(Math.Abs(g.Baseline[2] - g.Baseline[3]), 0, 0.5);
        // Rows are divided; the first has nothing above it.
        Assert.Equal(["0px", "1px", "1px"], g.Divider);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_action_column_mirrors_in_a_right_to_left_page()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Answers("640px", "rtl"));
        var g = await Read<Geometry>(page, Measure);

        // The key at the right edge, the action at the left.
        Assert.InRange(Math.Abs(g.Key[1] - g.Row[1]), 0, 0.5);
        Assert.InRange(Math.Abs(g.Action[0] - g.Row[0]), 0, 0.5);
        Assert.InRange(Math.Abs(g.Action[2] - g.Row[2]), 0, 0.5);
        // The values end on one edge, beside the keys.
        Assert.InRange(Math.Abs(g.Value[1] - g.Value[3]), 0, 0.5);
        Assert.InRange(Math.Abs(g.Value[1] - g.Value[5]), 0, 0.5);
        Assert.True(g.Value[1] < g.Key[0], "A value runs into its key.");
        Assert.InRange(Math.Abs(g.Baseline[2] - g.Baseline[3]), 0, 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task On_a_narrow_screen_the_key_goes_above_its_answer_and_the_action_stays_beside_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Answers("300px"));
        var g = await Read<Geometry>(page, Measure);

        // The key has the line to itself: the value starts below it, at the row's start.
        Assert.True(g.Value[6] >= g.Key[2] - 0.5, $"The value starts at {g.Value[6]}, inside the key ending at {g.Key[2]}.");
        Assert.InRange(Math.Abs(g.Value[0] - g.Row[0]), 0, 0.5);
        Assert.InRange(Math.Abs(g.Key[5] - (g.Row[3] - g.Row[2])), 0, 0.5);
        // The action shares the value's line, at the end.
        Assert.InRange(Math.Abs(g.Action[1] - g.Row[1]), 0, 0.5);
        Assert.InRange(Math.Abs(g.Baseline[0] - g.Baseline[1]), 0, 0.5);
        Assert.InRange(Math.Abs(g.Baseline[2] - g.Baseline[3]), 0, 0.5);
        // Nothing runs past the row.
        Assert.True(g.Value[5] <= g.Action[2] + 0.5, "The value runs under its action.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_row_without_actions_beside_it_keeps_its_own_layout()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:320px">
              <div class="kv"><span class="k">Priority</span><span class="v" id="v">P3</span></div>
            </div>
            """);

        // The value still sits at the trailing edge; a check-answers row is an opt-in.
        var at = await page.EvaluateAsync<double[]>(
            "() => { const v = document.getElementById('v').getBoundingClientRect(); const p = document.getElementById('v').parentElement.getBoundingClientRect(); return [v.right, p.right, parseFloat(getComputedStyle(document.getElementById('v').parentElement).borderTopWidth)]; }");
        Assert.InRange(Math.Abs(at[0] - at[1]), 0, 0.5);
        Assert.Equal(0, at[2]);
    }

    private static async Task<T> Read<T>(Microsoft.Playwright.IPage page, string script) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(
            await page.EvaluateAsync<string>("() => JSON.stringify((" + script + ")())"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
}
