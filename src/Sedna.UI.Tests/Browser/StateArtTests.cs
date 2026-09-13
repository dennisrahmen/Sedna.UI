using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// The state illustrations, rendered: what a <c>&lt;use&gt;</c> of a shipped symbol
/// actually paints, read back pixel by pixel.
/// </summary>
/// <remarks>
/// <para>
/// A <c>&lt;use&gt;</c> clones the symbol into a closed shadow tree, so nothing about
/// the drawing can be read through the DOM — <c>getComputedStyle</c> reaches the
/// original in the sprite, whose custom properties resolve against the sprite's own
/// ancestors and not the card the art sits in. The only honest witness is the paint,
/// so these tests screenshot the art and sample it.
/// </para>
/// <para>
/// This is the test that would have caught the drawings rendering black on iOS: the
/// knockouts filled with the initial <c>fill</c> because the surface token never
/// reached an external reference. It runs in Chromium, which was never the engine
/// that failed, but the sprite is now in the page, where every engine agrees.
/// </para>
/// </remarks>
public class StateArtTests : ScriptTestBase
{
    private static string Sprite =>
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "StateArt", "Sedna.UI.states.svg"));

    [Fact]
    public async Task The_knockout_takes_the_colour_of_the_surface_the_art_sits_on()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            {Sprite}
            <div class="card" id="host" style="width:240px;padding:24px">
                <svg class="state-art state-art--lg" id="art" aria-hidden="true"><use href="#session-expired" /></svg>
            </div>
            """);

        // The clock's knockout ring: inside the r=26 disc, outside the r=20 accent
        // stroke, over the pass's tinted rectangle. On a card it has to be the card.
        var pixel = await Sample(page, "#art", 65 / 120.0, 80 / 120.0);
        var card = await Rgb(page, "getComputedStyle(document.getElementById('host')).backgroundColor");

        AssertClose(card, pixel, "the knockout over a card");
    }

    [Fact]
    public async Task The_accent_follows_the_state_family()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            {Sprite}
            <div class="card" style="width:240px;padding:24px">
                <svg class="state-art state-art--lg" id="brand" aria-hidden="true"><use href="#session-expired" /></svg>
                <svg class="state-art state-art--lg state-art--danger" id="danger" aria-hidden="true"><use href="#session-expired" /></svg>
            </div>
            <span id="probe-brand" style="color: var(--brand)"></span>
            <span id="probe-danger" style="color: var(--danger-fg)"></span>
            """);

        // The middle of the clock's accent ring, at three o'clock: r=20 from (88,80).
        var brand = await Sample(page, "#brand", 108 / 120.0, 80 / 120.0);
        var danger = await Sample(page, "#danger", 108 / 120.0, 80 / 120.0);

        AssertClose(await Rgb(page, "getComputedStyle(document.getElementById('probe-brand')).color"), brand, "the default accent");
        AssertClose(await Rgb(page, "getComputedStyle(document.getElementById('probe-danger')).color"), danger, "the danger accent");
    }

    [Fact]
    public async Task Motion_is_paused_until_the_art_is_live()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            {Sprite}
            <div class="card" style="width:400px;padding:24px;display:flex;gap:24px">
                <svg class="state-art state-art--lg" id="still" aria-hidden="true"><use href="#offline" /></svg>
                <svg class="state-art state-art--lg state-art--live" id="live" aria-hidden="true"><use href="#offline" /></svg>
            </div>
            """);

        // The upper spark of the missing signal, which blinks over 1.6s. Sampled
        // several times across more than one cycle rather than twice a fixed interval
        // apart: a screenshot takes an unknowable time on a loaded machine, so two
        // samples can land in the same phase by accident, and a still drawing has to
        // read the same at every one of them.
        var still = new List<int[]>();
        var live = new List<int[]>();
        for (var i = 0; i < 8; i++)
        {
            still.Add(await Sample(page, "#still", 60 / 120.0, 33 / 120.0));
            live.Add(await Sample(page, "#live", 60 / 120.0, 33 / 120.0));
            await page.WaitForTimeoutAsync(250);
        }

        foreach (var sample in still) AssertClose(still[0], sample, "a paused drawing");

        var spread = live.Max(a => live.Max(b => Distance(a, b)));
        Assert.True(spread > 24,
            "A live drawing did not move: " + string.Join(", ", live.Select(Text)));
    }

    /// <summary>Screenshots an element and returns the pixel at a fraction of its box.</summary>
    private static async Task<int[]> Sample(IPage page, string selector, double x, double y)
    {
        var element = await page.QuerySelectorAsync(selector);
        Assert.NotNull(element);
        var png = Convert.ToBase64String(await element!.ScreenshotAsync(new() { Type = ScreenshotType.Png }));

        // Decoded by the browser itself, so the test needs no image library.
        return await page.EvaluateAsync<int[]>("""
            async ([png, x, y]) => {
                const img = new Image();
                img.src = 'data:image/png;base64,' + png;
                await img.decode();
                const c = document.createElement('canvas');
                c.width = img.width; c.height = img.height;
                const ctx = c.getContext('2d');
                ctx.drawImage(img, 0, 0);
                const d = ctx.getImageData(Math.round(img.width * x), Math.round(img.height * y), 1, 1).data;
                return [d[0], d[1], d[2]];
            }
            """, new object[] { png, x, y });
    }

    private static async Task<int[]> Rgb(IPage page, string expression)
    {
        var css = await page.EvaluateAsync<string>($"() => {expression}");
        var parts = css.Replace("rgb(", "", StringComparison.Ordinal).Replace("rgba(", "", StringComparison.Ordinal)
            .TrimEnd(')').Split(',');
        return [int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()), int.Parse(parts[2].Trim())];
    }

    private static int Distance(int[] a, int[] b) =>
        Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) + Math.Abs(a[2] - b[2]);

    private static string Text(int[] rgb) => $"rgb({rgb[0]}, {rgb[1]}, {rgb[2]})";

    private static void AssertClose(int[] expected, int[] actual, string what) =>
        Assert.True(Distance(expected, actual) <= 12,
            $"{what} painted {Text(actual)}, expected {Text(expected)}.");
}
