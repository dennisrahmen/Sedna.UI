using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Delegated dropdowns: hidden is the closed state, and Escape returns focus to the trigger.
/// </summary>
public class MenuTests : ScriptTestBase
{
    private const string MenuFixture = """
        <div class="menu-anchor">
            <button type="button" data-menu-toggle aria-expanded="false" id="trigger">Actions</button>
            <div class="menu" hidden>
                <a class="menu-item" href="#one" id="item">Rename</a>
            </div>
        </div>
        <button type="button" id="outside">Elsewhere</button>
        """;

    [Fact]
    public async Task A_menu_opens_on_its_toggle_and_closes_on_an_outside_click()
    {
        if (NoBrowser) return;
        var page = await Open(MenuFixture);

        await page.Locator("#trigger").ClickAsync();
        Assert.Equal("true", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));
        Assert.True(await page.Locator(".menu").IsVisibleAsync());

        await page.Locator("#outside").ClickAsync();
        Assert.Equal("false", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));
        // `hidden` is the closed state, not a class — so the items leave the tab order.
        Assert.True(await page.EvaluateAsync<bool>("() => document.querySelector('.menu').hidden"));
    }

    [Fact]
    public async Task Escape_closes_a_menu_and_gives_focus_back_to_the_control_that_opened_it()
    {
        if (NoBrowser) return;
        // Without the focus return, focus is left on a node that has just been hidden
        // and the next Tab starts from the top of the document.
        var page = await Open(MenuFixture);

        await page.Locator("#trigger").ClickAsync();
        await page.Locator("#item").FocusAsync();
        await page.Keyboard.PressAsync("Escape");

        Assert.Equal("false", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));
        Assert.Equal("trigger", await page.EvaluateAsync<string>("() => document.activeElement.id"));
    }

    [Fact]
    public async Task Clicking_a_menu_item_closes_the_menu_and_closeAll_closes_every_one()
    {
        if (NoBrowser) return;
        var page = await Open(MenuFixture);

        await page.Locator("#trigger").ClickAsync();
        await page.Locator("#item").ClickAsync();
        Assert.Equal("false", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));

        await page.Locator("#trigger").ClickAsync();
        await Js.Run(page, "() => sednaUi.menu.closeAll()");
        Assert.Equal("false", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));
    }

    // ── typeahead ───────────────────────────────────────────────────────────

    private const string LettersFixture = """
        <div class="menu-anchor">
            <button type="button" data-menu-toggle aria-expanded="false" id="trigger">Move to</button>
            <div class="menu" hidden>
                <button class="menu-item" type="button" id="incoming"><i class="ri-inbox-line"></i> <span>Incoming</span></button>
                <button class="menu-item" type="button" id="packing"><span>Packing</span><span class="menu-item-note">P</span></button>
                <button class="menu-item" type="button" id="parked" disabled><span>Parked</span></button>
                <button class="menu-item" type="button" id="picking"><span>Picking</span></button>
                <button class="menu-item" type="button" id="returns" data-label="Refunds and returns"><span>Returns</span></button>
            </div>
        </div>
        """;

    private static Task<string> Focused(IPage page) => page.EvaluateAsync<string>("() => document.activeElement.id");

    [Fact]
    public async Task A_letter_moves_to_the_next_item_whose_label_starts_with_it()
    {
        if (NoBrowser) return;
        // Paused, so letters typed one after another are one word or two only as the test says.
        var page = await Open(LettersFixture, clockPausedAt: new DateTime(2030, 1, 1));

        await page.Locator("#trigger").ClickAsync();
        await page.Locator("#incoming").FocusAsync();

        await page.Keyboard.PressAsync("p");
        Assert.Equal("packing", await Focused(page));
        // The same letter again cycles, passing over the disabled item.
        await page.Clock.RunForAsync(1000);
        await page.Keyboard.PressAsync("p");
        Assert.Equal("picking", await Focused(page));

        // Letters typed together are one word, matched from the item the focus is on.
        await page.Clock.RunForAsync(1000);
        await page.Keyboard.PressAsync("p");
        await page.Keyboard.PressAsync("a");
        Assert.Equal("packing", await Focused(page));

        // data-label is what an item is found by when it has one.
        await page.Clock.RunForAsync(1000);
        await page.Keyboard.PressAsync("r");
        Assert.Equal("returns", await Focused(page));

        // A letter nothing starts with leaves the focus where it is.
        await page.Clock.RunForAsync(1000);
        await page.Keyboard.PressAsync("z");
        Assert.Equal("returns", await Focused(page));
    }

    // ── a menu that filters ─────────────────────────────────────────────────

    private const string FilterFixture = """
        <div class="menu-anchor">
            <button type="button" data-menu-toggle aria-expanded="false" id="trigger">Move to queue</button>
            <div class="menu" id="panel" hidden>
                <input class="form-input form-input--sm menu-filter" id="filter" type="search" data-menu-filter aria-label="Find a queue">
                <span class="menu-label" id="fulfilment">Fulfilment</span>
                <button class="menu-item" type="button" id="incoming" onclick="window.chosen = this.id">Incoming</button>
                <button class="menu-item" type="button" id="packing" onclick="window.chosen = this.id">Packing</button>
                <hr class="menu-sep" id="sep">
                <span class="menu-label" id="after-sale">After sale</span>
                <button class="menu-item" type="button" id="returns" onclick="window.chosen = this.id">Returns</button>
                <button class="menu-item" type="button" id="refunds" onclick="window.chosen = this.id">Refunds</button>
                <p class="menu-empty" id="empty">No queue matches.</p>
            </div>
        </div>
        """;

    private static Task<string[]> Shown(IPage page) =>
        page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('#panel > [id]')].filter(el => el.getClientRects().length > 0).map(el => el.id)");

    [Fact]
    public async Task A_filter_hides_the_items_that_do_not_match_and_the_headings_left_with_nothing()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(FilterFixture);

        await page.Locator("#trigger").ClickAsync();
        // The menu opens with the reader typing into its filter.
        Assert.Equal("filter", await Focused(page));
        Assert.Equal(new[] { "filter", "fulfilment", "incoming", "packing", "sep", "after-sale", "returns", "refunds" }, await Shown(page));

        await page.Keyboard.TypeAsync("re");
        Assert.Equal(new[] { "filter", "after-sale", "returns", "refunds" }, await Shown(page));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('incoming').hidden"));

        // A subsequence matches too, as in the palette: "pg" is P-ackin-g.
        await page.Locator("#filter").FillAsync("pg");
        Assert.Equal(new[] { "filter", "fulfilment", "packing" }, await Shown(page));

        // Nothing matches: the app's own row says so.
        await page.Locator("#filter").FillAsync("zz");
        Assert.Equal(new[] { "filter", "empty" }, await Shown(page));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('panel').hasAttribute('data-menu-empty')"));

        // Closed and opened again, it starts from the whole list.
        await page.Keyboard.PressAsync("Escape");
        await page.Locator("#trigger").ClickAsync();
        Assert.Equal("", await page.Locator("#filter").InputValueAsync());
        Assert.Equal(8, (await Shown(page)).Length);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_arrow_keys_move_between_the_filter_and_the_items_that_match()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(FilterFixture);

        await page.Locator("#trigger").ClickAsync();
        await page.Keyboard.TypeAsync("re");

        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("returns", await Focused(page));
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("refunds", await Focused(page));
        // Wraps over the visible items only.
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("returns", await Focused(page));
        // Up from the first item goes back to the filter.
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("filter", await Focused(page));

        // Enter in the filter chooses the first match, which closes the menu.
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("returns", await page.EvaluateAsync<string>("() => window.chosen"));
        Assert.Equal("false", await page.Locator("#trigger").GetAttributeAsync("aria-expanded"));
        Assert.Empty(errors);
    }

    // ── Tab out of a menu ───────────────────────────────────────────────────
    // Every way the script opens a menu, so the next one that leaves a menu hanging open
    // after Tab fails here.
    private const string Items = """
        <button class="menu-item" type="button" id="first">Rename</button>
        <button class="menu-item" type="button" id="last">Delete</button>
        """;

    public static TheoryData<string, string, string> TabFixtures => new()
    {
        {
            "dropdown",
            $"""
            <button type="button" id="before">Before</button>
            <div class="menu-anchor">
                <button type="button" data-menu-toggle aria-expanded="false" id="trigger">Actions</button>
                <div class="menu" id="m" hidden>{Items}</div>
            </div>
            <button type="button" id="after">After</button>
            """,
            "() => document.getElementById('trigger').click()"
        },
        {
            "at a point",
            $"""
            <button type="button" id="before">Before</button>
            <div class="menu" id="m" popover>{Items}</div>
            <button type="button" id="after">After</button>
            """,
            "() => sednaUi.menu.openAt('m', 120, 120)"
        },
        {
            "popovertarget",
            $"""
            <button type="button" id="before" popovertarget="m">Actions</button>
            <div class="menu" id="m" popover>{Items}</div>
            <button type="button" id="after">After</button>
            """,
            "() => document.getElementById('before').click()"
        },
    };

    private const string MenuShown =
        "() => { const m = document.getElementById('m'); return m.hasAttribute('popover') ? m.matches(':popover-open') : !m.hidden; }";

    [Theory]
    [MemberData(nameof(TabFixtures))]
    public async Task Tab_out_of_a_menu_closes_it_and_the_focus_goes_on(string kind, string markup, string open)
    {
        if (NoBrowser) return;
        _ = kind;
        var (page, errors) = await OpenStyled(markup);

        await Js.Run(page, open);
        await Js.Run(page, "() => document.getElementById('first').focus()");
        Assert.True(await page.EvaluateAsync<bool>(MenuShown));

        await page.Keyboard.PressAsync(Keys.Tab);                   // to the next item: still open
        Assert.Equal("last", await Focused(page));
        await Js.Frames(page);
        Assert.True(await page.EvaluateAsync<bool>(MenuShown));

        await page.Keyboard.PressAsync(Keys.Tab);                   // out of it: closed, focus moved on
        await page.WaitForFunctionAsync($"() => !({MenuShown})()");
        Assert.Equal("after", await Focused(page));

        await Js.Run(page, open);
        await Js.Run(page, "() => document.getElementById('first').focus()");
        Assert.True(await page.EvaluateAsync<bool>(MenuShown));
        await page.Keyboard.PressAsync(Keys.ShiftTab);              // and backwards out of it
        await page.WaitForFunctionAsync($"() => !({MenuShown})()");
        Assert.NotEqual("first", await Focused(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_menu_at_the_pointer_with_a_filter_opens_on_the_filter()
    {
        if (NoBrowser) return;
        var page = await Open("""
            <div id="row" data-menu-context="ctx">ORD-4209</div>
            <div class="menu" id="ctx" popover>
                <input class="menu-filter" id="ctx-filter" data-menu-filter aria-label="Find an action">
                <button class="menu-item" type="button" id="snooze">Snooze</button>
                <button class="menu-item" type="button" id="assign">Assign</button>
            </div>
            """);

        await page.Locator("#row").ClickAsync(new() { Button = MouseButton.Right });
        await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.id === 'ctx-filter'");
        await page.Keyboard.TypeAsync("as");
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('snooze').hidden"));
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("assign", await Focused(page));
    }
}
