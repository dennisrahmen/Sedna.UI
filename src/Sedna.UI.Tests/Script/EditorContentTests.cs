using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What a document may hold beyond text — colours, tables, a pasted file — and a value set
/// from outside that the editor once held.
/// </summary>
public class EditorContentTests : ScriptTestBase
{
    private static string Editor(string value, string toolbar, string attrs = "data-editor-eager") => $"""
        <div class="editor" data-editor id="note" {attrs}>
          <div class="editor-toolbar" role="toolbar" aria-label="Formatting">{toolbar}</div>
          <div class="editor-body prose" data-editor-body></div>
          <textarea id="note-value" data-editor-value hidden aria-label="Note">{value}</textarea>
        </div>
        """;

    private async Task<IPage> OpenEditor(string body)
    {
        var page = await Open(body, head: StylesheetTag);
        await page.WaitForFunctionAsync("() => document.getElementById('note')?.dataset.editorState === 'ready'");
        return page;
    }

    private static Task<string> Html(IPage page) =>
        page.EvaluateAsync<string>("() => sednaUi.editor.get('note').then(h => h.html())");

    [Fact]
    public async Task A_value_set_from_outside_loads_even_when_the_editor_held_it_before()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("", """<button type="button" data-editor-format="bold" aria-label="Bold"></button>"""));

        await Js.Run(page, "() => sednaUi.editor.get('note').then(h => h.set('<p>text</p>'))");
        await page.WaitForFunctionAsync("() => document.getElementById('note-value').value === '<p>text</p>'");

        // A form reset after sending: the textarea is emptied from outside.
        await Js.Run(page, "() => { document.getElementById('note-value').value = ''; }");

        Assert.Equal("", await Html(page));
    }

    [Fact]
    public async Task A_stored_colour_survives_an_edit_where_the_editor_takes_colours()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor(
            "&lt;p&gt;&lt;span style=\"color: rgb(204, 0, 0);\"&gt;Overdue&lt;/span&gt; orders&lt;/p&gt;",
            """<input type="color" data-editor-format="color" aria-label="Text colour">"""));

        Assert.Contains("color: rgb(204, 0, 0)", await Html(page), StringComparison.Ordinal);

        // Colouring a selection from the app's own colour field.
        await Js.Run(page, "() => sednaUi.editor.get('note').then(h => h.quill.setSelection(13, 6))");
        await Js.Run(page, """
            () => { const c = document.querySelector('[data-editor-format=color]'); c.value = '#0055aa'; c.dispatchEvent(new Event('change', { bubbles: true })); }
            """);
        var html = await Html(page);
        Assert.Contains("color: rgb(204, 0, 0)", html, StringComparison.Ordinal);
        Assert.Contains("rgb(0, 85, 170)", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_colour_control_a_stored_colour_is_dropped_as_before()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor(
            "&lt;p&gt;&lt;span style=\"color: rgb(204, 0, 0);\"&gt;Overdue&lt;/span&gt;&lt;/p&gt;",
            """<button type="button" data-editor-format="bold" aria-label="Bold"></button>"""));

        Assert.Equal("<p>Overdue</p>", await Html(page));
    }

    [Fact]
    public async Task A_stored_table_survives_and_the_app_adds_a_row_to_it()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor(
            "&lt;table&gt;&lt;tbody&gt;&lt;tr&gt;&lt;td&gt;Site&lt;/td&gt;&lt;td&gt;Orders&lt;/td&gt;&lt;/tr&gt;&lt;tr&gt;&lt;td&gt;North&lt;/td&gt;&lt;td&gt;12&lt;/td&gt;&lt;/tr&gt;&lt;/tbody&gt;&lt;/table&gt;",
            """
            <button type="button" id="ins" data-editor-action="table-insert" aria-label="Insert table"></button>
            <button type="button" id="row" data-editor-action="table-row-below" aria-label="Add a row"></button>
            """));

        var cells = await page.EvaluateAsync<int>("() => document.querySelectorAll('.ql-editor td').length");
        Assert.Equal(4, cells);

        await Js.Run(page, "() => sednaUi.editor.get('note').then(h => h.quill.setSelection(0, 0))");
        await page.Locator("#row").ClickAsync();
        Assert.Equal(6, await page.EvaluateAsync<int>("() => document.querySelectorAll('.ql-editor td').length"));
        Assert.Contains("<table", await page.EvaluateAsync<string>("() => document.getElementById('note-value').value"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_tables_a_stored_table_reads_as_its_text()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor(
            "&lt;table&gt;&lt;tbody&gt;&lt;tr&gt;&lt;td&gt;Site&lt;/td&gt;&lt;/tr&gt;&lt;/tbody&gt;&lt;/table&gt;",
            """<button type="button" data-editor-format="bold" aria-label="Bold"></button>"""));

        Assert.DoesNotContain("<table", await Html(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pasted_file_is_announced_and_can_be_read_by_its_id()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("", """<button type="button" data-editor-format="image" aria-label="Image"></button>"""));
        await Js.Run(page, "() => { window.arrived = null; document.addEventListener('sedna-editor-file', e => window.arrived = e.detail); }");

        await Js.Run(page, """
            () => {
                const q = document.querySelector('.ql-editor');
                q.focus();
                const data = new DataTransfer();
                data.items.add(new File([new Uint8Array([137, 80, 78, 71])], 'screenshot.png', { type: 'image/png' }));
                q.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
            }
            """);
        await page.WaitForFunctionAsync("() => window.arrived !== null");

        var arrived = await page.EvaluateAsync<JsonElement>("() => window.arrived");
        var file = arrived.GetProperty("files")[0];
        Assert.Equal("screenshot.png", file.GetProperty("name").GetString());
        Assert.Equal("image/png", file.GetProperty("type").GetString());
        Assert.Equal(4, file.GetProperty("size").GetInt32());

        var size = await page.EvaluateAsync<int>("id => sednaUi.editor.get('note').then(h => h.file(id).size)", file.GetProperty("id").GetString());
        Assert.Equal(4, size);
        // Nothing went into the document: uploading is the app's.
        Assert.Equal("", await Html(page));
    }

    // ── A file nobody takes ─────────────────────────────────────────────────────

    private const string Notice = """
        <p class="editor-notice form-warning" role="alert" data-editor-file-notice hidden>Pictures cannot be pasted here.</p>
        """;

    private static string WithNotice(string toolbar) =>
        Editor("", toolbar).Replace("<textarea", Notice + "<textarea", StringComparison.Ordinal);

    private const string ImageButton = """<button type="button" data-editor-format="image" aria-label="Image"></button>""";

    private static Task Paste(IPage page, string name, string type) => Js.Run(page, $$"""
        () => {
            const q = document.querySelector('.ql-editor');
            q.focus();
            const data = new DataTransfer();
            data.items.add(new File([new Uint8Array([137, 80, 78, 71])], '{{name}}', { type: '{{type}}' }));
            q.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
        }
        """);

    [Fact]
    public async Task A_pasted_picture_nobody_takes_shows_the_apps_notice_until_the_reader_types()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(WithNotice(ImageButton));
        var notice = page.Locator("[data-editor-file-notice]");
        await Assertions.Expect(notice).ToBeHiddenAsync();

        await Paste(page, "screenshot.png", "image/png");
        await Assertions.Expect(notice).ToBeVisibleAsync();
        Assert.Equal("", await Html(page));

        await page.Keyboard.TypeAsync("x");
        await Assertions.Expect(notice).ToBeHiddenAsync();
    }

    [Fact]
    public async Task A_picture_the_app_takes_shows_no_notice()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(WithNotice(ImageButton));
        await Js.Run(page, "() => { window.arrived = null; document.addEventListener('sedna-editor-file', e => { e.preventDefault(); window.arrived = e.detail; }); }");

        await Paste(page, "screenshot.png", "image/png");
        await page.WaitForFunctionAsync("() => window.arrived !== null");
        await Js.Frames(page);

        await Assertions.Expect(page.Locator("[data-editor-file-notice]")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task A_file_the_editor_cannot_hold_shows_the_notice_and_sends_no_event()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(WithNotice(ImageButton));
        await Js.Run(page, "() => { window.arrived = 0; document.addEventListener('sedna-editor-file', e => { e.preventDefault(); window.arrived++; }); }");

        await Paste(page, "minutes.pdf", "application/pdf");

        await Assertions.Expect(page.Locator("[data-editor-file-notice]")).ToBeVisibleAsync();
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.arrived"));
    }

    [Fact]
    public async Task A_dropped_picture_nobody_takes_shows_the_notice()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(WithNotice(ImageButton));
        await Js.Run(page, "() => sednaUi.editor.get('note').then(h => h.set('<p>Delivery note</p>'))");

        await Js.Run(page, """
            () => {
                const p = document.querySelector('.ql-editor p');
                const r = p.getBoundingClientRect();
                const data = new DataTransfer();
                data.items.add(new File([new Uint8Array([255, 216, 255])], 'pallet.jpg', { type: 'image/jpeg' }));
                p.dispatchEvent(new DragEvent('drop', { dataTransfer: data, bubbles: true, cancelable: true, clientX: r.left + 4, clientY: r.top + r.height / 2 }));
            }
            """);

        await Assertions.Expect(page.Locator("[data-editor-file-notice]")).ToBeVisibleAsync();
        Assert.Equal("<p>Delivery note</p>", await Html(page));
    }
}
