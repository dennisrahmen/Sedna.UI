using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Presence is a shape, not a colour: every state still reads when the colour is gone.
/// </summary>
/// <remarks>
/// <para>
/// Forced colours are the witness, because they take every colour away at once — go, warn
/// and danger all become the one ink, so two states drawn as the same disc in different
/// colours become the same picture. The colour-blind palette is the milder form of the same
/// fault. So the marks are rendered in forced colours, read back from a screenshot,
/// reduced to ink and no ink, and compared pairwise.
/// </para>
/// <para>
/// It samples the paint rather than the computed <c>background</c> because the glyphs
/// are gradients, and two different gradient strings can draw the same pixels.
/// </para>
/// </remarks>
public class AvatarPresenceTests : ScriptTestBase
{
    private static readonly string[] States =
    [
        "online", "away", "busy", "do-not-disturb", "meeting", "presenting",
        "out-of-office", "offline", "unknown",
    ];

    [Theory]
    [InlineData("avatar")]
    [InlineData("avatar avatar-lg")]
    public async Task Every_presence_state_is_a_different_shape_in_forced_colours(string avatarClass)
    {
        if (NoBrowser) return;
        var marks = string.Join("\n", States.Select(s =>
            $"""<span class="avatar-presence avatar-presence--{s}" id="p-{s}"><span class="{avatarClass}">AF</span></span>"""));
        var (page, _) = await OpenStyled($"""<div style="display:flex;gap:24px;padding:24px">{marks}</div>""");
        await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });

        // The mode really did take the colour away, or the comparison below proves nothing.
        var fills = await page.EvaluateAsync<string[]>("""
            () => ['online', 'away', 'busy'].map(s =>
                getComputedStyle(document.getElementById('p-' + s), '::after').backgroundColor)
            """);
        Assert.True(fills.Distinct().Count() == 1,
            $"Forced colours left the states different colours ({string.Join(" / ", fills)}), so this test cannot see shape.");

        var masks = new Dictionary<string, bool[]>();
        foreach (var state in States)
            masks[state] = await Ink(page, $"p-{state}");

        var failures = new List<string>();
        for (var i = 0; i < States.Length; i++)
            for (var j = i + 1; j < States.Length; j++)
            {
                var a = masks[States[i]];
                var b = masks[States[j]];
                var differing = a.Zip(b).Count(p => p.First != p.Second);
                if (differing < 3)
                    failures.Add($"--{States[i]} and --{States[j]} ({differing}px apart)");
            }

        Assert.True(failures.Count == 0,
            "These presence states draw the same shape once the colour is gone, so they differ by colour alone: "
            + string.Join(", ", failures));
    }

    [Fact]
    public async Task The_mark_grows_with_a_large_avatar_and_stays_on_the_corner()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <span class="avatar-presence avatar-presence--online" id="sm"><span class="avatar avatar-sm">AF</span></span>
            <span class="avatar-presence avatar-presence--online" id="md"><span class="avatar">AF</span></span>
            <span class="avatar-presence avatar-presence--online" id="lg"><span class="avatar avatar-lg">AF</span></span>
            <span class="avatar-presence avatar-presence--label avatar-presence--online" id="label">Online</span>
            """);

        var sizes = await page.EvaluateAsync<double[]>("""
            () => ['sm', 'md', 'lg'].map(id => parseFloat(getComputedStyle(document.getElementById(id), '::after').width))
            """);
        Assert.True(Math.Abs(sizes[0] - sizes[1]) < 0.01 && sizes[2] > sizes[1], $"Mark sizes: {string.Join(", ", sizes)}.");
        Assert.All(sizes, s => Assert.True(Math.Abs(s % 2) < 0.01, $"A mark of {s}px puts a centred stroke on a half pixel."));

        var corner = await page.EvaluateAsync<bool>("""
            () => ['sm', 'md', 'lg'].every(id => {
                const host = document.getElementById(id).getBoundingClientRect();
                const avatar = document.getElementById(id).firstElementChild.getBoundingClientRect();
                return Math.abs(host.width - avatar.width) < 0.5 && Math.abs(host.height - avatar.height) < 0.5;
            })
            """);
        Assert.True(corner, "The presence wrapper is not the avatar's own box, so the mark is off its corner.");

        // In front of text the mark is in the flow, before the words, with no surface ring.
        var label = await page.EvaluateAsync<string[]>("""
            () => { const s = getComputedStyle(document.getElementById('label'), '::after');
                    return [s.position, s.order, s.boxShadow]; }
            """);
        Assert.Equal(["static", "-1", "none"], label);
    }

    /// <summary>
    /// Screenshots the presence mark of the wrapper <paramref name="id"/> and returns, per
    /// pixel, whether it is ink or the surface.
    /// </summary>
    private static async Task<bool[]> Ink(IPage page, string id)
    {
        var box = await page.EvaluateAsync<double[]>("""
            id => {
                const host = document.getElementById(id).getBoundingClientRect();
                const size = parseFloat(getComputedStyle(document.getElementById(id), '::after').width);
                return [host.right - size, host.bottom - size, size];
            }
            """, id);
        var png = Convert.ToBase64String(await page.ScreenshotAsync(new()
        {
            Type = ScreenshotType.Png,
            Clip = new Clip { X = (float)box[0], Y = (float)box[1], Width = (float)box[2], Height = (float)box[2] },
        }));

        // Decoded by the browser itself, so the test needs no image library.
        return await page.EvaluateAsync<bool[]>("""
            async png => {
                const img = new Image();
                img.src = 'data:image/png;base64,' + png;
                await img.decode();
                const c = document.createElement('canvas');
                c.width = img.width; c.height = img.height;
                const ctx = c.getContext('2d');
                ctx.drawImage(img, 0, 0);
                const d = ctx.getImageData(0, 0, img.width, img.height).data;
                const lum = i => d[i] * 0.2126 + d[i + 1] * 0.7152 + d[i + 2] * 0.0722;
                // The top-left pixel is outside the disc: the surface, i.e. Canvas. Ink is
                // anything clearly not that, so a GrayText ring counts as well as CanvasText.
                const canvas = lum(0);
                const ink = [];
                for (let i = 0; i < d.length; i += 4)
                    ink.push(Math.abs(lum(i) - canvas) > 48);
                return ink;
            }
            """, png);
    }
}
