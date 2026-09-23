using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// The avatar ring and the avatar pair: where they actually paint.
/// </summary>
/// <remarks>
/// An outline has no box in the DOM, so whether a ring is concentric with its avatar can
/// only be read from the paint. These tests screenshot a margin around each avatar, find
/// the pixels in the ring's colour, and compare that shape's box with the avatar's.
/// </remarks>
public class AvatarRingTests : ScriptTestBase
{
    [Fact]
    public async Task The_ring_is_concentric_with_the_avatar_at_every_size()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="card" style="display:flex;gap:32px;align-items:center;padding:24px">
                <span class="avatar avatar-sm avatar--ring-warn" id="sm">AF</span>
                <span class="avatar avatar--ring-warn" id="md">AF</span>
                <span class="avatar avatar-lg avatar--ring-warn" id="lg">AF</span>
                <span class="avatar avatar-lg avatar--ring" id="brand">AF</span>
                <span class="avatar-presence avatar-presence--busy"><span class="avatar avatar-lg avatar--ring-warn" id="presence">AF</span></span>
                <span class="user-avatar avatar--ring-warn" id="widget">AF</span>
                <span class="avatar-pair avatar--ring-warn" id="pair-md"><span class="avatar">PN</span><span class="avatar">TF</span></span>
                <span class="avatar-pair avatar-lg avatar--ring-warn" id="pair-lg"><span class="avatar">PN</span><span class="avatar">TF</span></span>
            </div>
            """);

        foreach (var id in new[] { "sm", "md", "lg", "brand", "presence", "widget", "pair-md", "pair-lg" })
        {
            var ring = await RingBox(page, id);
            Assert.True(ring.Found, $"#{id}: no pixel in the ring's colour was painted.");
            Assert.True(Math.Abs(ring.CentreX) <= 1 && Math.Abs(ring.CentreY) <= 1,
                $"#{id}: the ring's centre is {ring.CentreX:0.#}, {ring.CentreY:0.#}px off the avatar's.");
            Assert.True(Math.Abs(ring.Width - ring.Expected) <= 1.5 && Math.Abs(ring.Height - ring.Expected) <= 1.5,
                $"#{id}: the ring is {ring.Width}×{ring.Height}px, expected {ring.Expected:0.#}px across.");
        }
    }

    [Fact]
    public async Task The_gap_inside_the_ring_is_the_surface_it_sits_on()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="card" id="card" style="padding:24px"><span class="avatar avatar-lg avatar--ring-warn" id="on-card">AF</span></div>
            <div style="padding:24px" id="page"><span class="avatar avatar-lg avatar--ring-warn" id="on-page">AF</span></div>
            """);

        foreach (var (id, surface) in new[] { ("on-card", "card"), ("on-page", "page") })
        {
            // One pixel out from the avatar's right edge, on its horizontal centre line:
            // inside the 2px gap, where the surface must show through.
            var sample = await page.EvaluateAsync<double[]>("""
                id => { const r = document.getElementById(id).getBoundingClientRect();
                        return [r.right + 1, r.top + r.height / 2]; }
                """, id);
            var gap = await Pixel(page, sample[0], sample[1]);
            var bg = await page.EvaluateAsync<double[]>("""
                id => { const r = document.getElementById(id).getBoundingClientRect(); return [r.left + 4, r.top + 4]; }
                """, surface);
            var ground = await Pixel(page, bg[0], bg[1]);
            Assert.True(Distance(gap, ground) < 12,
                $"#{id}: the gap is rgb({string.Join(", ", gap)}) on a surface of rgb({string.Join(", ", ground)}).");
        }
    }

    [Theory]
    [InlineData("avatar-sm", 22)]
    [InlineData("", 28)]
    [InlineData("avatar-lg", 40)]
    public async Task A_pair_is_one_avatar_square_with_the_front_one_in_the_far_corner(string size, double square)
    {
        if (NoBrowser) return;
        foreach (var dir in new[] { "ltr", "rtl" })
        {
            var (page, _) = await OpenStyled($"""
                <div dir="{dir}" style="padding:24px">
                    <span class="avatar-pair {size}" id="pair"><span class="avatar" id="back">P</span><span class="avatar" id="front">T</span></span>
                </div>
                """);
            var boxes = await page.EvaluateAsync<double[][]>("""
                () => ['pair', 'back', 'front'].map(id => { const r = document.getElementById(id).getBoundingClientRect();
                                                          return [r.left, r.top, r.width, r.height]; })
                """);
            var (pair, back, front) = (boxes[0], boxes[1], boxes[2]);

            Assert.True(Math.Abs(pair[2] - square) < 0.5 && Math.Abs(pair[3] - square) < 0.5,
                $"{dir} {size}: the pair is {pair[2]}×{pair[3]}px, not one {square}px avatar's square.");
            Assert.True(back[2] < square && Math.Abs(back[2] % 2) < 0.01, $"{dir} {size}: the avatars inside are {back[2]}px.");

            // Both inside the square, the front one lower and at the inline end.
            foreach (var b in new[] { back, front })
                Assert.True(b[0] >= pair[0] - 0.5 && b[0] + b[2] <= pair[0] + pair[2] + 0.5
                            && b[1] >= pair[1] - 0.5 && b[1] + b[3] <= pair[1] + pair[3] + 0.5,
                    $"{dir} {size}: an avatar leaves the pair's square.");
            Assert.True(front[1] > back[1], $"{dir} {size}: the front avatar is not the lower one.");
            Assert.True(dir == "ltr" ? front[0] > back[0] : front[0] < back[0],
                $"{dir} {size}: the front avatar is not at the inline end.");

            // The back avatar is cut, and the hole is centred on the front one. The centre is a
            // custom property holding a calc(), so a probe inside the back avatar resolves it
            // through a margin, which takes a negative length as readily as a positive one.
            var cut = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('back')).maskImage");
            Assert.StartsWith("radial-gradient", cut, StringComparison.Ordinal);
            var hole = await page.EvaluateAsync<double[]>("""
                () => { const b = document.getElementById('back').getBoundingClientRect();
                        const f = document.getElementById('front').getBoundingClientRect();
                        const probe = document.createElement('span');
                        probe.style.cssText = 'display:block;margin-left:var(--avatar-cut-x);margin-top:var(--avatar-cut-y)';
                        document.getElementById('back').append(probe);
                        const s = getComputedStyle(probe);
                        return [f.left + f.width / 2 - b.left, parseFloat(s.marginLeft),
                                f.top + f.height / 2 - b.top, parseFloat(s.marginTop)]; }
                """);
            Assert.True(Math.Abs(hole[2] - hole[3]) < 0.5,
                $"{dir} {size}: the hole is centred {hole[3]}px down the back avatar, the front one at {hole[2]}px.");
            Assert.True(Math.Abs(hole[0] - hole[1]) < 0.5,
                $"{dir} {size}: the hole is centred at {hole[1]}px across the back avatar, the front one at {hole[0]}px.");
            await page.Context.CloseAsync();
        }
    }

    private sealed record Ring(bool Found, double CentreX, double CentreY, double Width, double Height, double Expected);

    /// <summary>
    /// Screenshots a margin around <paramref name="id"/> and measures the box of the pixels
    /// painted in its outline colour, relative to the element's own centre.
    /// </summary>
    private static async Task<Ring> RingBox(IPage page, string id)
    {
        const double margin = 12;
        var info = await page.EvaluateAsync<double[]>("""
            id => {
                const el = document.getElementById(id);
                const r = el.getBoundingClientRect();
                const s = getComputedStyle(el);
                const rgb = s.outlineColor.match(/[\d.]+/g).map(Number);
                const reach = parseFloat(s.outlineOffset) + parseFloat(s.outlineWidth);
                return [r.left, r.top, r.width, r.height, rgb[0], rgb[1], rgb[2], reach];
            }
            """, id);
        var (left, top, width, height) = (info[0], info[1], info[2], info[3]);
        var png = Convert.ToBase64String(await page.ScreenshotAsync(new()
        {
            Type = ScreenshotType.Png,
            Clip = new Clip
            {
                X = (float)(left - margin), Y = (float)(top - margin),
                Width = (float)(width + margin * 2), Height = (float)(height + margin * 2),
            },
        }));

        var box = await page.EvaluateAsync<double[]>("""
            async ([png, r, g, b]) => {
                const img = new Image();
                img.src = 'data:image/png;base64,' + png;
                await img.decode();
                const c = document.createElement('canvas');
                c.width = img.width; c.height = img.height;
                const ctx = c.getContext('2d');
                ctx.drawImage(img, 0, 0);
                const d = ctx.getImageData(0, 0, img.width, img.height).data;
                let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
                for (let y = 0; y < img.height; y++)
                    for (let x = 0; x < img.width; x++) {
                        const i = (y * img.width + x) * 4;
                        if (Math.abs(d[i] - r) + Math.abs(d[i + 1] - g) + Math.abs(d[i + 2] - b) < 60) {
                            x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);
                        }
                    }
                return x0 === Infinity ? [] : [x0, y0, x1 + 1, y1 + 1];
            }
            """, new object[] { png, info[4], info[5], info[6] });

        if (box.Length == 0) return new Ring(false, 0, 0, 0, 0, 0);
        var centreX = (box[0] + box[2]) / 2 - (margin + width / 2);
        var centreY = (box[1] + box[3]) / 2 - (margin + height / 2);
        return new Ring(true, centreX, centreY, box[2] - box[0], box[3] - box[1], width + info[7] * 2);
    }

    private static async Task<int[]> Pixel(IPage page, double x, double y)
    {
        var png = Convert.ToBase64String(await page.ScreenshotAsync(new()
        {
            Type = ScreenshotType.Png,
            Clip = new Clip { X = (float)Math.Floor(x), Y = (float)Math.Floor(y), Width = 1, Height = 1 },
        }));
        return await page.EvaluateAsync<int[]>("""
            async png => {
                const img = new Image();
                img.src = 'data:image/png;base64,' + png;
                await img.decode();
                const c = document.createElement('canvas');
                c.width = 1; c.height = 1;
                const ctx = c.getContext('2d');
                ctx.drawImage(img, 0, 0);
                const d = ctx.getImageData(0, 0, 1, 1).data;
                return [d[0], d[1], d[2]];
            }
            """, png);
    }

    private static int Distance(int[] a, int[] b) =>
        Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) + Math.Abs(a[2] - b[2]);
}
