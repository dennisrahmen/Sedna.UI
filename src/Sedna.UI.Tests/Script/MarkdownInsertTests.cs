using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.md.insert</c> — text the app supplies, typed at the reader's caret in a
/// <c>.md-editor</c>'s textarea.
/// </summary>
/// <remarks>
/// The usual caller has just closed a dialog it opened from the editor's toolbar, so the
/// textarea is not focused when the call arrives. The last test is that case, with a real
/// modal <c>&lt;dialog&gt;</c> taking focus in between.
/// </remarks>
public class MarkdownInsertTests : ScriptTestBase
{
    private const string Fixture = """
        <div class="md-editor" data-md-view="write">
            <div class="md-toolbar">
                <button class="md-tool" type="button" id="pick">Link</button>
            </div>
            <textarea class="form-input md-input" data-md-input id="ta">See the order. Thanks.</textarea>
            <div class="markdown-body md-preview" data-md-preview id="preview"></div>
        </div>
        <dialog id="picker"><form method="dialog"><button id="choose" value="x">Choose</button></form></dialog>
        """;

    [Fact]
    public async Task The_selection_is_replaced_and_the_caret_lands_after_the_text()
    {
        if (NoBrowser) return;
        var page = await OpenEditor();

        // "the order" selected.
        await page.EvaluateAsync("() => { const ta = document.getElementById('ta'); ta.focus(); ta.setSelectionRange(4, 13); }");
        await Insert(page, "[ORD-4204](https://example.com/orders/ORD-4204)");

        var (value, start, end) = await Read(page);
        Assert.Equal("See [ORD-4204](https://example.com/orders/ORD-4204). Thanks.", value);
        var after = 4 + "[ORD-4204](https://example.com/orders/ORD-4204)".Length;
        Assert.Equal(after, start);
        Assert.Equal(after, end);
    }

    [Fact]
    public async Task A_collapsed_caret_inserts_without_removing_anything()
    {
        if (NoBrowser) return;
        var page = await OpenEditor();

        await page.EvaluateAsync("() => { const ta = document.getElementById('ta'); ta.focus(); ta.setSelectionRange(3, 3); }");
        await Insert(page, " all");

        var (value, start, end) = await Read(page);
        Assert.Equal("See all the order. Thanks.", value);
        Assert.Equal(7, start);
        Assert.Equal(7, end);
    }

    [Fact]
    public async Task One_bubbling_input_event_reaches_the_binding_and_the_preview()
    {
        if (NoBrowser) return;
        var page = await OpenEditor();

        // Listening on the document proves the event bubbles, which is what Blazor's
        // delegated @bind:event="oninput" needs.
        await page.EvaluateAsync("""
            () => {
                window.inputs = [];
                document.addEventListener('input', e => window.inputs.push(e.target.id));
                const ta = document.getElementById('ta'); ta.focus(); ta.setSelectionRange(0, 0);
            }
            """);
        await Insert(page, "**Urgent** ");

        Assert.Equal(["ta"], await page.EvaluateAsync<string[]>("() => window.inputs"));
        Assert.Equal("Urgent",
            await page.EvaluateAsync<string>("() => document.querySelector('#preview strong').textContent"));
    }

    [Fact]
    public async Task Focus_returns_to_the_textarea()
    {
        if (NoBrowser) return;
        var page = await OpenEditor();

        await page.EvaluateAsync("() => document.getElementById('ta').setSelectionRange(3, 3)");
        await page.FocusAsync("#pick");
        await Insert(page, "!");

        Assert.Equal("ta", await page.EvaluateAsync<string>("() => document.activeElement.id"));
    }

    [Fact]
    public async Task After_a_dialog_has_had_focus_the_text_lands_where_the_caret_was()
    {
        if (NoBrowser) return;
        var page = await OpenEditor();

        // The reader puts the caret after "order", then presses the toolbar button, which
        // opens a modal picker; the picker closes; only then does the app insert. Nothing
        // in the script remembers the caret — the textarea keeps its own selection across
        // the blur, and this pins that it still does.
        await page.ClickAsync("#ta");
        await page.EvaluateAsync("() => document.getElementById('ta').setSelectionRange(13, 13)");
        await page.ClickAsync("#pick");
        await page.EvaluateAsync("() => document.getElementById('picker').showModal()");
        Assert.Equal("choose", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.ClickAsync("#choose");
        Assert.NotEqual("ta", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        await Insert(page, " ORD-4204");

        var (value, start, _) = await Read(page);
        Assert.Equal("See the order ORD-4204. Thanks.", value);
        Assert.Equal(22, start);
    }

    [Fact]
    public async Task Assigning_the_same_value_while_blurred_keeps_the_caret()
    {
        if (NoBrowser) return;
        var page = await OpenEditor();

        // What a Blazor re-render does after the input event: it writes the bound value
        // back, which is the value the textarea already holds. The platform moves the
        // caret only for a different value, so the next insert still lands in place.
        await page.EvaluateAsync("""
            () => {
                const ta = document.getElementById('ta');
                ta.focus(); ta.setSelectionRange(3, 3);
                document.getElementById('pick').focus();
                ta.value = ta.value;
            }
            """);
        await Insert(page, ",");

        Assert.Equal("See, the order. Thanks.", (await Read(page)).Value);
    }

    private async Task<IPage> OpenEditor()
    {
        var page = await Open(Fixture);
        await page.EvaluateAsync("() => sednaUi.md.init()");
        return page;
    }

    /// <summary>Calls <c>insert</c> with the text as an argument, so it arrives exactly as written.</summary>
    private static Task Insert(IPage page, string text) =>
        page.EvaluateAsync("t => sednaUi.md.insert(document.getElementById('ta'), t)", text);

    private static async Task<(string Value, int Start, int End)> Read(IPage page)
    {
        var r = await page.EvaluateAsync<Selection>(
            "() => { const ta = document.getElementById('ta'); return { value: ta.value, start: ta.selectionStart, end: ta.selectionEnd }; }");
        return (r.Value, r.Start, r.End);
    }

    private sealed class Selection
    {
        public string Value { get; set; } = "";
        public int Start { get; set; }
        public int End { get; set; }
    }
}
