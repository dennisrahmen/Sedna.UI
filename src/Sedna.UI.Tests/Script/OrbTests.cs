using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The orb from markup to drawing, asserted against <c>docs/orb.md</c>: it loads only when
/// a page shows one, draws every state at every size, repaints when the state or its
/// colours change, holds still for a reader who asked for no motion, and lets go of an
/// element that has gone.
/// </summary>
/// <remarks>
/// The front door in <c>Sedna.UI.js</c> imports <c>Sedna.UI.orb.js</c> by relative URL;
/// both are served from their shipped files, so a broken import fails here as it would in
/// an app. What an orb draws is read back from its own canvas.
/// </remarks>
public class OrbTests : ScriptTestBase
{
    private static readonly string[] Modes =
    [
        "idle", "thinking", "searching", "working", "listening", "speaking", "waiting", "error", "done",
        "solving", "writing", "connecting", "planning", "shaping", "reading", "remembering", "syncing", "analyzing",
    ];

    private async Task<IPage> OpenOrb(string body, bool reduced = false) =>
        await Open(body, head: StylesheetTag, reducedMotion: reduced ? ReducedMotion.Reduce : ReducedMotion.NoPreference);

    private static async Task Drawn(IPage page, string selector) =>
        await page.WaitForFunctionAsync(
            $"() => {{ const c = document.querySelector('{selector} > canvas'); return c && c.width > 0; }}");

    /// <summary>How many pixels of the orb's canvas are painted at all.</summary>
    private static Task<int> Painted(IPage page, string selector) =>
        page.EvaluateAsync<int>($$"""
            () => {
                const c = document.querySelector('{{selector}} > canvas');
                const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
                let n = 0;
                for (let i = 3; i < d.length; i += 4) if (d[i] > 8) n++;
                return n;
            }
            """);

    private static Task<string> Frame(IPage page, string selector) =>
        page.EvaluateAsync<string>($"() => document.querySelector('{selector} > canvas').toDataURL()");

    [Fact]
    public async Task A_page_without_an_orb_never_downloads_the_module()
    {
        if (NoBrowser) return;
        var page = await OpenOrb("<main><p>No orb here.</p></main>");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.DoesNotContain(Requests, r => r.Url.Contains("Sedna.UI.orb.js", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_state_and_activity_draws_at_every_size()
    {
        if (NoBrowser) return;
        // The three drawings by size — lines below 28px, dots, finer dots from 72px —
        // each have to show something for every mode, or an orb in a table row is blank.
        var body = string.Concat(Modes.SelectMany(mode => new[] { 16, 32, 96 }.Select(size =>
            $"""<span class="orb" id="o-{mode}-{size}" data-orb="{mode}" data-orb-eager style="--orb-size: {size}px" aria-label="{mode}"></span>""")));
        var page = await OpenOrb(body, reduced: true);

        var blank = new List<string>();
        foreach (var mode in Modes)
        {
            foreach (var size in new[] { 16, 32, 96 })
            {
                var id = $"#o-{mode}-{size}";
                await Drawn(page, id);
                if (await Painted(page, id) < size) blank.Add($"{mode} at {size}px");
            }
        }

        Assert.True(blank.Count == 0, "Drew nothing: " + string.Join(", ", blank));
    }

    [Fact]
    public async Task The_orb_is_an_image_named_by_the_app()
    {
        if (NoBrowser) return;
        var page = await OpenOrb("""<span class="orb" id="o" data-orb="thinking" data-orb-eager aria-label="Thinking about the sync"></span>""");
        await Drawn(page, "#o");

        Assert.Equal("img", await page.GetAttributeAsync("#o", "role"));
        Assert.Equal("Thinking about the sync", await page.GetAttributeAsync("#o", "aria-label"));
        // No word of its own: the canvas is hidden, and nothing else was added that reads.
        Assert.Equal("true", await page.GetAttributeAsync("#o > canvas", "aria-hidden"));
        Assert.Equal("", (await page.InnerTextAsync("#o")).Trim());
    }

    [Fact]
    public async Task Changing_the_state_draws_the_new_one()
    {
        if (NoBrowser) return;
        // Reduced motion, so no clock runs and only the change itself can repaint.
        var page = await OpenOrb("""<span class="orb orb--lg" id="o" data-orb="thinking" data-orb-eager aria-label="Thinking"></span>""", reduced: true);
        await Drawn(page, "#o");
        var before = await Frame(page, "#o");

        await page.EvaluateAsync("() => document.getElementById('o').dataset.orb = 'done'");
        await page.WaitForFunctionAsync("before => document.querySelector('#o > canvas').toDataURL() !== before", before);
    }

    [Fact]
    public async Task A_change_of_colour_repaints_the_orb()
    {
        if (NoBrowser) return;
        var page = await OpenOrb("""<span class="orb orb--lg" id="o" data-orb="working" data-orb-eager aria-label="Working"></span>""", reduced: true);
        await Drawn(page, "#o");

        // The tone, on the orb itself.
        var before = await Frame(page, "#o");
        await page.EvaluateAsync("() => document.getElementById('o').dataset.orbTone = 'danger'");
        await page.WaitForFunctionAsync("before => document.querySelector('#o > canvas').toDataURL() !== before", before);

        // The variant, on the root — what the theme toggle writes.
        before = await Frame(page, "#o");
        await page.EvaluateAsync("() => document.documentElement.setAttribute('data-variant', 'light')");
        await page.WaitForFunctionAsync("before => document.querySelector('#o > canvas').toDataURL() !== before", before);
    }

    [Fact]
    public async Task Under_reduced_motion_the_orb_holds_one_still_frame()
    {
        if (NoBrowser) return;
        var page = await OpenOrb("""<span class="orb orb--lg" id="o" data-orb="searching" data-orb-eager aria-label="Searching"></span>""", reduced: true);
        await Drawn(page, "#o");
        Assert.True(await Painted(page, "#o") > 64, "The still frame is blank.");

        var first = await Frame(page, "#o");
        await page.WaitForTimeoutAsync(400);
        Assert.Equal(first, await Frame(page, "#o"));
    }

    [Fact]
    public async Task With_motion_allowed_the_orb_moves()
    {
        if (NoBrowser) return;
        var page = await OpenOrb("""<span class="orb orb--lg" id="o" data-orb="searching" data-orb-eager aria-label="Searching"></span>""");
        await Drawn(page, "#o");

        var first = await Frame(page, "#o");
        await page.WaitForFunctionAsync("first => document.querySelector('#o > canvas').toDataURL() !== first", first);
    }

    [Fact]
    public async Task A_small_orb_draws_half_as_often_as_a_large_one()
    {
        if (NoBrowser) return;
        // Below 72px a dot moves well under a pixel a frame, so those orbs draw at most
        // thirty times a second; the finest drawing at most sixty — on any display. Counted
        // by the canvas being cleared, which is the first thing every frame of an orb does.
        var page = await OpenOrb("""
            <span class="orb" id="small" data-orb="searching" data-orb-eager style="--orb-size: 32px" aria-label="Searching"></span>
            <span class="orb" id="large" data-orb="searching" data-orb-eager style="--orb-size: 96px" aria-label="Searching"></span>
            """);
        await Drawn(page, "#small");
        await Drawn(page, "#large");

        var counts = await page.EvaluateAsync<int[]>("""
            async () => {
                const clear = CanvasRenderingContext2D.prototype.clearRect;
                const seen = new Map();
                CanvasRenderingContext2D.prototype.clearRect = function (...a) { seen.set(this.canvas, (seen.get(this.canvas) || 0) + 1); return clear.apply(this, a); };
                await new Promise(r => setTimeout(r, 1000));
                CanvasRenderingContext2D.prototype.clearRect = clear;
                return ['#small', '#large'].map(id => seen.get(document.querySelector(id + ' > canvas')) || 0);
            }
            """);

        // A ratio rather than two rates, so a slow machine dropping frames does not fail it.
        Assert.True(counts[0] > 0 && counts[0] <= 34, $"The small orb drew {counts[0]} times in a second.");
        Assert.True(counts[1] <= 64, $"The large orb drew {counts[1]} times in a second.");
        Assert.True(counts[0] * 1.6 <= counts[1], $"The small orb drew {counts[0]} times and the large {counts[1]}.");
    }

    [Fact]
    public async Task An_orb_whose_element_has_gone_is_let_go()
    {
        if (NoBrowser) return;
        var page = await OpenOrb("""<div id="host"><span class="orb" id="o" data-orb="working" data-orb-eager aria-label="Working"></span></div>""");
        await Drawn(page, "#o");

        await page.EvaluateAsync("() => { window.__orb = document.getElementById('o'); window.__orb.remove(); }");
        // The canvas and the probe the module added are taken back out of the element.
        await page.WaitForFunctionAsync("() => window.__orb.querySelector('canvas') === null");
    }
}
