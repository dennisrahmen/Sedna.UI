using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.sedna-grid-24</c>, <c>.sedna-split</c> and <c>.card--fill</c>, measured.
/// </summary>
/// <remarks>
/// <para>
/// The failures here are all geometric and none of them is visible in the source: spans
/// that should share a row wrapping, a collapse step that reads the viewport instead of
/// the grid, a tall panel moving the row under it. Each fixture fixes the grid's width
/// and measures the children.
/// </para>
/// <para>
/// The grid is a size container, for its collapse steps. A size container is the
/// containing block for fixed descendants, which is how a <c>.menu</c> escapes its
/// panel, so a menu opened in a panel is opened and clicked here.
/// </para>
/// </remarks>
public class LayoutGridTests : ScriptTestBase
{
    private static string Grid24(int width, string gridClass, string children) =>
        $"""
        <div style="width:{width}px">
          <div class="sedna-grid-24 {gridClass}" id="grid">{children}</div>
        </div>
        """;

    private static string Boxes(params string[] spans) =>
        string.Concat(spans.Select((span, i) => $"<div class=\"{span}\" id=\"c{i}\">x</div>"));

    private sealed class Box
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Right { get; set; }
    }

    private static async Task<Box[]> Measure(Microsoft.Playwright.IPage page, int count) =>
        await page.EvaluateAsync<Box[]>(
            """
            n => Array.from({ length: n }, (_, i) => {
              const r = document.getElementById('c' + i).getBoundingClientRect();
              return { left: r.left, top: r.top, width: r.width, right: r.right };
            })
            """, count);

    private static async Task<Box> MeasureGrid(Microsoft.Playwright.IPage page) =>
        await page.EvaluateAsync<Box>(
            """
            () => {
              const r = document.getElementById('grid').getBoundingClientRect();
              return { left: r.left, top: r.top, width: r.width, right: r.right };
            }
            """);

    [Theory]
    [InlineData(960, "", new[] { "sedna-span-16", "sedna-span-8" })]
    [InlineData(960, "", new[] { "sedna-span-8", "sedna-span-8", "sedna-span-8" })]
    [InlineData(961, "", new[] { "sedna-span-6", "sedna-span-6", "sedna-span-6", "sedna-span-6" })]
    [InlineData(997, "sedna-gap-3", new[] { "sedna-span-4", "sedna-span-4", "sedna-span-4", "sedna-span-4", "sedna-span-4", "sedna-span-4" })]
    [InlineData(1003, "sedna-gap-0", new[] { "sedna-span-1", "sedna-span-23" })]
    [InlineData(999, "sedna-gap-1", new[] { "sedna-span-5", "sedna-span-7", "sedna-span-12" })]
    public async Task Spans_that_sum_to_24_share_one_row_and_fill_it(int width, string gridClass, string[] spans)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Grid24(width, gridClass, Boxes(spans)));

        var boxes = await Measure(page, spans.Length);
        var grid = await MeasureGrid(page);

        Assert.All(boxes, box => Assert.Equal(boxes[0].Top, box.Top, 0.5));
        Assert.Equal(grid.Left, boxes[0].Left, 0.5);
        Assert.Equal(grid.Right, boxes[^1].Right, 1.0);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_span_is_its_share_of_24_columns()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid24(960, "sedna-gap-0", Boxes("sedna-span-6", "sedna-span-18")));

        var boxes = await Measure(page, 2);
        Assert.Equal(240, boxes[0].Width, 0.5);
        Assert.Equal(720, boxes[1].Width, 0.5);
    }

    [Fact]
    public async Task A_child_with_no_span_takes_the_whole_row()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid24(960, "", Boxes("", "sedna-span-6")));

        var boxes = await Measure(page, 2);
        var grid = await MeasureGrid(page);
        Assert.Equal(grid.Width, boxes[0].Width, 0.5);
        Assert.True(boxes[1].Top > boxes[0].Top, "The span after a full-row child should start the next row.");
    }

    [Fact]
    public async Task Below_the_middle_step_tiles_pair_up_and_panels_take_the_row()
    {
        if (NoBrowser) return;
        // 640px is under --grid-md (48rem) and over --grid-sm (30rem).
        var (page, _) = await OpenStyled(Grid24(640, "sedna-gap-0",
            Boxes("sedna-span-6", "sedna-span-6", "sedna-span-6", "sedna-span-6", "sedna-span-16", "sedna-span-8")));

        var boxes = await Measure(page, 6);

        Assert.All(boxes[..4], box => Assert.Equal(320, box.Width, 0.5));
        Assert.Equal(boxes[0].Top, boxes[1].Top, 0.5);
        Assert.True(boxes[2].Top > boxes[0].Top, "Four tiles at half width should make two rows of two.");
        Assert.Equal(640, boxes[4].Width, 0.5);
        Assert.Equal(640, boxes[5].Width, 0.5);
    }

    [Fact]
    public async Task Below_the_small_step_every_child_takes_the_row()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid24(400, "", Boxes("sedna-span-3", "sedna-span-6", "sedna-span-12")));

        var boxes = await Measure(page, 3);
        Assert.All(boxes, box => Assert.Equal(400, box.Width, 0.5));
    }

    [Fact]
    public async Task The_steps_read_the_grid_and_not_the_viewport()
    {
        if (NoBrowser) return;
        // A wide viewport, a narrow grid: the grid must collapse by its own width.
        var (page, _) = await OpenStyled(Grid24(400, "", Boxes("sedna-span-6", "sedna-span-6")));
        await page.SetViewportSizeAsync(1600, 900);

        var boxes = await Measure(page, 2);
        Assert.Equal(400, boxes[0].Width, 0.5);
    }

    [Fact]
    public async Task A_menu_opened_in_a_panel_is_not_clipped_by_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <div style="width:960px">
              <div class="sedna-grid-24">
                <div class="card sedna-span-12" style="height:80px"><div class="card-body">
                  <div class="menu-anchor">
                    <button class="btn btn-sm" id="trigger" type="button" data-menu-toggle aria-expanded="false">More</button>
                    <div class="menu" hidden>
                      <button class="menu-item" type="button">One</button>
                      <button class="menu-item" type="button">Two</button>
                      <button class="menu-item" type="button">Three</button>
                      <button class="menu-item" type="button" id="last">Four</button>
                    </div>
                  </div>
                </div></div>
                <div class="card sedna-span-12" style="height:80px"></div>
              </div>
            </div>
            <div style="height:600px"></div>
            """);
        await page.ClickAsync("#trigger");

        var outside = await page.EvaluateAsync<bool>(
            """
            () => {
              const last = document.getElementById('last').getBoundingClientRect();
              const card = document.getElementById('trigger').closest('.card').getBoundingClientRect();
              return last.top > card.bottom;
            }
            """);
        Assert.True(outside, "The fixture should put the last item below the panel's edge.");

        // Playwright refuses to click an element that is covered or clipped away, so
        // this asks the reader's question directly: can the last item be reached.
        await page.ClickAsync("#last", new() { Timeout = 5_000 });
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_panel_two_rows_tall_leaves_the_rows_beside_it_to_the_short_ones()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(
            """
            <div style="width:960px">
              <div class="sedna-grid-24" id="grid">
                <div class="sedna-span-8 sedna-row-span-2" id="c0" style="height:300px">tall</div>
                <div class="sedna-span-8" id="c1" style="height:60px">a</div>
                <div class="sedna-span-8" id="c2" style="height:60px">b</div>
                <div class="sedna-span-8" id="c3" style="height:60px">c</div>
                <div class="sedna-span-8" id="c4" style="height:60px">d</div>
              </div>
            </div>
            """);

        var b = await Measure(page, 5);
        Assert.Equal(b[0].Top, b[1].Top, 0.5);
        Assert.Equal(b[0].Top, b[2].Top, 0.5);
        Assert.True(b[3].Top < b[0].Top + 300,
            $"The second row of short panels starts {b[3].Top - b[0].Top}px down, below the tall panel instead of beside it.");
        Assert.True(b[3].Left > b[0].Right, "The second row of short panels should sit beside the tall one.");
    }

    [Fact]
    public async Task A_stack_is_as_long_as_its_own_content_and_moves_nothing_beside_it()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(
            """
            <div style="width:960px">
              <div class="sedna-grid-24">
                <div class="sedna-span-16 sedna-stack">
                  <div id="c0" style="height:400px">long</div>
                  <div id="c1" style="height:60px">under it</div>
                </div>
                <div class="sedna-span-8 sedna-stack">
                  <div id="c2" style="height:60px">a</div>
                  <div id="c3" style="height:60px">b</div>
                </div>
              </div>
            </div>
            """);

        var b = await Measure(page, 4);
        // A 60px panel and the grid's own 16px gap: nothing in the other column matters.
        Assert.Equal(b[2].Top + 60 + 16, b[3].Top, 0.5);
        Assert.Equal(b[0].Top + 400 + 16, b[1].Top, 0.5);
    }

    [Fact]
    public async Task In_fixed_rows_a_panel_is_its_rows_tall_and_its_body_scrolls()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <div style="width:960px">
              <div class="sedna-grid-24 sedna-grid-24--rows" style="--grid-row:200px">
                <div class="card card--fill sedna-span-12" id="short">
                  <div class="card-head"><strong>Short</strong></div>
                  <div class="card-body">One line.</div>
                </div>
                <div class="card card--fill sedna-span-12" id="long">
                  <div class="card-head"><strong>Long</strong></div>
                  <div class="card-body" id="body"><p>1</p><p>2</p><p>3</p><p>4</p><p>5</p><p>6</p><p>7</p><p>8</p><p>9</p><p>10</p><p>11</p><p>12</p></div>
                  <div class="card-foot" id="foot"><button class="btn btn-sm" type="button">Open</button></div>
                </div>
                <div class="card card--fill sedna-row-span-2" id="double">
                  <div class="card-body">Two rows.</div>
                </div>
              </div>
            </div>
            """);

        Assert.Equal(200, await Height(page, "short"), 0.5);
        Assert.Equal(200, await Height(page, "long"), 0.5);
        Assert.Equal(200 * 2 + 16, await Height(page, "double"), 0.5);

        var scrolls = await page.EvaluateAsync<bool>(
            "() => { const b = document.getElementById('body'); return b.scrollHeight > b.clientHeight; }");
        Assert.True(scrolls, "The long card's body should scroll inside its fixed row.");

        var footInside = await page.EvaluateAsync<bool>(
            """
            () => document.getElementById('foot').getBoundingClientRect().bottom
                  <= document.getElementById('long').getBoundingClientRect().bottom + 0.5
            """);
        Assert.True(footInside, "The foot should stay inside the card, under the scrolling body.");
        Assert.Empty(errors);
    }

    private static Task<double> Height(Microsoft.Playwright.IPage page, string id) =>
        page.EvaluateAsync<double>("id => document.getElementById(id).getBoundingClientRect().height", id);

    [Theory]
    [InlineData("", false)]
    [InlineData("sedna-grid-24--dense", true)]
    public async Task Dense_fills_the_hole_a_wide_panel_leaves(string modifier, bool filled)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid24(960, modifier,
            Boxes("sedna-span-16", "sedna-span-16", "sedna-span-8")));

        var b = await Measure(page, 3);
        Assert.Equal(filled, Math.Abs(b[2].Top - b[0].Top) < 0.5);
    }

    private const string SplitFixture =
        """
        <div style="width:{0}px">
          <div class="sedna-split" id="split">
            <div id="main">Main column</div>
            <aside class="sedna-split-aside" id="aside">Aside</aside>
          </div>
        </div>
        """;

    [Fact]
    public async Task A_split_puts_the_aside_beside_the_main_column_while_there_is_room()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(string.Format(SplitFixture, 960));

        var (main, aside) = await MainAndAside(page);
        Assert.Equal(main.Top, aside.Top, 0.5);
        Assert.True(aside.Left > main.Right, "The aside should sit after the main column.");
        Assert.True(aside.Width < main.Width, $"The aside measured {aside.Width}px against a {main.Width}px main column.");
    }

    [Fact]
    public async Task A_split_stacks_when_the_main_column_would_get_too_narrow()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(string.Format(SplitFixture, 420));

        var (main, aside) = await MainAndAside(page);
        Assert.True(aside.Top > main.Top, "The aside should drop below the main column.");
        Assert.Equal(420, main.Width, 0.5);
        Assert.Equal(420, aside.Width, 0.5);
    }

    private static async Task<(Box Main, Box Aside)> MainAndAside(Microsoft.Playwright.IPage page)
    {
        var boxes = await page.EvaluateAsync<Box[]>(
            """
            () => ['main', 'aside'].map(id => {
              const r = document.getElementById(id).getBoundingClientRect();
              return { left: r.left, top: r.top, width: r.width, right: r.right };
            })
            """);
        return (boxes[0], boxes[1]);
    }

    [Fact]
    public async Task Filled_cards_in_one_row_end_their_feet_on_one_line()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <div style="width:960px">
              <div class="sedna-grid-24">
                <div class="card card--fill sedna-span-12">
                  <div class="card-body"><p>One line.</p></div>
                  <div class="card-foot" id="foot-short"><button class="btn">Open</button></div>
                </div>
                <div class="card card--fill sedna-span-12">
                  <div class="card-body"><p>One.</p><p>Two.</p><p>Three.</p><p>Four.</p></div>
                  <div class="card-warning">Counts may lag.</div>
                  <div class="card-foot" id="foot-long"><button class="btn">Open</button></div>
                </div>
              </div>
            </div>
            """);

        var bottoms = await page.EvaluateAsync<double[]>(
            "() => ['foot-short', 'foot-long'].map(id => document.getElementById(id).getBoundingClientRect().bottom)");
        Assert.Equal(bottoms[0], bottoms[1], 0.5);

        var gap = await page.EvaluateAsync<double>(
            """
            () => {
              const warning = document.querySelector('.card-warning').getBoundingClientRect();
              return document.getElementById('foot-long').getBoundingClientRect().top - warning.bottom;
            }
            """);
        Assert.Equal(0, gap, 0.5);
        Assert.Empty(errors);
    }
}
