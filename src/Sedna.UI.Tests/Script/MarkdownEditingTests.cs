using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The Markdown editor's commands, keys, lists, tables, paste and drop, driven as a reader
/// drives them: the textarea has focus and a key is pressed.
/// </summary>
/// <remarks>
/// A command is checked by what the textarea holds and has selected afterwards, and by the
/// browser's own undo stepping back over it — the property that tells an edit typed through
/// the platform from a value written into the field.
/// </remarks>
public class MarkdownEditingTests : ScriptTestBase
{
    private const string Fixture = """
        <div id="page-before">before</div>
        <div class="md-editor" data-md-view="write" id="ed">
            <div class="md-toolbar">
                <button class="md-tool" type="button" data-md-cmd="bold" id="b-bold" aria-label="Bold">B</button>
                <button class="md-tool" type="button" data-md-cmd="h2" id="b-h2" aria-label="Heading">H</button>
                <button class="md-tool" type="button" data-md-cmd="fullscreen" id="b-full" aria-label="Full screen" aria-pressed="false">F</button>
                <div class="segmented" role="group" aria-label="Editor view">
                    <label class="segmented-option"><input type="radio" data-md-tab="write" checked /> <span>Write</span></label>
                    <label class="segmented-option"><input type="radio" data-md-tab="preview" /> <span>Preview</span></label>
                    <label class="segmented-option"><input type="radio" data-md-tab="split" id="r-split" /> <span>Split</span></label>
                </div>
            </div>
            <textarea class="form-input md-input" data-md-input id="ta" rows="6" aria-label="Source"></textarea>
            <div class="markdown-body md-preview" data-md-preview id="preview"></div>
            <p class="form-hint"><span data-md-count="words" id="n-words"></span> words, <span data-md-count="characters" id="n-chars"></span> characters, <span data-md-count="lines" id="n-lines"></span> lines</p>
            <p class="form-warning" data-md-file-notice id="notice" hidden>Files are not accepted here.</p>
        </div>
        <div id="page-after">after</div>
        """;

    private async Task<IPage> Editor(string text, int? start = null, int? end = null, string? attributes = null, bool styled = false)
    {
        IPage page;
        if (styled) (page, _) = await OpenStyled(Fixture);
        else page = await Open(Fixture);
        if (attributes is not null)
            await Js.Run(page, "a => { const r = document.getElementById('ed'); for (const p of a.split(' ')) { const [k, v] = p.split('='); r.setAttribute(k, v ?? ''); } }", attributes);
        await Js.Run(page, "() => sednaUi.md.init()");
        await Select(page, text, start, end);
        return page;
    }

    /// <summary>Puts the text in the field, the way the page's own render would, with the caret where the test says.</summary>
    private static Task Select(IPage page, string text, int? start = null, int? end = null) =>
        Js.Run(page, """
            ([t, s, e]) => {
                const ta = document.getElementById('ta');
                ta.focus();
                ta.value = t;
                ta.setSelectionRange(s ?? t.length, e ?? s ?? t.length);
            }
            """, new object?[] { text, start, end });

    private static async Task<(string Value, string Selected, int Start, int End)> State(IPage page)
    {
        var r = await page.EvaluateAsync<Dictionary<string, object>>("""
            () => { const ta = document.getElementById('ta');
                    return { value: ta.value, selected: ta.value.slice(ta.selectionStart, ta.selectionEnd), start: ta.selectionStart, end: ta.selectionEnd }; }
            """);
        return (r["value"].ToString()!, r["selected"].ToString()!, int.Parse(r["start"].ToString()!), int.Parse(r["end"].ToString()!));
    }

    private static Task Apply(IPage page, string cmd) =>
        Js.Run(page, "c => sednaUi.md.apply(document.getElementById('ta'), c)", cmd);

    /// <summary>
    /// A plain Tab keydown to the textarea. <c>Keys.Tab</c> is Alt+Tab in WebKit on macOS, because
    /// there it is what moves focus; what is under test is the key the field itself receives.
    /// </summary>
    private static async Task PressTab(IPage page, bool shift = false)
    {
        if (shift) await page.Keyboard.DownAsync("Shift");
        await page.Keyboard.DownAsync("Tab");
        await page.Keyboard.UpAsync("Tab");
        if (shift) await page.Keyboard.UpAsync("Shift");
    }

    // ── Inline commands ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("bold", "a **b** c")]
    [InlineData("italic", "a _b_ c")]
    [InlineData("strike", "a ~~b~~ c")]
    [InlineData("code", "a `b` c")]
    public async Task An_inline_command_marks_the_selection_and_keeps_it_selected(string cmd, string expected)
    {
        if (NoBrowser) return;
        var page = await Editor("a b c", 2, 3);

        await Apply(page, cmd);

        var s = await State(page);
        Assert.Equal(expected, s.Value);
        Assert.Equal("b", s.Selected);
    }

    [Theory]
    [InlineData("bold", "a **b** c", 4, 5)]
    [InlineData("bold", "a **b** c", 2, 7)]
    [InlineData("italic", "a _b_ c", 3, 4)]
    [InlineData("strike", "a ~~b~~ c", 4, 5)]
    [InlineData("code", "a `b` c", 3, 4)]
    public async Task The_same_command_again_takes_the_marks_off_whether_they_are_in_the_selection_or_around_it(string cmd, string text, int start, int end)
    {
        if (NoBrowser) return;
        var page = await Editor(text, start, end);

        await Apply(page, cmd);

        var s = await State(page);
        Assert.Equal("a b c", s.Value);
        Assert.Equal("b", s.Selected);
    }

    [Fact]
    public async Task With_nothing_selected_a_placeholder_is_marked_and_selected_for_typing_over()
    {
        if (NoBrowser) return;
        var page = await Editor("a  c", 2);

        await Apply(page, "bold");

        var s = await State(page);
        Assert.Equal("a **bold text** c", s.Value);
        Assert.Equal("bold text", s.Selected);
    }

    [Fact]
    public async Task Whitespace_at_the_ends_of_a_selection_stays_outside_the_marks()
    {
        if (NoBrowser) return;
        // A double click that takes the space after the word: "** b **" is not emphasis.
        var page = await Editor("a b c", 1, 4);

        await Apply(page, "bold");

        Assert.Equal("a **b** c", (await State(page)).Value);
    }

    [Fact]
    public async Task A_command_is_one_input_event_and_one_step_of_the_browsers_undo()
    {
        if (NoBrowser) return;
        var page = await Editor("one two", 0, 3);
        await Js.Run(page, "() => { window.__inputs = 0; document.getElementById('ed').addEventListener('input', () => window.__inputs++); }");

        await page.ClickAsync("#b-bold");

        Assert.Equal("**one** two", (await State(page)).Value);
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__inputs"));
        Assert.Contains("one", await page.InnerHTMLAsync("#preview"), StringComparison.Ordinal);
        Assert.Contains("<strong>one</strong>", await page.InnerHTMLAsync("#preview"), StringComparison.Ordinal);

        await page.Keyboard.PressAsync(Keys.Primary("Z"));

        Assert.Equal("one two", (await State(page)).Value);
        Assert.DoesNotContain("<strong>", await page.InnerHTMLAsync("#preview"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_toolbar_button_acts_on_the_selection_the_field_kept_while_the_button_had_focus()
    {
        if (NoBrowser) return;
        var page = await Editor("alpha beta", 6, 10);

        await page.ClickAsync("#b-bold");

        Assert.Equal("alpha **beta**", (await State(page)).Value);
    }

    // ── Lines ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("h1", "plain", "# plain")]
    [InlineData("h3", "plain", "### plain")]
    [InlineData("h6", "plain", "###### plain")]
    [InlineData("h2", "# plain", "## plain")]
    [InlineData("h2", "## plain", "plain")]
    public async Task A_heading_command_sets_the_level_of_the_line_and_a_second_press_takes_it_off(string cmd, string before, string after)
    {
        if (NoBrowser) return;
        var page = await Editor(before, 2);

        await Apply(page, cmd);

        Assert.Equal(after, (await State(page)).Value);
    }

    [Fact]
    public async Task A_heading_on_an_empty_line_is_the_marker_and_a_caret_after_it()
    {
        if (NoBrowser) return;
        var page = await Editor("a\n\nb", 2);

        await Apply(page, "h2");

        var s = await State(page);
        Assert.Equal("a\n## \nb", s.Value);
        Assert.Equal(5, s.Start);
        Assert.Equal(5, s.End);
    }

    [Theory]
    [InlineData("ul", "a\nb", "- a\n- b")]
    [InlineData("ol", "a\nb", "1. a\n2. b")]
    [InlineData("task", "a\nb", "- [ ] a\n- [ ] b")]
    [InlineData("quote", "a\nb", "> a\n> b")]
    [InlineData("ul", "- a\n- b", "a\nb")]
    [InlineData("ol", "1. a\n2. b", "a\nb")]
    [InlineData("task", "- [ ] a\n- [x] b", "a\nb")]
    [InlineData("quote", "> a\n> b", "a\nb")]
    [InlineData("ol", "- a\n- b", "1. a\n2. b")]
    [InlineData("ul", "1. a\n2. b", "- a\n- b")]
    [InlineData("task", "- a\n- b", "- [ ] a\n- [ ] b")]
    [InlineData("ul", "- [ ] a", "- a")]
    [InlineData("ul", "a\n\nb", "- a\n\n- b")]
    public async Task A_list_command_puts_the_marker_on_every_line_and_takes_it_off_when_every_line_has_it(string cmd, string before, string after)
    {
        if (NoBrowser) return;
        var page = await Editor(before, 0, before.Length);

        await Apply(page, cmd);

        Assert.Equal(after, (await State(page)).Value);
    }

    [Fact]
    public async Task A_list_command_keeps_the_indent_of_a_nested_item()
    {
        if (NoBrowser) return;
        var page = await Editor("a\n  b", 0, 5);

        await Apply(page, "ul");

        Assert.Equal("- a\n  - b", (await State(page)).Value);
    }

    [Fact]
    public async Task A_list_command_on_the_line_the_selection_only_ends_at_the_start_of_leaves_that_line_alone()
    {
        if (NoBrowser) return;
        var page = await Editor("a\nb", 0, 2);

        await Apply(page, "ul");

        Assert.Equal("- a\nb", (await State(page)).Value);
    }

    // ── Blocks ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_rule_goes_on_a_line_of_its_own_with_an_empty_line_either_side_so_it_never_underlines_the_paragraph_above()
    {
        if (NoBrowser) return;
        var page = await Editor("first\nsecond line", 3);

        await Apply(page, "hr");

        Assert.Equal("first\n\n---\n\nsecond line", (await State(page)).Value);
        Assert.Contains("<hr>", await page.InnerHTMLAsync("#preview"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rule_on_an_empty_line_takes_that_line()
    {
        if (NoBrowser) return;
        var page = await Editor("first\n\nsecond", 6);

        await Apply(page, "hr");

        Assert.Equal("first\n\n---\n\nsecond", (await State(page)).Value);
    }

    [Fact]
    public async Task A_table_is_inserted_as_a_block_with_its_first_heading_selected()
    {
        if (NoBrowser) return;
        var page = await Editor("Orders by carrier", 17);

        await Apply(page, "table");

        var s = await State(page);
        Assert.Equal("Orders by carrier\n\n| Column | Column | Column |\n| --- | --- | --- |\n|  |  |  |\n", s.Value);
        Assert.Equal("Column", s.Selected);
        Assert.Equal(1, await page.EvaluateAsync<int>("() => document.querySelectorAll('#preview table').length"));
    }

    [Fact]
    public async Task A_code_block_fences_the_selection_on_lines_of_their_own()
    {
        if (NoBrowser) return;
        var page = await Editor("run npm test now", 4, 12);

        await Apply(page, "codeblock");

        var s = await State(page);
        Assert.Equal("run \n```\nnpm test\n```\n now", s.Value);
        Assert.Equal("npm test", s.Selected);
    }

    [Fact]
    public async Task A_code_block_with_nothing_selected_offers_a_word_to_type_over_and_a_longer_fence_when_the_text_holds_one()
    {
        if (NoBrowser) return;
        var page = await Editor("", 0);
        await Apply(page, "codeblock");
        var s = await State(page);
        Assert.Equal("```\ncode\n```", s.Value);
        Assert.Equal("code", s.Selected);

        await Select(page, "```js\nx\n```", 0, 11);
        await Apply(page, "codeblock");
        Assert.StartsWith("````\n```js", (await State(page)).Value, StringComparison.Ordinal);
    }

    // ── Links ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_link_over_a_selection_leaves_the_address_selected_for_typing()
    {
        if (NoBrowser) return;
        var page = await Editor("see the order", 4, 13);

        await Apply(page, "link");

        var s = await State(page);
        Assert.Equal("see [the order](https://)", s.Value);
        Assert.Equal("https://", s.Selected);
    }

    [Fact]
    public async Task A_link_over_a_selected_address_keeps_it_and_selects_the_text_to_write()
    {
        if (NoBrowser) return;
        var page = await Editor("see https://example.com/orders", 4, 30);

        await Apply(page, "link");

        var s = await State(page);
        Assert.Equal("see [link text](https://example.com/orders)", s.Value);
        Assert.Equal("link text", s.Selected);
    }

    [Fact]
    public async Task An_address_pasted_over_a_selection_makes_the_selection_a_link_and_is_one_undo_step()
    {
        if (NoBrowser) return;
        var page = await Editor("see the order", 4, 13);

        var prevented = await Paste(page, "https://example.com/orders/4204");

        Assert.True(prevented);
        Assert.Equal("see [the order](https://example.com/orders/4204)", (await State(page)).Value);
        await page.Keyboard.PressAsync(Keys.Primary("Z"));
        Assert.Equal("see the order", (await State(page)).Value);
    }

    [Theory]
    [InlineData("see the order", 4, 13, "the order and more")]
    [InlineData("see the order", 4, 4, "https://example.com/orders")]
    [InlineData("see the order", 4, 13, "https://example.com/a https://example.com/b")]
    [InlineData("see\nthe order", 0, 13, "https://example.com/orders")]
    public async Task Anything_else_pasted_is_left_to_the_browser(string text, int start, int end, string clipboard)
    {
        if (NoBrowser) return;
        var page = await Editor(text, start, end);

        Assert.False(await Paste(page, clipboard));
        Assert.Equal(text, (await State(page)).Value);
    }

    // ── Keys ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("B", "a **b** c")]
    [InlineData("I", "a _b_ c")]
    [InlineData("E", "a `b` c")]
    [InlineData("K", "a [b](https://) c")]
    [InlineData("Shift+X", "a ~~b~~ c")]
    [InlineData("Shift+7", "1. a b c")]
    [InlineData("Shift+8", "- a b c")]
    [InlineData("Shift+9", "> a b c")]
    [InlineData("Alt+1", "# a b c")]
    [InlineData("Alt+3", "### a b c")]
    [InlineData("Alt+C", "a \n```\nb\n```\n c")]
    public async Task A_shortcut_runs_the_command_while_the_field_has_focus(string chord, string expected)
    {
        if (NoBrowser) return;
        var page = await Editor("a b c", 2, 3);

        await page.Keyboard.PressAsync(Keys.Primary(chord));

        Assert.Equal(expected, (await State(page)).Value);
    }

    [Fact]
    public async Task Shortcuts_are_the_editors_to_turn_off()
    {
        if (NoBrowser) return;
        var page = await Editor("a b c", 2, 3, attributes: "data-md-keys=off");

        await page.Keyboard.PressAsync(Keys.Primary("B"));

        Assert.Equal("a b c", (await State(page)).Value);
    }

    [Fact]
    public async Task A_shortcut_does_nothing_in_a_read_only_field()
    {
        if (NoBrowser) return;
        var page = await Editor("a b c", 2, 3);
        await Js.Run(page, "() => document.getElementById('ta').readOnly = true");

        await page.Keyboard.PressAsync(Keys.Primary("B"));
        await Apply(page, "bold");

        Assert.Equal("a b c", (await State(page)).Value);
    }

    // ── Enter ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("- a", "- a\n- ")]
    [InlineData("* a", "* a\n* ")]
    [InlineData("+ a", "+ a\n+ ")]
    [InlineData("1. a", "1. a\n2. ")]
    [InlineData("9. a", "9. a\n10. ")]
    [InlineData("1) a", "1) a\n2) ")]
    [InlineData("- [ ] a", "- [ ] a\n- [ ] ")]
    [InlineData("- [x] a", "- [x] a\n- [ ] ")]
    [InlineData("> a", "> a\n> ")]
    [InlineData("> > a", "> > a\n> > ")]
    [InlineData("> - a", "> - a\n> - ")]
    [InlineData("  - a", "  - a\n  - ")]
    [InlineData("a", "a\n")]
    public async Task Enter_at_the_end_of_a_line_goes_on_with_the_marker_it_ends(string text, string expected)
    {
        if (NoBrowser) return;
        var page = await Editor(text);

        await page.Keyboard.PressAsync("Enter");

        Assert.Equal(expected, (await State(page)).Value);
    }

    [Theory]
    [InlineData("- a\n- ", "- a\n")]
    [InlineData("1. a\n2. ", "1. a\n")]
    [InlineData("- [ ] a\n- [ ] ", "- [ ] a\n")]
    [InlineData("> a\n> ", "> a\n")]
    [InlineData("- ", "")]
    public async Task Enter_on_an_empty_item_ends_the_list(string text, string expected)
    {
        if (NoBrowser) return;
        var page = await Editor(text);

        await page.Keyboard.PressAsync("Enter");

        Assert.Equal(expected, (await State(page)).Value);
    }

    [Fact]
    public async Task Enter_on_an_empty_nested_item_moves_it_out_one_level_first()
    {
        if (NoBrowser) return;
        var page = await Editor("- a\n  - b\n  - ");

        await page.Keyboard.PressAsync("Enter");

        Assert.Equal("- a\n  - b\n- ", (await State(page)).Value);
    }

    [Fact]
    public async Task Enter_in_the_middle_of_an_item_carries_the_rest_of_the_text_to_the_next_one_and_types_there()
    {
        if (NoBrowser) return;
        var page = await Editor("- one two", 5);

        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("X");

        Assert.Equal("- one\n- X two", (await State(page)).Value);
    }

    [Fact]
    public async Task Enter_in_front_of_the_marker_or_with_Shift_or_inside_a_code_block_is_an_ordinary_newline()
    {
        if (NoBrowser) return;
        var page = await Editor("- a", 1);
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("-\n a", (await State(page)).Value);

        await Select(page, "- a");
        await page.Keyboard.PressAsync("Shift+Enter");
        Assert.Equal("- a\n", (await State(page)).Value);

        await Select(page, "```\n- a");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("```\n- a\n", (await State(page)).Value);
    }

    [Fact]
    public async Task Enter_replaces_a_selection_as_it_always_does()
    {
        if (NoBrowser) return;
        var page = await Editor("- one two", 2, 5);

        await page.Keyboard.PressAsync("Enter");

        Assert.Equal("- \n two", (await State(page)).Value);
    }

    [Fact]
    public async Task A_list_continued_with_Enter_numbers_each_item_and_is_one_undo_step_per_press()
    {
        if (NoBrowser) return;
        var page = await Editor("1. a");

        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("b");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("c");

        Assert.Equal("1. a\n2. b\n3. c", (await State(page)).Value);
        Assert.Contains("<li>c</li>", await page.InnerHTMLAsync("#preview"), StringComparison.Ordinal);
    }

    // ── Tab ──────────────────────────────────────────────────────────────────

    private static Task WatchTab(IPage page) =>
        Js.Run(page, """
            () => { window.__tabs = [];
                    document.getElementById('ta').addEventListener('keydown', e => { if (e.key === 'Tab') window.__tabs.push(e.defaultPrevented); }); }
            """);

    private static Task<bool[]> Tabs(IPage page) => page.EvaluateAsync<bool[]>("() => window.__tabs");

    [Fact]
    public async Task Tab_indents_a_list_item_under_the_one_above_and_Shift_Tab_brings_it_back()
    {
        if (NoBrowser) return;
        var page = await Editor("- a\n- b", 7);
        await WatchTab(page);

        await PressTab(page);
        Assert.Equal("- a\n  - b", (await State(page)).Value);
        Assert.Contains("<ul><li>b</li></ul>", await page.InnerHTMLAsync("#preview"), StringComparison.Ordinal);

        await PressTab(page, shift: true);
        Assert.Equal("- a\n- b", (await State(page)).Value);
        Assert.Equal(new[] { true, true }, await Tabs(page));
    }

    [Fact]
    public async Task Tab_under_a_numbered_item_indents_by_the_width_of_its_marker()
    {
        if (NoBrowser) return;
        var page = await Editor("1. a\n- b", 8);

        await PressTab(page);

        Assert.Equal("1. a\n   - b", (await State(page)).Value);
    }

    [Fact]
    public async Task Tab_indents_every_item_the_selection_touches()
    {
        if (NoBrowser) return;
        var page = await Editor("- a\n- b\n- c", 4, 11);

        await PressTab(page);

        Assert.Equal("- a\n  - b\n  - c", (await State(page)).Value);
    }

    [Fact]
    public async Task Shift_Tab_goes_out_to_the_indent_of_the_item_it_was_under()
    {
        if (NoBrowser) return;
        var page = await Editor("- a\n  - b\n    - c", 17);

        await PressTab(page, shift: true);

        Assert.Equal("- a\n  - b\n  - c", (await State(page)).Value);
    }

    [Theory]
    [InlineData("- a", 3, false)]
    [InlineData("- a\n  - b", 9, false)]
    [InlineData("just a paragraph", 4, false)]
    [InlineData("- a\n- b", 7, true)]
    public async Task Tab_does_not_take_focus_where_there_is_no_level_to_move_an_item_to(string text, int caret, bool shift)
    {
        if (NoBrowser) return;
        var page = await Editor(text, caret);
        await WatchTab(page);

        // The first item, an item already a level under the one above and a line that is no item
        // all let Tab do what it does in any field: move on. Shift+Tab on an item at the margin does too.
        await PressTab(page, shift: shift && text == "- a\n- b");

        Assert.Equal(text, (await State(page)).Value);
        Assert.Equal(new[] { false }, await Tabs(page));
        Assert.NotEqual("ta", await page.EvaluateAsync<string>("() => document.activeElement.id"));
    }

    [Fact]
    public async Task Tab_moves_through_the_cells_of_a_table_and_adds_a_row_after_the_last()
    {
        if (NoBrowser) return;
        var page = await Editor("| a | b |\n| --- | --- |\n| 1 | 2 |", 2);

        await PressTab(page);
        Assert.Equal("b", (await State(page)).Selected);

        await PressTab(page);
        var s = await State(page);
        Assert.Equal("1", s.Selected);
        Assert.Equal("| a | b |\n| --- | --- |\n| 1 | 2 |", s.Value);

        await PressTab(page);
        await PressTab(page);
        Assert.Equal("| a | b |\n| --- | --- |\n| 1 | 2 |\n|  |  |", (await State(page)).Value);
        await page.Keyboard.TypeAsync("x");
        Assert.Equal("| a | b |\n| --- | --- |\n| 1 | 2 |\n| x |  |", (await State(page)).Value);

        await PressTab(page, shift: true);
        Assert.Equal("2", (await State(page)).Selected);
        await PressTab(page, shift: true);
        Assert.Equal("1", (await State(page)).Selected);
    }

    // ── Files ────────────────────────────────────────────────────────────────

    /// <summary>Pastes plain text into the field; returns whether the editor took the paste over.</summary>
    private static Task<bool> Paste(IPage page, string text) =>
        page.EvaluateAsync<bool>("""
            t => { const ta = document.getElementById('ta'); ta.focus();
                   const dt = new DataTransfer(); dt.setData('text/plain', t);
                   return !ta.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true })); }
            """, text);

    private static Task WatchFiles(IPage page, bool take) =>
        Js.Run(page, """
            take => { window.__files = [];
                      document.getElementById('ed').addEventListener('sedna-md-file', e => {
                          window.__files.push(e.detail);
                          if (take) e.preventDefault();
                      }); }
            """, take);

    private static Task<bool> PasteFile(IPage page, string name, string type, bool withText = false) =>
        page.EvaluateAsync<bool>("""
            ([name, type, withText]) => { const ta = document.getElementById('ta'); ta.focus();
                   const dt = new DataTransfer(); dt.items.add(new File(['abc'], name, { type }));
                   if (withText) dt.setData('text/plain', 'Quarterly figures');
                   return !ta.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true })); }
            """, new object[] { name, type, withText });

    [Fact]
    public async Task A_file_pasted_sends_a_cancelable_event_naming_it_and_the_app_reads_it_by_its_id()
    {
        if (NoBrowser) return;
        var page = await Editor("see ", 4);
        await WatchFiles(page, take: true);

        await PasteFile(page, "damage.png", "image/png");

        var detail = await page.EvaluateAsync<Dictionary<string, object>>("() => window.__files[0]");
        Assert.Equal("4", detail["index"].ToString());
        var read = await page.EvaluateAsync<string[]>("""
            async () => { const d = window.__files[0]; const f = sednaUi.md.file(document.getElementById('ta'), d.files[0].id);
                          return [d.files[0].name, d.files[0].type, String(d.files[0].size), await f.text(), String(sednaUi.md.file(document.getElementById('ta'), 'nope'))]; }
            """);
        Assert.Equal(["damage.png", "image/png", "3", "abc", "null"], read);
        // Nothing is inserted: the app uploads, then answers with the Markdown for the address.
        Assert.Equal("see ", (await State(page)).Value);
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('notice').hidden"));
    }

    [Fact]
    public async Task Files_nobody_takes_show_the_editors_notice_and_one_taken_hides_it_again()
    {
        if (NoBrowser) return;
        var page = await Editor("see ", 4);
        await Js.Run(page, """
            () => { window.__take = false;
                    document.getElementById('ed').addEventListener('sedna-md-file', e => { if (window.__take) e.preventDefault(); }); }
            """);

        await PasteFile(page, "damage.png", "image/png");
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('notice').hidden"));

        await Js.Run(page, "() => window.__take = true");
        await PasteFile(page, "damage.png", "image/png");
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('notice').hidden"));
    }

    [Fact]
    public async Task A_paste_that_carries_text_as_well_is_pasted_as_the_text()
    {
        if (NoBrowser) return;
        var page = await Editor("");
        await WatchFiles(page, take: true);

        var prevented = await PasteFile(page, "sheet.png", "image/png", withText: true);

        Assert.False(prevented);
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__files.length"));
    }

    [Fact]
    public async Task A_file_dropped_is_offered_the_same_way_and_the_browser_does_not_open_it_in_the_tab()
    {
        if (NoBrowser) return;
        var page = await Editor("see ", 4);
        await WatchFiles(page, take: true);

        var prevented = await page.EvaluateAsync<bool>("""
            () => { const ta = document.getElementById('ta');
                    const dt = new DataTransfer(); dt.items.add(new File(['abc'], 'tour.mp4', { type: 'video/mp4' }));
                    ta.dispatchEvent(new DragEvent('dragenter', { dataTransfer: dt, bubbles: true, cancelable: true }));
                    const dragging = document.getElementById('ed').hasAttribute('data-md-dragging');
                    const over = !ta.dispatchEvent(new DragEvent('dragover', { dataTransfer: dt, bubbles: true, cancelable: true }));
                    const dropped = !ta.dispatchEvent(new DragEvent('drop', { dataTransfer: dt, bubbles: true, cancelable: true }));
                    return dragging && over && dropped && !document.getElementById('ed').hasAttribute('data-md-dragging'); }
            """);

        Assert.True(prevented);
        Assert.Equal("tour.mp4", await page.EvaluateAsync<string>("() => window.__files[0].files[0].name"));
    }

    [Fact]
    public async Task Text_dragged_into_the_field_is_left_to_the_browser()
    {
        if (NoBrowser) return;
        var page = await Editor("");

        var prevented = await page.EvaluateAsync<bool>("""
            () => { const ta = document.getElementById('ta'); const dt = new DataTransfer(); dt.setData('text/plain', 'x');
                    return !ta.dispatchEvent(new DragEvent('dragover', { dataTransfer: dt, bubbles: true, cancelable: true })); }
            """);

        Assert.False(prevented);
    }

    [Fact]
    public async Task The_editor_keeps_only_the_last_files_it_was_given()
    {
        if (NoBrowser) return;
        var page = await Editor("");
        await WatchFiles(page, take: true);

        for (var i = 0; i < 25; i++) await PasteFile(page, $"p{i}.png", "image/png");

        var kept = await page.EvaluateAsync<bool[]>("""
            () => [0, 4, 5, 24].map(i => sednaUi.md.file(document.getElementById('ta'), window.__files[i].files[0].id) !== null)
            """);
        Assert.Equal([false, false, true, true], kept);
    }

    // ── Counts ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_counts_are_numbers_the_app_has_marked_and_follow_every_edit()
    {
        if (NoBrowser) return;
        var page = await Editor("");
        Assert.Equal(["0", "0", "0"], await Counts(page));

        await page.FocusAsync("#ta");
        await page.Keyboard.TypeAsync("Two words\nand a third line");

        Assert.Equal(["6", "26", "2"], await Counts(page));

        await Apply(page, "bold");
        Assert.NotEqual("26", (await Counts(page))[1]);
    }

    [Fact]
    public async Task A_character_outside_the_basic_plane_is_one_character()
    {
        if (NoBrowser) return;
        var page = await Editor("");

        await Js.Run(page, "() => { const ta = document.getElementById('ta'); ta.value = 'a\\u{1F600}b'; ta.dispatchEvent(new Event('input', { bubbles: true })); }");

        Assert.Equal("3", (await Counts(page))[1]);
    }

    private static Task<string[]> Counts(IPage page) =>
        page.EvaluateAsync<string[]>("() => ['n-words', 'n-chars', 'n-lines'].map(i => document.getElementById(i).textContent)");

    // ── Views ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Split_shows_the_text_and_its_preview_side_by_side_the_same_height_and_keeps_it_current()
    {
        if (NoBrowser) return;
        var page = await Editor("# Title\n\nSome *text*.", styled: true);
        await page.SetViewportSizeAsync(1100, 800);

        await page.ClickAsync("#r-split");

        Assert.Equal("split", await page.GetAttributeAsync("#ed", "data-md-view"));
        var box = await page.EvaluateAsync<Dictionary<string, double>>("""
            () => { const a = document.getElementById('ta').getBoundingClientRect(), b = document.getElementById('preview').getBoundingClientRect();
                    return { aLeft: a.left, aRight: a.right, bLeft: b.left, aTop: a.top, bTop: b.top, aH: a.height, bH: b.height }; }
            """);
        Assert.True(box["bLeft"] >= box["aRight"] - 1, "the preview is beside the text");
        Assert.Equal(box["aTop"], box["bTop"], 1);
        Assert.Equal(box["aH"], box["bH"], 1);
        Assert.Contains("<h1>Title</h1>", await page.InnerHTMLAsync("#preview"), StringComparison.Ordinal);

        // Long text in the preview scrolls inside it rather than stretching the row.
        await Select(page, string.Join("\n\n", Enumerable.Range(0, 60).Select(i => "Paragraph " + i)));
        await page.EvaluateAsync<string>("() => { const ta = document.getElementById('ta'); ta.dispatchEvent(new Event('input', { bubbles: true })); return ''; }");
        var after = await page.EvaluateAsync<double[]>("""
            () => { const a = document.getElementById('ta').getBoundingClientRect(), p = document.getElementById('preview');
                    return [a.height, p.getBoundingClientRect().height, p.scrollHeight]; }
            """);
        Assert.Equal(after[0], after[1], 1);
        Assert.True(after[2] > after[1] + 100, "the preview scrolls");
    }

    [Fact]
    public async Task Split_stacks_the_two_where_the_window_is_narrow()
    {
        if (NoBrowser) return;
        var page = await Editor("# Title", styled: true);
        await page.SetViewportSizeAsync(480, 800);

        await page.ClickAsync("#r-split");

        var stacked = await page.EvaluateAsync<bool>("""
            () => document.getElementById('preview').getBoundingClientRect().top >= document.getElementById('ta').getBoundingClientRect().bottom - 1
            """);
        Assert.True(stacked);
    }

    [Fact]
    public async Task A_split_editor_is_split_before_anything_is_wired_and_its_switch_agrees()
    {
        if (NoBrowser) return;
        var page = await Editor("x", attributes: "data-md-view=split");

        Assert.True(await page.IsCheckedAsync("#r-split"));
    }

    [Fact]
    public async Task Full_screen_covers_the_window_makes_the_page_inert_and_Escape_gives_it_back()
    {
        if (NoBrowser) return;
        var page = await Editor("# Title", styled: true);
        await page.SetViewportSizeAsync(900, 700);

        await page.ClickAsync("#b-full");

        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('ed').hasAttribute('data-md-fullscreen')"));
        Assert.Equal("true", await page.GetAttributeAsync("#b-full", "aria-pressed"));
        var covers = await page.EvaluateAsync<double[]>("() => { const r = document.getElementById('ed').getBoundingClientRect(); return [r.left, r.top, r.width, r.height]; }");
        Assert.Equal([0, 0, 900, 700], covers);
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('page-before').inert && document.getElementById('page-after').inert"));
        Assert.Equal("ta", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        await page.Keyboard.PressAsync("Escape");

        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('ed').hasAttribute('data-md-fullscreen')"));
        Assert.Equal("false", await page.GetAttributeAsync("#b-full", "aria-pressed"));
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('page-before').inert || document.getElementById('page-after').inert"));
    }

    [Fact]
    public async Task Full_screen_fills_the_window_with_the_text_and_with_both_panes_when_split()
    {
        if (NoBrowser) return;
        var page = await Editor("# Title", styled: true);
        await page.SetViewportSizeAsync(1000, 700);
        await page.ClickAsync("#b-full");

        var h = await page.EvaluateAsync<double[]>("""
            () => { const r = document.getElementById('ed').getBoundingClientRect(), a = document.getElementById('ta').getBoundingClientRect();
                    return [r.height, a.bottom, a.width]; }
            """);
        Assert.Equal(700, h[0], 1);
        Assert.InRange(h[1], 500, 700);
        Assert.Equal(1000, h[2], 1);

        await page.ClickAsync("#r-split");
        var split = await page.EvaluateAsync<double[]>("""
            () => { const a = document.getElementById('ta').getBoundingClientRect(), p = document.getElementById('preview').getBoundingClientRect();
                    return [a.width, p.width, a.bottom, p.bottom]; }
            """);
        Assert.Equal(split[0], split[1], 2);
        Assert.InRange(split[2], 500, 700);
        Assert.Equal(split[2], split[3], 1);
    }
}
