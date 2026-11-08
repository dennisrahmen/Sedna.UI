using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Ghost text: a <c>textarea[data-suggestion]</c> shows the rest of the suggestion after the
/// caret, Tab accepts it as typed input, and Escape or a mismatch dismisses it.
/// </summary>
public class GhostTextTests : ScriptTestBase
{
    // The page's own Tab — what accepts the suggestion — and not focus navigation, which
    // Keys.Tab spells for the platform.
    private const string Accept = "Tab";

    private static string Fixture(string dir = "ltr") => $$"""
        <div dir="{{dir}}" style="width:420px">
          <div class="ghost-field" id="host">
            <textarea class="form-input" id="t" rows="3" placeholder="Ask"
                      data-suggestion="Restart the sync worker"></textarea>
            <div class="ghost-text" id="g" aria-hidden="true"></div>
          </div>
          <button type="button" id="other">Other</button>
        </div>
        <script>
          window.inputs = 0;
          document.getElementById('t').addEventListener('input', () => window.inputs++);
        </script>
        """;

    private static Task<string?> Rest(IPage page) => page.GetAttributeAsync("#g", "data-ghost-rest");

    private static Task Showing(IPage page, string rest) =>
        page.WaitForFunctionAsync("r => document.getElementById('g').getAttribute('data-ghost-rest') === r", rest);

    private static Task Gone(IPage page) =>
        page.WaitForFunctionAsync("() => !document.getElementById('g').hasAttribute('data-ghost-rest')");

    [Fact]
    public async Task Typing_the_start_shows_the_rest_after_the_caret()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await page.FocusAsync("#t");
        await page.Keyboard.TypeAsync("rest");
        await Showing(page, "art the sync worker");

        Assert.Equal("rest", await page.GetAttributeAsync("#g", "data-ghost-typed"));
        Assert.Equal("block", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('g')).display"));
        // The mirror lies over the textarea, so its text starts where the caret is.
        var boxes = await page.EvaluateAsync<double[]>("""
            () => { const a = document.getElementById('t').getBoundingClientRect(), g = document.getElementById('g').getBoundingClientRect();
                    return [a.left, a.top, a.width, a.height, g.left, g.top, g.width, g.height]; }
            """);
        for (var i = 0; i < 4; i++) Assert.Equal(boxes[i], boxes[i + 4], 0.5);
        Assert.Equal(
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('t')).paddingLeft"),
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('g')).paddingLeft"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Tab_accepts_it_as_input_and_leaves_the_caret_at_the_end()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await page.FocusAsync("#t");
        await page.Keyboard.TypeAsync("Rest");
        await Showing(page, "art the sync worker");
        var before = await page.EvaluateAsync<int>("() => window.inputs");

        await page.Keyboard.PressAsync(Accept);
        Assert.Equal("Restart the sync worker", await page.InputValueAsync("#t"));
        Assert.Equal(before + 1, await page.EvaluateAsync<int>("() => window.inputs"));
        Assert.Equal("t", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Equal(23, await page.EvaluateAsync<int>("() => document.getElementById('t').selectionStart"));
        await Gone(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Without_a_suggestion_Tab_moves_on()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await page.FocusAsync("#t");
        await page.Keyboard.TypeAsync("Stop");
        await Js.Frames(page);
        Assert.Null(await Rest(page));

        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("other", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Equal("Stop", await page.InputValueAsync("#t"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Escape_dismisses_it_until_the_app_writes_another()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await page.FocusAsync("#t");
        await page.Keyboard.TypeAsync("Re");
        await Showing(page, "start the sync worker");

        await page.Keyboard.PressAsync("Escape");
        await Gone(page);
        await page.Keyboard.TypeAsync("s");
        await Js.Frames(page);
        Assert.Null(await Rest(page));

        await Js.Run(page, "() => document.getElementById('t').setAttribute('data-suggestion', 'Resume the queue')");
        await Showing(page, "ume the queue");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Typing_a_mismatch_dismisses_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await page.FocusAsync("#t");
        await page.Keyboard.TypeAsync("Rex");
        await Js.Frames(page);
        Assert.Null(await Rest(page));

        // Back to a matching start, the same suggestion stays dismissed.
        await page.Keyboard.PressAsync("Backspace");
        await Js.Frames(page);
        Assert.Null(await Rest(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task It_shows_only_with_the_focus_and_the_caret_at_the_end()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await page.FocusAsync("#t");
        await page.Keyboard.TypeAsync("Res");
        await Showing(page, "tart the sync worker");

        await page.Keyboard.PressAsync("ArrowLeft");
        await Gone(page);
        await page.Keyboard.PressAsync("ArrowRight");
        await Showing(page, "tart the sync worker");

        await page.FocusAsync("#other");
        await Gone(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Right_to_left_the_mirror_takes_the_direction_and_an_empty_field_shows_all_of_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture("rtl"));
        await page.FocusAsync("#t");
        // An empty field with a suggestion shows the whole of it.
        await Showing(page, "Restart the sync worker");

        Assert.Equal("rtl", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('g')).direction"));
        Assert.Equal(
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('t')).paddingRight"),
            await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('g')).paddingRight"));
        Assert.True(await page.EvaluateAsync<bool>("() => document.querySelector('.ghost-field:has(> .ghost-text[data-ghost-rest]) > textarea') !== null"));

        await page.Keyboard.PressAsync(Accept);
        Assert.Equal("Restart the sync worker", await page.InputValueAsync("#t"));
        Assert.Empty(errors);
    }
}
