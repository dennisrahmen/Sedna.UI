using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-editor-inline-images</c>: an editor with nowhere to upload to keeps a pasted or dropped
/// raster picture in the document as a <c>data:</c> address — after the app has been offered it
/// and let it go — and keeps one in the HTML it loads. Never SVG, never over the cap, never in an
/// editor that did not opt in or does not take images.
/// </summary>
public class EditorInlineImageTests : ScriptTestBase
{
    // An 8x8 white PNG.
    private const string Png =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAD0lEQVR4nGP4jwMwDC0JALoev0Ewkwr8AAAAAElFTkSuQmCC";

    private static string Editor(string attrs, string value = "") => $"""
        <div class="editor" data-editor id="note" data-editor-eager {attrs}>
          <div class="editor-toolbar" role="toolbar" aria-label="Formatting"><button type="button" data-editor-format="image" aria-label="Image"></button></div>
          <div class="editor-body prose" data-editor-body></div>
          <p class="editor-notice form-warning" role="alert" data-editor-file-notice hidden>That picture cannot be kept.</p>
          <textarea id="note-value" data-editor-value hidden aria-label="Description">{value}</textarea>
        </div>
        """;

    private async Task<IPage> OpenEditor(string body)
    {
        var page = await Open(body, head: StylesheetTag);
        await page.WaitForFunctionAsync("() => document.getElementById('note')?.dataset.editorState === 'ready'");
        return page;
    }

    private static Task Paste(IPage page, string type, int bytes = 0) => Js.Run(page, $$"""
        async () => {
            const q = document.querySelector('.ql-editor');
            q.focus();
            const blob = {{(bytes > 0 ? $"new Uint8Array({bytes})" : "await (await fetch('" + Png + "')).arrayBuffer()")}};
            const data = new DataTransfer();
            data.items.add(new File([blob], 'screenshot', { type: '{{type}}' }));
            q.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
        }
        """);

    private static Task<string> Value(IPage page) => page.EvaluateAsync<string>("() => document.getElementById('note-value').value");

    [Fact]
    public async Task A_pasted_screenshot_nobody_takes_is_kept_in_the_document()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("data-editor-inline-images"));
        await Js.Run(page, "() => { window.offered = 0; document.addEventListener('sedna-editor-file', () => window.offered++); }");
        await Paste(page, "image/png");
        await page.WaitForFunctionAsync("() => document.getElementById('note-value').value.includes('data:image/png;base64,')");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.offered"));
        Assert.True(await page.Locator("[data-editor-file-notice]").IsHiddenAsync());
    }

    [Fact]
    public async Task A_file_the_app_takes_is_not_inlined()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("data-editor-inline-images"));
        await Js.Run(page, "() => document.addEventListener('sedna-editor-file', e => e.preventDefault())");
        await Paste(page, "image/png");
        await Js.Frames(page, 10);
        Assert.DoesNotContain("<img", await Value(page), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("image/svg+xml", 0, "")]
    [InlineData("image/png", 4000, "data-editor-inline-max=\"1000\"")]
    public async Task An_svg_or_a_picture_over_the_cap_shows_the_notice_and_is_not_kept(string type, int bytes, string cap)
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("data-editor-inline-images " + cap));
        await Paste(page, type, bytes);
        await Assertions.Expect(page.Locator("[data-editor-file-notice]")).ToBeVisibleAsync();
        Assert.DoesNotContain("<img", await Value(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loaded_html_keeps_a_raster_inline_picture_and_drops_an_svg_one()
    {
        if (NoBrowser) return;
        var html = $"&lt;p&gt;a&lt;img src=\"{Png}\"&gt;b&lt;img src=\"data:image/svg+xml;base64,PHN2Zy8+\"&gt;c&lt;/p&gt;";
        var page = await OpenEditor(Editor("data-editor-inline-images", html));
        var value = await page.EvaluateAsync<string>("() => sednaUi.editor.get('note').then(h => h.html())");
        Assert.Contains("data:image/png;base64,", value, StringComparison.Ordinal);
        Assert.DoesNotContain("svg", value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_the_opt_in_an_inline_picture_is_dropped_everywhere()
    {
        if (NoBrowser) return;
        var html = $"&lt;p&gt;a&lt;img src=\"{Png}\"&gt;b&lt;/p&gt;";
        var page = await OpenEditor(Editor("", html));
        Assert.DoesNotContain("data:", await page.EvaluateAsync<string>("() => sednaUi.editor.get('note').then(h => h.html())"), StringComparison.Ordinal);
        Assert.Null(await page.EvaluateAsync<object?>($"() => sednaUi.editor.get('note').then(h => h.insertImage('{Png}', 'A screenshot'))"));
        await Paste(page, "image/png");
        await Js.Frames(page, 10);
        Assert.DoesNotContain("data:", await page.EvaluateAsync<string>("() => sednaUi.editor.get('note').then(h => h.html())"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InsertImage_takes_an_inline_picture_where_the_editor_opted_in()
    {
        if (NoBrowser) return;
        var page = await OpenEditor(Editor("data-editor-inline-images"));
        await Js.Run(page, $"() => sednaUi.editor.get('note').then(h => h.insertImage('{Png}', 'A screenshot'))");
        await page.WaitForFunctionAsync("() => document.getElementById('note-value').value.includes('data:image/png;base64,')");
    }
}
