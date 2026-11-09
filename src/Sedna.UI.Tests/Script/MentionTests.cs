using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.mention</c>: "@" in a <c>textarea[data-mention]</c> opens the app's list, typing filters
/// it, the keys choose while the focus stays in the field, and the choice is written as typed input.
/// </summary>
public class MentionTests : ScriptTestBase
{
    // The page's own Tab — what chooses the item — and not focus navigation, which
    // Keys.Tab spells for the platform.
    private const string Choose = "Tab";

    private static string Fixture(string dir = "ltr", string extra = "") => $$"""
        <div dir="{{dir}}" style="width:420px; margin:40px">
          <div class="mention-field" id="host">
            <textarea class="form-input" id="t" rows="3" data-mention="people" aria-label="Comment" {{extra}}></textarea>
            <div class="menu" id="people" hidden aria-label="People">
              <button class="menu-item" type="button" tabindex="-1" id="alex" data-mention-value="alex.fischer" data-label="Alex Fischer"><span>Alex Fischer</span></button>
              <button class="menu-item" type="button" tabindex="-1" id="priya" data-mention-value="priya.nair" data-label="Priya Nair"><span>Priya Nair</span></button>
              <button class="menu-item" type="button" tabindex="-1" id="tomas" data-mention-value="tomas.ferreira" data-label="Tomás Ferreira"><span>Tomás Ferreira</span></button>
            </div>
          </div>
          <button type="button" id="other">Other</button>
        </div>
        <script>
          window.inputs = 0;
          window.sent = 0;
          document.getElementById('t').addEventListener('input', () => window.inputs++);
          // The app's own Enter-to-send, as a composer has.
          document.getElementById('t').addEventListener('keydown', e => { if (e.key === 'Enter') { window.sent++; e.preventDefault(); } });
        </script>
        """;

    private async Task<(IPage Page, List<string> Errors)> OpenField(string dir = "ltr")
    {
        var (page, errors) = await OpenStyled(Fixture(dir));
        await page.FocusAsync("#t");
        return (page, errors);
    }

    private static Task<bool> IsOpen(IPage page) => page.EvaluateAsync<bool>("() => !document.getElementById('people').hidden");

    private static Task Opened(IPage page) => page.WaitForFunctionAsync("() => !document.getElementById('people').hidden");

    private static Task<string[]> Shown(IPage page) =>
        page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('#people .menu-item')].filter(i => !i.hidden).map(i => i.id)");

    private static Task<string?> Active(IPage page) =>
        page.EvaluateAsync<string?>("() => document.querySelector('#people [data-active]')?.id ?? null");

    [Fact]
    public async Task At_opens_the_apps_list_under_the_field_and_the_focus_stays_in_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenField();
        Assert.False(await IsOpen(page));

        await page.Keyboard.TypeAsync("Ask @");
        await Opened(page);
        Assert.Equal(["alex", "priya", "tomas"], await Shown(page));
        Assert.Equal("alex", await Active(page));
        Assert.Equal("alex", await page.GetAttributeAsync("#t", "aria-activedescendant"));
        Assert.Equal("people", await page.GetAttributeAsync("#t", "aria-controls"));
        Assert.Equal("listbox", await page.GetAttributeAsync("#people", "role"));
        Assert.Equal("option", await page.GetAttributeAsync("#alex", "role"));
        Assert.Equal("true", await page.GetAttributeAsync("#alex", "aria-selected"));
        Assert.Equal("t", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        // Anchored under the field, from its start edge.
        var field = (await page.Locator("#t").BoundingBoxAsync())!;
        var list = (await page.Locator("#people").BoundingBoxAsync())!;
        Assert.True(list.Y >= field.Y + field.Height, "The list is not under the field.");
        Assert.Equal(field.X, list.X, 1.0);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task At_inside_a_word_opens_nothing()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenField();
        await page.Keyboard.TypeAsync("alex@example.com");
        await Js.Frames(page);
        Assert.False(await IsOpen(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Typing_filters_by_name_and_by_handle_and_nothing_matching_closes_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenField();
        await page.Keyboard.TypeAsync("@pr");
        await Opened(page);
        Assert.Equal(["priya"], await Shown(page));
        Assert.Equal("priya", await Active(page));

        await page.Keyboard.PressAsync("Backspace");
        await page.Keyboard.PressAsync("Backspace");
        await page.Keyboard.TypeAsync("ferr");
        await page.WaitForFunctionAsync("() => !document.getElementById('tomas').hidden && document.getElementById('alex').hidden");
        Assert.Equal(["tomas"], await Shown(page));

        await page.Keyboard.TypeAsync("xq");
        await page.WaitForFunctionAsync("() => document.getElementById('people').hidden");
        Assert.Null(await page.GetAttributeAsync("#t", "aria-activedescendant"));

        // A space ends the word, and the list stays closed.
        await page.Keyboard.TypeAsync(" ");
        await Js.Frames(page);
        Assert.False(await IsOpen(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Enter_writes_the_mention_at_the_caret_dispatches_input_and_does_not_send()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenField();
        await page.Keyboard.TypeAsync("Ask @pri about it");
        // The caret back after "@pri".
        await Js.Run(page, "() => { const t = document.getElementById('t'); t.setSelectionRange(8, 8); }");
        await page.Keyboard.TypeAsync("y");
        await Opened(page);
        var before = await page.EvaluateAsync<int>("() => window.inputs");

        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("Ask @priya.nair  about it", await page.InputValueAsync("#t"));
        Assert.Equal(16, await page.EvaluateAsync<int>("() => document.getElementById('t').selectionStart"));
        Assert.Equal(before + 1, await page.EvaluateAsync<int>("() => window.inputs"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.sent"));
        Assert.False(await IsOpen(page));

        // Closed, Enter is the app's again.
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.sent"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_arrows_move_and_wrap_and_Tab_chooses()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenField();
        await page.Keyboard.TypeAsync("@");
        await Opened(page);

        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("priya", await Active(page));
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("alex", await Active(page));
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("tomas", await Active(page));
        Assert.Equal("tomas", await page.GetAttributeAsync("#t", "aria-activedescendant"));

        await page.Keyboard.PressAsync(Choose);
        Assert.Equal("@tomas.ferreira ", await page.InputValueAsync("#t"));
        Assert.Equal("t", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Escape_closes_it_until_the_next_at()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenField();
        await page.Keyboard.TypeAsync("@a");
        await Opened(page);

        await page.Keyboard.PressAsync("Escape");
        Assert.False(await IsOpen(page));
        await page.Keyboard.TypeAsync("l");
        await Js.Frames(page);
        Assert.False(await IsOpen(page));

        await page.Keyboard.TypeAsync(" and @");
        await Opened(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_press_on_an_item_writes_it_and_leaving_the_field_closes_the_list()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenField();
        await page.Keyboard.TypeAsync("cc @");
        await Opened(page);
        await page.Locator("#priya").ClickAsync();
        Assert.Equal("cc @priya.nair ", await page.InputValueAsync("#t"));
        Assert.Equal("t", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.False(await IsOpen(page));

        await page.Keyboard.TypeAsync("@");
        await Opened(page);
        await page.FocusAsync("#other");
        Assert.False(await IsOpen(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task In_rtl_the_list_lines_up_with_the_fields_right_edge()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenField("rtl");
        // The page's direction, as an app sets it: the list is placed in the viewport's.
        await Js.Run(page, "() => document.documentElement.dir = 'rtl'");
        await page.Keyboard.TypeAsync("שלום @");
        await Opened(page);

        var field = (await page.Locator("#t").BoundingBoxAsync())!;
        var list = (await page.Locator("#people").BoundingBoxAsync())!;
        Assert.Equal(field.X + field.Width, list.X + list.Width, 1.0);
        Assert.True(list.Y >= field.Y + field.Height, "The list is not under the field.");

        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("שלום @priya.nair ", await page.InputValueAsync("#t"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task With_no_room_below_the_list_opens_above_the_field()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div style="position:fixed; inset-inline:20px; bottom:8px">{Fixture()}</div>
            """);
        await page.FocusAsync("#t");
        await page.Keyboard.TypeAsync("@");
        await Opened(page);

        var field = (await page.Locator("#t").BoundingBoxAsync())!;
        var list = (await page.Locator("#people").BoundingBoxAsync())!;
        Assert.True(list.Y + list.Height <= field.Y + 0.5, $"The list ends at {list.Y + list.Height}, the field starts at {field.Y}.");
        Assert.Empty(errors);
    }
}
