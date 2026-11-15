using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What a rich-text editor offers beyond the basics: the bar on a chosen image or video, how a
/// picture sits against its text, the horizontal rule, Markdown shortcuts while typing, mentions and
/// the length limit — each on a real page, with real keys and presses.
/// </summary>
public class EditorFeatureTests : ScriptTestBase
{
    private const string Bar = """
        <div class="editor-media-bar" data-editor-media-bar role="toolbar" aria-label="Media" hidden id="bar">
          <button type="button" data-editor-media-size="160" id="small">Small</button>
          <button type="button" data-editor-media-size="" id="natural">Original</button>
          <button type="button" data-editor-media-align="start" aria-label="Left" id="left"></button>
          <button type="button" data-editor-media-align="center" aria-label="Centre" id="centre"></button>
          <button type="button" data-editor-media-align="" aria-label="Inline" id="inline"></button>
          <button type="button" data-editor-media-edit id="edit">Edit</button>
          <button type="button" data-editor-media-remove id="remove">Remove</button>
        </div>
        <div class="editor-media" data-editor-media hidden id="media-form">
          <input class="form-input" type="url" data-editor-media-url aria-label="Address" id="media-url" />
          <input class="form-input" type="number" data-editor-media-width aria-label="Width" id="media-width" />
          <button type="button" data-editor-media-save id="media-save">Insert</button>
        </div>
        """;

    private const string Toolbar = """
        <div class="editor-toolbar" role="toolbar" aria-label="Formatting">
          <button type="button" data-editor-format="bold" aria-label="Bold" id="bold"></button>
          <button type="button" data-editor-format="image" aria-label="Image" id="image"></button>
          <button type="button" data-editor-format="video" aria-label="Video" id="video"></button>
          <button type="button" data-editor-action="rule" aria-label="Rule" id="rule"></button>
          <button type="button" data-editor-format="direction" value="rtl" aria-label="Right to left" id="rtl"></button>
          <button type="button" data-fullscreen aria-controls="note" aria-label="Full screen" id="full"></button>
        </div>
        """;

    private const string Typing = """
        <div class="editor-toolbar" role="toolbar" aria-label="Formatting">
          <button type="button" data-editor-format="bold" aria-label="Bold"></button>
          <button type="button" data-editor-format="italic" aria-label="Italic"></button>
          <button type="button" data-editor-format="strike" aria-label="Strike"></button>
          <button type="button" data-editor-format="code" aria-label="Code"></button>
          <button type="button" data-editor-format="header" value="2" aria-label="Heading"></button>
          <button type="button" data-editor-format="blockquote" aria-label="Quote"></button>
          <button type="button" data-editor-format="code-block" aria-label="Code block"></button>
          <button type="button" data-editor-action="rule" aria-label="Rule"></button>
        </div>
        """;

    private const string Mention = """
        <div class="menu editor-mention" data-editor-mention hidden aria-label="People" id="people">
          <button class="menu-item" type="button" tabindex="-1" data-mention-value="alex.fischer" data-label="Alex Fischer" id="alex"><span>Alex Fischer</span></button>
          <button class="menu-item" type="button" tabindex="-1" data-mention-value="sam.okafor" data-label="Sam Okafor" id="sam"><span>Sam Okafor</span></button>
          <template data-editor-mention-item>
            <button class="menu-item" type="button" tabindex="-1"><span data-label></span><span data-note></span></button>
          </template>
        </div>
        """;

    private static string Editor(string value = "", string toolbar = Toolbar, string extra = Bar, string attrs = "data-editor-eager", string textarea = "") => $"""
        <label for="note-value" id="note-label">Note</label>
        <div class="editor" data-editor id="note" {attrs}>
          {toolbar}
          <div class="editor-body prose" data-editor-body></div>
          {extra}
          <textarea id="note-value" name="note" data-editor-value hidden placeholder="Write it here" {textarea}>{System.Net.WebUtility.HtmlEncode(value)}</textarea>
        </div>
        """;

    private async Task<IPage> Open(string body, bool light = false)
    {
        var page = await Open(body, head: StylesheetTag);
        await page.WaitForFunctionAsync("() => document.getElementById('note')?.dataset.editorState === 'ready'");
        if (light) await Js.Run(page, "() => document.documentElement.setAttribute('data-variant', 'light')");
        return page;
    }

    private static Task<string> Html(IPage page) =>
        page.EvaluateAsync<string>("() => sednaUi.editor.get('note').then(e => e.html())");

    private static async Task<string> Value(IPage page)
    {
        await Js.Frames(page);
        return await page.EvaluateAsync<string>("() => document.getElementById('note-value').value");
    }

    private const string Picture = """<p>Before <img src="https://example.com/chart.png" alt="Orders" width="480" height="300"> after, with enough words to wrap around a floated picture and show that the text goes on beside it.</p>""";
    private const string Clip = """<p>Watch</p><video controls="" src="/missing.mp4" width="320" height="180"></video><p>End</p>""";

    // ── The bar ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_bar_is_shown_above_a_chosen_image_and_hidden_when_it_is_let_go()
    {
        if (NoBrowser) return;
        var page = await Open(Editor(Picture));
        await Assertions.Expect(page.Locator("#bar")).ToBeHiddenAsync();

        await page.Locator(".ql-editor img").ClickAsync();
        await Assertions.Expect(page.Locator("#bar")).ToBeVisibleAsync();
        var box = await page.EvaluateAsync<double[]>("() => { const a = document.getElementById('bar').getBoundingClientRect(), b = document.querySelector('.ql-editor img').getBoundingClientRect(), f = document.getElementById('note').getBoundingClientRect(); return [a.bottom, b.top, a.top, f.top, f.width, a.width]; }");
        // Above the picture, or below it where the frame has no room above; never over it.
        Assert.True(box[0] <= box[1] + 1 || box[2] >= box[1], $"the bar overlaps the picture: {string.Join(",", box)}");
        Assert.True(box[5] <= box[4]);

        await page.Locator(".ql-editor p").ClickAsync(new() { Position = new() { X = 4, Y = 6 } });
        await Assertions.Expect(page.Locator("#bar")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task The_bars_size_presets_set_the_width_and_clear_the_height_and_original_clears_both()
    {
        if (NoBrowser) return;
        var page = await Open(Editor(Picture));
        await page.Locator(".ql-editor img").ClickAsync();

        await page.Locator("#small").ClickAsync();
        Assert.Equal("""<p>Before <img src="https://example.com/chart.png" alt="Orders" width="160"> after, with enough words to wrap around a floated picture and show that the text goes on beside it.</p>""", await Value(page));
        // The bar stays, and says which size it is.
        await Assertions.Expect(page.Locator("#bar")).ToBeVisibleAsync();
        Assert.Equal("true", await page.Locator("#small").GetAttributeAsync("aria-pressed"));
        Assert.Equal("false", await page.Locator("#natural").GetAttributeAsync("aria-pressed"));

        await page.Locator("#natural").ClickAsync();
        Assert.DoesNotContain("width=", await Value(page), StringComparison.Ordinal);
        Assert.DoesNotContain("height=", await Value(page), StringComparison.Ordinal);
        Assert.Equal("true", await page.Locator("#natural").GetAttributeAsync("aria-pressed"));
        Assert.DoesNotContain("data-editor-selected", await Value(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_bars_alignment_controls_write_data_align_and_none_removes_it()
    {
        if (NoBrowser) return;
        var page = await Open(Editor(Picture));
        await page.Locator(".ql-editor img").ClickAsync();

        await page.Locator("#left").ClickAsync();
        Assert.Contains("data-align=\"start\"", await Value(page), StringComparison.Ordinal);
        Assert.Equal("true", await page.Locator("#left").GetAttributeAsync("aria-pressed"));
        await page.Locator("#centre").ClickAsync();
        Assert.Contains("data-align=\"center\"", await Value(page), StringComparison.Ordinal);
        Assert.Equal("false", await page.Locator("#left").GetAttributeAsync("aria-pressed"));
        await page.Locator("#inline").ClickAsync();
        Assert.DoesNotContain("data-align", await Value(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_bars_edit_opens_the_form_on_that_image_and_remove_deletes_it()
    {
        if (NoBrowser) return;
        var page = await Open(Editor(Picture));
        await page.Locator(".ql-editor img").ClickAsync();
        await page.Locator("#edit").ClickAsync();
        await Assertions.Expect(page.Locator("#media-form")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#media-url")).ToHaveValueAsync("https://example.com/chart.png");
        await Assertions.Expect(page.Locator("#media-width")).ToHaveValueAsync("480");
        await page.Locator("#media-width").FillAsync("240");
        await page.Locator("#media-save").ClickAsync();
        Assert.Contains("width=\"240\"", await Value(page), StringComparison.Ordinal);
        Assert.Equal(1, await page.Locator(".ql-editor img").CountAsync());

        await page.Locator(".ql-editor img").ClickAsync();
        await page.Locator("#remove").ClickAsync();
        Assert.Equal(0, await page.Locator(".ql-editor img").CountAsync());
        Assert.DoesNotContain("<img", await Value(page), StringComparison.Ordinal);
        await Assertions.Expect(page.Locator("#bar")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task The_bar_is_reachable_from_the_keyboard_and_escape_comes_back_to_the_document_with_the_image_still_chosen()
    {
        if (NoBrowser) return;
        var page = await Open(Editor(Picture));
        await page.Locator(".ql-editor img").ClickAsync();

        await page.Keyboard.PressAsync("Alt+F10");
        Assert.Equal("small", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        // Choosing did not let go because the focus moved.
        await Assertions.Expect(page.Locator("#bar")).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("natural", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("End");
        Assert.Equal("remove", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("Enter");
        Assert.Contains("width=\"160\"", await Value(page), StringComparison.Ordinal);

        await page.Keyboard.PressAsync("Escape");
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement.classList.contains('ql-editor')"));
        await Assertions.Expect(page.Locator("#bar")).ToBeVisibleAsync();
        Assert.Equal("", await page.Locator(".ql-editor img").GetAttributeAsync("data-editor-selected"));
    }

    [Fact]
    public async Task A_chosen_video_has_the_bar_too()
    {
        if (NoBrowser) return;
        var page = await Open(Editor(Clip));
        await page.Locator(".ql-editor video").ClickAsync(new() { Position = new() { X = 160, Y = 20 } });
        await Assertions.Expect(page.Locator("#bar")).ToBeVisibleAsync();
        await page.Locator("#small").ClickAsync();
        await page.Locator("#centre").ClickAsync();
        Assert.Equal("""<p>Watch</p><video controls="" src="/missing.mp4" width="160" data-align="center"></video><p>End</p>""", await Value(page));
    }

    [Fact]
    public async Task An_editor_without_the_bar_chooses_media_all_the_same()
    {
        if (NoBrowser) return;
        var page = await Open(Editor(Picture, extra: ""));
        await page.Locator(".ql-editor img").ClickAsync();
        Assert.Equal("", await page.Locator(".ql-editor img").GetAttributeAsync("data-editor-selected"));
    }

    // ── How a picture sits ────────────────────────────────────────────────────

    [Fact]
    public async Task Alignment_survives_the_round_trip_and_a_value_that_is_not_one_is_dropped()
    {
        if (NoBrowser) return;
        var page = await Open(Editor());
        await Js.Run(page, """
            () => sednaUi.editor.get('note').then(e => e.set(
                '<p><img src="/a.png" data-align="start"><img src="/b.png" data-align="center"><img src="/c.png" data-align="left"></p>' +
                '<video src="/a.mp4" data-align="end"></video>'))
            """);
        Assert.Equal("""<p><img src="/a.png" data-align="start"><img src="/b.png" data-align="center"><img src="/c.png"></p><video controls="" src="/a.mp4" data-align="end"></video>""", await Value(page));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_floated_picture_lets_the_text_go_on_beside_it_and_mirrors_in_rtl(bool rtl)
    {
        if (NoBrowser) return;
        var html = """<p><img src="/a.png" width="120" height="80" data-align="start">Text that goes on beside the picture and wraps around it for a good while, as a news article would.</p>""";
        var page = await Open(Editor(html));
        if (rtl) await Js.Run(page, "() => document.getElementById('note').dir = 'rtl'");
        var edge = await page.EvaluateAsync<double[]>("() => { const i = document.querySelector('.ql-editor img').getBoundingClientRect(), e = document.querySelector('.ql-editor').getBoundingClientRect(); const p = document.querySelector('.ql-editor p').getBoundingClientRect(); return [i.left - e.left, e.right - i.right, i.width, p.width]; }");
        // At the start edge — the right one in a right-to-left document — with the text beside it.
        if (rtl) Assert.True(edge[1] < 60 && edge[0] > 200, string.Join(",", edge));
        else Assert.True(edge[0] < 60 && edge[1] > 200, string.Join(",", edge));
        var lineWidth = await page.EvaluateAsync<double>("() => document.querySelector('.ql-editor p').getBoundingClientRect().width");
        Assert.True(edge[2] < lineWidth);
    }

    [Fact]
    public async Task A_floated_picture_stacks_at_phone_width()
    {
        if (NoBrowser) return;
        var page = await Open(Editor("""<p><img src="/a.png" width="120" height="80" data-align="end">Text that goes on beside the picture.</p>"""));
        await page.SetViewportSizeAsync(400, 800);
        var float_ = await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.ql-editor img')).float");
        Assert.Equal("none", float_);
    }

    // ── The rule ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_rule_action_puts_an_hr_in_a_line_of_its_own_and_the_caret_after_it()
    {
        if (NoBrowser) return;
        var page = await Open(Editor("<p>Above</p><p>Below</p>"));
        await page.Locator(".ql-editor p").Last.ClickAsync(new() { Position = new() { X = 4, Y = 6 } });
        await page.Keyboard.PressAsync(Keys.LineStart);
        await page.Locator("#rule").ClickAsync();
        Assert.Equal("<p>Above</p><hr><p>Below</p>", await Value(page));
        await page.Keyboard.TypeAsync("X");
        Assert.Equal("<p>Above</p><hr><p>XBelow</p>", await Value(page));

        // Kept through a load, and drawn by .prose.
        await Js.Run(page, "() => sednaUi.editor.get('note').then(e => e.set('<p>a</p><hr><p>b</p>'))");
        Assert.Equal("<p>a</p><hr><p>b</p>", await Value(page));
        Assert.Equal("1px", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.ql-editor hr')).borderTopWidth"));
    }

    [Fact]
    public async Task An_editor_that_offers_no_rule_cuts_one_away()
    {
        if (NoBrowser) return;
        var page = await Open(Editor("<p>a</p><hr><p>b</p>", toolbar: Typing.Replace("""<button type="button" data-editor-action="rule" aria-label="Rule"></button>""", "", StringComparison.Ordinal)));
        Assert.Equal("<p>a</p><p>b</p>", await Html(page));    }

    // ── Markdown shortcuts ────────────────────────────────────────────────────

    private async Task<IPage> TypingEditor(string html = "") => await Open(Editor(html, toolbar: Typing, extra: ""));

    [Theory]
    [InlineData("# Title", "<h1>Title</h1>", false)]
    [InlineData("#### Title", "<h4>Title</h4>", false)]
    [InlineData("###### Title", "<h6>Title</h6>", false)]
    [InlineData("> Quote", "<blockquote>Quote</blockquote>", false)]
    public async Task A_block_shortcut_typed_at_the_start_of_a_line_makes_the_block(string typed, string html, bool enter)
    {
        if (NoBrowser) return;
        var page = await TypingEditor();
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.TypeAsync(typed);
        if (enter) await page.Keyboard.PressAsync("Enter");
        Assert.Equal(html, await Value(page));
    }

    [Fact]
    public async Task Dashes_and_enter_make_a_rule_and_three_backticks_and_enter_a_code_block()
    {
        if (NoBrowser) return;
        var page = await TypingEditor();
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.TypeAsync("---");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("```");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("x = 1");
        var html = await Value(page);
        Assert.StartsWith("<hr>", html, StringComparison.Ordinal);
        Assert.Contains("<pre", html, StringComparison.Ordinal);
        Assert.Contains("x = 1", html, StringComparison.Ordinal);
        Assert.DoesNotContain("```", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a **bold** b", "<p>a <strong>bold</strong> b</p>")]
    [InlineData("a *soft* b", "<p>a <em>soft</em> b</p>")]
    [InlineData("a `code` b", "<p>a <code>code</code> b</p>")]
    [InlineData("a ~~gone~~ b", "<p>a <s>gone</s> b</p>")]
    public async Task An_inline_mark_closed_while_typing_makes_the_format_and_the_text_after_it_is_plain(string typed, string html)
    {
        if (NoBrowser) return;
        var page = await TypingEditor();
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.TypeAsync(typed);
        Assert.Equal(html, await Value(page));
    }

    [Fact]
    public async Task Undo_takes_the_shortcut_back_to_what_was_typed()
    {
        if (NoBrowser) return;
        var page = await TypingEditor();
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.TypeAsync("a **bold**");
        Assert.Equal("<p>a <strong>bold</strong></p>", await Value(page));
        await page.Keyboard.PressAsync(Keys.Primary("z"));
        Assert.Equal("<p>a **bold**</p>", await Value(page));

        await page.Keyboard.PressAsync(Keys.SelectAll);
        await page.Keyboard.PressAsync("Backspace");
        await page.Keyboard.TypeAsync("## ");
        await page.Keyboard.TypeAsync("Heading");
        Assert.Equal("<h2>Heading</h2>", await Value(page));
        // Two steps back: the word, then the shortcut.
        await page.Keyboard.PressAsync(Keys.Primary("z"));
        await page.Keyboard.PressAsync(Keys.Primary("z"));
        Assert.Equal("<p>##</p>", await Value(page));
    }

    [Fact]
    public async Task A_shortcut_for_a_format_the_editor_does_not_accept_stays_text_and_code_is_left_alone()
    {
        if (NoBrowser) return;
        var page = await Open(Editor("", toolbar: """<div class="editor-toolbar" role="toolbar" aria-label="F"><button type="button" data-editor-format="bold" aria-label="Bold"></button></div>""", extra: ""));
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.TypeAsync("# Not a heading ");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("a *soft* **bold**");
        Assert.Equal("<p># Not a heading </p><p>a *soft* <strong>bold</strong></p>", await Value(page));
    }

    // ── Mentions ──────────────────────────────────────────────────────────────

    private async Task<IPage> MentionEditor() => await Open(Editor("<p>Hi </p>", toolbar: Typing, extra: Mention));

    [Fact]
    public async Task At_opens_the_list_filters_it_and_enter_writes_the_mention()
    {
        if (NoBrowser) return;
        var page = await MentionEditor();
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync(Keys.DocumentEnd);
        await page.Keyboard.TypeAsync(" @");
        await Assertions.Expect(page.Locator("#people")).ToBeVisibleAsync();
        Assert.Equal("listbox", await page.Locator("#people").GetAttributeAsync("role"));
        Assert.Equal("alex", await page.EvaluateAsync<string>("() => document.querySelector('.ql-editor').getAttribute('aria-activedescendant')"));

        await page.Keyboard.TypeAsync("sam");
        await Assertions.Expect(page.Locator("#alex")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#sam")).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("#people")).ToBeHiddenAsync();
        Assert.Equal("""<p>Hi <data value="sam.okafor">@Sam Okafor</data> </p>""", await Value(page));

        // What is typed next is plain text, outside the mention.
        await page.Keyboard.TypeAsync("thanks");
        Assert.Equal("""<p>Hi <data value="sam.okafor">@Sam Okafor</data> thanks</p>""", await Value(page));
    }

    [Fact]
    public async Task The_arrows_choose_escape_closes_and_a_press_on_an_item_writes_it()
    {
        if (NoBrowser) return;
        var page = await MentionEditor();
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync(Keys.DocumentEnd);
        await page.Keyboard.TypeAsync(" @");
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("sam", await page.EvaluateAsync<string>("() => document.querySelector('.ql-editor').getAttribute('aria-activedescendant')"));
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("#people")).ToBeHiddenAsync();
        Assert.Equal("<p>Hi @</p>", await Value(page));

        await page.Keyboard.PressAsync("Backspace");
        await page.Keyboard.TypeAsync("@");
        await page.Locator("#alex").ClickAsync();
        Assert.Equal("""<p>Hi <data value="alex.fischer">@Alex Fischer</data> </p>""", await Value(page));
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement.classList.contains('ql-editor')"));
    }

    [Fact]
    public async Task A_mention_is_kept_through_a_load_and_an_email_address_does_not_open_the_list()
    {
        if (NoBrowser) return;
        var page = await MentionEditor();
        await Js.Run(page, """() => sednaUi.editor.get('note').then(e => e.set('<p>Ask <data value="alex.fischer">@Alex</data></p>'))""");
        Assert.Equal("""<p>Ask <data value="alex.fischer">@Alex</data></p>""", await Value(page));

        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync(Keys.DocumentEnd);
        await page.Keyboard.TypeAsync(" mail alex@example.com");
        await Assertions.Expect(page.Locator("#people")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task An_app_that_cancels_the_event_fills_the_list_from_its_template_and_every_row_is_shown()
    {
        if (NoBrowser) return;
        var page = await MentionEditor();
        await Js.Run(page, """
            () => {
                window.asked = [];
                document.addEventListener('sedna-editor-mention', e => { asked.push(e.detail.query); e.preventDefault(); });
            }
            """);
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync(Keys.DocumentEnd);
        await page.Keyboard.TypeAsync(" @ri");
        Assert.Equal("""["","r","ri"]""", await page.EvaluateAsync<string>("() => JSON.stringify(asked)"));

        var made = await page.EvaluateAsync<int>("() => sednaUi.editor.invoke('note', 'mentions', [[{ value: 'rita.novak', label: 'Rita Novak', note: 'Dispatch' }, { value: '', label: 'skipped' }]])");
        Assert.Equal(1, made);
        await Assertions.Expect(page.Locator("[data-editor-mention-made]")).ToHaveCountAsync(1);
        Assert.Equal("Dispatch", await page.Locator("[data-editor-mention-made] [data-note]").InnerTextAsync());
        // The two rows the app wrote are shown too: the app answered, the editor does not filter.
        await Assertions.Expect(page.Locator("#alex")).ToBeVisibleAsync();
        await page.Locator("[data-editor-mention-made]").ClickAsync();
        Assert.Contains("""<data value="rita.novak">@Rita Novak</data>""", await Value(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_editor_without_a_mention_list_types_an_at_sign_as_text()
    {
        if (NoBrowser) return;
        var page = await Open(Editor("", toolbar: Typing, extra: ""));
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.TypeAsync("@alex");
        Assert.Equal("<p>@alex</p>", await Value(page));
    }

    // ── The limit, the direction, the toolbar ─────────────────────────────────

    [Fact]
    public async Task Maxlength_stops_typing_beyond_it_marks_the_frame_and_clears_at_the_next_change()
    {
        if (NoBrowser) return;
        var page = await Open(Editor("", toolbar: Typing, extra: "", textarea: "maxlength=\"10\""));
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.TypeAsync("0123456789");
        Assert.Null(await page.Locator("#note").GetAttributeAsync("aria-invalid"));
        await page.Keyboard.TypeAsync("abc");
        Assert.Equal("<p>0123456789</p>", await Value(page));
        Assert.Equal("true", await page.Locator("#note").GetAttributeAsync("aria-invalid"));

        await page.Keyboard.PressAsync("Backspace");
        Assert.Null(await page.Locator("#note").GetAttributeAsync("aria-invalid"));
        Assert.Equal("<p>012345678</p>", await Value(page));

        // A paste is cut at the limit, not refused whole.
        await Js.Run(page, "() => { const d = new DataTransfer(); d.setData('text/plain', 'WXYZ'); document.querySelector('.ql-editor').dispatchEvent(new ClipboardEvent('paste', { clipboardData: d, bubbles: true, cancelable: true })); }");
        Assert.Equal("<p>012345678W</p>", await Value(page));
        // The document the app sets is its own.
        await Js.Run(page, "() => sednaUi.editor.get('note').then(e => e.set('<p>a document longer than the limit</p>'))");
        Assert.Equal("<p>a document longer than the limit</p>", await Value(page));
    }

    [Fact]
    public async Task The_direction_format_and_a_full_screen_button_are_part_of_the_toolbar()
    {
        if (NoBrowser) return;
        var page = await Open(Editor("<p>Text</p>"));
        await page.Locator(".ql-editor p").ClickAsync();
        await page.Locator("#rtl").ClickAsync();
        Assert.Contains("ql-direction-rtl", await Value(page), StringComparison.Ordinal);
        Assert.Equal("true", await page.Locator("#rtl").GetAttributeAsync("aria-pressed"));

        // The full-screen button is one of the toolbar's stops: the arrows reach it, and it has one tab stop.
        await page.Keyboard.PressAsync("Alt+F10");
        await page.Keyboard.PressAsync("End");
        Assert.Equal("full", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => [...document.querySelectorAll('.editor-toolbar button')].filter(b => b.tabIndex === 0).length"));
    }

    [Fact]
    public async Task The_chosen_image_and_the_bar_are_readable_in_both_variants()
    {
        if (NoBrowser) return;
        foreach (var light in new[] { false, true })
        {
            var page = await Open(Editor(Picture), light);
            await page.Locator(".ql-editor img").ClickAsync();
            await Assertions.Expect(page.Locator("#bar")).ToBeVisibleAsync();
            var colours = await page.EvaluateAsync<string[]>("() => { const b = getComputedStyle(document.getElementById('bar')); const i = getComputedStyle(document.querySelector('.ql-editor img')); return [b.backgroundColor, b.borderTopColor, i.outlineStyle]; }");
            Assert.NotEqual("rgba(0, 0, 0, 0)", colours[0]);
            Assert.Equal("solid", colours[2]);
            await page.CloseAsync();
        }
    }
}
