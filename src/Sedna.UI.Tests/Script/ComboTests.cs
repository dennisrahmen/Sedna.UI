using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The combo field's script turns key presses into the clicks and changes an app's own markup
/// handles, and never edits that markup unless the field is managed.
/// </summary>
/// <remarks>
/// The contract that matters is the one Blazor depends on: an app renders the chips and the
/// options, and a node the script inserted or removed would break its next render. So most of
/// these fixtures are unmanaged, and assert that the script <i>clicked</i> something rather than
/// that anything changed.
/// </remarks>
public class ComboTests : ScriptTestBase
{
    private static string Options(bool multi, params string[] names) => Options(multi, false, names);

    private static string Options(bool multi, bool more, params string[] names) =>
        $"""
        <div class="form-combo-panel">
            <div class="form-combo-list" id="list" role="listbox" {(multi ? "aria-multiselectable=\"true\"" : "")}>
                {string.Concat(names.Select((n, i) =>
                    $"<div class=\"form-combo-option\" role=\"option\" id=\"o{i}\" data-value=\"{n.ToLowerInvariant()}\" aria-selected=\"false\">{n}</div>"))}
            </div>
            {(more ? "<button class=\"form-combo-more\" type=\"button\">Load more</button>" : "")}
            <div class="form-combo-empty">Nothing matches.</div>
        </div>
        """;

    private static string Field(
        string mode, string chips = "", string attributes = "", string panel = "", string id = "q") =>
        $"""
        <div class="form-combo" data-combo="{mode}" {attributes} style="width:320px">
            <div class="form-combo-box">
                {chips}
                <input class="form-combo-input" id="{id}" type="text" role="combobox" placeholder="Pick…"
                       aria-expanded="false" aria-controls="list" autocomplete="off">
            </div>
            {panel}
        </div>
        <button id="elsewhere" type="button">Elsewhere</button>
        """;

    private const string Chip = """
        <span class="chip" data-value="chai"><span class="chip-label">Chai</span><button class="chip-dismiss" type="button" aria-label="Remove Chai">x</button></span>
        <span class="chip" data-value="chang"><span class="chip-label">Chang</span><button class="chip-dismiss" type="button" aria-label="Remove Chang">x</button></span>
        """;

    private const string Template = """
        <template data-combo-chip>
            <span class="chip"><span class="chip-label"></span><button class="chip-dismiss" type="button" aria-label="Remove {label}">x</button><input type="hidden" name="products"></span>
        </template>
        """;

    private static readonly string[] Products = ["Chai", "Chang", "Konbu", "Tofu"];

    [Fact]
    public async Task Arrow_down_opens_the_list_and_marks_the_first_option_for_a_screen_reader()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Field("single", panel: Options(false, Products)));

        await page.Locator("#q").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");

        var state = await page.EvaluateAsync<JsonElement>("""
            () => ({
                expanded: q.getAttribute('aria-expanded'),
                shown: getComputedStyle(document.querySelector('.form-combo-panel')).display,
                active: document.querySelector('[data-active]')?.id,
                descendant: q.getAttribute('aria-activedescendant'),
                panelWidth: document.querySelector('.form-combo-panel').getBoundingClientRect().width,
                boxWidth: document.querySelector('.form-combo-box').getBoundingClientRect().width
            })
            """);

        Assert.Equal("true", state.GetProperty("expanded").GetString());
        Assert.Equal("block", state.GetProperty("shown").GetString());
        Assert.Equal("o0", state.GetProperty("active").GetString());
        Assert.Equal("o0", state.GetProperty("descendant").GetString());
        // Anchored and as wide as the box it hangs from.
        Assert.Equal(state.GetProperty("boxWidth").GetDouble(), state.GetProperty("panelWidth").GetDouble(), 1);

        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("o1", await page.EvaluateAsync<string>("() => q.getAttribute('aria-activedescendant')"));

        // A filtered row has to leave the screen, not just carry the attribute: the option's own
        // `display` outranks the browser's `[hidden]` rule.
        await page.Keyboard.TypeAsync("ko");
        var shown = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('.form-combo-option')]
                .filter(o => getComputedStyle(o).display !== 'none').map(o => o.textContent)
            """);
        Assert.Equal(["Konbu"], shown);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Typing_hides_the_rows_that_do_not_match_but_a_server_list_is_the_apps_to_filter()
    {
        if (NoBrowser) return;
        var page = await Open(
            Field("multi", panel: Options(true, Products)) +
            Field("multi", attributes: "data-combo-source=\"server\"", panel: Options(true, Products), id: "s"));

        const string Hidden = """
            (i) => [...document.querySelectorAll('.form-combo-list')[i].querySelectorAll('.form-combo-option')]
                .map(o => o.hidden)
            """;

        await page.Locator("#q").FocusAsync();
        await page.Keyboard.TypeAsync("ch");
        bool[] typed = await page.EvaluateAsync<bool[]>(Hidden, 0);
        Assert.Equal([false, false, true, true], typed);

        // Leaving a field clears its query, so every row is back.
        await page.Locator("#s").FocusAsync();
        bool[] leftBehind = await page.EvaluateAsync<bool[]>(Hidden, 0);
        Assert.Equal([false, false, false, false], leftBehind);

        await page.Keyboard.TypeAsync("ch");
        bool[] server = await page.EvaluateAsync<bool[]>(Hidden, 1);
        Assert.Equal([false, false, false, false], server);
    }

    [Fact]
    public async Task Enter_clicks_the_active_option_and_a_multiselect_stays_open_with_its_query_cleared()
    {
        if (NoBrowser) return;
        var page = await Open(Field("multi", panel: Options(true, Products)));
        await page.EvaluateAsync("""
            () => { window.clicked = []; document.addEventListener('click', e => {
                const o = e.target.closest('.form-combo-option'); if (o) clicked.push(o.id); }); }
            """);

        await page.Locator("#q").FocusAsync();
        await page.Keyboard.TypeAsync("to");
        await page.Keyboard.PressAsync("Enter");

        var state = await page.EvaluateAsync<JsonElement>("""
            () => ({ clicked, expanded: q.getAttribute('aria-expanded'), query: q.value,
                     selected: document.querySelector('#o3').getAttribute('aria-selected') })
            """);

        Assert.Equal(["o3"], state.GetProperty("clicked").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("true", state.GetProperty("expanded").GetString());
        Assert.Equal("", state.GetProperty("query").GetString());
        // Unmanaged: the app decides what a click means, so nothing was selected for it.
        Assert.Equal("false", state.GetProperty("selected").GetString());
    }

    [Fact]
    public async Task A_single_select_closes_after_a_pick_and_Escape_closes_and_clears_the_query()
    {
        if (NoBrowser) return;
        var page = await Open(Field("single", panel: Options(false, Products)));

        await page.Locator("#q").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("false", await page.EvaluateAsync<string>("() => q.getAttribute('aria-expanded')"));

        await page.Keyboard.TypeAsync("ko");
        Assert.Equal("true", await page.EvaluateAsync<string>("() => q.getAttribute('aria-expanded')"));
        await page.Keyboard.PressAsync("Escape");

        var state = await page.EvaluateAsync<string[]>(
            "() => [q.getAttribute('aria-expanded'), q.value, String(q.hasAttribute('aria-activedescendant'))]");
        Assert.Equal(["false", "", "false"], state);
    }

    [Fact]
    public async Task Backspace_marks_the_last_chip_and_only_the_second_press_clicks_its_dismiss_button()
    {
        if (NoBrowser) return;
        var page = await Open(Field("multi", chips: Chip, panel: Options(true, Products)));
        await page.EvaluateAsync("""
            () => { window.dismissed = []; document.addEventListener('click', e => {
                const d = e.target.closest('.chip-dismiss'); if (d) dismissed.push(d.getAttribute('aria-label')); }); }
            """);

        await page.Locator("#q").FocusAsync();
        await page.Keyboard.PressAsync("Backspace");
        var first = await page.EvaluateAsync<JsonElement>("""
            () => ({ dismissed, armed: [...document.querySelectorAll('.chip')].map(c => c.hasAttribute('data-armed')) })
            """);
        Assert.Empty(first.GetProperty("dismissed").EnumerateArray());
        Assert.Equal([false, true], first.GetProperty("armed").EnumerateArray().Select(e => e.GetBoolean()));

        await page.Keyboard.PressAsync("Backspace");
        var second = await page.EvaluateAsync<JsonElement>(
            "() => ({ dismissed, chips: document.querySelectorAll('.chip').length })");
        Assert.Equal(["Remove Chang"], second.GetProperty("dismissed").EnumerateArray().Select(e => e.GetString()));
        // Unmanaged: removing the chip is the app's render, not the script's.
        Assert.Equal(2, second.GetProperty("chips").GetInt32());
    }

    [Fact]
    public async Task A_managed_multiselect_builds_each_chip_from_its_template_and_takes_it_away_again()
    {
        if (NoBrowser) return;
        var page = await Open(Field("multi", attributes: "data-combo-managed",
            panel: Template + Options(true, Products)));

        await page.Locator("#q").ClickAsync();
        await page.Locator("#o2").ClickAsync();

        var added = await page.EvaluateAsync<JsonElement>("""
            () => {
                const chip = document.querySelector('.form-combo-box > .chip');
                return {
                    value: chip?.getAttribute('data-value'),
                    label: chip?.querySelector('.chip-label').textContent,
                    aria: chip?.querySelector('.chip-dismiss').getAttribute('aria-label'),
                    posted: chip?.querySelector('input[type=hidden]').value,
                    beforeInput: chip?.nextElementSibling === q,
                    selected: document.querySelector('#o2').getAttribute('aria-selected'),
                    open: q.getAttribute('aria-expanded')
                };
            }
            """);
        Assert.Equal("konbu", added.GetProperty("value").GetString());
        Assert.Equal("Konbu", added.GetProperty("label").GetString());
        Assert.Equal("Remove Konbu", added.GetProperty("aria").GetString());
        Assert.Equal("konbu", added.GetProperty("posted").GetString());
        Assert.True(added.GetProperty("beforeInput").GetBoolean());
        Assert.Equal("true", added.GetProperty("selected").GetString());
        Assert.Equal("true", added.GetProperty("open").GetString());

        await page.Locator(".chip-dismiss").ClickAsync();
        var removed = await page.EvaluateAsync<string[]>("""
            () => [String(document.querySelectorAll('.form-combo-box > .chip').length),
                   document.querySelector('#o2').getAttribute('aria-selected')]
            """);
        Assert.Equal(["0", "false"], removed);
    }

    [Fact]
    public async Task A_free_entry_sends_one_change_per_entry_split_out_of_a_paste_with_duplicates_dropped()
    {
        if (NoBrowser) return;
        var page = await Open(Field("free", chips: """
            <span class="chip" data-value="alex.fischer@example.com"><span class="chip-label">alex.fischer@example.com</span></span>
            """, attributes: "data-combo-validate=\"email\""));
        await page.EvaluateAsync("""
            () => { window.changes = []; document.addEventListener('change', e => changes.push(e.target.value)); }
            """);

        await page.Locator("#q").FocusAsync();
        await page.EvaluateAsync("""
            () => {
                const data = new DataTransfer();
                data.setData('text/plain', 'ops@example.org, Alex.Fischer@example.com; billing@example.net ops@example.org');
                q.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
            }
            """);
        await page.Keyboard.TypeAsync("desk@example.net");
        await page.Keyboard.PressAsync(",");
        // A name every plain object already has, which a set built on one reported as a duplicate.
        await page.Keyboard.TypeAsync("constructor");
        await page.Keyboard.PressAsync("Enter");

        var state = await page.EvaluateAsync<JsonElement>("() => ({ changes, value: q.value })");
        Assert.Equal(["ops@example.org", "billing@example.net", "desk@example.net", "constructor"],
            state.GetProperty("changes").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("", state.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Leaving_a_free_entry_commits_what_was_typed_exactly_once()
    {
        if (NoBrowser) return;
        // The platform fires its own `change` on blur. Let through, the app would get the entry
        // from it and then again from the commit.
        var page = await Open(Field("free"));
        await page.EvaluateAsync("""
            () => { window.changes = []; document.addEventListener('change', e => changes.push(e.target.value)); }
            """);

        await page.Locator("#q").FocusAsync();
        await page.Keyboard.TypeAsync("Rotterdam");
        await page.Locator("#elsewhere").ClickAsync();

        Assert.Equal(["Rotterdam"], await page.EvaluateAsync<string[]>("() => changes"));
        Assert.Equal("", await page.EvaluateAsync<string>("() => q.value"));
    }

    [Fact]
    public async Task A_managed_address_field_keeps_an_address_without_a_dotted_domain_as_an_invalid_chip()
    {
        if (NoBrowser) return;
        var page = await Open(Field("free", attributes: "data-combo-managed data-combo-validate=\"email\"",
            panel: Template));

        await page.Locator("#q").FocusAsync();
        await page.Keyboard.TypeAsync("ops@example");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("ops@example.org");
        await page.Keyboard.PressAsync("Enter");

        var state = await page.EvaluateAsync<JsonElement>("""
            () => ({
                invalid: [...document.querySelectorAll('.form-combo-box > .chip')].map(c => c.classList.contains('chip--invalid')),
                ariaInvalid: q.getAttribute('aria-invalid')
            })
            """);
        Assert.Equal([true, false], state.GetProperty("invalid").EnumerateArray().Select(e => e.GetBoolean()));
        Assert.Equal("true", state.GetProperty("ariaInvalid").GetString());
    }

    [Fact]
    public async Task A_collapsed_field_opens_on_focus_and_counts_its_hidden_chips_beside_the_last_visible_one()
    {
        if (NoBrowser) return;
        var chips = string.Concat(Products.Concat(["Pavlova", "Scones"]).Select(n =>
            $"<span class=\"chip\" data-value=\"{n.ToLowerInvariant()}\"><span class=\"chip-label\">{n}</span></span>"));
        var field = Field("multi", chips: chips, attributes: "", panel: Options(true, Products))
            .Replace("class=\"form-combo\"", "class=\"form-combo form-combo--collapse\"", StringComparison.Ordinal)
            .Replace("autocomplete=\"off\">", "autocomplete=\"off\"><span class=\"form-combo-actions\"><button class=\"form-combo-btn form-combo-toggle\" type=\"button\" tabindex=\"-1\">v</button></span>", StringComparison.Ordinal);
        var (page, errors) = await OpenStyled(field);

        // Tree order puts the count after every chip it counts; `order` puts it back beside the
        // third one, ahead of the input and the buttons.
        var resting = await page.EvaluateAsync<string[]>("""
            () => {
                const box = document.querySelector('.form-combo-box');
                return [getComputedStyle(box, '::after').order, getComputedStyle(q).order,
                        getComputedStyle(box.querySelector('.form-combo-actions')).order,
                        getComputedStyle(box.querySelectorAll('.chip')[3]).visibility];
            }
            """);
        Assert.Equal(["1", "2", "2", "hidden"], resting);

        // Pressing the chevron of a field that is not focused: the press focuses and opens it,
        // and the click that follows must not shut it again.
        await page.Locator(".form-combo-toggle").ClickAsync();
        Assert.Equal("true", await page.EvaluateAsync<string>("() => q.getAttribute('aria-expanded')"));
        Assert.Equal("visible", await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelectorAll('.chip')[3]).visibility"));

        await page.Keyboard.PressAsync("Escape");
        await page.Locator("#elsewhere").FocusAsync();
        await page.Locator("#q").FocusAsync();
        Assert.Equal("true", await page.EvaluateAsync<string>("() => q.getAttribute('aria-expanded')"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_next_page_is_asked_for_when_its_button_scrolls_into_view_and_not_while_busy()
    {
        if (NoBrowser) return;
        var rows = Enumerable.Range(1, 30).Select(i => $"Product {i}").ToArray();
        var (page, _) = await OpenStyled(Field("multi", attributes: "data-combo-source=\"server\"",
            panel: Options(true, more: true, rows)));
        await page.EvaluateAsync("""
            () => { window.more = 0; document.addEventListener('click', e => { if (e.target.closest('.form-combo-more')) more++; }); }
            """);

        await page.Locator("#q").FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal(0, await page.EvaluateAsync<int>("() => more"));

        await page.EvaluateAsync("() => { list.setAttribute('aria-busy', 'true'); list.scrollTop = list.scrollHeight; }");
        await page.WaitForTimeoutAsync(100);
        Assert.Equal(0, await page.EvaluateAsync<int>("() => more"));

        // Two separate scrolls: back to the top and down again in one task is no scroll at all.
        await page.EvaluateAsync("() => { list.removeAttribute('aria-busy'); list.scrollTop = 0; }");
        await page.WaitForTimeoutAsync(100);
        await page.EvaluateAsync("() => { list.scrollTop = list.scrollHeight; }");
        await page.WaitForTimeoutAsync(100);
        Assert.Equal(1, await page.EvaluateAsync<int>("() => more"));
    }
}
