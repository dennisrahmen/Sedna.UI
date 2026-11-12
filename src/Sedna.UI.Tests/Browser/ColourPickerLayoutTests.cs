using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.form-color-picker</c> (45-colour-picker.css): the area fills the picker and shows the pure
/// hue in its corner, the thumb sits on its point, the preview lines up with the field beside it,
/// and forced colours keep the spectrum and the thumbs.
/// </summary>
public class ColourPickerLayoutTests : ScriptTestBase
{
    private const string Picker = """
        <div style="width:360px; padding:24px">
          <div class="form-color-picker" data-color-picker id="picker">
            <div class="form-color-area" data-color-area id="area" role="slider" tabindex="0" aria-label="Saturation and brightness"></div>
            <input class="form-range form-range--hue" data-color-hue id="hue" type="range" min="0" max="360" aria-label="Hue">
            <input class="form-range form-range--alpha" data-color-alpha id="alpha" type="range" min="0" max="100" aria-label="Opacity">
            <div class="sedna-row sedna-gap-12" id="row">
              <span class="form-color-preview" id="preview"></span>
              <input class="form-input" data-color-value id="hex" type="text" value="#3aa8e280" aria-label="Hex">
            </div>
          </div>
        </div>
        """;

    /// <summary>The geometry and paint the stylesheet gives the picker, read in the page.</summary>
    private const string Measure = """
        () => {
            const $ = id => document.getElementById(id);
            const r = el => el.getBoundingClientRect();
            const picker = r($('picker')), area = r($('area')), hue = r($('hue')), preview = r($('preview')), hex = r($('hex'));
            const a = getComputedStyle($('area')), thumb = getComputedStyle($('area'), '::after');
            const probe = document.createElement('i');
            probe.style.forcedColorAdjust = 'none';
            $('picker').append(probe);
            probe.style.color = 'hsl(' + $('picker').style.getPropertyValue('--picker-hue') + ' 100% 50%)';
            const pureHue = getComputedStyle(probe).color;
            probe.remove();
            const p = getComputedStyle($('preview'));
            return {
                pickerWidth: picker.width, areaWidth: area.width, areaHeight: area.height, hueWidth: hue.width,
                areaBackground: a.backgroundColor, areaImage: a.backgroundImage, pureHue,
                thumbLeft: parseFloat(thumb.left), thumbTop: parseFloat(thumb.top),
                thumbBorder: thumb.borderTopColor, thumbShadow: thumb.boxShadow,
                previewHeight: preview.height, hexHeight: hex.height,
                previewCentre: preview.top + preview.height / 2, hexCentre: hex.top + hex.height / 2,
                previewImage: p.backgroundImage,
                hueDirection: getComputedStyle($('hue')).direction,
            };
        }
        """;

    private sealed record Layout(
        double PickerWidth, double AreaWidth, double AreaHeight, double HueWidth,
        string AreaBackground, string AreaImage, string PureHue,
        double ThumbLeft, double ThumbTop, string ThumbBorder, string ThumbShadow,
        double PreviewHeight, double HexHeight, double PreviewCentre, double HexCentre,
        string PreviewImage, string HueDirection);

    private static readonly System.Text.Json.JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static async Task<Layout> Read(IPage page) =>
        System.Text.Json.JsonSerializer.Deserialize<Layout>(
            await page.EvaluateAsync<string>("() => JSON.stringify((" + Measure + ")())"), Json)!;

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task The_area_fills_the_picker_and_the_thumb_sits_on_its_point(string variant)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Picker);
        await Js.Run(page, "v => { document.documentElement.dataset.variant = v; }", variant);
        var l = await Read(page);

        Assert.Equal(l.PickerWidth, l.AreaWidth, 0.5);
        Assert.Equal(l.PickerWidth, l.HueWidth, 0.5);
        Assert.Equal(160, l.AreaHeight, 0.5);
        // The pure hue under two fades, so the top right corner is the hue itself.
        Assert.Equal(l.PureHue, l.AreaBackground);
        Assert.Equal(2, l.AreaImage.Split("linear-gradient").Length - 1);

        // #3aa8e2: saturation 74.3%, brightness 88.6%.
        Assert.Equal(l.AreaWidth * 0.743, l.ThumbLeft, 1.0);
        Assert.Equal(l.AreaHeight * 0.114, l.ThumbTop, 1.0);
        // White ring, black edge — the same in both variants, so it shows on any colour.
        Assert.Equal("rgb(255, 255, 255)", l.ThumbBorder);
        Assert.Contains("rgb(0, 0, 0)", l.ThumbShadow, StringComparison.Ordinal);

        // The preview is the field's height, on the field's centre line, and carries the colour
        // with its opacity over the checkerboard.
        Assert.Equal(l.HexHeight, l.PreviewHeight, 0.5);
        Assert.Equal(l.HexCentre, l.PreviewCentre, 0.5);
        Assert.Contains("rgba(58, 168, 226, 0.5", l.PreviewImage, StringComparison.Ordinal);
        Assert.Contains("conic-gradient", l.PreviewImage, StringComparison.Ordinal);
        Assert.Equal("ltr", l.HueDirection);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_sliders_stay_left_to_right_under_rtl()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div dir="rtl">{Picker}</div>""");
        var l = await Read(page);
        Assert.Equal("ltr", l.HueDirection);
        Assert.Equal(l.AreaWidth * 0.743, l.ThumbLeft, 1.0);

        // A native range in a left-to-right box puts its low end on the left.
        var box = (await page.Locator("#hue").BoundingBoxAsync())!;
        await page.Mouse.ClickAsync(box.X + 2, box.Y + box.Height / 2);
        Assert.True(int.Parse(await page.InputValueAsync("#hue"), System.Globalization.CultureInfo.InvariantCulture) < 20);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Forced_colours_keep_the_spectrum_the_thumbs_and_a_focus_ring()
    {
        // Forced colours exist in Chromium only (see BrowserEngine).
        if (NoBrowser || !BrowserEngine.EmulatesForcedColors) return;
        var (page, errors) = await OpenStyled(Picker);
        await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        await page.FocusAsync("#area");
        await page.Keyboard.PressAsync("ArrowLeft");

        var l = await Read(page);
        Assert.Equal(l.PureHue, l.AreaBackground);
        Assert.Contains("linear-gradient", l.AreaImage, StringComparison.Ordinal);
        Assert.Equal("rgb(255, 255, 255)", l.ThumbBorder);
        Assert.Contains(", 0.5)", l.PreviewImage, StringComparison.Ordinal);

        var outline = await page.EvaluateAsync<string[]>("""
            () => { const s = getComputedStyle(document.getElementById('area'));
                    return [s.outlineStyle, s.outlineWidth]; }
            """);
        Assert.Equal("solid", outline[0]);
        Assert.NotEqual("0px", outline[1]);
        Assert.Empty(errors);
    }
}
