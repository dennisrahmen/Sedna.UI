using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>.list--tasks</c> row measured in a layout engine: the status at the row's end on the
/// name's first line, names that wrap instead of clamping, and the mirror in a right-to-left page.
/// </summary>
public class TaskListLayoutTests : ScriptTestBase
{
    /// <summary>
    /// A zero-height inline-block at the start of a line stands on that line's baseline.
    /// </summary>
    private static string Fixture(string dir = "ltr") =>
        $$"""
        <div dir="{{dir}}" style="width:360px">
          <ul class="list list--tasks" id="list">
            <li><a class="list-row" href="#" id="row1">
              <span class="list-main"><span class="list-title" id="t1"><span class="bl" id="t1b"></span>Contact details</span></span>
              <span class="list-status" id="s1"><span class="bl" id="s1b"></span><span class="badge badge--info">In progress</span></span>
            </a></li>
            <li><a class="list-row" href="#" id="row2">
              <span class="list-main"><span class="list-title" id="t2"><span class="bl" id="t2b"></span>Delivery addresses and the goods-in hours of every depot that will receive orders</span></span>
              <span class="list-status" id="s2"><span class="bl" id="s2b"></span><span class="badge badge--danger">There is a problem</span></span>
            </a></li>
            <li><a class="list-row" href="#" id="row3">
              <span class="list-main"><span class="list-title" id="t3"><span class="bl" id="t3b"></span>Tell us about your business</span><span class="list-sub">Its name and address.</span></span>
              <span class="list-status" id="s3"><span class="bl" id="s3b"></span>Completed</span>
            </a></li>
            <li><span class="list-row" id="row4">
              <span class="list-main"><span class="list-title" id="t4"><span class="bl" id="t4b"></span>Check and send</span></span>
              <span class="list-status list-status--muted" id="s4"><span class="bl" id="s4b"></span>Cannot start yet</span>
            </span></li>
          </ul>
          <p id="accent" style="color: var(--accent)">x</p>
          <p id="muted" style="color: var(--muted)">x</p>
          <p id="fg" style="color: var(--fg)">x</p>
        </div>
        <style>.bl { display: inline-block; width: 0; height: 0; }</style>
        """;

    private const string Measure =
        """
        () => {
          const el = id => document.getElementById(id);
          const r = id => el(id).getBoundingClientRect();
          const out = [];
          for (const n of [1, 2, 3, 4]) {
            const row = el('row' + n), cs = getComputedStyle(row);
            const inner = { left: r('row' + n).left + parseFloat(cs.paddingLeft), right: r('row' + n).right - parseFloat(cs.paddingRight) };
            const lines = Math.round(r('t' + n).height / parseFloat(getComputedStyle(el('t' + n)).lineHeight));
            out.push({
              titleBaseline: r('t' + n + 'b').top, statusBaseline: r('s' + n + 'b').top,
              statusLeft: r('s' + n).left, statusRight: r('s' + n).right, statusHeight: r('s' + n).height,
              titleLeft: r('t' + n).left, titleRight: r('t' + n).right,
              rowLeft: inner.left, rowRight: inner.right, lines,
              titleColour: getComputedStyle(el('t' + n)).color, statusColour: getComputedStyle(el('s' + n)).color,
            });
          }
          return out;
        }
        """;

    private sealed record Row(
        double TitleBaseline, double StatusBaseline, double StatusLeft, double StatusRight, double StatusHeight,
        double TitleLeft, double TitleRight, double RowLeft, double RowRight, int Lines,
        string TitleColour, string StatusColour);

    private static Task<string> Colour(Microsoft.Playwright.IPage page, string id) =>
        page.EvaluateAsync<string>($"() => getComputedStyle(document.getElementById('{id}')).color");

    [Fact]
    public async Task The_status_stands_at_the_end_on_the_first_line_of_the_name()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        var rows = await Read<Row[]>(page, Measure);

        foreach (var (row, i) in rows.Select((r, i) => (r, i + 1)))
        {
            Assert.True(Math.Abs(row.StatusRight - row.RowRight) <= 0.5,
                $"Row {i}: the status ends at {row.StatusRight}, not at the row's end {row.RowRight}.");
            Assert.True(Math.Abs(row.StatusBaseline - row.TitleBaseline) <= 0.5,
                $"Row {i}: the status stands on {row.StatusBaseline}, the name's first line on {row.TitleBaseline}.");
            Assert.True(row.TitleRight <= row.StatusLeft + 0.5, $"Row {i}: the name runs under its status.");
        }
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_long_name_wraps_and_its_status_stays_on_one_line()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        var rows = await Read<Row[]>(page, Measure);

        Assert.True(rows[1].Lines >= 2, $"The long name shows {rows[1].Lines} line — it was clamped, not wrapped.");
        Assert.Equal(1, rows[0].Lines);
        // A status never breaks: "There is a problem" keeps its one line beside a wrapping name.
        var oneLine = rows[0].StatusHeight;
        Assert.InRange(rows[1].StatusHeight, oneLine - 0.5, oneLine + 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_task_reads_as_a_link_only_where_it_is_one()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture());
        var rows = await Read<Row[]>(page, Measure);

        var accent = await Colour(page, "accent");
        var muted = await Colour(page, "muted");
        var fg = await Colour(page, "fg");
        Assert.All(rows.Take(3), r => Assert.Equal(accent, r.TitleColour));
        Assert.Equal(fg, rows[3].TitleColour);
        Assert.Equal(fg, rows[2].StatusColour);
        Assert.Equal(muted, rows[3].StatusColour);
    }

    [Fact]
    public async Task The_status_mirrors_in_a_right_to_left_page()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture("rtl"));
        var rows = await Read<Row[]>(page, Measure);

        foreach (var (row, i) in rows.Select((r, i) => (r, i + 1)))
        {
            Assert.True(Math.Abs(row.StatusLeft - row.RowLeft) <= 0.5,
                $"Row {i}: the status starts at {row.StatusLeft}, not at the row's left edge {row.RowLeft}.");
            Assert.True(Math.Abs(row.TitleRight - row.RowRight) <= 0.5, $"Row {i}: the name does not start at the right edge.");
            Assert.True(row.StatusRight <= row.TitleLeft + 0.5, $"Row {i}: the name runs under its status.");
            Assert.True(Math.Abs(row.StatusBaseline - row.TitleBaseline) <= 0.5, $"Row {i}: the status left the name's first line.");
        }
        Assert.Empty(errors);
    }

    private static async Task<T> Read<T>(Microsoft.Playwright.IPage page, string script) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(
            await page.EvaluateAsync<string>("() => JSON.stringify((" + script + ")())"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
}
