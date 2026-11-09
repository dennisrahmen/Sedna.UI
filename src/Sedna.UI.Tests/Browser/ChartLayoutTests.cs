using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The chart's frame measured: its sizes, the table that is its text alternative, the wait, the
/// empty and failed states, the donut's figure, the legend's keys, and labels that keep apart at
/// a phone's width — in every engine.
/// </summary>
public class ChartLayoutTests : ScriptTestBase
{
    private const string Rows = """
        <thead><tr><th>Day</th><th>Builds</th><th>Failed</th></tr></thead>
        <tbody>
          <tr><th>Monday</th><td>42</td><td>3</td></tr><tr><th>Tuesday</th><td>57</td><td>5</td></tr>
          <tr><th>Wednesday</th><td>49</td><td>2</td></tr><tr><th>Thursday</th><td>63</td><td>7</td></tr>
          <tr><th>Friday</th><td>38</td><td>1</td></tr><tr><th>Saturday</th><td>12</td><td>0</td></tr>
          <tr><th>Sunday</th><td>9</td><td>0</td></tr>
        </tbody>
        """;

    private static string Chart(string id, string type = "column", string cls = "", string attrs = "", string inside = "", string rows = Rows) => $"""
        <div class="chart {cls}" id="{id}" data-chart="{type}" data-chart-eager aria-label="Builds" {attrs}>
          <table class="chart-data" data-chart-data>{rows}</table>
          <div class="chart-plot"></div>
          <div class="chart-wait skeleton skeleton-block"></div>
          {inside}
        </div>
        """;

    private static Task Drawn(Microsoft.Playwright.IPage page, string id) =>
        page.WaitForFunctionAsync($"() => document.getElementById('{id}').hasAttribute('data-chart-state')");

    private static Task<double> Height(Microsoft.Playwright.IPage page, string id) =>
        page.EvaluateAsync<double>($"() => document.getElementById('{id}').getBoundingClientRect().height");

    [Fact]
    public async Task The_sizes_set_the_height_and_the_drawing_fills_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div style="width:480px">
              {Chart("md")}
              {Chart("sm", cls: "chart--sm")}
              {Chart("lg", cls: "chart--lg")}
              <div style="height:300px; display:flex; flex-direction:column">{Chart("fill", cls: "chart--fill")}</div>
            </div>
            """);
        foreach (var id in new[] { "md", "sm", "lg", "fill" }) await Drawn(page, id);

        Assert.Equal(256, await Height(page, "md"), 0.5);
        Assert.Equal(160, await Height(page, "sm"), 0.5);
        Assert.Equal(384, await Height(page, "lg"), 0.5);
        Assert.Equal(300, await Height(page, "fill"), 0.5);

        var svg = await page.EvaluateAsync<double[]>("""
            () => { const s = document.querySelector('#md .chart-svg').getBoundingClientRect(); return [s.width, s.height]; }
            """);
        Assert.Equal(480, svg[0], 0.5);
        Assert.Equal(256, svg[1], 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_table_is_hidden_from_the_eye_and_not_from_a_screen_reader()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""<div style="width:480px">{Chart("c")}</div>""");
        await Drawn(page, "c");

        // Still a table, still visible to the accessibility tree, clipped to nothing — and
        // clipped by the chart, so its rows never grow the page's scroll.
        var table = await page.EvaluateAsync<string[]>("""
            () => { const t = document.querySelector('#c .chart-data'), s = getComputedStyle(t), c = document.getElementById('c');
                    return [s.display, s.visibility, s.position, s.clipPath, String(c.scrollHeight <= c.clientHeight && c.scrollWidth <= c.clientWidth),
                            String(t.getBoundingClientRect().width <= 2)]; }
            """);
        Assert.Equal(["table", "visible", "absolute", "inset(50%)", "true", "true"], table);
    }

    [Fact]
    public async Task The_wait_covers_the_plot_until_the_drawing_is_there_and_while_the_app_loads()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div style="width:480px">
              <div class="chart" id="held"><div class="chart-plot"></div><div class="chart-wait skeleton skeleton-block"></div></div>
              {Chart("c")}
            </div>
            """);
        await Drawn(page, "c");
        const string WaitShown = "id => getComputedStyle(document.querySelector('#' + id + ' .chart-wait')).display !== 'none'";

        // Before the script has written a state — an element no module has started.
        Assert.True(await page.EvaluateAsync<bool>(WaitShown, "held"));
        var cover = await page.EvaluateAsync<double[]>("""
            () => { const c = document.getElementById('held').getBoundingClientRect(), w = document.querySelector('#held .chart-wait').getBoundingClientRect();
                    return [w.width - c.width, w.height - c.height]; }
            """);
        Assert.Equal(0, cover[0], 0.5);
        Assert.Equal(0, cover[1], 0.5);

        Assert.False(await page.EvaluateAsync<bool>(WaitShown, "c"));
        await Js.Run(page, "() => document.getElementById('c').setAttribute('aria-busy', 'true')");
        Assert.True(await page.EvaluateAsync<bool>(WaitShown, "c"));
        Assert.Equal("hidden", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#c .chart-plot')).visibility"));
    }

    [Fact]
    public async Task The_empty_and_the_failed_state_show_the_app_s_message()
    {
        if (NoBrowser) return;
        const string Messages = """
            <div class="chart-empty empty-state"><span class="empty-state-title">No builds yet</span></div>
            <div class="chart-empty chart-empty--error empty-state empty-state--failed"><span class="empty-state-title">Could not load</span></div>
            """;
        var (page, _) = await OpenStyled($"""
            <div style="width:480px">
              {Chart("empty", inside: Messages, rows: "<thead><tr><th>Day</th><th>Builds</th></tr></thead><tbody></tbody>")}
              {Chart("failed", attrs: "data-chart-error", inside: Messages, rows: "<thead><tr><th>Day</th><th>Builds</th></tr></thead><tbody></tbody>")}
              {Chart("ready", inside: Messages)}
            </div>
            """);
        foreach (var id in new[] { "empty", "failed", "ready" }) await Drawn(page, id);
        const string Shown = """
            id => [...document.querySelectorAll('#' + id + ' .chart-empty')].map(e => getComputedStyle(e).display !== 'none')
            """;

        Assert.Equal("empty", await page.GetAttributeAsync("#empty", "data-chart-state"));
        Assert.Equal(new[] { true, false }, await page.EvaluateAsync<bool[]>(Shown, "empty"));
        Assert.Equal(new[] { false, true }, await page.EvaluateAsync<bool[]>(Shown, "failed"));
        Assert.Equal(new[] { false, false }, await page.EvaluateAsync<bool[]>(Shown, "ready"));

        // The message is centred over the plot.
        var centre = await page.EvaluateAsync<double[]>("""
            () => { const c = document.getElementById('empty').getBoundingClientRect(), t = document.querySelector('#empty .empty-state-title').getBoundingClientRect();
                    return [(t.left + t.right) / 2 - (c.left + c.right) / 2, (t.top + t.bottom) / 2 - (c.top + c.bottom) / 2]; }
            """);
        Assert.InRange(centre[0], -1, 1);
        Assert.InRange(centre[1], -2, 2);
    }

    [Fact]
    public async Task The_donut_s_figure_sits_in_its_hole()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div style="width:480px">
              {Chart("d", type: "donut", inside: """<div class="chart-total"><span class="chart-total-value">257</span><span class="chart-total-label">builds</span></div>""")}
            </div>
            """);
        await Drawn(page, "d");

        var offset = await page.EvaluateAsync<double[]>("""
            () => { const t = document.querySelector('#d .chart-total-value').getBoundingClientRect();
                    const ring = [...document.querySelectorAll('#d .chart-slice')].map(s => s.getBoundingClientRect());
                    const left = Math.min(...ring.map(r => r.left)), right = Math.max(...ring.map(r => r.right));
                    const top = Math.min(...ring.map(r => r.top)), bottom = Math.max(...ring.map(r => r.bottom));
                    return [(t.left + t.right) / 2 - (left + right) / 2, t.width, right - left]; }
            """);
        Assert.InRange(offset[0], -2, 2);
        Assert.True(offset[1] < offset[2] * 0.6, "The figure is wider than the hole.");
    }

    [Fact]
    public async Task At_a_phone_s_width_no_two_labels_collide_and_none_leaves_the_chart()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div style="width:300px">{Chart("c", type: "line")}{Chart("b", type: "bar")}</div>
            """);
        await Drawn(page, "c");
        await Drawn(page, "b");

        foreach (var id in new[] { "c", "b" })
        {
            var problems = await page.EvaluateAsync<string[]>("""
                id => {
                    const chart = document.getElementById(id).getBoundingClientRect();
                    const labels = [...document.querySelectorAll('#' + id + ' .chart-svg text')].map(t => ({ t: t.textContent, r: t.getBoundingClientRect() }));
                    const out = [];
                    for (const a of labels) {
                        if (a.r.left < chart.left - 0.5 || a.r.right > chart.right + 0.5) out.push(a.t + ' leaves the chart');
                        for (const b of labels) {
                            if (a === b) continue;
                            if (a.r.left < b.r.right - 0.5 && b.r.left < a.r.right - 0.5 && a.r.top < b.r.bottom - 0.5 && b.r.top < a.r.bottom - 0.5) out.push(a.t + ' / ' + b.t);
                        }
                    }
                    return out;
                }
                """, id);
            Assert.True(problems.Length == 0, $"#{id}: " + string.Join(", ", problems));
        }
    }

    [Fact]
    public async Task Each_legend_swatch_is_its_series_marker_and_a_key_switched_off_reads_as_off()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="chart-legend">
              <span class="chart-key"><span class="chart-swatch series-1"></span><span>A</span></span>
              <span class="chart-key"><span class="chart-swatch chart-swatch--square series-2"></span><span>B</span></span>
              <span class="chart-key"><span class="chart-swatch chart-swatch--diamond series-3"></span><span>C</span></span>
              <span class="chart-key"><span class="chart-swatch chart-swatch--triangle series-4"></span><span>D</span></span>
              <span class="chart-key"><span class="chart-swatch chart-swatch--triangle-down series-5"></span><span>E</span></span>
              <span class="chart-key"><span class="chart-swatch chart-swatch--cross series-6"></span><span>F</span></span>
              <button class="chart-key" id="off" type="button" aria-pressed="false"><span class="chart-swatch chart-swatch--line series-1"></span><span>G</span></button>
            </div>
            """);

        var looks = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('.chart-swatch:not(.chart-swatch--line)')].map(s => { const c = getComputedStyle(s); return c.borderRadius + '|' + c.clipPath + '|' + c.transform; })
            """);
        Assert.Equal(6, looks.Distinct().Count());

        var off = await page.EvaluateAsync<string[]>("""
            () => { const k = document.getElementById('off'); return [getComputedStyle(k.querySelector('.chart-swatch')).opacity, getComputedStyle(k).textDecorationLine]; }
            """);
        Assert.Equal("0.35", off[0]);
        Assert.Equal("line-through", off[1]);
    }

    [Fact]
    public async Task The_keyboard_s_ring_is_drawn_inside_the_frame_and_not_for_the_pointer()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""<div style="width:480px">{Chart("c")}</div>""");
        await Drawn(page, "c");
        const string Ring = "() => getComputedStyle(document.getElementById('c'), '::after').boxShadow";

        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Contains("inset", await page.EvaluateAsync<string>(Ring), StringComparison.Ordinal);

        await Js.Run(page, "() => document.getElementById('c').setAttribute('data-chart-pointer', '')");
        Assert.Equal("none", await page.EvaluateAsync<string>(Ring));
    }
}
