using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The graph without a pointer: one tab stop, a ring moved between records by the keys,
/// and every move said in the app's own words.
/// </summary>
/// <remarks>
/// A canvas is a picture to assistive technology, so this is the only way into a graph for
/// a keyboard or a screen reader — a record the keys cannot reach is a record they cannot
/// use at all. The records sit at fixed positions (<c>preset</c>), so "the nearest record
/// that way" is a known answer rather than whatever the springs made of it.
/// </remarks>
public class GraphKeyboardTests : GraphTestBase
{
    //              north (0, -150)
    //  west (-200, 0)   hub (0, 0)   east (200, 0)   far (420, 10)
    //              south (0, 150)
    private const string Compass = """
        <ul class="graph-data" data-graph-data>
          <li data-node="hub" data-kind="service" data-root data-href="/records/hub" data-x="0" data-y="0">Orders API</li>
          <li data-node="east" data-kind="store" data-x="200" data-y="0">Orders database</li>
          <li data-node="west" data-kind="service" data-x="-200" data-y="0">Web shop</li>
          <li data-node="north" data-kind="queue" data-x="0" data-y="-150">Order queue</li>
          <li data-node="south" data-kind="host" data-x="0" data-y="150">Build runner</li>
          <li data-node="far" data-x="420" data-y="10" data-owner="Alex Fischer">Replica</li>
          <li data-edge data-source="hub" data-target="east">reads</li>
          <li data-edge data-source="west" data-target="hub">calls</li>
          <li data-edge data-source="hub" data-target="north">publishes</li>
          <li data-edge data-source="south" data-target="hub">deploys</li>
          <li data-edge data-source="east" data-target="far">copies</li>
        </ul>
        <p class="visually-hidden" data-graph-live aria-live="polite" id="live"
           data-graph-announce="{label}, {kind}. {links} links."
           data-graph-announce-select="{label} selected."></p>
        """;

    private const string Menu = """
        <div class="menu graph-menu" data-graph-menu role="menu" hidden id="menu">
          <span class="menu-label" data-graph-field="label" id="menu-label"></span>
          <button class="menu-item" role="menuitem" type="button" data-graph-action="focus" id="menu-focus">Show its neighbourhood</button>
          <button class="menu-item" role="menuitem" type="button" data-graph-action="hide" id="menu-hide">Hide</button>
        </div>
        """;

    private async Task<IPage> OpenCompass(string extra = "", string attrs = "")
    {
        var page = await OpenGraph("""<button type="button" id="before">Before</button>"""
            + Graph(inside: Compass + extra, attrs: "data-graph-eager data-graph-layout=\"preset\" " + attrs)
            + """<button type="button" id="after">After</button>""");
        await Ready(page);
        return page;
    }

    // Tab from the button before the graph: a keyboard focus, so :focus-visible.
    private static async Task TabIn(IPage page)
    {
        await page.Locator("#before").FocusAsync();
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));
    }

    /// <summary>The record the ring is on — the one drawn with an outline — or null.</summary>
    private static Task<string?> Ringed(IPage page) =>
        page.EvaluateAsync<string?>(
            "() => { const r = cyOf('g').nodes().filter(n => n.numericStyle('outline-width') > 0); return r.length === 1 ? r.id() : r.length ? 'several' : null; }");

    private static async Task Press(IPage page, string key, string expected)
    {
        await page.Keyboard.PressAsync(key);
        Assert.Equal(expected, await Ringed(page));
    }

    [Fact]
    public async Task The_graph_is_one_tab_stop_and_a_keyboard_focus_puts_the_ring_on_the_root()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();

        Assert.Equal("0", await page.Locator("#g").GetAttributeAsync("tabindex"));
        Assert.Equal("application", await page.Locator("#g").GetAttributeAsync("role"));
        await ClearLog(page);

        await TabIn(page);

        Assert.Equal("hub", await Ringed(page));
        var hover = (await Log(page, "sedna-graph-hover")).Single();
        Assert.Equal("hub", hover.Detail.GetProperty("id").GetString());
        Assert.True(hover.Detail.GetProperty("keyboard").GetBoolean());
        await Assertions.Expect(page.Locator("#live")).ToHaveTextAsync("Orders API, service. 4 links.");

        // One stop: the next Tab leaves the graph, and takes the ring with it.
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("after", await Eval<string>(page, "() => document.activeElement.id"));
        Assert.Null(await Ringed(page));
        AssertQuiet();
    }

    [Fact]
    public async Task A_tabindex_and_role_the_app_wrote_are_kept()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(attrs: "data-graph-eager tabindex=\"-1\" role=\"figure\" aria-roledescription=\"graph\""));
        await Ready(page);

        Assert.Equal("-1", await page.Locator("#g").GetAttributeAsync("tabindex"));
        Assert.Equal("figure", await page.Locator("#g").GetAttributeAsync("role"));
        Assert.Equal("graph", await page.Locator("#g").GetAttributeAsync("aria-roledescription"));
        AssertQuiet();
    }

    [Fact]
    public async Task A_click_on_the_canvas_focuses_the_graph_without_a_ring()
    {
        if (NoBrowser) return;
        // The ring is for the keyboard; a click has already chosen what it wanted.
        var page = await OpenCompass();

        var box = await page.Locator("#g .graph-canvas").BoundingBoxAsync();
        await page.Mouse.ClickAsync(box!.X + 6, box.Y + 6);

        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));
        Assert.Null(await Ringed(page));
        // The keys carry on from there.
        await Press(page, "ArrowRight", "hub");
        AssertQuiet();
    }

    [Fact]
    public async Task The_arrows_move_the_ring_to_the_nearest_record_in_that_direction()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();
        await TabIn(page);

        await Press(page, "ArrowRight", "east");
        await Press(page, "ArrowRight", "far");
        // Nothing further that way: the ring stays.
        await Press(page, "ArrowRight", "far");
        await Press(page, "ArrowLeft", "east");
        await Press(page, "ArrowLeft", "hub");
        await Press(page, "ArrowLeft", "west");
        // Up from the far left: the nearest record within sixty degrees of straight up.
        await Press(page, "ArrowUp", "north");
        await Press(page, "ArrowDown", "hub");
        await Press(page, "ArrowDown", "south");

        await Assertions.Expect(page.Locator("#live")).ToHaveTextAsync("Build runner, host. 1 links.");
        var hovers = await Log(page, "sedna-graph-hover");
        Assert.Equal("south", hovers.Last().Detail.GetProperty("id").GetString());
        Assert.All(hovers, h => Assert.True(h.Detail.GetProperty("keyboard").GetBoolean()));
        AssertQuiet();
    }

    [Fact]
    public async Task An_announcement_leaves_out_a_field_the_record_does_not_have_and_can_name_the_apps_own()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();
        await page.EvaluateAsync("() => document.getElementById('live').setAttribute('data-graph-announce', '{label}, {kind}, {owner}. {links} links.')");
        await TabIn(page);

        await Assertions.Expect(page.Locator("#live")).ToHaveTextAsync("Orders API, service. 4 links.");
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(page.Locator("#live")).ToHaveTextAsync("Replica, Alex Fischer. 1 links.");
        AssertQuiet();
    }

    [Fact]
    public async Task Page_Down_walks_along_the_links()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();
        await TabIn(page);

        var visited = new List<string?> { await Ringed(page) };
        for (var i = 0; i < 4; i++)
        {
            await page.Keyboard.PressAsync("PageDown");
            visited.Add(await Ringed(page));
        }

        // Every step is to a record linked to the one before it.
        var links = await Eval<string[]>(page, "() => cyOf('g').edges().map(e => e.source().id() + '|' + e.target().id())");
        for (var i = 1; i < visited.Count; i++)
            Assert.True(links.Contains($"{visited[i - 1]}|{visited[i]}") || links.Contains($"{visited[i]}|{visited[i - 1]}"),
                $"PageDown went from {visited[i - 1]} to {visited[i]}, which are not linked: {string.Join(" → ", visited)}");
        AssertQuiet();
    }

    [Fact]
    public async Task Page_Up_goes_the_other_way_round_the_links_from_Page_Down()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();
        await TabIn(page);
        await Press(page, "ArrowRight", "east");

        await page.Keyboard.PressAsync("PageDown");
        var next = await Ringed(page);
        await page.Keyboard.PressAsync("Home");
        await Press(page, "ArrowRight", "east");
        await page.Keyboard.PressAsync("PageUp");
        var previous = await Ringed(page);

        Assert.NotEqual(next, previous);
        Assert.Contains(next, new[] { "hub", "far" });
        Assert.Contains(previous, new[] { "hub", "far" });
        AssertQuiet();
    }

    [Fact]
    public async Task Home_brings_the_ring_back_to_the_root()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();
        await TabIn(page);
        await Press(page, "ArrowRight", "east");
        await Press(page, "ArrowRight", "far");

        await Press(page, "Home", "hub");
        AssertQuiet();
    }

    [Fact]
    public async Task Home_without_a_root_goes_to_the_most_connected_record()
    {
        if (NoBrowser) return;
        var page = await OpenGraph("""<button type="button" id="before">Before</button>"""
            + Graph(inside: Compass.Replace(" data-root", "", StringComparison.Ordinal), attrs: "data-graph-eager data-graph-layout=\"preset\""));
        await Ready(page);
        await TabIn(page);

        Assert.Equal("hub", await Ringed(page));
        await Press(page, "ArrowRight", "east");
        await Press(page, "Home", "hub");
        AssertQuiet();
    }

    [Fact]
    public async Task Space_selects_the_ringed_record_and_Space_again_clears_it()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();
        await TabIn(page);
        await page.Keyboard.PressAsync("ArrowRight");
        await ClearLog(page);

        await page.Keyboard.PressAsync("Space");
        Assert.Equal(["east"], await Eval<string[]>(page, "() => cyOf('g').$(':selected').map(n => n.id())"));
        Assert.Equal("east", (await Log(page, "sedna-graph-select")).Single().Detail.GetProperty("id").GetString());
        await Assertions.Expect(page.Locator("#live")).ToHaveTextAsync("Orders database selected.");

        await page.Keyboard.PressAsync("Space");
        Assert.Equal(0, await Eval<int>(page, "() => cyOf('g').$(':selected').length"));
        Assert.Equal(JsonValueKind.Null, (await Log(page, "sedna-graph-select")).Last().Detail.GetProperty("id").ValueKind);
        AssertQuiet();
    }

    [Fact]
    public async Task Escape_clears_the_selection_first_then_the_ring()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();
        await TabIn(page);
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("Space");

        await page.Keyboard.PressAsync("Escape");
        Assert.Equal(0, await Eval<int>(page, "() => cyOf('g').$(':selected').length"));
        Assert.Equal("east", await Ringed(page));

        await page.Keyboard.PressAsync("Escape");
        Assert.Null(await Ringed(page));
        // Focus stays on the graph; the next arrow starts again from the root.
        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));
        await Press(page, "ArrowDown", "hub");
        AssertQuiet();
    }

    [Fact]
    public async Task Enter_opens_the_ringed_record_and_says_the_keyboard_did_it()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();
        await page.EvaluateAsync("() => document.addEventListener('sedna-graph-open', e => e.preventDefault())");
        await TabIn(page);
        await ClearLog(page);

        await page.Keyboard.PressAsync("Enter");
        await Heard(page, "sedna-graph-open");

        var open = (await Log(page, "sedna-graph-open")).Single();
        Assert.Equal("hub", open.Detail.GetProperty("id").GetString());
        Assert.True(open.Detail.GetProperty("keyboard").GetBoolean());
        AssertQuiet();
    }

    [Fact]
    public async Task Plus_and_minus_zoom_about_the_ring_zero_fits_and_Shift_and_an_arrow_pans()
    {
        if (NoBrowser) return;
        var page = await OpenCompass();
        await TabIn(page);
        var fitted = await Eval<double>(page, "() => cyOf('g').zoom()");

        await page.Keyboard.PressAsync("+");
        Assert.Equal(fitted * 1.25, await Eval<double>(page, "() => cyOf('g').zoom()"), 6);
        // About the ring: the record under it stays where it was on the screen.
        var before = await Eval<double[]>(page, "() => { const p = cyOf('g').getElementById('hub').renderedPosition(); return [p.x, p.y]; }");
        await page.Keyboard.PressAsync("+");
        var after = await Eval<double[]>(page, "() => { const p = cyOf('g').getElementById('hub').renderedPosition(); return [p.x, p.y]; }");
        Assert.Equal(before[0], after[0], 3);
        Assert.Equal(before[1], after[1], 3);

        await page.Keyboard.PressAsync("-");
        Assert.Equal(fitted * 1.25, await Eval<double>(page, "() => cyOf('g').zoom()"), 6);

        var pan = await Eval<double>(page, "() => cyOf('g').pan().x");
        await page.Keyboard.PressAsync("Shift+ArrowRight");
        Assert.Equal(pan - 60, await Eval<double>(page, "() => cyOf('g').pan().x"), 6);
        // Moving the view does not move the ring.
        Assert.Equal("hub", await Ringed(page));

        // Fitted again — to within what the records' own size changes with the zoom.
        await page.Keyboard.PressAsync("0");
        Assert.InRange(await Eval<double>(page, "() => cyOf('g').zoom()"), fitted * 0.97, fitted * 1.03);
        AssertQuiet();
    }

    [Fact]
    public async Task The_context_menu_key_opens_the_menu_on_the_ringed_record_with_its_first_item_focused()
    {
        if (NoBrowser) return;
        var page = await OpenCompass(extra: Menu);
        await TabIn(page);
        await page.Keyboard.PressAsync("ArrowRight");
        await ClearLog(page);

        await page.Keyboard.PressAsync("ContextMenu");

        await Assertions.Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#menu-label")).ToHaveTextAsync("Orders database");
        Assert.Equal("menu-focus", await Eval<string>(page, "() => document.activeElement.id"));
        var context = (await Log(page, "sedna-graph-context")).Single();
        Assert.Equal("east", context.Detail.GetProperty("id").GetString());
        // From the keyboard, the point is the record's own.
        var at = await page.EvaluateAsync<JsonElement>("() => cyOf('g').getElementById('east').renderedPosition()");
        Assert.Equal(Math.Round(at.GetProperty("x").GetDouble()), context.Detail.GetProperty("x").GetDouble());

        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("menu-hide", await Eval<string>(page, "() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();
        Assert.Equal("g", await Eval<string>(page, "() => document.activeElement.id"));

        // Shift + F10 is the same key on a keyboard without one.
        await page.Keyboard.PressAsync("Shift+F10");
        await Assertions.Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("#menu")).ToBeHiddenAsync();
        await page.WaitForFunctionAsync("() => cyOf('g').getElementById('west').hasClass('hidden')");
        Assert.Equal(["east", "far", "hub"], await Shown(page));
        AssertQuiet();
    }
}
