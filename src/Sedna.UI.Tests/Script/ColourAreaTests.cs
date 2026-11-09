using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.form-color-picker</c> (23-colour.js): a pointer or the keyboard on the area, and the hue and
/// alpha ranges, write the app's own field and dispatch its <c>input</c> and <c>change</c>; typing
/// in the field moves the rest; and the spectrum stays physical under <c>dir="rtl"</c>.
/// </summary>
public class ColourAreaTests : ScriptTestBase
{
    /// <summary>
    /// A picker on #3aa8e2, an RGB field, channels, a hidden native colour input, and a listener
    /// where a framework binds — on the document, bubbling — recording what it reads.
    /// </summary>
    private static string Picker(string dir = "ltr") => $$"""
        <form id="f" dir="{{dir}}" style="width:320px; padding:24px">
          <div class="form-color-picker" data-color-picker id="picker">
            <div class="form-color-area" data-color-area id="area" role="slider" tabindex="0"
                 aria-label="Saturation and brightness" data-color-valuetext="S {s} V {v} H {h} A {a} {hex}"></div>
            <input class="form-range form-range--hue" data-color-hue id="hue" type="range" min="0" max="360" value="0" aria-label="Hue">
            <input class="form-range form-range--alpha" data-color-alpha id="alpha" type="range" min="0" max="100" value="100" aria-label="Opacity">
            <span class="form-color-preview"></span>
            <input data-color-value id="hex" type="text" value="#3aa8e2" aria-label="Hex">
            <input data-color-value data-color-format="rgb" id="rgb" type="text" aria-label="RGB">
            <input data-color-value id="native" type="color" hidden>
            <input data-color-channel="r" id="r" type="number" aria-label="Red">
            <input data-color-channel="a" id="a" type="number" aria-label="Opacity">
          </div>
        </form>
        <script>
          window.read = [];
          for (const type of ['input', 'change'])
            document.addEventListener(type, e => { if (e.target.id === 'hex') window.read.push(type + ' ' + e.target.value); });
        </script>
        """;

    private static Task<string> Value(IPage page, string id) =>
        page.EvaluateAsync<string>("id => document.getElementById(id).value", id);

    private static Task<string[]> Read(IPage page) => page.EvaluateAsync<string[]>("() => window.read.splice(0)");

    private static Task Type(IPage page, string id, string text, string type) =>
        Js.Run(page, "([id, text, type]) => { const i = document.getElementById(id); i.value = text; i.dispatchEvent(new Event(type, { bubbles: true })); }",
            new object[] { id, text, type });

    [Fact]
    public async Task The_picker_starts_from_the_field()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Picker());

        Assert.Equal("201", await Value(page, "hue"));
        Assert.Equal("100", await Value(page, "alpha"));
        Assert.Equal("rgb(58 168 226)", await Value(page, "rgb"));
        Assert.Equal("#3aa8e2", await Value(page, "native"));
        Assert.Equal("58", await Value(page, "r"));

        var area = await page.EvaluateAsync<string[]>("""
            () => { const a = document.getElementById('area'), s = document.getElementById('picker').style;
                    return [a.getAttribute('aria-valuenow'), a.getAttribute('aria-valuetext'),
                            s.getPropertyValue('--picker-x'), s.getPropertyValue('--picker-y'),
                            s.getPropertyValue('--picker-color')]; }
            """);
        Assert.Equal(["74", "S 74 V 89 H 201 A 100 #3aa8e2", "74.3%", "11.4%", "rgb(58 168 226 / 1)"], area);
        // Nothing was dispatched for the colour the page started on.
        Assert.Empty(await Read(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_drag_over_the_area_writes_the_field_and_ends_with_a_change()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Picker());
        var box = (await page.Locator("#area").BoundingBoxAsync())!;

        // The top right corner is the pure hue, the bottom edge black, the left edge grey.
        await page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X + box.Width - 1, box.Y + 1);
        await page.Mouse.MoveAsync(box.X + box.Width + 40, box.Y - 40);
        await page.Mouse.UpAsync();

        // Past the corner is the corner: the hue at full saturation and brightness.
        Assert.Equal("#00a7ff", await Value(page, "hex"));
        var read = await Read(page);
        Assert.Equal("change #00a7ff", read[^1]);
        Assert.All(read[..^1], r => Assert.StartsWith("input ", r, StringComparison.Ordinal));
        Assert.Equal(1, read.Count(r => r.StartsWith("change", StringComparison.Ordinal)));
        Assert.Equal("area", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        await page.Mouse.MoveAsync(box.X + 2, box.Y + box.Height - 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X - 30, box.Y + box.Height + 30);
        await page.Mouse.UpAsync();
        Assert.Equal("#000000", await Value(page, "hex"));
        // A black keeps its hue, so dragging back out does not jump to red.
        Assert.Equal("201", await Value(page, "hue"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_arrow_keys_move_the_thumb_by_one_and_by_ten_with_shift()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Picker());
        await Type(page, "hex", "#ff0000", "input");
        await Read(page);
        await page.FocusAsync("#area");

        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("99", await page.GetAttributeAsync("#area", "aria-valuenow"));
        Assert.Equal(["input #ff0303", "change #ff0303"], await Read(page));

        await page.Keyboard.PressAsync("Shift+ArrowLeft");
        await page.Keyboard.PressAsync("Shift+ArrowDown");
        Assert.Equal("89", await page.GetAttributeAsync("#area", "aria-valuenow"));
        Assert.Equal("S 89 V 90 H 0 A 100 #e61919", await page.GetAttributeAsync("#area", "aria-valuetext"));

        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("PageUp");
        Assert.Equal("#ffffff", await Value(page, "hex"));
        await page.Keyboard.PressAsync("PageDown");
        Assert.Equal("#000000", await Value(page, "hex"));
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("#030000", await Value(page, "hex"));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_spectrum_and_its_keys_stay_physical_in_either_direction(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Picker(dir));
        await Type(page, "hex", "#808080", "input");
        await page.FocusAsync("#area");
        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("1", await page.GetAttributeAsync("#area", "aria-valuenow"));

        // The thumb sits at its saturation from the left edge, and the hue range runs left to right.
        var geometry = await page.EvaluateAsync<double[]>("""
            () => {
                const area = document.getElementById('area').getBoundingClientRect();
                const thumb = getComputedStyle(document.getElementById('area'), '::after');
                return [parseFloat(thumb.left) / area.width,
                        getComputedStyle(document.getElementById('hue')).direction === 'ltr' ? 1 : 0];
            }
            """);
        Assert.Equal(0.01, geometry[0], 0.005);
        Assert.Equal(1, geometry[1], 0.001);

        var box = (await page.Locator("#area").BoundingBoxAsync())!;
        await page.Mouse.ClickAsync(box.X + box.Width * 0.25f, box.Y + box.Height * 0.5f);
        Assert.Equal("25", await page.GetAttributeAsync("#area", "aria-valuenow"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_hue_and_alpha_ranges_write_the_field()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Picker());

        await Type(page, "hue", "0", "input");
        Assert.Equal("#e23a3a", await Value(page, "hex"));
        Assert.Equal(["input #e23a3a"], await Read(page));
        await Type(page, "hue", "0", "change");
        Assert.Equal(["change #e23a3a"], await Read(page));

        await Type(page, "alpha", "50", "input");
        Assert.Equal("#e23a3a80", await Value(page, "hex"));
        Assert.Equal("rgb(226 58 58 / 0.5)", await Value(page, "rgb"));
        // A native colour input holds no opacity.
        Assert.Equal("#e23a3a", await Value(page, "native"));
        Assert.Equal("50", await Value(page, "a"));

        // The keyboard on a native range is the range's own.
        await page.FocusAsync("#alpha");
        await page.Keyboard.PressAsync("End");
        Assert.Equal("#e23a3a", await Value(page, "hex"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Typing_in_a_field_moves_the_rest_and_a_change_rewrites_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Picker());

        await Type(page, "hex", "#0f08", "input");
        Assert.Equal("120", await Value(page, "hue"));
        Assert.Equal("53", await Value(page, "alpha"));
        Assert.Equal("rgb(0 255 0 / 0.53)", await Value(page, "rgb"));
        // The field being typed in is left as typed until the reader leaves it.
        Assert.Equal("#0f08", await Value(page, "hex"));
        await Type(page, "hex", "#0f08", "change");
        Assert.Equal("#00ff0088", await Value(page, "hex"));
        Assert.Equal(["input #0f08", "change #00ff0088"], await Read(page));

        // What does not parse goes back to the last colour on change, before a binding reads it.
        await Type(page, "hex", "not a colour", "input");
        Assert.Equal("120", await Value(page, "hue"));
        await Type(page, "hex", "not a colour", "change");
        Assert.Equal("#00ff0088", await Value(page, "hex"));
        Assert.Equal(["input not a colour", "change #00ff0088"], await Read(page));

        await Type(page, "rgb", "rgba(255, 0, 0, 25%)", "input");
        Assert.Equal("#ff000040", await Value(page, "hex"));
        await Type(page, "r", "0", "input");
        Assert.Equal("#00000040", await Value(page, "hex"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_read_only_area_takes_neither_the_pointer_nor_the_keys()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Picker());
        await Js.Run(page, "() => document.getElementById('area').setAttribute('aria-readonly', 'true')");
        await page.FocusAsync("#area");
        await page.Keyboard.PressAsync("Shift+ArrowLeft");
        var box = (await page.Locator("#area").BoundingBoxAsync())!;
        await page.Mouse.ClickAsync(box.X + 2, box.Y + 2);
        Assert.Equal("#3aa8e2", await Value(page, "hex"));
        Assert.Empty(await Read(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_reset_reads_the_colour_again()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Picker());
        await Type(page, "hue", "0", "input");
        await Js.Run(page, "() => document.getElementById('f').reset()");
        await page.WaitForFunctionAsync("() => document.getElementById('hue').value === '201'");
        Assert.Equal("rgb(58 168 226)", await Value(page, "rgb"));
        Assert.Empty(errors);
    }
}
