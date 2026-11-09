using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>[data-selection-toolbar]</c>: selecting text opens the app's toolbar over the selection,
/// below it where there is no room above, inside the viewport, and an action reports what
/// was selected and where.
/// </summary>
public class SelectionToolbarTests : ScriptTestBase
{
    private static string Fixture(string style = "padding-top:200px", string dir = "ltr") => $$"""
        <div dir="{{dir}}" style="{{style}}">
          <div class="chat" id="thread" data-selection-toolbar="bar" style="width:600px">
            <div class="chat-message" data-selection-item="m-1">
              <div class="chat-body"><div class="chat-bubble"><p id="p1">The nightly sync stopped at 02:14 when the pool was full.</p></div></div>
            </div>
            <div class="chat-message" data-selection-item="m-2">
              <div class="chat-body"><div class="chat-bubble"><p id="p2">Restart the export worker to free the pool.</p></div></div>
            </div>
          </div>
          <p id="outside">Text outside the thread.</p>
          <textarea id="field">A field of its own</textarea>
        </div>
        <div class="selection-toolbar" id="bar" popover="manual" role="toolbar" aria-label="Selected text">
          <button class="btn btn--sm btn--ghost" type="button" id="quote" data-selection-action="quote">Quote</button>
          <button class="btn btn--sm btn--ghost" type="button" id="explain" data-selection-action="explain">Explain</button>
          <button class="btn btn--sm btn--ghost" type="button" id="copy" data-selection-action="copy">Copy</button>
        </div>
        <script>
          window.got = [];
          document.addEventListener('sedna-selection', e => window.got.push(Object.assign({ on: e.target.id }, e.detail)));
        </script>
        """;

    // Selects a run of characters inside one element's first text node, as a script would.
    private static Task Select(IPage page, string id, int from, int to, string? endId = null, int? endAt = null) =>
        Js.Run(page, """
            ([id, from, to, endId, endAt]) => {
              const a = document.getElementById(id).firstChild;
              const b = endId ? document.getElementById(endId).firstChild : a;
              getSelection().setBaseAndExtent(a, from, b, endAt ?? to);
            }
            """, new object?[] { id, from, to, endId, endAt });

    private static Task Opened(IPage page) =>
        page.WaitForFunctionAsync("() => document.getElementById('bar').matches(':popover-open')");

    private static Task Closed(IPage page) =>
        page.WaitForFunctionAsync("() => !document.getElementById('bar').matches(':popover-open')");

    private static Task<double[]> Rects(IPage page) => page.EvaluateAsync<double[]>("""
        () => {
          const r = getSelection().getRangeAt(0).getBoundingClientRect();
          const b = document.getElementById('bar').getBoundingClientRect();
          return [r.left, r.top, r.right, r.bottom, b.left, b.top, b.right, b.bottom,
                  document.documentElement.clientWidth, document.documentElement.clientHeight];
        }
        """);

    [Fact]
    public async Task A_selection_opens_the_toolbar_centred_above_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await Select(page, "p1", 17, 33);
        await Opened(page);

        var r = await Rects(page);
        Assert.True(r[7] <= r[1], $"The toolbar ({r[5]}–{r[7]}) is not above the selection starting at {r[1]}.");
        Assert.InRange(r[1] - r[7], 4.0, 12.0);
        Assert.Equal((r[0] + r[2]) / 2, (r[4] + r[6]) / 2, 1.5);
        Assert.Equal("top", await page.GetAttributeAsync("#bar", "data-side"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Near_the_top_of_the_viewport_it_flips_below_the_selection()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture("padding-top:2px"));
        await Select(page, "p1", 4, 16);
        await Opened(page);

        var r = await Rects(page);
        Assert.True(r[5] >= r[3], $"The toolbar ({r[5]}–{r[7]}) is not below the selection ending at {r[3]}.");
        Assert.Equal("bottom", await page.GetAttributeAsync("#bar", "data-side"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task At_the_edge_it_is_clamped_into_the_viewport_in_either_direction()
    {
        if (NoBrowser) return;
        foreach (var dir in new[] { "ltr", "rtl" })
        {
            var (page, errors) = await OpenStyled(Fixture("padding-top:200px", dir));
            // A word at the start of the line, against the viewport's edge on that side.
            await page.SetViewportSizeAsync(620, 600);
            await Select(page, "p1", 0, 3);
            await Opened(page);

            var r = await Rects(page);
            Assert.True(r[4] >= 7.5 && r[6] <= r[8] - 7.5, $"{dir}: the toolbar ({r[4]}–{r[6]}) runs past the viewport (0–{r[8]}).");
            Assert.True(r[6] - r[4] > 0, $"{dir}: the toolbar has no width.");
            Assert.Empty(errors);
        }
    }

    [Fact]
    public async Task Nothing_opens_for_a_selection_outside_the_host_across_hosts_or_in_a_field()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await Select(page, "outside", 0, 4);
        await Js.Frames(page);
        await page.FocusAsync("#field");
        await Js.Run(page, "() => document.getElementById('field').setSelectionRange(0, 5)");
        await Js.Frames(page);
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('bar').matches(':popover-open')"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_double_click_on_a_word_opens_it_and_collapsing_the_selection_closes_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        var box = (await page.Locator("#p2").BoundingBoxAsync())!;
        await page.Mouse.DblClickAsync(box.X + 30, box.Y + box.Height / 2);
        await Opened(page);

        await page.Mouse.ClickAsync(box.X + box.Width + 40, box.Y + box.Height / 2);
        await Closed(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Escape_closes_it_and_leaves_the_selection()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await Select(page, "p1", 4, 16);
        await Opened(page);

        await page.Keyboard.PressAsync("Escape");
        await Closed(page);
        Assert.Equal("nightly sync", await page.EvaluateAsync<string>("() => getSelection().toString()"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_scroll_closes_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture("padding-top:200px; height:3000px"));
        await Select(page, "p1", 4, 16);
        await Opened(page);

        await Js.Run(page, "() => window.scrollBy(0, 120)");
        await Closed(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_action_reports_the_action_the_text_the_item_and_the_host_and_closes_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await Select(page, "p1", 4, 16);
        await Opened(page);

        await page.Locator("#explain").ClickAsync();
        await Closed(page);
        await page.WaitForFunctionAsync("() => window.got.length === 1");
        var got = await page.EvaluateAsync<string[]>("() => { const g = window.got[0]; return [g.on, g.action, g.text, g.item, g.source]; }");
        Assert.Equal(new[] { "bar", "explain", "nightly sync", "m-1", "thread" }, got);
        // The press did not take the selection away.
        Assert.Equal("nightly sync", await page.EvaluateAsync<string>("() => getSelection().toString()"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_selection_across_two_messages_has_no_item()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await Select(page, "p1", 40, 0, "p2", 7);
        await Opened(page);

        await page.Locator("#quote").ClickAsync();
        await page.WaitForFunctionAsync("() => window.got.length === 1");
        Assert.True(await page.EvaluateAsync<bool>("() => window.got[0].item === null && window.got[0].action === 'quote'"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Alt_F10_moves_into_it_the_arrows_move_along_it_and_Escape_gives_focus_back()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await Select(page, "p1", 4, 16);
        await Opened(page);

        await page.Keyboard.PressAsync("Alt+F10");
        Assert.Equal("quote", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("explain", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("End");
        Assert.Equal("copy", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("quote", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('bar').matches(':popover-open')"));

        await page.Keyboard.PressAsync("Escape");
        await Closed(page);
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('bar').contains(document.activeElement)"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Right_to_left_the_arrows_are_mirrored()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture("padding-top:200px", "rtl").Replace("""class="selection-toolbar" """, """class="selection-toolbar" dir="rtl" """, StringComparison.Ordinal));
        await Select(page, "p1", 4, 16);
        await Opened(page);

        await page.Keyboard.PressAsync("Alt+F10");
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("explain", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Empty(errors);
    }
}
