using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.form-range-box</c> (21-range.js): the low end of a span never passes the high end, by
/// pointer, keyboard or code; the fill and each bubble follow the values as ratios of the
/// track; and all of it mirrors under <c>dir="rtl"</c>.
/// </summary>
public class RangeTests : ScriptTestBase
{
    /// <summary>
    /// A span with a bubble on each end, a value slot inside one of them, and a listener where a
    /// framework binds — on the document, bubbling — recording what it reads.
    /// </summary>
    private const string Span = """
        <form id="f" style="width:420px; padding:40px">
          <div class="form-range-box" id="box">
            <output class="form-range-bubble" for="lo" id="blo" data-range-value>200</output>
            <output class="form-range-bubble" for="hi" id="bhi"><span data-range-value>800</span> EUR</output>
            <input class="form-range" id="lo" name="lo" type="range" min="0" max="1000" step="50" value="200" aria-label="Lowest">
            <input class="form-range" id="hi" name="hi" type="range" min="0" max="1000" step="50" value="800" aria-label="Highest">
          </div>
        </form>
        <script>
          window.read = [];
          document.addEventListener('input', e => window.read.push(e.target.id + '=' + e.target.value));
        </script>
        """;

    private static Task<string[]> Values(IPage page) =>
        page.EvaluateAsync<string[]>("() => [document.getElementById('lo').value, document.getElementById('hi').value]");

    private static Task<string[]> Fill(IPage page) =>
        page.EvaluateAsync<string[]>("""
            () => { const s = document.getElementById('box').style;
                    return [s.getPropertyValue('--range-start'), s.getPropertyValue('--range-end')]; }
            """);

    /// <summary>Sets a range's value the way a drag does: the value, then its input event.</summary>
    private static Task Drag(IPage page, string id, int value) =>
        Js.Run(page, "([id, v]) => { const i = document.getElementById(id); i.value = v; i.dispatchEvent(new Event('input', { bubbles: true })); }",
            new object[] { id, value });

    [Fact]
    public async Task The_fill_and_the_bubbles_start_where_the_values_are()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Span);

        Assert.Equal(["0.2", "0.8"], await Fill(page));
        var at = await page.EvaluateAsync<string[]>("""
            () => ['blo', 'bhi'].map(id => document.getElementById(id).style.getPropertyValue('--range-at'))
            """);
        Assert.Equal(["0.2", "0.8"], at);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_low_end_never_passes_the_high_end()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Span);

        await Drag(page, "lo", 950);
        Assert.Equal(["800", "800"], await Values(page));

        await Drag(page, "hi", 100);
        Assert.Equal(["800", "800"], await Values(page));

        await Drag(page, "hi", 900);
        await Drag(page, "lo", 300);
        Assert.Equal(["300", "900"], await Values(page));
        Assert.Equal(["0.3", "0.9"], await Fill(page));

        // A listener where a framework binds read each end already put back.
        var read = await page.EvaluateAsync<string[]>("() => window.read");
        Assert.Equal(["lo=800", "hi=800", "hi=900", "lo=300"], read);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_keyboard_stops_each_end_at_the_other()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Span);

        await page.FocusAsync("#lo");
        await page.Keyboard.PressAsync("End");
        Assert.Equal(["800", "800"], await Values(page));
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal(["800", "800"], await Values(page));

        await page.FocusAsync("#hi");
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal(["800", "850"], await Values(page));
        await page.Keyboard.PressAsync("Home");
        Assert.Equal(["800", "800"], await Values(page));

        await page.FocusAsync("#lo");
        await page.Keyboard.PressAsync("Home");
        Assert.Equal(["0", "800"], await Values(page));
        Assert.Equal(["0", "0.8"], await Fill(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_bubble_shows_its_value_over_its_thumb()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Span);
        await Drag(page, "lo", 350);
        await Drag(page, "hi", 650);

        var text = await page.EvaluateAsync<string[]>("() => ['blo', 'bhi'].map(id => document.getElementById(id).textContent)");
        Assert.Equal(["350", "650 EUR"], text);

        foreach (var (bubble, ratio) in new[] { ("blo", 0.35), ("bhi", 0.65) })
        {
            var (centre, want) = await Centres(page, bubble, ratio, rtl: false);
            Assert.True(Math.Abs(centre - want) <= 0.5, $"{bubble} is centred at {centre:0.00}, its thumb at {want:0.00}.");
        }
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Under_rtl_the_track_runs_from_the_right_and_the_fill_and_bubbles_follow()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Span);
        await Js.Run(page, "() => document.documentElement.dir = 'rtl'");
        await Drag(page, "lo", 250);

        foreach (var (bubble, ratio) in new[] { ("blo", 0.25), ("bhi", 0.8) })
        {
            var (centre, want) = await Centres(page, bubble, ratio, rtl: true);
            Assert.True(Math.Abs(centre - want) <= 0.5, $"Under rtl {bubble} is centred at {centre:0.00}, its thumb at {want:0.00}.");
        }

        var toward = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('box'), '::before').backgroundImage");
        Assert.StartsWith("linear-gradient(to left", toward, StringComparison.Ordinal);

        await page.FocusAsync("#lo");
        await page.Keyboard.PressAsync("End");
        Assert.Equal(["800", "800"], await Values(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_reset_and_a_render_are_followed()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Span);
        await Drag(page, "lo", 600);
        await Js.Run(page, "() => document.getElementById('f').reset()");
        await page.WaitForFunctionAsync("() => document.getElementById('box').style.getPropertyValue('--range-start') === '0.2'");
        Assert.Equal("200", await page.EvaluateAsync<string>("() => document.getElementById('blo').textContent"));

        // Markup a render adds — as a framework swaps in its own on connecting — is picked up.
        await Js.Run(page, """
            () => document.body.insertAdjacentHTML('beforeend',
              '<div class="form-range-box" id="late"><input class="form-range" type="range" min="10" max="20" value="15" aria-label="Late"></div>')
            """);
        await page.WaitForFunctionAsync("() => document.getElementById('late').style.getPropertyValue('--range-end') === '0.5'");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_bubble_written_by_the_app_keeps_its_text()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="form-range-box" style="width:300px">
              <output class="form-range-bubble" for="r" id="b">Half</output>
              <input class="form-range" id="r" type="range" value="50" aria-label="Level">
            </div>
            """);
        await Drag(page, "r", 70);
        Assert.Equal("Half", await page.EvaluateAsync<string>("() => document.getElementById('b').textContent"));
        Assert.Equal("0.7", await page.EvaluateAsync<string>("() => document.getElementById('b').style.getPropertyValue('--range-at')"));
        Assert.Empty(errors);
    }

    /// <summary>
    /// A bubble's centre, and where its thumb's centre is by the value alone: half a thumb in
    /// from the track's start edge, plus the ratio of what is left.
    /// </summary>
    private static async Task<(double Centre, double Want)> Centres(IPage page, string bubble, double ratio, bool rtl)
    {
        var m = await page.EvaluateAsync<double[]>("""
            id => {
              const b = document.getElementById(id).getBoundingClientRect();
              const box = document.getElementById('box').getBoundingClientRect();
              const thumb = parseFloat(getComputedStyle(document.getElementById('box')).getPropertyValue('--range-thumb'));
              return [b.left + b.width / 2, box.left, box.right, thumb];
            }
            """, bubble);
        var along = m[3] / 2 + ratio * (m[2] - m[1] - m[3]);
        return (m[0], rtl ? m[2] - along : m[1] + along);
    }
}
