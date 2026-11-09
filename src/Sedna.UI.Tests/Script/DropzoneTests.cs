using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.dropzone</c>: a <c>data-dropzone</c> zone's highlight and the file a drop hands its
/// input, and the whole window as the target behind the app's <c>data-dropzone="page"</c> overlay.
/// </summary>
/// <remarks>
/// A drag from the desktop cannot be driven from a test, so the events are the ones the browser
/// would send, carrying a real <c>DataTransfer</c> with a real <c>File</c> in it: what is under test
/// is what the script does with them.
/// </remarks>
public class DropzoneTests : ScriptTestBase
{
    private const string Helpers = """
        <script>
          window.heard = [];
          document.addEventListener('change', e => heard.push(e.target.id + ':' + Array.from(e.target.files).map(f => f.name).join(',')));
          // A drag event as the browser sends it: files, or a drag from inside the page.
          window.drag = (type, selector, files) => {
            const data = new DataTransfer();
            if (files === undefined) files = ['note.txt'];
            if (files) for (const name of files) data.items.add(new File(['x'], name, { type: 'text/plain' }));
            else data.setData('text/plain', 'a row');
            const target = typeof selector === 'string' ? document.querySelector(selector) : selector;
            const ev = new DragEvent(type, { bubbles: true, cancelable: true, dataTransfer: data });
            target.dispatchEvent(ev);
            return ev.defaultPrevented;
          };
        </script>
        """;

    private const string Zone = """
        <label class="dropzone" data-dropzone id="zone">
          <input type="file" id="zone-input" class="visually-hidden" />
          <i class="ri-upload-cloud-2-line" id="icon"></i>
          <span id="label">Drop files here, or click to choose</span>
        </label>
        """;

    private const string PageTarget = """
        <main id="main" style="height:600px">
          <p id="text">The order</p>
          <input type="file" id="attach" multiple />
        </main>
        <div class="dropzone dropzone--page" data-dropzone="page" data-dropzone-input="attach" id="overlay" aria-hidden="true">
          <span>Drop to attach</span>
        </div>
        """;

    private async Task<IPage> OpenDrop(string body)
    {
        var (page, errors) = await OpenStyled(body + Helpers);
        Assert.Empty(errors);
        return page;
    }

    private static Task<bool> Over(IPage page, string id) =>
        page.EvaluateAsync<bool>($"() => document.getElementById('{id}').classList.contains('dropzone--over')");

    private static Task<string[]> Heard(IPage page) => page.EvaluateAsync<string[]>("() => heard");

    private static Task<bool> Drag(IPage page, string type, string selector, string[]? files = null, bool noFiles = false) =>
        page.EvaluateAsync<bool>("([t, s, f]) => drag(t, s, f)", new object?[] { type, selector, noFiles ? false : files ?? new[] { "note.txt" } });

    // ── An ordinary zone ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_zone_stays_lit_while_the_drag_moves_between_its_children()
    {
        if (NoBrowser) return;
        var page = await OpenDrop(Zone);

        Assert.True(await Drag(page, "dragenter", "#icon"));
        Assert.True(await Over(page, "zone"));
        // Onto the label: its enter comes before the icon's leave.
        await Drag(page, "dragenter", "#label");
        await Drag(page, "dragleave", "#icon");
        Assert.True(await Over(page, "zone"));

        await Drag(page, "dragleave", "#label");
        Assert.False(await Over(page, "zone"));
    }

    [Fact]
    public async Task A_zone_takes_the_drop_into_its_own_input_as_a_change()
    {
        if (NoBrowser) return;
        var page = await OpenDrop(Zone);

        Assert.True(await Drag(page, "dragover", "#label"));   // or the browser opens the file
        await Drag(page, "dragenter", "#label");
        Assert.True(await Drag(page, "drop", "#label", ["first.pdf", "second.pdf"]));

        Assert.False(await Over(page, "zone"));
        // A single input takes one file of two.
        Assert.Equal(["zone-input:first.pdf"], await Heard(page));
    }

    [Fact]
    public async Task Reset_clears_a_zone_left_lit()
    {
        if (NoBrowser) return;
        var page = await OpenDrop(Zone);
        await Drag(page, "dragenter", "#label");
        Assert.True(await Over(page, "zone"));

        await Js.Run(page, "() => sednaUi.dropzone.reset()");
        Assert.False(await Over(page, "zone"));
    }

    // ── The whole page ────────────────────────────────────────────────────────

    private static Task<string> Display(IPage page) =>
        page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('overlay')).display");

    [Fact]
    public async Task Files_dragged_over_the_window_show_the_apps_overlay_and_leaving_hides_it()
    {
        if (NoBrowser) return;
        var page = await OpenDrop(PageTarget);
        Assert.Equal("none", await Display(page));

        Assert.True(await Drag(page, "dragenter", "#main"));
        Assert.True(await Over(page, "overlay"));
        Assert.Equal("flex", await Display(page));
        Assert.Equal("fixed", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('overlay')).position"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('overlay')).pointerEvents"));

        // Moving onto a child keeps it; leaving the last element hides it.
        await Drag(page, "dragenter", "#text");
        await Drag(page, "dragleave", "#main");
        Assert.True(await Over(page, "overlay"));
        await Drag(page, "dragleave", "#text");
        Assert.False(await Over(page, "overlay"));
        Assert.Equal("none", await Display(page));
    }

    [Fact]
    public async Task A_drag_with_no_files_in_it_does_not_show_the_overlay()
    {
        if (NoBrowser) return;
        var page = await OpenDrop(PageTarget);

        Assert.False(await Drag(page, "dragenter", "#main", noFiles: true));
        Assert.False(await Drag(page, "dragover", "#main", noFiles: true));
        Assert.False(await Over(page, "overlay"));
    }

    [Fact]
    public async Task Escape_and_the_end_of_the_drag_hide_the_overlay()
    {
        if (NoBrowser) return;
        var page = await OpenDrop(PageTarget);

        await Drag(page, "dragenter", "#main");
        Assert.True(await Over(page, "overlay"));
        await page.Keyboard.PressAsync("Escape");
        Assert.False(await Over(page, "overlay"));

        await Drag(page, "dragenter", "#main");
        Assert.True(await Over(page, "overlay"));
        await Drag(page, "dragend", "#main");
        Assert.False(await Over(page, "overlay"));

        // And the count starts again from nothing: one enter shows it, one leave hides it.
        await Drag(page, "dragenter", "#main");
        await Drag(page, "dragleave", "#main");
        Assert.False(await Over(page, "overlay"));
    }

    [Fact]
    public async Task A_drop_anywhere_goes_to_the_named_input_as_a_change()
    {
        if (NoBrowser) return;
        var page = await OpenDrop(PageTarget);

        await Drag(page, "dragenter", "#text");
        Assert.True(await Drag(page, "dragover", "#text"));
        Assert.True(await Drag(page, "drop", "#text", ["dock-1.jpg", "dock-2.jpg"]));

        Assert.False(await Over(page, "overlay"));
        Assert.Equal(["attach:dock-1.jpg,dock-2.jpg"], await Heard(page));
    }

    [Fact]
    public async Task Without_a_named_input_the_overlays_own_takes_the_drop()
    {
        if (NoBrowser) return;
        var page = await OpenDrop("""
            <p id="text">The order</p>
            <div class="dropzone dropzone--page" data-dropzone="page" id="overlay" aria-hidden="true">
              <input type="file" id="own" multiple hidden />
              <span>Drop to attach</span>
            </div>
            """);

        await Drag(page, "dragenter", "#text");
        await Drag(page, "drop", "#text");
        Assert.Equal(["own:note.txt"], await Heard(page));
    }

    [Fact]
    public async Task An_ordinary_zone_under_the_overlay_keeps_its_own_drop()
    {
        if (NoBrowser) return;
        var page = await OpenDrop(PageTarget + Zone);

        await Drag(page, "dragenter", "#label");
        Assert.True(await Over(page, "zone"));
        Assert.True(await Over(page, "overlay"));

        Assert.True(await Drag(page, "drop", "#label"));
        Assert.False(await Over(page, "zone"));
        Assert.False(await Over(page, "overlay"));
        Assert.Equal(["zone-input:note.txt"], await Heard(page));
    }

    [Fact]
    public async Task The_overlay_mirrors_nothing_and_covers_the_window_inside_its_margin_in_rtl()
    {
        if (NoBrowser) return;
        var page = await OpenDrop("<div dir=\"rtl\">" + PageTarget + "</div>");

        await Drag(page, "dragenter", "#main");
        var box = await page.EvaluateAsync<double[]>("""
            () => { const r = document.getElementById('overlay').getBoundingClientRect();
                    return [r.left, innerWidth - r.right, r.top, innerHeight - r.bottom]; }
            """);
        Assert.Equal(box[0], box[1], 0.5);
        Assert.Equal(box[2], box[3], 0.5);
        Assert.True(box[0] > 0, "The overlay keeps a margin from the window's edge.");
    }
}
