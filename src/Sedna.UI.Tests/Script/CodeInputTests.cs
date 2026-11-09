using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A one-time code (<c>[data-code-group]</c>, 23-code.js): one character to a box, a paste or an
/// autofill spread over all of them, Backspace going back, and the whole code in the one
/// <c>[data-code-value]</c> input, which dispatches <c>input</c> and <c>change</c> as a field does.
/// </summary>
public class CodeInputTests : ScriptTestBase
{
    private const string Fixture = """
        <form id="f">
          <div class="form-code" data-code-group role="group" aria-label="Sign-in code">
            <input type="hidden" name="code" id="value" data-code-value>
            <input class="form-input" id="b0" inputmode="numeric" autocomplete="one-time-code" aria-label="Digit 1 of 6">
            <input class="form-input" id="b1" inputmode="numeric" autocomplete="off" aria-label="Digit 2 of 6">
            <input class="form-input" id="b2" inputmode="numeric" autocomplete="off" aria-label="Digit 3 of 6">
            <input class="form-input" id="b3" inputmode="numeric" autocomplete="off" aria-label="Digit 4 of 6">
            <input class="form-input" id="b4" inputmode="numeric" autocomplete="off" aria-label="Digit 5 of 6">
            <input class="form-input" id="b5" inputmode="numeric" autocomplete="off" aria-label="Digit 6 of 6">
          </div>
        </form>
        """;

    /// <summary>Records every <c>input</c> and <c>change</c> the value input dispatches, as seen from the document.</summary>
    private const string Record = """
        () => {
          window.__events = [];
          for (const type of ['input', 'change'])
            document.addEventListener(type, e => { if (e.target.id === 'value') window.__events.push(type + ':' + e.target.value); });
        }
        """;

    private static Task<string[]> Boxes(IPage page) =>
        page.EvaluateAsync<string[]>("() => [0, 1, 2, 3, 4, 5].map(i => document.getElementById('b' + i).value)");

    private static Task<string> Value(IPage page) =>
        page.EvaluateAsync<string>("() => document.getElementById('value').value");

    private static Task<string> Focused(IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement && document.activeElement.id");

    private static Task<string[]> Events(IPage page) =>
        page.EvaluateAsync<string[]>("() => window.__events");

    [Fact]
    public async Task Typing_fills_a_box_moves_on_and_writes_the_code_into_the_value_input_with_its_events()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        await Js.Run(page, Record);

        await page.FocusAsync("#b0");
        await page.Keyboard.TypeAsync("4829");

        Assert.Equal(["4", "8", "2", "9", "", ""], await Boxes(page));
        Assert.Equal("4829", await Value(page));
        Assert.Equal("b4", await Focused(page));
        Assert.Equal(
            ["input:4", "change:4", "input:48", "change:48", "input:482", "change:482", "input:4829", "change:4829"],
            await Events(page));

        // A letter in a numeric box is refused, and nothing is dispatched for it.
        await page.Keyboard.TypeAsync("x");
        Assert.Equal("", await page.InputValueAsync("#b4"));
        Assert.Equal("b4", await Focused(page));
        Assert.Equal(8, (await Events(page)).Length);

        // A box already holding a digit takes the one typed over it.
        await page.FocusAsync("#b1");
        await page.Keyboard.TypeAsync("5");
        Assert.Equal(["4", "5", "2", "9", "", ""], await Boxes(page));
        Assert.Equal("4529", await Value(page));
        Assert.Equal("b2", await Focused(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_paste_fills_every_box_from_the_first_when_it_is_the_whole_code()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        await Js.Run(page, Record);

        await page.FocusAsync("#b2");
        var cancelled = await Paste(page, "#b2", "123 456");

        Assert.True(cancelled, "The paste reached the box it landed in as well.");
        Assert.Equal(["1", "2", "3", "4", "5", "6"], await Boxes(page));
        Assert.Equal("123456", await Value(page));
        Assert.Equal("b5", await Focused(page));
        Assert.Equal(["input:123456", "change:123456"], await Events(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_shorter_paste_fills_from_the_box_it_landed_in()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await page.FocusAsync("#b0");
        await page.Keyboard.TypeAsync("12");
        await Paste(page, "#b2", "7-8");

        Assert.Equal(["1", "2", "7", "8", "", ""], await Boxes(page));
        Assert.Equal("1278", await Value(page));
        Assert.Equal("b4", await Focused(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_first_box_offers_the_platforms_code_and_an_autofill_spreads_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        await Js.Run(page, Record);

        // The platform fills the whole code into the field marked one-time-code, as one
        // replacement: the box must take no maxlength, and the script spreads it.
        Assert.Equal("one-time-code", await page.GetAttributeAsync("#b0", "autocomplete"));
        Assert.Null(await page.GetAttributeAsync("#b0", "maxlength"));
        Assert.Equal("numeric", await page.GetAttributeAsync("#b0", "inputmode"));
        await Js.Run(page, """
            () => {
              const box = document.getElementById('b0');
              box.focus();
              box.value = '654321';
              box.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertReplacementText' }));
            }
            """);

        Assert.Equal(["6", "5", "4", "3", "2", "1"], await Boxes(page));
        Assert.Equal("654321", await Value(page));
        Assert.Equal(["input:654321", "change:654321"], await Events(page));
        Assert.Equal("one-time-code", await page.GetAttributeAsync("#b0", "autocomplete"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Backspace_clears_a_box_and_in_an_empty_one_goes_back_and_clears_the_one_before()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await page.FocusAsync("#b0");
        await page.Keyboard.TypeAsync("1234");
        Assert.Equal("b4", await Focused(page));
        await Js.Run(page, Record);

        await page.Keyboard.PressAsync("Backspace");
        Assert.Equal(["1", "2", "3", "", "", ""], await Boxes(page));
        Assert.Equal("b3", await Focused(page));
        Assert.Equal("123", await Value(page));

        await page.Keyboard.PressAsync("Backspace");
        Assert.Equal(["1", "2", "", "", "", ""], await Boxes(page));
        Assert.Equal("b2", await Focused(page));

        // In a box that holds a character, Backspace clears it and stays.
        await page.FocusAsync("#b1");
        await Js.Run(page, "() => document.getElementById('b1').setSelectionRange(1, 1)");
        await page.Keyboard.PressAsync("Backspace");
        Assert.Equal(["1", "", "", "", "", ""], await Boxes(page));
        Assert.Equal("b1", await Focused(page));
        Assert.Equal("1", await Value(page));
        Assert.Equal(["input:123", "change:123", "input:12", "change:12", "input:1", "change:1"], await Events(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_arrow_keys_move_between_boxes_left_to_right_even_on_a_right_to_left_page()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        await Js.Run(page, "() => document.documentElement.setAttribute('dir', 'rtl')");

        var order = await page.EvaluateAsync<bool>("""
            () => document.getElementById('b0').getBoundingClientRect().left < document.getElementById('b5').getBoundingClientRect().left
            """);
        Assert.True(order, "On a right-to-left page the code's first box is not on the left.");

        await page.FocusAsync("#b2");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("b3", await Focused(page));
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("b1", await Focused(page));
        await page.Keyboard.PressAsync("End");
        Assert.Equal("b5", await Focused(page));
        await page.Keyboard.PressAsync("Home");
        Assert.Equal("b0", await Focused(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_value_the_app_writes_is_spread_over_the_boxes_and_a_reset_empties_both()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        // A Blazor render sets the property; on a hidden input that is its attribute.
        await Js.Run(page, "() => { document.getElementById('value').value = '987654'; }");
        await page.WaitForFunctionAsync("() => document.getElementById('b5').value === '4'");
        Assert.Equal(["9", "8", "7", "6", "5", "4"], await Boxes(page));

        await Js.Run(page, "() => { document.getElementById('value').value = ''; }");
        await page.WaitForFunctionAsync("() => document.getElementById('b0').value === ''");
        Assert.Equal(["", "", "", "", "", ""], await Boxes(page));

        await page.FocusAsync("#b0");
        await page.Keyboard.TypeAsync("31");
        await Js.Run(page, Record);
        await Js.Run(page, "() => document.getElementById('f').reset()");
        await page.WaitForFunctionAsync("() => document.getElementById('value').value === ''");

        Assert.Equal(["", "", "", "", "", ""], await Boxes(page));
        Assert.Equal(["input:", "change:"], await Events(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_code_markup_without_the_script_still_shows_square_boxes_in_one_row()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div class="sedna-col">
              <span class="form-label">Code</span>
              {Fixture}
            </div>
            """);

        var boxes = await page.EvaluateAsync<double[][]>("""
            () => [0, 1, 2, 3, 4, 5].map(i => { const r = document.getElementById('b' + i).getBoundingClientRect(); return [r.left, r.top, r.width, r.height]; })
            """);
        var group = await page.EvaluateAsync<double>("() => document.querySelector('.form-code').getBoundingClientRect().width");

        Assert.All(boxes, b => Assert.Equal(boxes[0][1], b[1], 0.5));
        Assert.All(boxes, b => Assert.Equal(b[3], b[2], 0.5));
        Assert.Equal(36, boxes[0][3], 0.5);
        Assert.True(group < 400, $"The code row ran {group}px wide in a column.");
        Assert.Empty(errors);
    }

    /// <summary>A paste carrying <paramref name="text"/>; true when the script cancelled it.</summary>
    private static Task<bool> Paste(IPage page, string selector, string text) =>
        page.EvaluateAsync<bool>("""
            ([selector, text]) => {
              const box = document.querySelector(selector);
              const e = new Event('paste', { bubbles: true, cancelable: true });
              Object.defineProperty(e, 'clipboardData', { value: { getData: () => text } });
              box.dispatchEvent(e);
              return e.defaultPrevented;
            }
            """, new[] { selector, text });
}
