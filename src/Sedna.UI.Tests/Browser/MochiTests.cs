using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Mochi, rendered: where each perch puts the drawing against a real card, how a pose
/// change eases, and every combination of perch, action and mood held to the rig's
/// invariants.
/// </summary>
/// <remarks>
/// <para>
/// A <c>&lt;use&gt;</c> clones the drawing into a closed shadow tree, where nothing can be
/// measured. So these fixtures write the sprite into the page, as <c>SednaMochi</c> does,
/// and then copy the symbol's parts into each Mochi's own <c>&lt;svg&gt;</c> in place of
/// the <c>&lt;use&gt;</c>. The parts carry the same classes and inherit the same custom
/// properties from the same span, so they take the same pose — and now a hand's position,
/// an arm's angle and a part's opacity can be read back.
/// </para>
/// <para>
/// Where a test is about what the reader sees through a clip, it reads the paint instead.
/// </para>
/// </remarks>
public class MochiTests : ScriptTestBase
{
    private static string Sprite =>
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "StateArt", "Sedna.UI.mochi.svg"));

    private const string Svg = """<svg viewBox="0 0 160 160" aria-hidden="true"><use href="#sedna-mochi" /></svg>""";

    /// <summary>Replaces every Mochi's &lt;use&gt; with a copy of the drawing's parts.</summary>
    private const string Inline = """
        () => {
            const symbol = document.getElementById('sedna-mochi');
            for (const svg of document.querySelectorAll('.mochi > svg')) {
                svg.replaceChildren(...[...symbol.childNodes].map(n => n.cloneNode(true)));
            }
        }
        """;

    private async Task<IPage> OpenMochi(string body, bool reduced = true, string dir = "ltr")
    {
        var page = await Open(
            $"""
            {Sprite}
            <div dir="{dir}" style="padding: 220px 240px">{body}</div>
            """,
            head: StylesheetTag,
            reducedMotion: reduced ? ReducedMotion.Reduce : ReducedMotion.NoPreference);
        await page.EvaluateAsync(Inline);
        return page;
    }

    private static string OnCard(string perch, int size, string extra = "") => $"""
        <div class="mochi-host" style="width: 320px">
            <div class="card" id="card" style="height: 140px"><div class="card-body">Order sync</div></div>
            <span class="mochi" id="m" data-perch="{perch}" {extra} style="--size: {size}px" role="img" aria-label="Ops agent">{Svg}</span>
        </div>
        """;

    /// <summary>The page-space box of the first element matching a selector inside #m.</summary>
    private static async Task<JsonElement> Box(IPage page, string selector) =>
        await page.EvaluateAsync<JsonElement>($$"""
            () => {
                const r = document.querySelector('#m {{selector}}').getBoundingClientRect();
                return { x: r.x + r.width / 2, y: r.y + r.height / 2, left: r.left, right: r.right, top: r.top, bottom: r.bottom };
            }
            """);

    private static async Task<JsonElement> CardBox(IPage page, string id = "card") =>
        await page.EvaluateAsync<JsonElement>($$"""
            () => { const r = document.getElementById('{{id}}').getBoundingClientRect();
                    return { left: r.left, right: r.right, top: r.top, bottom: r.bottom, y: r.y + r.height / 2, x: r.x + r.width / 2 }; }
            """);

    private static double D(JsonElement e, string p) => e.GetProperty(p).GetDouble();

    // ── Perches ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(64)]
    [InlineData(112)]
    [InlineData(220)]
    public async Task Peeking_puts_both_hands_on_the_top_edge(int size)
    {
        if (NoBrowser) return;
        var page = await OpenMochi(OnCard("peek", size));
        var card = await CardBox(page);

        foreach (var grip in new[] { ".sedna-mochi-grip-l .sedna-mochi-palm", ".sedna-mochi-grip-r .sedna-mochi-palm" })
        {
            var hand = await Box(page, grip);
            Assert.InRange(D(hand, "y") - D(card, "top"), -1, 1);
            Assert.InRange(D(hand, "x"), D(card, "left"), D(card, "right"));
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(112)]
    [InlineData(220)]
    public async Task Hanging_puts_both_hands_on_the_bottom_edge(int size)
    {
        if (NoBrowser) return;
        var page = await OpenMochi(OnCard("hang", size));
        var card = await CardBox(page);

        foreach (var grip in new[] { ".sedna-mochi-grip-hl .sedna-mochi-palm", ".sedna-mochi-grip-hr .sedna-mochi-palm" })
        {
            var hand = await Box(page, grip);
            Assert.InRange(D(hand, "y") - D(card, "bottom"), -1, 1);
        }

        // The body hangs below the card, not over it.
        var body = await Box(page, ".sedna-mochi-body");
        Assert.True(D(body, "top") > D(card, "bottom") - 2, "The body overlaps the card it hangs from.");
    }

    [Theory]
    [InlineData(64)]
    [InlineData(112)]
    [InlineData(220)]
    public async Task Standing_puts_the_feet_on_the_bottom_edge(int size)
    {
        if (NoBrowser) return;
        var page = await OpenMochi(OnCard("stand", size));
        var card = await CardBox(page);

        // The ground shadow is drawn on the feet's line.
        var ground = await Box(page, ".sedna-mochi-ground");
        Assert.InRange(D(ground, "y") - D(card, "bottom"), -1, 1);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(112)]
    [InlineData(220)]
    public async Task Sitting_puts_the_seat_on_the_top_edge(int size)
    {
        if (NoBrowser) return;
        var page = await OpenMochi(OnCard("sit", size));
        var card = await CardBox(page);

        // The seat's shadow is drawn two units under the seat line.
        var seat = await Box(page, ".sedna-mochi-seat");
        Assert.InRange(D(seat, "y") - D(card, "top") - 2.0 / 160 * size, -1, 1);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task Leaning_out_holds_the_end_edge_in_either_direction(string dir)
    {
        if (NoBrowser) return;
        var page = await OpenMochi(OnCard("side", 112), dir: dir);
        var card = await CardBox(page);

        var hand = await Box(page, ".sedna-mochi-grip-side .sedna-mochi-palm");
        var edge = dir == "rtl" ? D(card, "left") : D(card, "right");
        Assert.InRange(D(hand, "x") - edge, -2, 2);
    }

    [Theory]
    [InlineData("peek", 0, -1)]
    [InlineData("look", 0, -1)]
    [InlineData("hang", 0, 1)]
    [InlineData("side", 1, 0)]
    public async Task What_is_behind_the_card_is_not_painted_over_it(string perch, int dx, int dy)
    {
        if (NoBrowser) return;
        // A plain card, so a pixel of the body over it would stand out.
        var page = await OpenMochi(OnCard(perch, 160));
        var card = await CardBox(page);
        var mochi = await page.EvaluateAsync<JsonElement>(
            "() => { const r = document.getElementById('m').getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; }");

        // Just inside the card, where the body would be if nothing clipped it: on the
        // edge the perch meets, at the middle of the drawing along that edge.
        var (insideX, insideY) = (dx, dy) switch
        {
            (0, -1) => (D(mochi, "x") + 0, D(card, "top") + 14),
            (0, 1) => (D(mochi, "x") + 0, D(card, "bottom") - 14),
            _ => (D(card, "right") - 14, D(mochi, "y") + 22),
        };
        // And just outside it, where the body shows.
        var (outsideX, outsideY) = (dx, dy) switch
        {
            (0, -1) => (D(mochi, "x"), D(card, "top") - 24),
            (0, 1) => (D(mochi, "x"), D(card, "bottom") + 40),
            _ => (D(card, "right") + 24, D(mochi, "y") + 22),
        };

        var inside = await Pixel(page, insideX, insideY);
        var outside = await Pixel(page, outsideX, outsideY);
        var cardColour = await Pixel(page, D(card, "left") + 8, D(card, "bottom") - 8);
        var bodyColour = await page.EvaluateAsync<int[]>("""
            () => { const c = getComputedStyle(document.querySelector('#m .sedna-mochi-body')).fill.match(/\d+/g).map(Number); return c.slice(0, 3); }
            """);

        Assert.True(Distance(inside, cardColour) <= 16,
            $"Inside the card, under a {perch} Mochi, painted {Text(inside)} rather than the card's {Text(cardColour)}.");
        Assert.True(Distance(outside, bodyColour) <= 90,
            $"Outside the card a {perch} Mochi painted {Text(outside)}, not its body ({Text(bodyColour)}).");
    }

    [Theory]
    [InlineData("end")]
    [InlineData("start")]
    public async Task Pointing_puts_the_fingertip_on_the_anchor(string facing)
    {
        if (NoBrowser) return;
        var facingAttr = facing == "start" ? "data-facing=\"start\"" : "";
        var page = await OpenMochi($"""
            <div class="mochi-host" style="padding: 40px 160px">
                <button class="btn" id="target" type="button" style="anchor-name: --target">Approve the change</button>
                <span class="mochi" id="m" data-perch="point" data-action="point" {facingAttr}
                      style="--size: 104px; position-anchor: --target" role="img" aria-label="Ops agent">{Svg}</span>
            </div>
            """);
        var target = await CardBox(page, "target");
        var finger = await Box(page, ".sedna-mochi-finger");

        var tip = facing == "start" ? D(finger, "left") : D(finger, "right");
        var edge = facing == "start" ? D(target, "right") : D(target, "left");
        Assert.InRange(Math.Abs(tip - edge), 0, 6);
        Assert.InRange(Math.Abs(D(finger, "y") - D(target, "y")), 0, 4);
    }

    // ── Changing pose ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_new_action_eases_the_arm_the_short_way_round()
    {
        if (NoBrowser) return;
        var page = await OpenMochi("""<span class="mochi" id="m" data-action="idle" role="img" aria-label="Ops agent">""" + Svg + "</span>", reduced: false);

        // Idle holds the right arm at 67°, a wave at −53°. Sampled every frame across
        // the change: it has to pass through the angles in between, settle on the wave,
        // and never swing round behind the body to get there.
        var angles = await page.EvaluateAsync<double[]>("""
            async () => {
                const arm = document.querySelector('#m .sedna-mochi-arm-br');
                const angle = () => { const m = new DOMMatrix(getComputedStyle(arm).transform); return Math.atan2(m.b, m.a) * 180 / Math.PI; };
                const out = [angle()];
                document.getElementById('m').dataset.action = 'wave';
                const t0 = performance.now();
                while (performance.now() - t0 < 1100) {
                    await new Promise(r => requestAnimationFrame(r));
                    out.push(angle());
                }
                return out;
            }
            """);

        Assert.InRange(angles[0], 66, 68);
        Assert.InRange(angles[^1], -54, -52);
        Assert.True(angles.Count(a => a < 60 && a > -45) >= 3,
            "The arm jumped to the new pose instead of easing: " + string.Join(", ", angles.Select(a => a.ToString("0"))));
        Assert.All(angles, a => Assert.InRange(a, -75, 70));
    }

    [Fact]
    public async Task A_new_mood_cross_fades_the_face()
    {
        if (NoBrowser) return;
        var page = await OpenMochi("""<span class="mochi" id="m" data-mood="happy" role="img" aria-label="Ops agent">""" + Svg + "</span>", reduced: false);

        // The fade is short on purpose — a slow one shows two faces at once — so it is
        // frozen at its middle rather than sampled frame by frame and hoped for.
        var fade = await page.EvaluateAsync<double[]>("""
            () => {
                const frown = document.querySelector('#m .sedna-mochi-m-frown');
                // Styled once before the change, or there is no "before" to fade from.
                getComputedStyle(frown).opacity;
                document.getElementById('m').dataset.mood = 'worried';
                getComputedStyle(frown).opacity;
                const fade = frown.getAnimations().find(a => a instanceof CSSTransition && a.transitionProperty === 'opacity');
                if (!fade) return [-1, -1];
                fade.pause();
                fade.currentTime = fade.effect.getTiming().duration / 2;
                const middle = Number(getComputedStyle(frown).opacity);
                fade.finish();
                return [middle, Number(getComputedStyle(frown).opacity)];
            }
            """);

        Assert.True(fade[0] >= 0, "Changing the mood started no fade on the mouth.");
        Assert.InRange(fade[0], 0.05, 0.95);
        Assert.Equal(1, fade[1], 2);
    }

    [Fact]
    public async Task Under_reduced_motion_nothing_loops_and_every_change_lands_at_once()
    {
        if (NoBrowser) return;
        var page = await OpenMochi("""<span class="mochi" id="m" data-perch="hang" data-action="wave" role="img" aria-label="Ops agent">""" + Svg + "</span>");

        var moving = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('#m [class*="sedna-mochi-"]')]
                .filter(el => { const s = getComputedStyle(el); return s.animationName !== 'none' || (s.transitionDuration.split(',').some(d => parseFloat(d) > 0)); })
                .map(el => el.getAttribute('class'))
            """);

        Assert.True(moving.Length == 0, "Still moving under reduced motion: " + string.Join(", ", moving.Distinct()));
    }

    // ── Every combination ─────────────────────────────────────────────────────

    [Fact]
    public async Task Every_perch_action_and_mood_holds_the_rig_together()
    {
        if (NoBrowser) return;
        var page = await OpenMochi(OnCard("sit", 112));

        // Under reduced motion every change lands at once, so each combination is read
        // in its settled pose. The rules below are the ones a reader would see broken:
        // two mouths at once, no eyes, a laptop floating behind a card, a hand that let
        // go of nothing.
        var violations = await page.EvaluateAsync<string[]>("""
            () => {
                const m = document.getElementById('m');
                const q = c => m.querySelector('.sedna-mochi-' + c);
                const op = c => Number(getComputedStyle(q(c)).opacity);
                const shown = c => getComputedStyle(q(c)).display !== 'none';
                const perches = [null, 'sit', 'peek', 'look', 'side', 'stand', 'hang', 'point'];
                const actions = ['idle', 'wave', 'point', 'think', 'work', 'talk', 'listen', 'celebrate', 'oops', 'sleep', 'surprised', 'done', 'love'];
                const moods = [null, 'neutral', 'happy', 'excited', 'surprised', 'curious', 'focused', 'worried', 'sleepy', 'love', 'wink'];
                const eyes = ['e-open', 'e-wide', 'e-happy', 'e-closed', 'e-love', 'e-wink'];
                const mouths = ['m-smile', 'm-grin', 'm-flat', 'm-frown', 'm-o', 'm-o-small', 'm-cat', 'm-talk'];
                const props = { think: 'p-think', work: 'p-laptop', celebrate: 'p-confetti', oops: 'p-sweat', sleep: 'p-zzz', listen: 'p-waves', surprised: 'p-pop', done: 'p-check', love: 'p-hearts' };
                const behind = ['peek', 'look'];
                const out = [];
                for (const perch of perches) for (const action of actions) for (const mood of moods) {
                    perch ? m.setAttribute('data-perch', perch) : m.removeAttribute('data-perch');
                    m.setAttribute('data-action', action);
                    mood ? m.setAttribute('data-mood', mood) : m.removeAttribute('data-mood');
                    const at = `${perch ?? 'none'}/${action}/${mood ?? 'auto'}`;
                    const fail = what => out.push(`${at}: ${what}`);
                    const lets = action === 'wave' || action === 'point';

                    const eyeSum = eyes.reduce((s, c) => s + op(c), 0);
                    if (Math.abs(eyeSum - 1) > 0.01) fail(`eyes sum to ${eyeSum}`);
                    const mouthSum = mouths.reduce((s, c) => s + op(c), 0);
                    if (Math.abs(mouthSum - 1) > 0.01) fail(`mouths sum to ${mouthSum}`);
                    for (const c of [...eyes, ...mouths, 'legs-stand', 'legs-sit', 'grip-l', 'grip-r', 'grip-side', 'grip-hl', 'grip-hr', 'arm-bl', 'arm-br', 'arm-fl', 'arm-fr']) {
                        const o = op(c); if (o > 0.01 && o < 0.99) fail(`${c} half shown (${o})`);
                    }

                    const legs = op('legs-stand') + op('legs-sit');
                    if (behind.includes(perch) ? legs !== 0 : legs !== 1) fail(`legs ${legs}`);

                    const clipped = getComputedStyle(q('clip')).clipPath !== 'none';
                    if (clipped !== ['peek', 'look', 'side', 'hang'].includes(perch)) fail(`clip ${clipped}`);

                    if (behind.includes(perch)) {
                        if (op('grip-l') !== 1) fail('left hand let go of the edge');
                        if ((op('grip-r') === 1) === lets) fail('the right hand did not let go to ' + action);
                    } else if (op('grip-l') + op('grip-r') !== 0) fail('a hand grips an edge that is not there');
                    if (perch === 'hang') {
                        if (op('grip-hl') !== 1) fail('left hand let go of the edge');
                        if ((op('grip-hr') === 1) === lets) fail('the right hand did not let go to ' + action);
                    } else if (op('grip-hl') + op('grip-hr') !== 0) fail('a hand hangs from nothing');
                    if ((op('grip-side') === 1) !== (perch === 'side')) fail('side grip');
                    if (perch === 'side' && op('arm-fl') !== 0) fail('a front hand reaches across the card');

                    for (const [a, p] of Object.entries(props)) {
                        const want = a === action && !(p === 'p-laptop' && ['peek', 'look', 'side', 'hang'].includes(perch));
                        if (shown(p) !== want) fail(`${p} ${shown(p) ? 'shown' : 'hidden'}`);
                    }

                    const arms = ['arm-bl', 'arm-br', 'arm-fl', 'arm-fr'].filter(c => op(c) === 1);
                    for (const c of arms) {
                        const w = parseFloat(getComputedStyle(q('seg-' + c.slice(4))).width);
                        if (!(w > 0) && c.startsWith('arm-b')) fail(`${c} shown with no reach`);
                    }
                    if (!behind.includes(perch) && arms.length === 0) fail('no arms');
                    if (op('finger') === 1 && (op('arm-br') !== 1 || op('hand-br') !== 1)) fail('a finger with no hand');
                }
                return out;
            }
            """);

        Assert.True(violations.Length == 0,
            $"{violations.Length} combinations break the rig:\n" + string.Join("\n", violations.Take(40)));
    }

    [Fact]
    public async Task Every_tone_paints_its_own_body()
    {
        if (NoBrowser) return;
        var page = await OpenMochi(OnCard("sit", 112));

        var fills = await page.EvaluateAsync<string[]>("""
            () => ['pink', 'mint', 'sky', 'lemon', 'lilac', 'coral'].map(t => {
                const m = document.getElementById('m');
                t === 'pink' ? m.removeAttribute('data-tone') : m.setAttribute('data-tone', t);
                const s = getComputedStyle(m.querySelector('.sedna-mochi-body')).fill;
                const l = getComputedStyle(m.querySelector('.sedna-mochi-seg')).fill;
                return s + ' / ' + l;
            })
            """);

        Assert.Equal(fills.Length, fills.Distinct().Count());
        Assert.DoesNotContain(fills, f => f.Contains("rgb(0, 0, 0)", StringComparison.Ordinal));
    }

    // ── Paint ─────────────────────────────────────────────────────────────────

    /// <summary>The colour the page paints at a point, read from a screenshot.</summary>
    private static async Task<int[]> Pixel(IPage page, double x, double y)
    {
        var png = Convert.ToBase64String(await page.ScreenshotAsync(new()
        {
            Type = ScreenshotType.Png,
            Clip = new() { X = (float)Math.Round(x), Y = (float)Math.Round(y), Width = 1, Height = 1 },
        }));
        return await page.EvaluateAsync<int[]>("""
            async png => {
                const img = new Image();
                img.src = 'data:image/png;base64,' + png;
                await img.decode();
                const c = document.createElement('canvas');
                c.width = img.width; c.height = img.height;
                const ctx = c.getContext('2d');
                ctx.drawImage(img, 0, 0);
                const d = ctx.getImageData(0, 0, 1, 1).data;
                return [d[0], d[1], d[2]];
            }
            """, png);
    }

    private static int Distance(int[] a, int[] b) =>
        Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) + Math.Abs(a[2] - b[2]);

    private static string Text(int[] rgb) => $"rgb({rgb[0]}, {rgb[1]}, {rgb[2]})";
}
