using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The chart from markup to drawing, asserted against <c>docs/charts.md</c>: it loads only when a
/// page shows one, draws every type from the app's table, follows a render that changes the table,
/// moves between points by keyboard and says each in the app's words, takes its colours from the
/// series tokens through the theme, follows its container's size, holds still for a reader who
/// asked for no motion, and lets go of an element that has gone.
/// </summary>
/// <remarks>
/// The front door in <c>Sedna.UI.js</c> imports <c>Sedna.UI.chart.js</c> by relative URL; both are
/// served from their shipped files, so a broken import fails here as it would in an app. What a
/// chart draws is read back from its own SVG.
/// </remarks>
public class ChartTests : ScriptTestBase
{
    private const string Table = """
        <table class="chart-data" data-chart-data>
          <thead><tr><th>Month</th><th>2026</th><th>2025</th></tr></thead>
          <tbody>
            <tr><th>Jan</th><td>1240</td><td>1100</td></tr>
            <tr><th>Feb</th><td>1310</td><td>1180</td></tr>
            <tr><th>Mar</th><td data-value="1480">1 480</td><td></td></tr>
            <tr><th>Apr</th><td>1390</td><td>1300</td></tr>
          </tbody>
        </table>
        """;

    private static string Chart(string type, string attrs = "", string inside = "", string table = Table, string width = "480px") => $"""
        <div style="width:{width}">
          <div class="chart" id="c" data-chart="{type}" data-chart-eager aria-label="Orders per month" {attrs}>
            {table}
            <div class="chart-plot"></div>
            {inside}
          </div>
        </div>
        """;

    private async Task<IPage> OpenChart(string body, bool reduced = true, ForcedColors? forced = null) =>
        await Open(body, head: StylesheetTag,
            reducedMotion: reduced ? ReducedMotion.Reduce : ReducedMotion.NoPreference, forcedColors: forced);

    private static Task Drawn(IPage page, string selector = "#c") =>
        page.WaitForFunctionAsync($"() => {{ const s = document.querySelector('{selector} .chart-svg'); return s && s.querySelector('.chart-marks, .chart-axis'); }}");

    private static Task<int> Count(IPage page, string selector) =>
        page.EvaluateAsync<int>($"() => document.querySelectorAll('{selector}').length");

    [Fact]
    public async Task A_page_without_a_chart_never_downloads_the_module()
    {
        if (NoBrowser) return;
        var page = await OpenChart("<main><p>No chart here.</p></main>");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.DoesNotContain(Requests, r => r.Url.Contains("Sedna.UI.chart.js", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("line", ".chart-line", 2)]
    [InlineData("line", ".chart-marker", 7)]
    [InlineData("area", ".chart-area", 2)]
    [InlineData("column", ".chart-bar", 7)]
    [InlineData("bar", ".chart-bar", 7)]
    [InlineData("pie", ".chart-slice", 4)]
    [InlineData("donut", ".chart-slice", 4)]
    public async Task Every_type_draws_a_mark_for_every_value(string type, string mark, int expected)
    {
        if (NoBrowser) return;
        // Seven values: two series of four, with March's 2025 cell empty — a gap, never a zero.
        // A line breaks at it, so the 2025 area is one path of two runs.
        var page = await OpenChart(Chart(type));
        await Drawn(page);

        Assert.Equal(expected, await Count(page, $"#c {mark}"));
        Assert.Equal("ready", await page.GetAttributeAsync("#c", "data-chart-state"));
    }

    [Fact]
    public async Task A_scatter_places_each_value_at_its_row_s_own_number()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("scatter", table: """
            <table class="chart-data" data-chart-data>
              <thead><tr><th>Size</th><th>Search</th></tr></thead>
              <tbody><tr><th>4</th><td>92</td></tr><tr><th>40</th><td>150</td></tr><tr><th>10</th><td>110</td></tr></tbody>
            </table>
            """));
        await Drawn(page);

        // In the order of their numbers across, not of their rows.
        var xs = await page.EvaluateAsync<double[]>("""
            () => [...document.querySelectorAll('#c .chart-marker')].map(m => m.getBBox().x)
            """);
        Assert.Equal(3, xs.Length);
        Assert.True(xs[0] < xs[2] && xs[2] < xs[1], $"Markers at {string.Join(", ", xs)}.");
    }

    [Fact]
    public async Task The_marks_wear_the_series_class_of_their_tone_and_a_shape_each()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("line", table: """
            <table class="chart-data" data-chart-data>
              <thead><tr><th>Day</th><th>A</th><th data-tone="go">B</th><th data-shape="cross">C</th></tr></thead>
              <tbody><tr><th>Mon</th><td>1</td><td>2</td><td>3</td></tr><tr><th>Tue</th><td>2</td><td>3</td><td>4</td></tr></tbody>
            </table>
            """));
        await Drawn(page);

        var lines = await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('#c .chart-line')].map(l => l.getAttribute('class'))");
        Assert.Contains("series-1", lines[0].Split(' '));
        Assert.Contains("series-go", lines[1].Split(' '));
        Assert.Contains("series-3", lines[2].Split(' '));

        // Each series has its own marker: the second channel beside the colour.
        var shapes = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('#c .chart-markers .chart-marker')].map(m => m.getAttribute('d').replace(/[-\d.]+/g, '#'))
            """);
        Assert.Equal(6, shapes.Length);
        Assert.Equal(3, shapes.Distinct().Count());
    }

    [Fact]
    public async Task A_render_that_changes_the_table_redraws_the_chart()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("column"));
        await Drawn(page);
        var before = await page.EvaluateAsync<double>("() => document.querySelector('#c .chart-bar').getBBox().height");

        // What a Blazor render does: the cell's text changes.
        await Js.Run(page, "() => { document.querySelector('#c tbody td').textContent = '200'; }");
        await page.WaitForFunctionAsync("h => document.querySelector('#c .chart-bar').getBBox().height < h - 10", before);

        // A row added is a category more.
        await Js.Run(page, "() => { const r = document.querySelector('#c tbody tr').cloneNode(true); r.cells[0].textContent = 'May'; document.querySelector('#c tbody').append(r); }");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#c .chart-bar').length === 9");

        // And no rows at all is the empty state the stylesheet shows.
        await Js.Run(page, "() => document.querySelector('#c tbody').replaceChildren()");
        await page.WaitForFunctionAsync("() => document.getElementById('c').dataset.chartState === 'empty'");
    }

    [Fact]
    public async Task The_type_and_the_json_are_attributes_a_render_changes()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("line", table: "", attrs: """
            data-chart-json='{"categories":["a","b","c"],"series":[{"name":"S","values":[1,2,3]}]}'
            """));
        await Drawn(page);
        Assert.Equal(3, await Count(page, "#c .chart-marker"));

        await Js.Run(page, "() => document.getElementById('c').dataset.chart = 'column'");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#c .chart-bar').length === 3");

        await Js.Run(page, """() => document.getElementById('c').dataset.chartJson = '{"categories":["a","b"],"series":[{"name":"S","values":[5,null]}]}'""");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#c .chart-bar').length === 1");
    }

    [Fact]
    public async Task The_handle_draws_data_it_is_given_and_goes_back_to_the_markup()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("column"));
        await Drawn(page);

        var drawn = await page.EvaluateAsync<int>("""
            async () => {
                const chart = await sednaUi.chart.get('c');
                chart.set({ categories: ['x', 'y'], series: [{ name: 'Only', values: [3, 4] }] });
                const n = document.querySelectorAll('#c .chart-bar').length;
                chart.reset();
                return n * 100 + document.querySelectorAll('#c .chart-bar').length;
            }
            """);
        Assert.Equal(207, drawn);
    }

    [Fact]
    public async Task The_keyboard_moves_between_points_and_says_each_in_the_app_s_words()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("line", inside: """
            <p data-chart-live aria-live="polite"
               data-chart-announce="{category}, {series}: {value} orders, {index} of {count}."
               data-chart-announce-select="{series}, {category} chosen."></p>
            """));
        await Drawn(page);
        Assert.Equal("application", await page.GetAttributeAsync("#c", "role"));
        Assert.Equal("0", await page.GetAttributeAsync("#c", "tabindex"));

        // Reaching it by keyboard puts the reader on the first point.
        await page.Keyboard.PressAsync(Keys.Tab);
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("Jan, 2026: 1240 orders, 1 of 4.");
        Assert.Equal(1, await Count(page, "#c .chart-ring"));

        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("Mar, 2026: 1 480 orders, 3 of 4.");

        // Up to the next series; March has no 2025 value, so the ring stays.
        await page.Keyboard.PressAsync("ArrowUp");
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("Mar, 2026: 1 480 orders, 3 of 4.");
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("ArrowUp");
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("Apr, 2025: 1300 orders, 4 of 4.");

        // Enter chooses it: the event with its point, and the choice said.
        await Js.Run(page, "() => document.addEventListener('sedna-chart-select', e => { window.__chosen = e.detail; })");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("2025, Apr chosen.");
        var chosen = await page.EvaluateAsync<string>("() => JSON.stringify(window.__chosen)");
        Assert.Equal("""{"chart":"c","series":"2025","category":"Apr","value":1300,"text":"1300","index":3,"seriesIndex":1}""", chosen);

        await page.Keyboard.PressAsync("Escape");
        Assert.Equal(0, await Count(page, "#c .chart-ring"));
    }

    [Fact]
    public async Task A_bar_chart_lies_down_and_so_do_its_keys()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("bar", inside: """<p data-chart-live data-chart-announce="{category} {series}"></p>"""));
        await Drawn(page);

        await page.Keyboard.PressAsync(Keys.Tab);
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("Feb 2026");
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("Feb 2025");
    }

    [Fact]
    public async Task A_pie_s_arrows_walk_its_slices()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("pie", inside: """<p data-chart-live data-chart-announce="{category}: {percent}"></p>"""));
        await Drawn(page);

        await page.Keyboard.PressAsync(Keys.Tab);
        await page.Keyboard.PressAsync("ArrowDown");
        // 1310 of 5420.
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("Feb: 24.2%");
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("Apr: 25.6%");
    }

    [Fact]
    public async Task Right_to_left_the_categories_run_from_the_right_and_the_arrows_follow_the_drawing()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("column", attrs: "dir=\"rtl\"", inside: """<p data-chart-live data-chart-announce="{category}"></p>"""));
        await Drawn(page);

        var xs = await page.EvaluateAsync<double[]>("""
            () => [...document.querySelectorAll('#c .chart-bar.series-1')].map(b => b.getBBox().x)
            """);
        Assert.True(xs[0] > xs[1] && xs[1] > xs[2], $"Bars at {string.Join(", ", xs)}: January is not on the right.");

        await page.Keyboard.PressAsync(Keys.Tab);
        await page.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(page.Locator("#c [data-chart-live]")).ToHaveTextAsync("Feb");
    }

    [Fact]
    public async Task The_app_s_tooltip_is_filled_with_a_row_per_series()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("column", inside: """
            <div class="chart-tip" data-chart-tip hidden>
              <span class="chart-tip-title" data-chart-field="category"></span>
              <template data-chart-tip-row>
                <span class="chart-tip-row"><span class="chart-tip-swatch"></span><span data-chart-field="series"></span><span class="chart-tip-value" data-chart-field="value"></span></span>
              </template>
            </div>
            """));
        await Drawn(page);

        await page.Keyboard.PressAsync(Keys.Tab);
        await page.Keyboard.PressAsync("End");
        await Assertions.Expect(page.Locator("#c .chart-tip")).ToBeVisibleAsync();
        Assert.Equal("Apr", await page.InnerTextAsync("#c .chart-tip-title"));
        var rows = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('#c .chart-tip-row')].map(r => r.dataset.chartTone + ':'
                + [...r.querySelectorAll('[data-chart-field]')].map(f => f.textContent).join(' ') + (r.hasAttribute('data-chart-current') ? '*' : ''))
            """);
        Assert.Equal(["1:2026 1390*", "2:2025 1300"], rows);

        // The tip sits inside the chart.
        var inside = await page.EvaluateAsync<bool>("""
            () => { const c = document.getElementById('c').getBoundingClientRect(), t = document.querySelector('#c .chart-tip').getBoundingClientRect();
                    return t.left >= c.left - 1 && t.right <= c.right + 1 && t.top >= c.top - 1 && t.bottom <= c.bottom + 1; }
            """);
        Assert.True(inside, "The tooltip is placed outside the chart.");
    }

    [Fact]
    public async Task The_pointer_shows_the_point_under_it_and_a_click_chooses_it()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("column", inside: """
            <div class="chart-tip" data-chart-tip hidden><span data-chart-field="category"></span> <span data-chart-field="series"></span></div>
            """));
        await Drawn(page);
        var box = (await page.Locator("#c .chart-bar").Nth(2).BoundingBoxAsync())!;

        await page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Assertions.Expect(page.Locator("#c .chart-tip")).ToHaveTextAsync("Feb 2026");
        Assert.Equal(1, await Count(page, "#c .chart-band"));

        await Js.Run(page, "() => document.addEventListener('sedna-chart-select', e => { window.__chosen = e.detail.category + '/' + e.detail.series; })");
        await page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await page.WaitForFunctionAsync("() => window.__chosen === 'Feb/2026'");
        // A click takes the focus without the keyboard's ring.
        Assert.NotNull(await page.GetAttributeAsync("#c", "data-chart-pointer"));
    }

    [Fact]
    public async Task A_legend_key_hides_and_shows_its_series()
    {
        if (NoBrowser) return;
        var page = await OpenChart($"""
            <div data-chart-frame>
              {Chart("column")}
              <button class="chart-key" id="k" type="button" data-chart-toggle="2025" aria-pressed="true">2025</button>
            </div>
            """);
        await Drawn(page);
        Assert.Equal(7, await Count(page, "#c .chart-bar"));

        await page.ClickAsync("#k");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#c .chart-bar').length === 4");
        Assert.Equal("false", await page.GetAttributeAsync("#k", "aria-pressed"));

        await page.ClickAsync("#k");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#c .chart-bar').length === 7");
        Assert.Equal("true", await page.GetAttributeAsync("#k", "aria-pressed"));
    }

    [Fact]
    public async Task The_theme_and_colour_vision_reach_the_marks_through_the_series_tokens()
    {
        if (NoBrowser) return;
        // Series 3, whose step differs between the variants.
        var page = await OpenChart(Chart("column", table: Table.Replace("<th>2025</th>", "<th data-tone=\"3\">2025</th>", StringComparison.Ordinal)));
        await Drawn(page);
        const string Fill = "() => getComputedStyle(document.querySelector('#c .chart-bar.series-3')).fill";
        const string Token = "() => { const p = document.createElement('i'); p.style.color = 'var(--viz-3)'; document.body.append(p); const c = getComputedStyle(p).color; p.remove(); return c; }";

        var dark = await page.EvaluateAsync<string>(Fill);
        Assert.Equal(await page.EvaluateAsync<string>(Token), dark);

        // The variant: the same mark, repainted by the browser with no redraw.
        await Js.Run(page, "() => { window.__svg = document.querySelector('#c .chart-svg'); document.documentElement.setAttribute('data-variant', 'light'); }");
        var light = await page.EvaluateAsync<string>(Fill);
        Assert.NotEqual(dark, light);
        Assert.Equal(await page.EvaluateAsync<string>(Token), light);
        Assert.True(await page.EvaluateAsync<bool>("() => document.querySelector('#c .chart-svg') === window.__svg"));

        // Colour vision.
        await Js.Run(page, "() => document.documentElement.setAttribute('data-cvd', '1')");
        Assert.Equal(await page.EvaluateAsync<string>(Token), await page.EvaluateAsync<string>(Fill));
    }

    [Fact]
    public async Task Under_forced_colours_the_series_keep_apart()
    {
        if (NoBrowser || !BrowserEngine.EmulatesForcedColors) return;
        var page = await OpenChart(Chart("column"), forced: ForcedColors.Active);
        await Drawn(page);

        var fills = await page.EvaluateAsync<string[]>("""
            () => ['.series-1', '.series-2'].map(s => getComputedStyle(document.querySelector('#c .chart-bar' + s)).fill)
            """);
        Assert.NotEqual(fills[0], fills[1]);
    }

    [Fact]
    public async Task The_chart_follows_its_container_s_size()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("column", width: "480px"));
        await Drawn(page);
        var before = await page.EvaluateAsync<double>("() => document.querySelector('#c .chart-svg').viewBox.baseVal.width");

        await Js.Run(page, "() => { document.getElementById('c').parentElement.style.width = '320px'; }");
        await page.WaitForFunctionAsync("() => document.querySelector('#c .chart-svg').viewBox.baseVal.width === 320");
        Assert.Equal(480, before, 0.5);

        // Every bar is still inside the plot.
        var outside = await page.EvaluateAsync<int>("""
            () => { const w = 320; return [...document.querySelectorAll('#c .chart-bar')].filter(b => { const r = b.getBBox(); return r.x < 0 || r.x + r.width > w; }).length; }
            """);
        Assert.Equal(0, outside);
    }

    [Fact]
    public async Task Under_reduced_motion_the_chart_is_drawn_whole_at_once()
    {
        if (NoBrowser) return;
        var still = await OpenChart(Chart("column"), reduced: true);
        await Drawn(still);
        Assert.Equal("none", await still.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#c .chart-marks')).animationName"));

        var moving = await OpenChart(Chart("column"), reduced: false);
        await Drawn(moving);
        Assert.Equal("sedna-chart-in", await moving.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#c .chart-marks')).animationName"));

        // Only the first drawing: a redraw does not fade in again.
        await Js.Run(moving, "async () => (await sednaUi.chart.get('c')).redraw()");
        Assert.Equal("none", await moving.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#c .chart-marks')).animationName"));
    }

    [Fact]
    public async Task A_chart_whose_element_has_gone_is_let_go()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("line"));
        await Drawn(page);

        await Js.Run(page, "() => { window.__chart = document.getElementById('c'); window.__chart.remove(); }");
        // The loader sweeps it: the svg the module added is taken back out.
        await page.WaitForFunctionAsync("() => window.__chart.querySelector('.chart-svg') === null");
    }

    [Fact]
    public async Task The_chart_writes_no_word_of_its_own()
    {
        if (NoBrowser) return;
        var page = await OpenChart(Chart("column"));
        await Drawn(page);

        // Every label in the drawing is a category from the table or a number on the axis.
        var words = await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('#c .chart-svg text')].map(t => t.textContent)");
        Assert.NotEmpty(words);
        Assert.All(words, w => Assert.Matches(@"^(Jan|Feb|Mar|Apr|[\d,.\-−\s]+)$", w));
        Assert.Equal("true", await page.GetAttributeAsync("#c .chart-svg", "aria-hidden"));
    }
}
