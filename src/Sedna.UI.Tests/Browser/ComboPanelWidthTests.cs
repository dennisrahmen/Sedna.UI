using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A combo's open panel can be wider than its field, opens towards the side that has room, and
/// stays inside the viewport; its rows can wrap instead of ending in an ellipsis.
/// </summary>
/// <remarks>
/// Measured in a layout engine because every one of these is anchor positioning and sizing —
/// <c>anchor-size()</c>, <c>position-try-fallbacks</c> — which nothing short of a browser resolves.
/// </remarks>
public class ComboPanelWidthTests : ScriptTestBase
{
    private static readonly string[] Rows =
    [
        "Welcome letter",
        "Contract renewal reminder for annual plans with a custom billing cycle",
        "Contract renewal reminder for annual plans with a standard billing cycle",
    ];

    /// <summary>A narrow multiselect placed at the start, centre or end of its row.</summary>
    private static string Field(string place, string classes = "", string style = "", string width = "220px") =>
        $"""
        <div style="display:flex; justify-content:{place}; padding:0 16px">
          <div class="form-combo {classes}" data-combo="multi" style="width:{width}; {style}">
            <div class="form-combo-box">
              <input class="form-combo-input" id="q" type="text" role="combobox" placeholder="Pick…"
                     aria-expanded="false" aria-controls="list" autocomplete="off">
            </div>
            <div class="form-combo-panel">
              <div class="form-combo-list" id="list" role="listbox" aria-multiselectable="true">
                {string.Concat(Rows.Select((r, i) =>
                    $"<div class=\"form-combo-option\" role=\"option\" id=\"o{i}\" aria-selected=\"false\"><span class=\"form-combo-option-text\">{r}</span></div>"))}
              </div>
            </div>
          </div>
        </div>
        """;

    private const string Measure = """
        () => {
            const rect = el => document.querySelector(el).getBoundingClientRect();
            const panel = rect('.form-combo-panel'), box = rect('.form-combo-box');
            const texts = [...document.querySelectorAll('.form-combo-option-text')];
            return {
                panelLeft: panel.left, panelRight: panel.right, panelWidth: panel.width,
                boxLeft: box.left, boxRight: box.right, boxWidth: box.width,
                viewport: document.documentElement.clientWidth,
                cut: texts.some(t => t.scrollWidth > t.clientWidth + 1),
                rowHeights: [...document.querySelectorAll('.form-combo-option')].map(o => o.getBoundingClientRect().height)
            };
        }
        """;

    private static async Task<JsonElement> OpenAndMeasure(Microsoft.Playwright.IPage page)
    {
        await page.Locator("#q").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        return await page.EvaluateAsync<JsonElement>(Measure);
    }

    private static double D(JsonElement e, string name) => e.GetProperty(name).GetDouble();

    [Fact]
    public async Task Unset_the_panel_is_as_wide_as_its_field()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Field("flex-start", width: "320px"));

        var m = await OpenAndMeasure(page);

        Assert.Equal(D(m, "boxWidth"), D(m, "panelWidth"), 1);
        Assert.Equal(D(m, "boxLeft"), D(m, "panelLeft"), 1);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_width_knob_opens_a_wider_panel_from_the_fields_start_edge()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Field("flex-start", style: "--form-combo-panel-width: 28rem"));

        var m = await OpenAndMeasure(page);

        Assert.Equal(28 * 16, D(m, "panelWidth"), 1);
        Assert.Equal(D(m, "boxLeft"), D(m, "panelLeft"), 1);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_wide_panel_by_the_end_of_the_page_opens_back_towards_the_start()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Field("flex-end", style: "--form-combo-panel-width: 28rem"));

        var m = await OpenAndMeasure(page);

        Assert.Equal(28 * 16, D(m, "panelWidth"), 1);
        // Flush with the box's end edge, not running off the right of the page.
        Assert.Equal(D(m, "boxRight"), D(m, "panelRight"), 1);
        Assert.True(D(m, "panelLeft") >= 0, $"The panel starts at {D(m, "panelLeft")}px.");
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("flex-start")]
    [InlineData("center")]
    [InlineData("flex-end")]
    public async Task A_panel_wider_than_the_page_stays_inside_the_viewport(string place)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Field(place, style: "--form-combo-panel-width: 60rem", width: "200px"));
        // The narrowest window where the panel is still anchored: below 768px it is a sheet
        // the full width of the screen (AnchoredPanelTests).
        await page.SetViewportSizeAsync(800, 700);

        var m = await OpenAndMeasure(page);

        Assert.True(D(m, "panelWidth") <= D(m, "viewport") - 32 + 1,
            $"The panel is {D(m, "panelWidth")}px wide in a {D(m, "viewport")}px viewport.");
        Assert.True(D(m, "panelLeft") >= -1 && D(m, "panelRight") <= D(m, "viewport") + 1,
            $"The panel runs from {D(m, "panelLeft")}px to {D(m, "panelRight")}px in a {D(m, "viewport")}px viewport.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Fit_sizes_the_panel_to_its_longest_row_so_no_row_is_cut()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Field("flex-start"));

        var narrow = await OpenAndMeasure(page);
        Assert.True(narrow.GetProperty("cut").GetBoolean(), "The fixture's rows fit a 220px panel anyway.");

        var (fitPage, fitErrors) = await OpenStyled(Field("flex-start", classes: "form-combo--fit"));
        var fit = await OpenAndMeasure(fitPage);

        Assert.False(fit.GetProperty("cut").GetBoolean(), "A row is still cut in a panel that fits its rows.");
        Assert.True(D(fit, "panelWidth") > D(fit, "boxWidth"));
        Assert.True(D(fit, "panelRight") <= D(fit, "viewport"));
        Assert.Empty(errors);
        Assert.Empty(fitErrors);
    }

    [Fact]
    public async Task Fit_is_never_narrower_than_the_field()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Field("flex-start", classes: "form-combo--fit", width: "900px"));

        var m = await OpenAndMeasure(page);

        Assert.Equal(D(m, "boxWidth"), D(m, "panelWidth"), 1);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Wrap_lets_a_long_row_run_onto_a_second_line_on_a_phone()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            Field("flex-start", classes: "form-combo--fit form-combo--wrap", width: "calc(100vw - 32px)"));
        await page.SetViewportSizeAsync(375, 700);

        var m = await OpenAndMeasure(page);

        Assert.False(m.GetProperty("cut").GetBoolean(), "A row ends in an ellipsis instead of wrapping.");
        var heights = m.GetProperty("rowHeights").EnumerateArray().Select(h => h.GetDouble()).ToArray();
        Assert.True(heights[1] > heights[0] + 8, $"The long row is {heights[1]}px tall against {heights[0]}px.");
        Assert.True(D(m, "panelLeft") >= 0 && D(m, "panelRight") <= D(m, "viewport"),
            $"The panel runs from {D(m, "panelLeft")}px to {D(m, "panelRight")}px.");
        Assert.Empty(errors);
    }
}
