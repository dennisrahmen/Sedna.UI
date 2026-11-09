using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The signature pad from markup to value, asserted against <c>docs/signature.md</c>: a stroke drawn
/// with the pointer is written to the app's field with <c>input</c> and <c>change</c>, undo and clear
/// take strokes away, a value set from outside is drawn, a new size redraws the same strokes, a locked
/// field takes no stroke, the ink comes from its token, and the pad's edge follows the field states.
/// </summary>
/// <remarks>
/// The pad is driven with the real mouse, so the pointer events are the browser's own. What is drawn
/// is read back from the pad's canvas.
/// </remarks>
public class SignatureTests : ScriptTestBase
{
    private const string Pad = """
        <form id="f" style="width:600px">
          <div class="form-signature" id="s" data-signature data-signature-eager>
            <div class="form-signature-pad" id="pad" data-signature-pad aria-label="Signature">
              <span class="form-signature-hint">Sign here</span>
            </div>
            <input type="hidden" name="signature" id="v" data-signature-value{0}>
            <div class="form-signature-actions">
              <button class="btn btn--sm" type="button" id="undo" data-signature-undo>Undo</button>
              <button class="btn btn--sm" type="button" id="clear" data-signature-clear>Clear</button>
            </div>
          </div>
        </form>
        <script>
          window.__events = [];
          document.addEventListener('input', e => __events.push('input:' + e.target.id));
          document.addEventListener('change', e => __events.push('change:' + e.target.id));
        </script>
        """;

    private async Task<IPage> OpenPad(string fieldAttributes = "", float? scale = null, string extraHead = "")
    {
        var (page, _) = await OpenStyled(Pad.Replace("{0}", fieldAttributes, StringComparison.Ordinal), extraHead, deviceScaleFactor: scale);
        await page.WaitForFunctionAsync("() => document.getElementById('s').dataset.signatureState === 'ready'");
        return page;
    }

    /// <summary>Draws a stroke through points given as fractions of the pad's width and height.</summary>
    private static async Task Stroke(IPage page, params (double X, double Y)[] at)
    {
        var box = (await page.Locator("#pad").BoundingBoxAsync())!;
        await page.Mouse.MoveAsync((float)(box.X + (box.Width * at[0].X)), (float)(box.Y + (box.Height * at[0].Y)));
        await page.Mouse.DownAsync();
        foreach (var p in at.Skip(1))
            await page.Mouse.MoveAsync((float)(box.X + (box.Width * p.X)), (float)(box.Y + (box.Height * p.Y)), new() { Steps = 6 });
        await page.Mouse.UpAsync();
    }

    private static Task<string> Value(IPage page) => page.EvaluateAsync<string>("() => document.getElementById('v').value");

    private static Task<string[]> Events(IPage page) =>
        page.EvaluateAsync<string[]>("() => { const e = window.__events.slice(); window.__events.length = 0; return e; }");

    /// <summary>Painted pixels of the canvas, and the painted share's left and right edges as fractions of its width.</summary>
    private static Task<double[]> Paint(IPage page) =>
        page.EvaluateAsync<double[]>("""
            () => {
                const c = document.querySelector('#pad > canvas');
                const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
                let n = 0, left = c.width, right = -1;
                for (let i = 3; i < d.length; i += 4) {
                    if (d[i] <= 8) continue;
                    n++;
                    const x = ((i - 3) / 4) % c.width;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
                return [n, left / c.width, (right + 1) / c.width];
            }
            """);

    [Fact]
    public async Task A_page_without_a_pad_never_downloads_the_module()
    {
        if (NoBrowser) return;
        var page = await Open("<main><p>No pad here.</p></main>");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.DoesNotContain(Requests, r => r.Url.Contains("Sedna.UI.signature.js", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_stroke_writes_the_field_with_input_then_change()
    {
        if (NoBrowser) return;
        var page = await OpenPad();
        Assert.Equal("", await Value(page));
        Assert.Null(await page.GetAttributeAsync("#s", "data-signature-inked"));
        Assert.True(await page.Locator("#undo").IsDisabledAsync());
        Assert.True(await page.Locator("#clear").IsDisabledAsync());
        Assert.True(await page.Locator(".form-signature-hint").IsVisibleAsync());

        await Stroke(page, (0.1, 0.5), (0.3, 0.3), (0.5, 0.6), (0.7, 0.4));

        var value = await Value(page);
        Assert.Matches(@"^M[\d.]+ [\d.]+(Q[\d.]+ [\d.]+ [\d.]+ [\d.]+)+L[\d.]+ [\d.]+$", value);
        Assert.Equal(["input:v", "change:v"], await Events(page));
        Assert.NotNull(await page.GetAttributeAsync("#s", "data-signature-inked"));
        Assert.False(await page.Locator("#undo").IsDisabledAsync());
        Assert.False(await page.Locator("#clear").IsDisabledAsync());
        Assert.False(await page.Locator(".form-signature-hint").IsVisibleAsync());
        Assert.True((await Paint(page))[0] > 100, "The stroke was not drawn.");

        // In the value's box: 1000 wide, as tall as the pad's ratio makes it, the start where it began.
        var numbers = Regex.Matches(value, @"[\d.]+").Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        // The pointer lands inside the pad's border, so a pixel or two off the box's own fraction.
        Assert.InRange(numbers[0], 95, 105);
        Assert.InRange(numbers[1], (1000.0 / 3 * 0.5) - 5, (1000.0 / 3 * 0.5) + 5);
        Assert.All(numbers.Where((_, i) => i % 2 == 0), x => Assert.InRange(x, 0, 1000));
        Assert.All(numbers.Where((_, i) => i % 2 == 1), y => Assert.InRange(y, 0, 1000.0 / 3));

        // The form posts it.
        Assert.Equal(value, await page.EvaluateAsync<string>("() => new FormData(document.getElementById('f')).get('signature')"));
    }

    [Fact]
    public async Task Undo_takes_the_last_stroke_and_clear_takes_every_stroke()
    {
        if (NoBrowser) return;
        var page = await OpenPad();
        await Stroke(page, (0.1, 0.5), (0.4, 0.4));
        var first = await Value(page);
        await Stroke(page, (0.5, 0.5), (0.9, 0.6));
        var both = await Value(page);
        Assert.StartsWith(first + "M", both, StringComparison.Ordinal);
        await Events(page);

        await page.ClickAsync("#undo");
        Assert.Equal(first, await Value(page));
        Assert.Equal(["input:v", "change:v"], await Events(page));

        await page.ClickAsync("#clear");
        Assert.Equal("", await Value(page));
        Assert.Equal(["input:v", "change:v"], await Events(page));
        Assert.Null(await page.GetAttributeAsync("#s", "data-signature-inked"));
        Assert.True(await page.Locator("#undo").IsDisabledAsync());
        Assert.True(await page.Locator("#clear").IsDisabledAsync());
        Assert.Equal(0, (await Paint(page))[0]);
    }

    [Fact]
    public async Task A_dot_is_a_stroke()
    {
        if (NoBrowser) return;
        var page = await OpenPad();
        var box = (await page.Locator("#pad").BoundingBoxAsync())!;
        await page.Mouse.ClickAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));

        Assert.Matches(@"^M([\d.]+ [\d.]+)L\1$", await Value(page));
        Assert.True((await Paint(page))[0] > 0, "The dot was not drawn.");
    }

    [Fact]
    public async Task A_new_size_redraws_the_same_strokes_scaled_to_it()
    {
        if (NoBrowser) return;
        var page = await OpenPad();
        await Stroke(page, (0.2, 0.5), (0.5, 0.3), (0.8, 0.6));
        var value = await Value(page);
        var before = await Paint(page);
        var width = await page.EvaluateAsync<int>("() => document.querySelector('#pad > canvas').width");
        await Events(page);

        await Js.Run(page, "() => document.getElementById('f').style.width = '300px'");
        await page.WaitForFunctionAsync("w => document.querySelector('#pad > canvas').width < w", width);
        var after = await Paint(page);

        Assert.Equal(value, await Value(page));
        Assert.Empty(await Events(page));
        Assert.True(after[0] > 0, "The strokes were not redrawn.");
        // The same drawing, at the new width: its left and right edges at the same share of it.
        Assert.Equal(before[1], after[1], 0.03);
        Assert.Equal(before[2], after[2], 0.03);
    }

    [Fact]
    public async Task The_canvas_is_drawn_at_the_device_pixel_ratio()
    {
        if (NoBrowser) return;
        var page = await OpenPad(scale: 2);
        var sizes = await page.EvaluateAsync<double[]>("""
            () => { const c = document.querySelector('#pad > canvas'), r = c.getBoundingClientRect(); return [c.width, r.width, c.height, r.height]; }
            """);
        Assert.Equal(sizes[1] * 2, sizes[0], 1);
        Assert.Equal(sizes[3] * 2, sizes[2], 1);
        // Three to one, the value's box.
        Assert.Equal(3, sizes[1] / sizes[3], 1);
    }

    [Fact]
    public async Task A_value_set_from_outside_is_drawn_and_its_strokes_can_be_undone()
    {
        if (NoBrowser) return;
        var page = await OpenPad();
        await Js.Run(page, "() => { document.getElementById('v').value = 'M100 100Q200 50 300 100L400 150M500 200L500 200'; }");

        Assert.NotNull(await page.GetAttributeAsync("#s", "data-signature-inked"));
        Assert.True((await Paint(page))[0] > 0, "The value was not drawn.");
        Assert.Empty(await Events(page));

        // Undo works on strokes that were loaded: the dot goes, the curve stays.
        await page.ClickAsync("#undo");
        Assert.Equal("M100 100Q200 50 300 100L400 150", await Value(page));

    }

    [Fact]
    public async Task A_form_reset_puts_a_textarea_field_and_the_pad_back()
    {
        if (NoBrowser) return;
        // A hidden input's value is its default, so a reset leaves it; a textarea's is not.
        var (page, _) = await OpenStyled("""
            <form id="f" style="width:600px">
              <div class="form-signature" id="s" data-signature data-signature-eager>
                <div class="form-signature-pad" id="pad" data-signature-pad aria-label="Signature"></div>
                <textarea name="signature" id="v" data-signature-value hidden></textarea>
              </div>
            </form>
            """);
        await page.WaitForFunctionAsync("() => document.getElementById('s').dataset.signatureState === 'ready'");
        await Stroke(page, (0.2, 0.5), (0.8, 0.5));
        Assert.NotEqual("", await Value(page));
        Assert.NotNull(await page.GetAttributeAsync("#s", "data-signature-inked"));

        await Js.Run(page, "() => document.getElementById('f').reset()");
        await page.WaitForFunctionAsync("() => !document.getElementById('s').hasAttribute('data-signature-inked')");
        Assert.Equal("", await Value(page));
        Assert.Equal(0, (await Paint(page))[0]);
    }

    [Fact]
    public async Task A_read_only_field_shows_its_signature_and_takes_no_stroke()
    {
        if (NoBrowser) return;
        var page = await OpenPad(" readonly value=\"M100 100L400 200\"");
        Assert.NotNull(await page.GetAttributeAsync("#s", "data-signature-inked"));
        Assert.True((await Paint(page))[0] > 0, "The signature was not drawn.");
        Assert.True(await page.Locator("#undo").IsDisabledAsync());
        Assert.True(await page.Locator("#clear").IsDisabledAsync());

        await Stroke(page, (0.6, 0.5), (0.9, 0.5));
        Assert.Equal("M100 100L400 200", await Value(page));
        Assert.Empty(await Events(page));
    }

    [Fact]
    public async Task A_disabled_field_takes_no_stroke_until_it_is_enabled()
    {
        if (NoBrowser) return;
        var page = await OpenPad(" disabled");
        await Stroke(page, (0.2, 0.5), (0.8, 0.5));
        Assert.Equal("", await Value(page));
        Assert.Empty(await Events(page));
        Assert.Equal(0, (await Paint(page))[0]);

        await Js.Run(page, "() => document.getElementById('v').disabled = false");
        await Stroke(page, (0.2, 0.5), (0.8, 0.5));
        Assert.NotEqual("", await Value(page));
        Assert.Equal(["input:v", "change:v"], await Events(page));

        // Locked again: the buttons follow at once.
        await Js.Run(page, "() => document.getElementById('v').disabled = true");
        await page.WaitForFunctionAsync("() => document.getElementById('undo').disabled && document.getElementById('clear').disabled");
    }

    [Fact]
    public async Task The_ink_is_its_token_and_follows_the_variant()
    {
        if (NoBrowser) return;
        var page = await OpenPad();
        await Stroke(page, (0.1, 0.5), (0.9, 0.5));

        // The most opaque pixel of the stroke, against the token resolved through the pad.
        const string Read = """
            () => {
                const c = document.querySelector('#pad > canvas');
                const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
                let best = 3;
                for (let i = 3; i < d.length; i += 4) if (d[i] > d[best]) best = i;
                const probe = document.createElement('i');
                document.getElementById('pad').append(probe);
                probe.style.color = 'var(--signature-ink)';
                const want = getComputedStyle(probe).color.match(/[\d.]+/g).slice(0, 3).map(Number);
                probe.remove();
                return [d[best - 3], d[best - 2], d[best - 1], ...want];
            }
            """;
        foreach (var variant in new[] { "dark", "light" })
        {
            await Js.Run(page, $"() => document.documentElement.dataset.variant = '{variant}'");
            await Js.Frames(page);
            var c = await page.EvaluateAsync<double[]>(Read);
            for (var i = 0; i < 3; i++) Assert.Equal(c[i + 3], c[i], 2);
        }
    }

    [Fact]
    public async Task The_pad_is_an_image_named_by_the_app_and_its_edge_follows_the_field()
    {
        if (NoBrowser) return;
        // A transition would hand back a colour on its way there.
        var page = await OpenPad(extraHead: "<style>*, *::before, *::after { transition: none !important; }</style>");
        Assert.Equal("img", await page.GetAttributeAsync("#pad", "role"));
        Assert.Equal("Signature", await page.GetAttributeAsync("#pad", "aria-label"));
        Assert.Equal("true", await page.GetAttributeAsync("#pad > canvas", "aria-hidden"));

        var edges = await page.EvaluateAsync<string[]>("""
            () => {
                const pad = document.getElementById('pad'), v = document.getElementById('v');
                const probe = document.createElement('i');
                document.body.append(probe);
                const resolve = t => { probe.style.color = `var(${t})`; return getComputedStyle(probe).color; };
                const edge = () => getComputedStyle(pad).borderTopColor;
                const out = [];
                out.push(edge(), resolve('--field-border'));
                v.setAttribute('data-state', 'warn');
                out.push(edge(), resolve('--field-border-warn'));
                v.setAttribute('aria-invalid', 'true');
                out.push(edge(), resolve('--field-border-danger'));
                v.removeAttribute('data-state');
                out.push(edge(), resolve('--field-border-danger'));
                v.removeAttribute('aria-invalid');
                v.disabled = true;
                out.push(edge(), resolve('--disabled-border'));
                return out;
            }
            """);
        for (var i = 0; i < edges.Length; i += 2) Assert.Equal(edges[i + 1], edges[i]);
    }

    [Fact]
    public async Task Markup_without_a_field_is_reported_not_drawn()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""<div class="form-signature" id="s" data-signature data-signature-eager><div class="form-signature-pad" data-signature-pad aria-label="Signature"></div></div>""");
        await page.WaitForFunctionAsync("() => document.getElementById('s').dataset.signatureState === 'error'");
        Assert.Equal(0, await page.Locator("#s canvas").CountAsync());
    }
}
