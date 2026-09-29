using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The rich-text editor on a real page: the textarea it reads and writes, the app's toolbar driving
/// it, the link form, the keyboard, a form posting it, and the events a binding hears.
/// </summary>
/// <remarks>
/// Every keystroke and click is a real one at the element, so Quill's own handling is part of what is
/// tested — an editor that looks right and does not type is the failure that matters. The value is
/// read from the textarea, because that is what a form posts and what a binding reads.
/// </remarks>
public class EditorTests : ScriptTestBase
{
    private const string Toolbar = """
        <div class="editor-toolbar" role="toolbar" aria-label="Formatting">
          <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-action="undo" aria-label="Undo" id="undo"></button>
          <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-format="bold" aria-label="Bold" id="bold"></button>
          <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-format="italic" aria-label="Italic" id="italic"></button>
          <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-format="header" value="2" aria-label="Heading" id="h2"></button>
          <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-format="list" value="bullet" aria-label="List" id="ul"></button>
          <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-format="link" aria-label="Link" id="link"></button>
        </div>
        """;

    private const string LinkForm = """
        <div class="editor-link" data-editor-link hidden id="link-form">
          <input class="form-input" type="url" data-editor-link-url aria-label="Address" id="url" />
          <button class="btn btn-sm" type="button" data-editor-link-save id="save">Save</button>
          <button class="btn btn-ghost btn-sm" type="button" data-editor-link-remove id="remove">Remove</button>
        </div>
        """;

    private static string Editor(string value = "", string attrs = "data-editor-eager", string textarea = "",
        string toolbar = Toolbar, string link = LinkForm) => $"""
        <label for="note-value" id="note-label">Handover note</label>
        <div class="editor" data-editor id="note" {attrs}>
          {toolbar}
          <div class="editor-body prose" data-editor-body></div>
          {link}
          <textarea id="note-value" name="note" data-editor-value hidden placeholder="Write it here" {textarea}>{value}</textarea>
        </div>
        """;

    private async Task<IPage> OpenEditor(string body)
    {
        var page = await Open(body, head: StylesheetTag);
        await page.WaitForFunctionAsync("() => document.getElementById('note')?.dataset.editorState === 'ready'");
        return page;
    }

    private static Task<string> Value(IPage page) =>
        page.EvaluateAsync<string>("() => document.getElementById('note-value').value");

    private static Task<string?> Pressed(IPage page, string id) =>
        page.Locator("#" + id).GetAttributeAsync("aria-pressed");

    private static async Task TypeAtEnd(IPage page, string text)
    {
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync("Control+End");
        await page.Keyboard.TypeAsync(text);
    }

    // ── Loading and writing back ──────────────────────────────────────────────

    [Fact]
    public async Task It_loads_the_textarea_and_cuts_away_what_it_cannot_hold()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor(
            "&lt;p&gt;Hello &lt;strong&gt;world&lt;/strong&gt;&lt;/p&gt;&lt;script&gt;window.ran = true&lt;/script&gt;&lt;p style=\"color:red\" onclick=\"window.ran = true\"&gt;styled&lt;/p&gt;"));

        var html = await page.EvaluateAsync<string>("() => document.querySelector('.ql-editor').innerHTML");
        Assert.Equal("<p>Hello <strong>world</strong></p><p>window.ran = true</p><p>styled</p>", html);
        Assert.False(await page.EvaluateAsync<bool>("() => !!window.ran"));
    }

    [Fact]
    public async Task Typing_writes_the_html_back_to_the_textarea_with_the_events_a_textarea_sends()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("&lt;p&gt;Three lines reserved.&lt;/p&gt;") + """<button type="button" id="after">After</button>""");
        await page.EvaluateAsync("""
            () => {
                window.heard = [];
                const ta = document.getElementById('note-value');
                ta.addEventListener('input', () => heard.push('input'));
                ta.addEventListener('change', () => heard.push('change'));
                document.addEventListener('sedna-editor-change', e => heard.push('sedna:' + e.detail.length + ':' + e.detail.isEmpty));
            }
            """);

        await TypeAtEnd(page, " Two to go.");
        Assert.Equal("<p>Three lines reserved. Two to go.</p>", await Value(page));
        var heard = await page.EvaluateAsync<string[]>("() => heard");
        Assert.Contains("input", heard);
        Assert.Contains("sedna:32:false", heard);
        Assert.DoesNotContain("change", heard);

        // change, once, when the focus leaves after an edit — as a textarea sends it.
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForFunctionAsync("() => heard.includes('change')");
        Assert.Single(await page.EvaluateAsync<string[]>("() => heard.filter(h => h === 'change')"));
    }

    [Fact]
    public async Task A_value_written_to_the_textarea_from_outside_is_loaded_and_its_own_echo_is_not()
    {
        if (NoBrowser) return;
        // Blazor writes a bound textarea by setting its value; the editor follows it, and ignores a
        // value it wrote itself arriving back.
        var page = await OpenEditor(Editor());
        await page.EvaluateAsync("() => { document.getElementById('note-value').value = '<h2>From the app</h2><ul><li>one</li></ul>'; }");
        Assert.Equal("From the app\none\n", await page.EvaluateAsync<string>("() => sednaUi.editor.get('note').then(e => e.quill.getText())"));

        await TypeAtEnd(page, "!");
        var written = await Value(page);
        await page.EvaluateAsync("v => { document.getElementById('note-value').value = v; }", written);
        Assert.Equal(written, await Value(page));
        // Not loaded again: a load starts the history afresh, and the typing is still undoable.
        Assert.True(await page.EvaluateAsync<bool>("() => sednaUi.editor.get('note').then(e => e.quill.history.stack.undo.length > 0)"));
    }

    [Fact]
    public async Task An_empty_document_is_the_empty_string()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("&lt;p&gt;x&lt;/p&gt;"));
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.PressAsync("Backspace");
        Assert.Equal("", await Value(page));
        var state = await page.EvaluateAsync<JsonElement>("() => sednaUi.editor.get('note').then(e => e.state())");
        Assert.True(state.GetProperty("isEmpty").GetBoolean());
    }

    // ── The toolbar ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_format_button_toggles_its_format_and_shows_the_text_under_the_caret()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("&lt;p&gt;Hold two lines&lt;/p&gt;"));
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync("Control+End");
        await page.Keyboard.PressAsync("Shift+Home");

        await page.Locator("#bold").ClickAsync();
        Assert.Equal("true", await Pressed(page, "bold"));
        Assert.Equal("<p><strong>Hold two lines</strong></p>", await Value(page));
        // The pointer left the focus and the selection in the document.
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement.classList.contains('ql-editor')"));

        await page.Locator("#h2").ClickAsync();
        Assert.Equal("true", await Pressed(page, "h2"));
        Assert.Equal("<h2><strong>Hold two lines</strong></h2>", await Value(page));

        await page.Locator("#h2").ClickAsync();
        Assert.Equal("false", await Pressed(page, "h2"));
        await page.Locator("#ul").ClickAsync();
        Assert.Equal("<ul><li><strong>Hold two lines</strong></li></ul>", await Value(page));
    }

    [Fact]
    public async Task Only_the_formats_the_toolbar_offers_survive_a_paste()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor(toolbar: """
            <div class="editor-toolbar" role="toolbar" aria-label="Formatting">
              <button type="button" data-editor-format="bold" aria-label="Bold" id="bold"></button>
            </div>
            """, link: ""));
        await page.EvaluateAsync("() => sednaUi.editor.get('note').then(e => e.set('<h1>Title</h1><p><strong>bold</strong> and <em>italic</em> and <a href=\"https://example.com\">a link</a></p>'))");

        Assert.Equal("<p>Title</p><p><strong>bold</strong> and italic and a link</p>", await Value(page));
    }

    [Fact]
    public async Task Undo_is_disabled_until_there_is_something_to_undo()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("&lt;p&gt;A&lt;/p&gt;"));
        Assert.True(await page.Locator("#undo").IsDisabledAsync());

        await TypeAtEnd(page, "B");
        await Assertions.Expect(page.Locator("#undo")).ToBeEnabledAsync();
        await page.Locator("#undo").ClickAsync();
        Assert.Equal("<p>A</p>", await Value(page));
    }

    [Fact]
    public async Task The_toolbar_is_one_tab_stop_with_arrow_keys_and_alt_f10_reaches_it()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("&lt;p&gt;A&lt;/p&gt;"));
        // Undo is disabled and cannot hold the stop, so the first enabled button does.
        var stops = await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('.editor-toolbar button')].filter(b => b.tabIndex === 0).map(b => b.id)");
        Assert.Equal(["bold"], stops);

        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync("Alt+F10");
        Assert.Equal("bold", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("italic", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("End");
        Assert.Equal("link", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("Escape");
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement.classList.contains('ql-editor')"));
    }

    [Fact]
    public async Task Tab_leaves_the_document_as_it_leaves_a_textarea()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("&lt;p&gt;A&lt;/p&gt;") + """<button type="button" id="after">After</button>""");
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("after", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Equal("<p>A</p>", await Value(page));
    }

    // ── The link form ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Ctrl_k_opens_the_apps_link_form_and_enter_saves_the_address()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("&lt;p&gt;See the guide&lt;/p&gt;"));
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync("Control+End");
        for (var i = 0; i < 5; i++) await page.Keyboard.PressAsync("Shift+ArrowLeft");

        await page.Keyboard.PressAsync("Control+k");
        await Assertions.Expect(page.Locator("#link-form")).ToBeVisibleAsync();
        Assert.Equal("url", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await Assertions.Expect(page.Locator("#remove")).ToBeHiddenAsync();

        await page.Locator("#url").FillAsync("https://example.com/guide");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("#link-form")).ToBeHiddenAsync();
        Assert.Contains("<a href=\"https://example.com/guide\"", await Value(page), StringComparison.Ordinal);
        Assert.Contains(">guide</a>", await Value(page), StringComparison.Ordinal);
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement.classList.contains('ql-editor')"));

        // Opened again inside the link: the address is there, and Remove takes the link away.
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Locator("#link").ClickAsync();
        await Assertions.Expect(page.Locator("#url")).ToHaveValueAsync("https://example.com/guide");
        await page.Locator("#remove").ClickAsync();
        Assert.DoesNotContain("<a ", await Value(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ctrl_k_in_the_document_is_the_link_form_not_the_command_palette()
    {
        if (NoBrowser) return;
        // The palette's shortcut is global; a key the focused editor has already handled is not its.
        var page = await OpenEditor(Editor("&lt;p&gt;See the guide&lt;/p&gt;") + """
            <dialog class="palette" data-palette aria-label="Commands" id="palette">
              <input class="palette-input" type="text" aria-label="Find a command">
              <ul class="palette-list" aria-label="Commands"></ul>
              <template data-palette-item><li role="presentation"><div class="palette-item" role="option"><span data-label></span></div></li></template>
            </dialog>
            """);
        await page.EvaluateAsync("() => sednaUi.palette.register([{ label: 'Open the queue', run: () => {} }])");

        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync("Control+k");
        await Assertions.Expect(page.Locator("#link-form")).ToBeVisibleAsync();
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('palette').open"));

        // Anywhere else on the page the palette keeps its key.
        await page.Keyboard.PressAsync("Escape");
        await page.Locator("#note-label").ClickAsync(new() { Position = new() { X = 1, Y = 1 } });
        await page.EvaluateAsync("() => document.activeElement.blur()");
        await page.Keyboard.PressAsync("Control+k");
        await page.WaitForFunctionAsync("() => document.getElementById('palette').open");
    }

    [Fact]
    public async Task An_address_quill_does_not_accept_is_not_kept()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("&lt;p&gt;x&lt;/p&gt;"));
        await page.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.PressAsync("Control+k");
        await page.Locator("#url").FillAsync("javascript:alert(1)");
        await page.Keyboard.PressAsync("Enter");
        Assert.DoesNotContain("javascript:", await Value(page), StringComparison.Ordinal);
    }

    // ── The textarea's own state ──────────────────────────────────────────────

    [Fact]
    public async Task The_document_takes_the_textareas_label_placeholder_and_state()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor(textarea: "aria-describedby=\"hint\"") + """<span id="hint">Two sentences.</span>""");
        var a11y = await page.EvaluateAsync<string[]>("""
            () => { const r = document.querySelector('.ql-editor');
                    return [r.getAttribute('role'), r.getAttribute('aria-multiline'), r.getAttribute('aria-labelledby'),
                            r.getAttribute('aria-describedby'), r.dataset.placeholder, r.getAttribute('aria-readonly')]; }
            """);
        Assert.Equal(["textbox", "true", "note-label", "hint", "Write it here", "false"], a11y);

        await page.EvaluateAsync("() => { document.getElementById('note-value').disabled = true; }");
        await page.WaitForFunctionAsync("() => document.querySelector('.ql-editor').getAttribute('aria-readonly') === 'true'");
        Assert.Equal("false", await page.EvaluateAsync<string>("() => document.querySelector('.ql-editor').getAttribute('contenteditable')"));
        Assert.True(await page.Locator("#undo").IsDisabledAsync());
    }

    // ── A form ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_form_posts_the_document_under_the_textareas_name()
    {
        if (NoBrowser) return;
        var page = await OpenEditor($"""<form method="post" action="/notes" id="f">{Editor("&lt;p&gt;Hi&lt;/p&gt;")}<button type="submit" id="send">Send</button></form>""");
        await TypeAtEnd(page, " there");
        var posted = page.WaitForRequestAsync(r => r.Method == "POST" && r.Url.EndsWith("/notes", StringComparison.Ordinal));
        await page.Locator("#send").ClickAsync();
        var body = Uri.UnescapeDataString(((await posted).PostData ?? "").Replace('+', ' '));
        Assert.Equal("note=<p>Hi there</p>", body);
    }

    [Fact]
    public async Task A_required_editor_that_is_empty_stops_the_submit_and_takes_the_focus()
    {
        if (NoBrowser) return;
        var page = await OpenEditor($"""<form method="post" action="/notes" id="f">{Editor(textarea: "required")}<button type="submit" id="send">Send</button></form>""");
        Assert.Equal("true", await page.EvaluateAsync<string>("() => document.querySelector('.ql-editor').getAttribute('aria-required')"));

        await page.Locator("#send").ClickAsync();
        Assert.Equal("true", await page.Locator("#note").GetAttributeAsync("aria-invalid"));
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement.classList.contains('ql-editor')"));
        Assert.DoesNotContain(Requests, r => r.Method == "POST");

        await page.Keyboard.TypeAsync("Now it has one.");
        Assert.Null(await page.Locator("#note").GetAttributeAsync("aria-invalid"));
    }

    [Fact]
    public async Task A_form_reset_puts_the_document_back()
    {
        if (NoBrowser) return;
        var page = await OpenEditor($"""<form id="f">{Editor("&lt;p&gt;Start&lt;/p&gt;")}<button type="reset" id="reset">Reset</button></form>""");
        await TypeAtEnd(page, " and more");
        await page.Locator("#reset").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.ql-editor').textContent === 'Start'");
    }

    // ── Loading ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_page_without_an_editor_never_fetches_the_engine()
    {
        if (NoBrowser) return;
        var page = await Open("<p>No editor here.</p>", head: StylesheetTag);
        await page.WaitForTimeoutAsync(300);
        Assert.DoesNotContain(Requests, r => r.Url.Contains("/lib/quill/", StringComparison.Ordinal) || r.Url.EndsWith("Sedna.UI.editor.js", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Markup_without_a_textarea_is_an_error_state_not_an_exception_on_the_page()
    {
        if (NoBrowser) return;
        var page = await Open("""<div class="editor" data-editor data-editor-eager id="broken"><div class="editor-body"></div></div>""", head: StylesheetTag);
        await page.WaitForFunctionAsync("() => document.getElementById('broken').dataset.editorState === 'error'");
    }

    [Fact]
    public async Task An_id_with_no_editor_answers_null()
    {
        if (NoBrowser) return;
        var page = await Open("<p>No editor here.</p>", head: StylesheetTag);
        Assert.True(await page.EvaluateAsync<bool>("() => sednaUi.editor.invoke('nothing', 'state').then(r => r === null)"));
    }
}
