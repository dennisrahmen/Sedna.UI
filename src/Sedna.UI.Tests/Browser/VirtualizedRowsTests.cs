using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Blazor's <c>&lt;Virtualize SpacerElement="tr"&gt;</c> stands an empty row in for the rows it
/// does not render, before and after the window. The spacer is the one Blazor really emits —
/// rendered here by the framework itself — and none of the table's paint reaches it: the zebra
/// stripe counts past it, or follows <c>aria-rowindex</c>; a stacked table does not make it a
/// card; a flush popover's last row is the last row with cells; and a grid never lands on it.
/// </summary>
public class VirtualizedRowsTests : ScriptTestBase
{
    /// <summary>
    /// The tbody's rows as Virtualize renders them for <paramref name="count"/> items, each row
    /// with an id and, when <paramref name="rowIndex"/> is set, <c>aria-rowindex</c> from it.
    /// </summary>
    private static string Rows(int count, Func<int, int>? rowIndex = null)
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var items = Enumerable.Range(0, count).ToList();
        RenderFragment<int> row = i => b =>
        {
            b.OpenElement(0, "tr");
            if (rowIndex is not null) b.AddAttribute(1, "aria-rowindex", rowIndex(i).ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.AddAttribute(2, "id", $"row{i}");
            b.OpenElement(3, "td");
            b.AddAttribute(4, "id", $"c{i}");
            b.AddContent(5, $"Line {i}");
            b.CloseElement();
            b.OpenElement(6, "td");
            b.AddAttribute(7, "class", "col-num");
            b.AddContent(8, i * 3);
            b.CloseElement();
            b.CloseElement();
        };
        var cut = ctx.Render(b =>
        {
            b.OpenComponent<Virtualize<int>>(0);
            b.AddAttribute(1, "Items", (ICollection<int>)items);
            b.AddAttribute(2, "SpacerElement", "tr");
            b.AddAttribute(3, "ItemSize", 37f);
            b.AddAttribute(4, "ChildContent", row);
            b.CloseComponent();
        });
        // The renderer's own marker for an element reference is no HTML attribute. The
        // spacers' heights are set to something a test can see.
        var html = cut.Markup.Replace(" blazor:elementReference=\"\"", "", StringComparison.Ordinal);
        html = Regex.Replace(html, @"style=""height: [^;]*;", "style=\"height: 120px;");
        Assert.Matches(@"^<tr style=""height: 120px; flex-shrink: 0;[^""]*"" aria-hidden=""true""></tr><tr", html);
        return html;
    }

    private static string Background(string id) =>
        $"getComputedStyle(document.getElementById('{id}').cells[0]).backgroundColor";

    [Fact]
    public async Task The_zebra_stripe_counts_past_the_spacer_row()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <table class="table table--zebra" id="t">
              <thead><tr><th>Line</th><th class="col-num">Count</th></tr></thead>
              <tbody>{Rows(6)}</tbody>
            </table>
            <table class="table table--zebra" id="plain">
              <thead><tr><th>Line</th></tr></thead>
              <tbody><tr id="p0"><td>a</td></tr><tr id="p1"><td>b</td></tr></tbody>
            </table>
            """);

        var r = await page.EvaluateAsync<string[]>($"() => [{Background("row0")}, {Background("row1")}, {Background("row2")}, {Background("p0")}, {Background("p1")}]");
        // The same rows are striped as in a table with no spacer: the second, not the first.
        Assert.Equal(r[3], r[0]);
        Assert.Equal(r[4], r[1]);
        Assert.NotEqual(r[0], r[1]);
        Assert.Equal(r[0], r[2]);

        // The spacer keeps its height and paints nothing.
        var spacer = await page.EvaluateAsync<string[]>("""
            () => {
                const s = document.querySelector('#t tbody tr:first-child');
                const cs = getComputedStyle(s);
                return [String(s.cells.length), String(Math.round(s.getBoundingClientRect().height)), cs.backgroundColor, cs.borderBottomWidth];
            }
            """);
        Assert.Equal(["0", "120", "rgba(0, 0, 0, 0)", "0px"], spacer);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_row_with_aria_rowindex_is_striped_by_its_place_in_the_whole_table()
    {
        if (NoBrowser) return;
        // A window that starts at the table's ninth row: the header is row 1, so the first
        // rendered item is row 9, odd, and striped — whatever its place among its siblings.
        var (page, _) = await OpenStyled($"""
            <table class="table table--zebra" id="t" aria-rowcount="1001">
              <thead><tr aria-rowindex="1"><th>Line</th><th class="col-num">Count</th></tr></thead>
              <tbody>{Rows(4, i => i + 9)}</tbody>
            </table>
            <table class="table table--zebra" id="plain">
              <thead><tr><th>Line</th></tr></thead>
              <tbody><tr id="p0"><td>a</td></tr><tr id="p1"><td>b</td></tr></tbody>
            </table>
            """);

        var r = await page.EvaluateAsync<string[]>($"() => [{Background("row0")}, {Background("row1")}, {Background("row2")}, {Background("p0")}, {Background("p1")}]");
        Assert.Equal(r[4], r[0]);   // row 9: striped
        Assert.Equal(r[3], r[1]);   // row 10: not
        Assert.Equal(r[4], r[2]);   // row 11: striped

        // Selection still wins over the stripe.
        await Js.Run(page, "() => document.getElementById('row0').setAttribute('aria-selected', 'true')");
        Assert.NotEqual(r[0], await page.EvaluateAsync<string>($"() => {Background("row0")}"));
    }

    [Fact]
    public async Task A_stacked_table_does_not_make_the_spacer_a_card()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <table class="table table--stack" id="t">
              <thead><tr><th>Line</th><th class="col-num">Count</th></tr></thead>
              <tbody>{Rows(3)}</tbody>
            </table>
            """);
        await page.SetViewportSizeAsync(375, 700);

        var r = await page.EvaluateAsync<string[]>("""
            () => {
                const spacer = document.querySelector('#t tbody tr:first-child'), row = document.getElementById('row0');
                const s = getComputedStyle(spacer), cs = getComputedStyle(row);
                return [s.borderTopWidth, s.marginBottom, String(Math.round(spacer.getBoundingClientRect().height)), cs.borderTopWidth];
            }
            """);
        Assert.Equal("0px", r[0]);
        Assert.Equal("0px", r[1]);
        Assert.Equal("120", r[2]);
        Assert.Equal("1px", r[3]);
    }

    [Fact]
    public async Task A_flush_popover_table_ends_on_its_last_row_with_cells()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div class="popover popover--flush" id="p" popover>
              <table class="table" id="t"><tbody>{Rows(3)}</tbody></table>
            </div>
            """);
        await Js.Run(page, "() => document.getElementById('p').showPopover()");

        var r = await page.EvaluateAsync<string[]>("""
            () => ['row1', 'row2'].map(id => getComputedStyle(document.getElementById(id).cells[0]).borderBottomWidth)
            """);
        Assert.Equal(["1px", "0px"], r);
    }

    [Fact]
    public async Task A_grid_never_lands_on_a_spacer_row()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <table class="table" role="grid" id="g" aria-label="Lines">
              <thead><tr><th id="h0">Line</th><th class="col-num">Count</th></tr></thead>
              <tbody>{Rows(4)}</tbody>
            </table>
            """);
        await page.Locator("#c1").FocusAsync();

        await page.Keyboard.PressAsync(Keys.Primary("End"));
        Assert.Equal("row3", await page.EvaluateAsync<string>("() => document.activeElement.parentElement.id"));
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("row3", await page.EvaluateAsync<string>("() => document.activeElement.parentElement.id"));
        await page.Locator("#c0").FocusAsync();
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("h0", await page.EvaluateAsync<string>("() => document.activeElement.id"));
    }

    [Fact]
    public async Task A_load_more_row_is_never_striped_and_centres_its_button()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <table class="table table--zebra" id="t" style="width: 600px">
              <thead><tr><th>Line</th><th>Count</th></tr></thead>
              <tbody>
                <tr><td>a</td><td>1</td></tr>
                <tr id="striped"><td>b</td><td>2</td></tr>
                <tr><td>c</td><td>3</td></tr>
                <tr class="tr-more" id="more"><td colspan="999">
                  <button class="btn btn--sm" type="button" id="go"><i class="ri-arrow-down-line"></i><span>Show 50 more</span></button>
                  <span class="tr-more-count" id="count">3 of 1,284</span>
                </td></tr>
              </tbody>
            </table>
            """);

        var r = await page.EvaluateAsync<string[]>("""
            () => {
                const cell = document.querySelector('#more td'), c = cell.getBoundingClientRect();
                const go = document.getElementById('go').getBoundingClientRect(), n = document.getElementById('count').getBoundingClientRect();
                const middle = (go.left + n.right) / 2;
                return [getComputedStyle(cell).backgroundColor, getComputedStyle(document.querySelector('#striped td')).backgroundColor,
                        String(Math.abs(middle - (c.left + c.width / 2)) < 2), String(n.left > go.right)];
            }
            """);
        Assert.Equal("rgba(0, 0, 0, 0)", r[0]);
        Assert.NotEqual(r[0], r[1]);
        Assert.Equal("true", r[2]);
        Assert.Equal("true", r[3]);
    }
}
