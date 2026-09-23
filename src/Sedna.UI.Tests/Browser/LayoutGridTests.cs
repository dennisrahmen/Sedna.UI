using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.sedna-grid-24</c>, <c>.sedna-split</c> and <c>.card--fill</c>, measured.
/// </summary>
/// <remarks>
/// <para>
/// The 24-column grid computes every child's width from the grid's own with <c>calc()</c>,
/// so the failures are all geometric and none of them is visible in the source: spans
/// that should share a row wrapping by a sub-pixel, a gap utility that moves <c>gap</c>
/// without the width formula, a collapse step that reads the viewport instead of the
/// grid. Each fixture fixes the grid's width and measures the children.
/// </para>
/// <para>
/// The grid must not be a size container. <c>container-type: inline-size</c> makes it the
/// containing block for fixed descendants, and a <c>.menu</c> opened in a panel would be
/// clipped by that panel's <c>.card</c>.
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
    public async Task The_grid_is_not_a_size_container()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Grid24(960, "", Boxes("sedna-span-12")));

        var containerType = await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.getElementById('grid')).containerType");
        Assert.Equal("normal", containerType);
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
