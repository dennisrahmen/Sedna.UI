using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.mochi-host--room</c> keeps clear the room a perch reaches into, at every size.
/// </summary>
/// <remarks>
/// <para>
/// A perch draws outside its host, and a plain host does not grow for it, so every example
/// padded the space around Mochi by hand — 72 to 104px, worked out by eye for one size. The
/// modifier sizes the room from <c>--size</c> and the perch.
/// </para>
/// <para>
/// Read from the paint: each fixture is screenshot with Mochi shown and hidden, and no pixel
/// that Mochi paints may lie outside the box that holds the host and its room, on the side
/// the perch reaches out of. Each host sits in a flow-root box, so its margins are inside
/// that box. Pose loops are held still, so the two screenshots differ only by Mochi.
/// </para>
/// </remarks>
public class MochiRoomTests : ScriptTestBase
{
    private static string Sprite =>
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "StateArt", "Sedna.UI.mochi.svg"));

    private const string Svg = """<svg viewBox="0 0 160 160" aria-hidden="true"><use href="#sedna-mochi" /></svg>""";

    /// <summary>A fixture: the perch, what it perches on, Mochi's size class, and the sides its room is on.</summary>
    private sealed record Case(string Perch, string Host, string Size, string Sides, string Extra = "");

    private static readonly Case[] Cases =
    [
        new("sit", "card", "", "top"),
        new("sit", "card", "mochi--sm", "top", "data-action=\"celebrate\""),
        new("sit", "card", "mochi--lg", "top", "data-action=\"sleep\""),
        new("peek", "card", "", "top", "data-action=\"wave\""),
        new("look", "card", "mochi--lg", "top"),
        new("hang", "card", "", "bottom", "data-action=\"wave\""),
        new("hang", "card", "mochi--sm", "bottom"),
        new("side", "card", "", "end"),
        new("side", "card", "mochi--lg", "end"),
        new("sit", "divider", "mochi--md", "top bottom"),
        new("stand", "divider", "mochi--md", "top", "data-action=\"wave\""),
        new("hang", "divider", "mochi--md", "bottom"),
        new("point", "anchor", "", "top bottom start", "data-action=\"point\" style=\"position-anchor: --room-target\""),
        new("point", "anchor", "mochi--lg", "top bottom end", "data-action=\"point\" data-facing=\"start\" style=\"position-anchor: --room-target\""),
    ];

    private static string Fixture(Case c, int i)
    {
        var target = c.Host switch
        {
            "card" => """<div class="card"><div class="card-body">Order sync, 3 runs since 03:30.</div></div>""",
            "divider" => """<hr class="divider" />""",
            _ => $"""<button class="btn" type="button" style="anchor-name: --room-target-{i}">Approve the change</button>""",
        };
        var extra = c.Extra.Replace("--room-target", $"--room-target-{i}", StringComparison.Ordinal);
        return $"""
            <div class="room-case" style="padding: 260px 300px">
              <div id="w{i}" style="display: flow-root; width: 360px">
                <div class="mochi-host mochi-host--room" id="h{i}">
                  {target}
                  <span class="mochi {c.Size}" data-perch="{c.Perch}" {extra} role="img" aria-label="Ops agent">{Svg}</span>
                </div>
              </div>
            </div>
            """;
    }

    [Fact]
    public async Task Nothing_mochi_paints_falls_outside_the_room_its_host_keeps()
    {
        if (NoBrowser) return;
        var page = await Open(
            Sprite + string.Concat(Cases.Select(Fixture)),
            head: StylesheetTag,
            reducedMotion: ReducedMotion.Reduce);
        await Js.Settle(page);

        var failures = new List<string>();
        for (var i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            // A point perch's room is the host's padding, so the host is the box; every other
            // perch's room is the host's margin, which the flow-root box around it holds.
            var boxId = c.Perch == "point" ? $"h{i}" : $"w{i}";
            // In view, because a Mochi out of view is held and not drawn.
            var r = await page.EvaluateAsync<JsonElement>($$"""
                async () => {
                    const el = document.getElementById('{{boxId}}');
                    el.closest('.room-case').scrollIntoView({ block: 'center', behavior: 'instant' });
                    await new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)));
                    const r = el.getBoundingClientRect();
                    return { x: r.x, y: r.y, w: r.width, h: r.height };
                }
                """);
            await Js.Settle(page);
            double X = r.GetProperty("x").GetDouble(), Y = r.GetProperty("y").GetDouble();
            double W = r.GetProperty("w").GetDouble(), H = r.GetProperty("h").GetDouble();
            const double Around = 200;
            var clip = new Clip { X = (float)(X - Around), Y = (float)(Y - Around), Width = (float)(W + 2 * Around), Height = (float)(H + 2 * Around) };

            var shown = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = clip }));
            await Js.Run(page, $"() => document.querySelector('#h{i} > .mochi').style.visibility = 'hidden'");
            var hidden = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = clip }));
            await Js.Run(page, $"() => document.querySelector('#h{i} > .mochi').style.visibility = ''");

            var painted = 0;
            var outside = new Dictionary<string, int>();
            for (var y = 0; y < shown.Height; y++)
            for (var x = 0; x < shown.Width; x++)
            {
                var (r1, g1, b1) = shown.Pixel(x, y);
                var (r2, g2, b2) = hidden.Pixel(x, y);
                if (Math.Abs(r1 - r2) + Math.Abs(g1 - g2) + Math.Abs(b1 - b2) < 60) continue;
                painted++;
                // Half a pixel of antialiasing on the box's own edge is the edge.
                var px = x - Around; var py = y - Around;
                var side = py < -1 ? "top" : py > H + 1 ? "bottom" : px < -1 ? "start" : px > W + 1 ? "end" : null;
                if (side is not null && c.Sides.Split(' ').Contains(side)) outside[side] = outside.GetValueOrDefault(side) + 1;
            }

            if (painted < 200) failures.Add($"#{i} {c.Perch} on a {c.Host} paints {painted} pixels: the fixture did not draw Mochi.");
            foreach (var (side, n) in outside)
                failures.Add($"#{i} {c.Perch} on a {c.Host} {c.Size} {c.Extra}: {n} pixels painted past the room on the {side}.");

            // Beside the host and below it, Mochi's box stays in the room as well: an absolute
            // box past a scroller's edge is scrollable overflow, and widens the page on a phone.
            var spill = await page.EvaluateAsync<double[]>($$"""
                () => {
                    const box = document.getElementById('{{boxId}}').getBoundingClientRect();
                    const m = document.querySelector('#h{{i}} > .mochi').getBoundingClientRect();
                    return [box.left - m.left, m.right - box.right, m.bottom - box.bottom];
                }
                """);
            foreach (var (side, by) in new[] { ("start", spill[0]), ("end", spill[1]), ("bottom", spill[2]) })
                if (by > 1 && c.Sides.Split(' ').Contains(side))
                    failures.Add($"#{i} {c.Perch} on a {c.Host} {c.Size}: Mochi's box reaches {by:0.0}px past the room on the {side}.");
        }

        Assert.True(failures.Count == 0, $"{Engine}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [Fact]
    public async Task The_room_follows_the_size_the_host_is_given()
    {
        if (NoBrowser) return;
        var page = await Open(
            Sprite + $"""
            <div class="mochi-host mochi-host--room" id="a"><div class="card"><div class="card-body">x</div></div><span class="mochi" data-perch="sit">{Svg}</span></div>
            <div class="mochi-host mochi-host--room" id="b"><div class="card"><div class="card-body">x</div></div><span class="mochi mochi--xl" data-perch="sit">{Svg}</span></div>
            <div class="mochi-host mochi-host--room" id="c" style="--size: 50px"><div class="card"><div class="card-body">x</div></div><span class="mochi" data-perch="sit">{Svg}</span></div>
            """,
            head: StylesheetTag,
            reducedMotion: ReducedMotion.Reduce);

        var sizes = await page.EvaluateAsync<double[][]>("""
            () => ['a', 'b', 'c'].map(id => {
                const host = document.getElementById(id);
                return [parseFloat(getComputedStyle(host).marginTop), host.querySelector('.mochi').getBoundingClientRect().width];
            })
            """);
        // Mochi is drawn at the size the room is sized from, whether that came from the
        // default, a size class on the Mochi or the host's own --size.
        Assert.Equal([112, 220, 50], sizes.Select(s => Math.Round(s[1])).ToArray());
        foreach (var s in sizes)
            Assert.True(Math.Abs(s[0] / s[1] - 0.8) < 0.01, $"{Engine}: a sitting Mochi {s[1]}px wide keeps {s[0]}px above its host.");
    }
}
