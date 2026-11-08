using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The tree's script: one tab stop, the arrow keys mirrored in RTL, typeahead, selection, tri-state
/// ticks, branches loaded on demand, and the events that tell an app what changed.
/// </summary>
public class TreeTests : ScriptTestBase
{
    private static string Item(string value, string label, string attributes = "", string children = "") =>
        $"""
        <li role="treeitem" data-value="{value}" {attributes}>
            <span class="tree-row"><span class="tree-toggle"></span><span class="tree-label">{label}</span></span>
            {(children.Length > 0 ? $"<ul role=\"group\">{children}</ul>" : "")}
        </li>
        """;

    /// <summary>
    /// Fulfilment (open) › Frontline, Escalation; Network (closed) › On call; Applications.
    /// No tabindex anywhere: the script writes the stop.
    /// </summary>
    private static string Teams(string treeAttributes = "", string pick = "aria-selected=\"false\"") =>
        $"""
        <button id="before" type="button">Before</button>
        <ul class="tree" role="tree" aria-label="Teams" id="tree" {treeAttributes}>
            {Item("fulfilment", "Fulfilment", $"aria-expanded=\"true\" {pick}",
                Item("frontline", "Frontline", pick) + Item("escalation", "Escalation", pick))}
            {Item("network", "Network", $"aria-expanded=\"false\" {pick}", Item("on-call", "On call", pick))}
            {Item("applications", "Applications", pick)}
        </ul>
        <button id="after" type="button">After</button>
        """;

    private const string Logger = """
        () => {
            window.log = [];
            for (const name of ['sedna-tree-expand', 'sedna-tree-collapse', 'sedna-tree-select', 'sedna-tree-check'])
                document.addEventListener(name, e => window.log.push({ name, on: e.target.dataset.value, detail: e.detail }));
        }
        """;

    private static Task<string?> Focused(IPage page) =>
        page.EvaluateAsync<string?>("() => document.activeElement?.dataset.value ?? null");

    private static Task<JsonElement> Log(IPage page) => page.EvaluateAsync<JsonElement>("() => window.log");

    private static Task<string?> Attr(IPage page, string value, string name) =>
        page.EvaluateAsync<string?>($"() => document.querySelector('[data-value=\"{value}\"]').getAttribute('{name}')");

    [Fact]
    public async Task The_tree_is_one_tab_stop_that_follows_the_keyboard()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Teams());

        // The script gave the first item the stop and every other item -1.
        var stops = await page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('[role=treeitem]')].map(i => i.getAttribute('tabindex'))");
        Assert.Equal(["0", "-1", "-1", "-1", "-1", "-1"], stops);

        await page.Locator("#before").FocusAsync();
        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("fulfilment", await Focused(page));

        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("frontline", await Focused(page));

        // Leaving and coming back lands where the keyboard was, and the tree is still one stop.
        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("after", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync(Keys.ShiftTab);
        Assert.Equal("frontline", await Focused(page));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => document.querySelectorAll('[role=treeitem][tabindex=\"0\"]').length"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_arrow_keys_walk_the_visible_items_and_open_and_close_branches()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Teams());
        await Js.Run(page, Logger);
        await page.Locator("[data-value=fulfilment]").FocusAsync();

        // Down skips the closed branch's child.
        foreach (var expected in new[] { "frontline", "escalation", "network", "applications" })
        {
            await page.Keyboard.PressAsync("ArrowDown");
            Assert.Equal(expected, await Focused(page));
        }
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("applications", await Focused(page));

        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("network", await Focused(page));

        // Right opens a closed branch, then enters it.
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("true", await Attr(page, "network", "aria-expanded"));
        Assert.Equal("network", await Focused(page));
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("on-call", await Focused(page));
        // A leaf has nowhere further in.
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("on-call", await Focused(page));

        // Left climbs to the parent, then closes it.
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("network", await Focused(page));
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("false", await Attr(page, "network", "aria-expanded"));

        await page.Keyboard.PressAsync("Home");
        Assert.Equal("fulfilment", await Focused(page));
        await page.Keyboard.PressAsync("End");
        Assert.Equal("applications", await Focused(page));

        // * opens every branch beside the item.
        await page.Keyboard.PressAsync("*");
        Assert.Equal("true", await Attr(page, "network", "aria-expanded"));

        var log = await Log(page);
        var names = log.EnumerateArray().Select(e => $"{e.GetProperty("name").GetString()}:{e.GetProperty("on").GetString()}").ToArray();
        Assert.Equal(["sedna-tree-expand:network", "sedna-tree-collapse:network", "sedna-tree-expand:network"], names);
        Assert.False(log[0].GetProperty("detail").GetProperty("lazy").GetBoolean());
        Assert.Equal("network", log[0].GetProperty("detail").GetProperty("item").GetString());
    }

    [Fact]
    public async Task Closing_the_branch_the_keyboard_is_in_hands_it_the_keyboard()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Teams());
        await page.Locator("[data-value=escalation]").FocusAsync();

        // The caret of the open parent, clicked while a child has focus.
        await page.Locator("[data-value=fulfilment] > .tree-row > .tree-toggle").ClickAsync();

        Assert.Equal("false", await Attr(page, "fulfilment", "aria-expanded"));
        Assert.Equal("fulfilment", await Focused(page));
        Assert.Equal("0", await Attr(page, "fulfilment", "tabindex"));
        Assert.Equal("-1", await Attr(page, "escalation", "tabindex"));
    }

    [Fact]
    public async Task Right_and_left_are_mirrored_in_a_right_to_left_page()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"<div dir=\"rtl\">{Teams()}</div>");
        await page.Locator("[data-value=network]").FocusAsync();

        // In RTL the inline-forward key is Left: it opens, then enters.
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("false", await Attr(page, "network", "aria-expanded"));
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("true", await Attr(page, "network", "aria-expanded"));
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("on-call", await Focused(page));

        // Right climbs, then closes.
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("network", await Focused(page));
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("false", await Attr(page, "network", "aria-expanded"));
    }

    [Fact]
    public async Task Typeahead_finds_by_first_letters_cycles_on_a_repeat_and_starts_again_after_a_pause()
    {
        if (NoBrowser) return;
        var at = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
        var (page, _) = await OpenStyled(
            Teams().Replace("Applications", "Escrow", StringComparison.Ordinal), clockPausedAt: at);
        await page.Locator("[data-value=fulfilment]").FocusAsync();

        // One letter: the next visible item starting with it.
        await page.Keyboard.PressAsync("e");
        Assert.Equal("escalation", await Focused(page));
        // The same letter again goes on to the next one, and wraps.
        await page.Keyboard.PressAsync("e");
        Assert.Equal("applications", await Focused(page));
        await page.Keyboard.PressAsync("e");
        Assert.Equal("escalation", await Focused(page));

        // A pause ends the search; letters typed together are one word, matched from the item
        // the first letter reached, so "escr" passes Escalation and lands on Escrow.
        await page.Keyboard.PressAsync("Home");
        await page.Clock.RunForAsync(1000);
        await page.Keyboard.PressAsync("e");
        Assert.Equal("escalation", await Focused(page));
        await page.Keyboard.PressAsync("s");
        await page.Keyboard.PressAsync("c");
        await page.Keyboard.PressAsync("r");
        Assert.Equal("applications", await Focused(page));

        // A letter nothing starts with leaves the keyboard where it is; a closed branch's
        // child is not visible, so it is never found.
        await page.Clock.RunForAsync(1000);
        await page.Keyboard.PressAsync("o");
        Assert.Equal("applications", await Focused(page));
        await page.Keyboard.PressAsync("x");
        Assert.Equal("applications", await Focused(page));
    }

    [Fact]
    public async Task Space_picks_one_item_and_says_so()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Teams());
        await Js.Run(page, Logger);
        await page.Locator("[data-value=frontline]").FocusAsync();

        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");

        Assert.Equal("false", await Attr(page, "frontline", "aria-selected"));
        Assert.Equal("true", await Attr(page, "escalation", "aria-selected"));

        var log = await Log(page);
        Assert.Equal(2, log.GetArrayLength());
        var last = log[1].GetProperty("detail");
        Assert.Equal("escalation", last.GetProperty("item").GetString());
        Assert.True(last.GetProperty("selected").GetBoolean());
        Assert.Equal(["escalation"], last.GetProperty("items").EnumerateArray().Select(v => v.GetString()!).ToArray());

        // A click on the row picks too; a click on the caret only opens.
        await page.Locator("[data-value=network] > .tree-row > .tree-toggle").ClickAsync();
        Assert.Equal("true", await Attr(page, "network", "aria-expanded"));
        Assert.Equal("false", await Attr(page, "network", "aria-selected"));
        await page.Locator("[data-value=on-call] > .tree-row").ClickAsync();
        Assert.Equal("true", await Attr(page, "on-call", "aria-selected"));
        Assert.Equal("false", await Attr(page, "escalation", "aria-selected"));
        Assert.Equal("on-call", await Focused(page));
    }

    [Fact]
    public async Task A_multiselectable_tree_toggles_extends_and_picks_everything_but_a_disabled_item()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Teams("aria-multiselectable=\"true\"")
            .Replace("data-value=\"escalation\" aria-selected=\"false\"", "data-value=\"escalation\" aria-selected=\"false\" aria-disabled=\"true\"", StringComparison.Ordinal));
        await page.Locator("[data-value=fulfilment]").FocusAsync();

        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("Shift+ArrowDown");
        // The disabled item is reached and not picked.
        await page.Keyboard.PressAsync("Shift+ArrowDown");
        Assert.Equal("escalation", await Focused(page));

        Assert.Equal("true", await Attr(page, "fulfilment", "aria-selected"));
        Assert.Equal("true", await Attr(page, "frontline", "aria-selected"));
        Assert.Equal("false", await Attr(page, "escalation", "aria-selected"));

        // Space again unpicks.
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("Space");
        Assert.Equal("false", await Attr(page, "fulfilment", "aria-selected"));

        await page.Keyboard.PressAsync(Keys.SelectAll);
        var picked = await page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('[aria-selected=true]')].map(i => i.dataset.value)");
        // Every visible item but the disabled one; the closed branch's child is not visible.
        Assert.Equal(["fulfilment", "frontline", "network", "applications"], picked);
    }

    private const string Routing = """
        <ul class="tree" role="tree" aria-label="Routing" id="tree">
          <li role="treeitem" data-value="all" aria-expanded="true" aria-checked="false">
            <span class="tree-row"><span class="tree-toggle"></span><span class="tree-check"></span><span class="tree-label">All</span></span>
            <ul role="group">
              <li role="treeitem" data-value="north" aria-expanded="true" aria-checked="false">
                <span class="tree-row"><span class="tree-toggle"></span><span class="tree-check"></span><span class="tree-label">North</span></span>
                <ul role="group">
                  <li role="treeitem" data-value="n1" aria-checked="false"><span class="tree-row"><span class="tree-check"></span><span class="tree-label">N1</span></span></li>
                  <li role="treeitem" data-value="n2" aria-checked="false"><span class="tree-row"><span class="tree-check"></span><span class="tree-label">N2</span></span></li>
                </ul>
              </li>
              <li role="treeitem" data-value="south" aria-checked="false">
                <span class="tree-row"><span class="tree-check"></span><span class="tree-label">South</span></span>
              </li>
            </ul>
          </li>
        </ul>
        """;

    private static Task<string[]> Ticks(IPage page) =>
        page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('[role=treeitem]')].map(i => i.dataset.value + '=' + i.getAttribute('aria-checked'))");

    [Fact]
    public async Task A_tick_goes_down_to_the_children_and_up_to_the_parents_as_ticked_unticked_or_mixed()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Routing);
        await Js.Run(page, Logger);

        // One leaf: its parents are mixed.
        await page.Locator("[data-value=n1] > .tree-row").ClickAsync();
        Assert.Equal(["all=mixed", "north=mixed", "n1=true", "n2=false", "south=false"], await Ticks(page));

        // The other leaf: its parent is whole, the root still mixed.
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Space");
        Assert.Equal(["all=mixed", "north=true", "n1=true", "n2=true", "south=false"], await Ticks(page));

        // The root: everything under it.
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("Space");
        Assert.Equal(["all=true", "north=true", "n1=true", "n2=true", "south=true"], await Ticks(page));

        // A mixed parent's click ticks it whole, and unticking a branch clears its children.
        await page.Locator("[data-value=north] > .tree-row").ClickAsync();
        Assert.Equal(["all=mixed", "north=false", "n1=false", "n2=false", "south=true"], await Ticks(page));

        var log = await Log(page);
        Assert.All(log.EnumerateArray(), e => Assert.Equal("sedna-tree-check", e.GetProperty("name").GetString()));
        var last = log[log.GetArrayLength() - 1].GetProperty("detail");
        Assert.Equal("north", last.GetProperty("item").GetString());
        Assert.False(last.GetProperty("selected").GetBoolean());
        Assert.Equal(["south"], last.GetProperty("items").EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    [Fact]
    public async Task A_lazy_branch_is_busy_and_asks_for_its_children_until_the_app_renders_them()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <ul class="tree" role="tree" aria-label="Archive" id="tree">
                {Item("2024", "2024", "aria-expanded=\"false\" data-tree-lazy")}
                {Item("2023", "2023", "aria-expanded=\"false\" data-tree-lazy")}
            </ul>
            """);
        await Js.Run(page, Logger);
        await page.Locator("[data-value='2024']").FocusAsync();

        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("true", await Attr(page, "2024", "aria-expanded"));
        Assert.Equal("true", await Attr(page, "2024", "aria-busy"));
        var log = await Log(page);
        Assert.Equal("sedna-tree-expand", log[0].GetProperty("name").GetString());
        Assert.True(log[0].GetProperty("detail").GetProperty("lazy").GetBoolean());
        Assert.Equal("2024", log[0].GetProperty("detail").GetProperty("item").GetString());

        // The app answers: children, and the attribute dropped.
        await Js.Run(page, """
            () => {
                const item = document.querySelector('[data-value="2024"]');
                const group = document.createElement('ul');
                group.setAttribute('role', 'group');
                group.innerHTML = '<li role="treeitem" data-value="2024-10"><span class="tree-row"><span class="tree-label">October</span></span></li>';
                item.append(group);
                item.removeAttribute('data-tree-lazy');
            }
            """);
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-value=\"2024\"]').hasAttribute('aria-busy')");
        // The new child joined the tree as an item with no stop of its own.
        Assert.Equal("-1", await Attr(page, "2024-10", "tabindex"));
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("2024-10", await Focused(page));

        // An app that finds nothing only drops the attribute, and the spinner stops all the same.
        await page.Locator("[data-value='2023'] > .tree-row").ClickAsync();
        Assert.Equal("true", await Attr(page, "2023", "aria-busy"));
        await Js.Run(page, "() => document.querySelector('[data-value=\"2023\"]').removeAttribute('data-tree-lazy')");
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-value=\"2023\"]').hasAttribute('aria-busy')");
    }

    [Fact]
    public async Task Enter_follows_the_link_in_a_row()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <ul class="tree" role="tree" aria-label="Handbook">
              <li role="treeitem" data-value="a"><a class="tree-row" href="#one" tabindex="-1" id="link-a"><span class="tree-label">One</span></a></li>
              <li role="treeitem" data-value="b"><a class="tree-row" href="#two" tabindex="-1" id="link-b"><span class="tree-label">Two</span></a></li>
            </ul>
            """);
        await Js.Run(page, "() => { window.followed = []; document.addEventListener('click', e => { const a = e.target.closest('a'); if (a) { followed.push(a.id); e.preventDefault(); } }); }");

        await page.Locator("[data-value=a]").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");

        Assert.Equal(["link-b"], await page.EvaluateAsync<string[]>("() => window.followed"));
        Assert.Equal("b", await Focused(page));
    }

    [Fact]
    public async Task An_item_that_drags_from_itself_gives_space_and_the_arrows_to_the_drag()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <ul class="tree" role="tree" aria-label="Plan" data-drag-zone="plan">
              <li role="treeitem" data-value="a" aria-selected="false" data-drag-item="a"><span class="tree-row"><span class="tree-label">A</span></span></li>
              <li role="treeitem" data-value="b" aria-selected="false" data-drag-item="b"><span class="tree-row"><span class="tree-label">B</span></span></li>
              <li role="treeitem" data-value="c" aria-selected="false" data-drag-item="c"><span class="tree-row"><span class="tree-label">C</span></span></li>
            </ul>
            """);
        await Js.Run(page, "() => { window.drops = []; document.addEventListener('sedna-drop', e => drops.push(e.detail)); }");
        await page.Locator("[data-value=a]").FocusAsync();

        await page.Keyboard.PressAsync("Space");
        Assert.Equal("keyboard", await Attr(page, "a", "data-dragging"));
        Assert.Equal("false", await Attr(page, "a", "aria-selected"));

        // The arrows move the item, not the tree's focus.
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("a", await Focused(page));
        await page.Keyboard.PressAsync("Space");

        var drop = await page.EvaluateAsync<JsonElement>("() => window.drops[0]");
        Assert.Equal("a", drop.GetProperty("item").GetString());
        Assert.Equal(1, drop.GetProperty("index").GetInt32());
    }
}
