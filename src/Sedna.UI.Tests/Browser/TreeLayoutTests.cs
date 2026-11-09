using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The tree measured in a layout engine: closed branches hidden by their attribute alone, the
/// indent per level, labels in line whether or not an item has children, the caret turning the
/// right way in both directions, and every state painted from its ARIA attribute.
/// </summary>
public class TreeLayoutTests : ScriptTestBase
{
    private static string Row(string label, bool check = false) =>
        $"""<span class="tree-row"><span class="tree-toggle"></span>{(check ? "<span class=\"tree-check\"></span>" : "")}<span class="tree-label">{label}</span></span>""";

    private static string Fixture(string dir) =>
        $"""
        <div dir="{dir}" style="width:360px">
          <ul class="tree" role="tree" aria-label="Teams" id="tree">
            <li role="treeitem" id="open" aria-expanded="true">
              {Row("Fulfilment")}
              <ul role="group" id="open-group">
                <li role="treeitem" id="child" aria-selected="true">{Row("Frontline")}</li>
                <li role="treeitem" id="current" aria-current="page">{Row("Escalation")}</li>
              </ul>
            </li>
            <li role="treeitem" id="closed" aria-expanded="false">
              {Row("Network")}
              <ul role="group" id="closed-group"><li role="treeitem">{Row("On call")}</li></ul>
            </li>
            <li role="treeitem" id="leaf">{Row("Applications")}</li>
            <li role="treeitem" id="busy" aria-expanded="true" aria-busy="true">{Row("Archive")}</li>
            <li role="treeitem" id="ticked" aria-checked="true">{Row("Ticked", check: true)}</li>
            <li role="treeitem" id="mixed" aria-checked="mixed">{Row("Mixed", check: true)}</li>
            <li role="treeitem" id="unticked" aria-checked="false">{Row("Unticked", check: true)}</li>
          </ul>
          <div id="probe-tint" style="color: var(--brand-tint)"></div>
          <div id="probe-brand" style="color: var(--brand)"></div>
          <div id="probe-bg" style="color: var(--bg)"></div>
        </div>
        """;

    private const string Measure = """
        () => {
            const el = id => document.getElementById(id);
            const row = id => el(id).querySelector(':scope > .tree-row');
            const toggle = id => row(id).querySelector('.tree-toggle');
            const before = id => getComputedStyle(toggle(id), '::before');
            const sin = t => t === 'none' ? 0 : Number(t.match(/matrix\(([^)]+)\)/)[1].split(',')[1]);
            const check = id => row(id).querySelector('.tree-check');
            return {
                closedGroup: getComputedStyle(el('closed-group')).display,
                openGroup: getComputedStyle(el('open-group')).display,
                indent: Math.abs(row('child').getBoundingClientRect().left - row('open').getBoundingClientRect().left)
                    + Math.abs(row('child').getBoundingClientRect().right - row('open').getBoundingClientRect().right),
                branchLabel: row('closed').querySelector('.tree-label').getBoundingClientRect(),
                leafLabel: row('leaf').querySelector('.tree-label').getBoundingClientRect(),
                leafCaret: before('leaf').content,
                closedStart: before('closed').borderLeftWidth,
                closedEnd: before('closed').borderRightWidth,
                closedSin: sin(before('closed').transform),
                openSin: sin(before('open').transform),
                busyAnimation: before('busy').animationName,
                busyRadius: before('busy').borderTopLeftRadius,
                selectedBg: getComputedStyle(row('child')).backgroundColor,
                currentBg: getComputedStyle(row('current')).backgroundColor,
                tint: getComputedStyle(el('probe-tint')).color,
                brand: getComputedStyle(el('probe-brand')).color,
                bg: getComputedStyle(el('probe-bg')).color,
                tickedBg: getComputedStyle(check('ticked')).backgroundColor,
                mixedBg: getComputedStyle(check('mixed')).backgroundColor,
                untickedBg: getComputedStyle(check('unticked')).backgroundColor,
                mixedMark: getComputedStyle(check('mixed'), '::after').height,
                tickedMark: getComputedStyle(check('ticked'), '::after').transform,
                untickedMark: getComputedStyle(check('unticked'), '::after').content,
                lineHeight: getComputedStyle(row('leaf')).lineHeight
            };
        }
        """;

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task Every_state_paints_from_its_attribute_in_either_direction(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture(dir));
        var m = await page.EvaluateAsync<JsonElement>(Measure);

        Assert.Equal("none", m.GetProperty("closedGroup").GetString());
        Assert.Equal("block", m.GetProperty("openGroup").GetString());
        // One step in, from the start edge, whichever side that is.
        Assert.True(m.GetProperty("indent").GetDouble() > 20, $"A child row is indented {m.GetProperty("indent")}px.");

        // A leaf's caret draws nothing and keeps its width, so its label lines up with a branch's.
        Assert.Equal("none", m.GetProperty("leafCaret").GetString());
        var edge = dir == "ltr" ? "left" : "right";
        Assert.Equal(m.GetProperty("branchLabel").GetProperty(edge).GetDouble(), m.GetProperty("leafLabel").GetProperty(edge).GetDouble(), 0.5);

        // Closed, the caret points along the inline axis — its start border is the drawn one.
        var (start, end) = dir == "ltr" ? ("closedStart", "closedEnd") : ("closedEnd", "closedStart");
        Assert.Equal("5px", m.GetProperty(start).GetString());
        Assert.Equal("0px", m.GetProperty(end).GetString());
        Assert.Equal(0, m.GetProperty("closedSin").GetDouble(), 3);
        // Open, it turns down: clockwise from pointing right, anticlockwise from pointing left.
        Assert.Equal(dir == "ltr" ? 1 : -1, m.GetProperty("openSin").GetDouble(), 3);

        Assert.Equal("sedna-spin", m.GetProperty("busyAnimation").GetString());
        Assert.Equal("50%", m.GetProperty("busyRadius").GetString());

        var tint = m.GetProperty("tint").GetString();
        Assert.Equal(tint, m.GetProperty("selectedBg").GetString());
        Assert.Equal(tint, m.GetProperty("currentBg").GetString());

        var brand = m.GetProperty("brand").GetString();
        Assert.Equal(brand, m.GetProperty("tickedBg").GetString());
        Assert.Equal(brand, m.GetProperty("mixedBg").GetString());
        Assert.Equal(m.GetProperty("bg").GetString(), m.GetProperty("untickedBg").GetString());
        Assert.Equal("2px", m.GetProperty("mixedMark").GetString());
        Assert.NotEqual("none", m.GetProperty("tickedMark").GetString());
        Assert.Equal("none", m.GetProperty("untickedMark").GetString());

        // A whole, even number of pixels, so the row centres on a pixel.
        var line = double.Parse(m.GetProperty("lineHeight").GetString()!.Replace("px", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0, line % 2);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_keyboards_item_is_ringed_on_its_row_and_not_around_its_children()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture("ltr"));
        await page.Locator("#open").FocusAsync();
        // A keyboard focus, so :focus-visible matches.
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowUp");

        var m = await page.EvaluateAsync<JsonElement>("""
            () => ({
                focused: document.activeElement.id,
                item: getComputedStyle(document.getElementById('open')).outlineStyle,
                row: getComputedStyle(document.querySelector('#open > .tree-row')).boxShadow,
                child: getComputedStyle(document.querySelector('#child > .tree-row')).boxShadow
            })
            """);
        Assert.Equal("open", m.GetProperty("focused").GetString());
        Assert.Equal("none", m.GetProperty("item").GetString());
        Assert.NotEqual("none", m.GetProperty("row").GetString());
        Assert.Equal("none", m.GetProperty("child").GetString());
    }

    [Fact]
    public async Task A_tree_in_a_combo_panel_scrolls_like_the_list_it_replaces()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="form-combo" data-combo="single" style="width:320px">
              <div class="form-combo-box">
                <input class="form-combo-input" id="q" role="combobox" aria-expanded="true" aria-haspopup="tree" aria-controls="t">
              </div>
              <div class="form-combo-panel">
                <ul class="tree" role="tree" id="t" aria-label="Places">
                  <li role="treeitem" id="a" aria-selected="false" data-active><span class="tree-row"><span class="tree-toggle"></span><span class="tree-label">A</span></span></li>
                </ul>
                <div class="form-combo-empty">Nothing matches.</div>
              </div>
            </div>
            <div id="probe-hover" style="color: var(--bg-hover)"></div>
            """);
        var m = await page.EvaluateAsync<JsonElement>("""
            () => {
                const tree = getComputedStyle(document.getElementById('t'));
                return {
                    maxHeight: tree.maxHeight, overflow: tree.overflowY,
                    empty: getComputedStyle(document.querySelector('.form-combo-empty')).display,
                    active: getComputedStyle(document.querySelector('#a > .tree-row')).backgroundColor,
                    hover: getComputedStyle(document.getElementById('probe-hover')).color,
                    rem: parseFloat(getComputedStyle(document.documentElement).fontSize)
                };
            }
            """);
        Assert.Equal($"{16 * m.GetProperty("rem").GetDouble()}px", m.GetProperty("maxHeight").GetString());
        Assert.Equal("auto", m.GetProperty("overflow").GetString());
        // An item on show is not "nothing matches".
        Assert.Equal("none", m.GetProperty("empty").GetString());
        // The keyboard's item in a tree select is tinted as an option there is.
        Assert.Equal(m.GetProperty("hover").GetString(), m.GetProperty("active").GetString());

        await Js.Run(page, "() => document.getElementById('a').hidden = true");
        Assert.Equal("flex", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.form-combo-empty')).display"));
        Assert.Empty(errors);
    }
}
