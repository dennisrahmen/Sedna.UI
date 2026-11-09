using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>table[role=treegrid]</c> is the keyboard grid over rows in a hierarchy: the tree cell
/// opens, closes and climbs with the arrows — mirrored right to left — every other key is the
/// grid's, the rows under a closed branch are hidden, and each change is the tree's own event.
/// </summary>
public class TreeGridTests : ScriptTestBase
{
    private static string Row(string value, string label, int level, string extra = "") => $"""
        <tr aria-level="{level}" data-value="{value}" {extra}>
          <td id="t-{value}"><span class="tree-toggle"></span>{label}</td>
          <td id="n-{value}" class="col-num">{label.Length}</td>
        </tr>
        """;

    /// <summary>Operations holds Network, which holds Core and Edge; Finance is a leaf.</summary>
    private static string Teams(string extra = "") => $"""
        <button id="before" type="button">Before</button>
        <table class="table" role="treegrid" id="g" aria-label="Cost by team" {extra}>
          <thead><tr><th>Team</th><th class="col-num">Cost</th></tr></thead>
          <tbody>
            {Row("ops", "Operations", 1, "aria-expanded=\"true\"")}
            {Row("network", "Network", 2, "aria-expanded=\"false\"")}
            {Row("core", "Core", 3)}
            {Row("edge", "Edge", 3)}
            {Row("support", "Support", 2)}
            {Row("finance", "Finance", 1)}
          </tbody>
        </table>
        <button id="after" type="button">After</button>
        """;

    private const string Logger = """
        () => {
            window.log = [];
            for (const name of ['sedna-tree-expand', 'sedna-tree-collapse'])
                document.addEventListener(name, e => window.log.push({ name, on: e.target.dataset.value, detail: e.detail }));
        }
        """;

    private static Task<string> Active(IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement.id");

    private static Task<string[]> Shown(IPage page) =>
        page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('#g tbody tr')].filter(r => !r.hidden).map(r => r.dataset.value)");

    private static Task<string?> Attr(IPage page, string value, string name) =>
        page.EvaluateAsync<string?>($"() => document.querySelector('tr[data-value=\"{value}\"]').getAttribute('{name}')");

    private static Task<JsonElement> Log(IPage page) => page.EvaluateAsync<JsonElement>("() => window.log");

    [Fact]
    public async Task Rows_under_a_closed_branch_are_hidden_and_the_tree_cell_is_indented_by_level()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Teams());

        // Network is closed, so Core and Edge are hidden although the markup did not say so.
        Assert.Equal(["ops", "network", "support", "finance"], await Shown(page));

        var pads = await page.EvaluateAsync<double[]>("""
            () => ['ops', 'network', 'support'].map(v => parseFloat(getComputedStyle(document.getElementById('t-' + v)).paddingInlineStart))
            """);
        Assert.True(pads[1] > pads[0], $"a second-level row is indented past the first: {string.Join(", ", pads)}");
        Assert.Equal(pads[1], pads[2]);
        // The number column is not indented.
        var num = await page.EvaluateAsync<double[]>("""
            () => ['ops', 'network'].map(v => parseFloat(getComputedStyle(document.getElementById('n-' + v)).paddingInlineStart))
            """);
        Assert.Equal(num[0], num[1]);

        // One tab stop, as in any grid.
        await page.Locator("#before").FocusAsync();
        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("t-ops", await Active(page));
        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("after", await Active(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Right_opens_left_closes_and_then_climbs_to_the_parent()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Teams());
        await Js.Run(page, Logger);
        await page.Locator("#t-network").FocusAsync();

        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("true", await Attr(page, "network", "aria-expanded"));
        Assert.Equal(["ops", "network", "core", "edge", "support", "finance"], await Shown(page));
        Assert.Equal("t-network", await Active(page));

        // On an open branch → is the grid's: it moves to the next cell.
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("n-network", await Active(page));
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("t-network", await Active(page));

        // ↓ is the grid's too, into the branch.
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("t-core", await Active(page));

        // ← on a leaf goes up to its parent; on the open parent it closes it.
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("t-network", await Active(page));
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("false", await Attr(page, "network", "aria-expanded"));
        Assert.Equal(["ops", "network", "support", "finance"], await Shown(page));
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("t-ops", await Active(page));

        // At the top, with no parent, ← is the grid's and stays put on the first column.
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("false", await Attr(page, "ops", "aria-expanded"));
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("t-ops", await Active(page));

        var names = (await Log(page)).EnumerateArray()
            .Select(e => $"{e.GetProperty("name").GetString()}:{e.GetProperty("on").GetString()}:{e.GetProperty("detail").GetProperty("item").GetString()}")
            .ToArray();
        Assert.Equal(
            ["sedna-tree-expand:network:network", "sedna-tree-collapse:network:network", "sedna-tree-collapse:ops:ops"],
            names);
        Assert.Equal(["ops", "finance"], await Shown(page));
    }

    [Fact]
    public async Task In_a_right_to_left_tree_grid_left_opens_and_right_climbs()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"<div dir=\"rtl\">{Teams()}</div>");
        await page.Locator("#t-network").FocusAsync();

        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("true", await Attr(page, "network", "aria-expanded"));
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("t-core", await Active(page));
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("t-network", await Active(page));
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("false", await Attr(page, "network", "aria-expanded"));

        // The indent is on the inline start, which is the right edge here.
        var pad = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.getElementById('t-network')).paddingRight)");
        var start = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.getElementById('t-ops')).paddingRight)");
        Assert.True(pad > start);
    }

    [Fact]
    public async Task Enter_and_a_click_on_the_toggle_open_and_close_and_closing_brings_the_focus_up()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Teams());
        await Js.Run(page, Logger);

        await page.Locator("#t-network").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("true", await Attr(page, "network", "aria-expanded"));

        // Focus in a row under the branch; a click on the branch's toggle closes it and the
        // focus, whose row is now hidden, comes up to the branch rather than to the page.
        await page.Locator("#n-edge").FocusAsync();
        await page.Locator("tr[data-value='network'] .tree-toggle").ClickAsync();
        Assert.Equal("false", await Attr(page, "network", "aria-expanded"));
        Assert.Equal("t-network", await Active(page));

        // A leaf has no branch to toggle: Enter does nothing to it.
        await page.Locator("#t-support").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        Assert.Null(await Attr(page, "support", "aria-expanded"));

        Assert.Equal(2, (await Log(page)).GetArrayLength());
    }

    [Fact]
    public async Task A_lazy_branch_is_busy_until_the_app_renders_its_rows_and_the_open_branches_are_readable()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <table class="table" role="treegrid" id="g" aria-label="Archive">
              <thead><tr><th>Year</th><th class="col-num">Orders</th></tr></thead>
              <tbody>
                {Row("2024", "2024", 1, "aria-expanded=\"false\" data-tree-lazy")}
                {Row("2023", "2023", 1, "aria-expanded=\"true\"")}
                {Row("2023-12", "December", 2)}
              </tbody>
            </table>
            """);
        await Js.Run(page, Logger);
        Assert.Equal(["2023"], await page.EvaluateAsync<string[]>("() => sednaUi.grid.expanded('g')"));

        await page.Locator("#t-2024").FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("true", await Attr(page, "2024", "aria-busy"));
        var log = await Log(page);
        Assert.True(log[0].GetProperty("detail").GetProperty("lazy").GetBoolean());
        Assert.Equal(["2024", "2023"], await page.EvaluateAsync<string[]>("() => sednaUi.grid.expanded('g')"));

        // The app answers with the rows and drops the attribute.
        await Js.Run(page, """
            () => {
                const row = document.querySelector('tr[data-value="2024"]');
                const child = document.createElement('tr');
                child.setAttribute('aria-level', '2');
                child.dataset.value = '2024-10';
                child.innerHTML = '<td><span class="tree-toggle"></span>October</td><td class="col-num">7</td>';
                row.after(child);
                row.removeAttribute('data-tree-lazy');
            }
            """);
        await page.WaitForFunctionAsync("() => !document.querySelector('tr[data-value=\"2024\"]').hasAttribute('aria-busy')");
        Assert.Equal(["2024", "2024-10", "2023", "2023-12"], await Shown(page));
        // The new row's cells joined the grid with no tab stop of their own.
        Assert.Equal("-1", await page.EvaluateAsync<string>("() => document.querySelector('tr[data-value=\"2024-10\"] td').getAttribute('tabindex')"));
    }

    [Fact]
    public async Task A_branch_the_app_closes_hides_its_rows_after_the_render()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Teams());

        await Js.Run(page, "() => document.querySelector('tr[data-value=\"ops\"]').setAttribute('aria-expanded', 'false')");
        await page.WaitForFunctionAsync("() => document.querySelector('tr[data-value=\"support\"]').hidden");
        Assert.Equal(["ops", "finance"], await Shown(page));
    }
}
