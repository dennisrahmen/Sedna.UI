using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// A number on a bare icon marks the icon's corner and never its body: measured on the paint, as
/// the share of the glyph's own ink the badge and its cut-out ring hide.
/// </summary>
/// <remarks>
/// The badge's box says nothing about this, because an icon's ink does not fill its box. The page is
/// screenshotted twice at four device pixels per CSS pixel, the badge hidden and shown, and the ink is
/// the pixels the first picture paints that are not the icon box's background.
/// </remarks>
public class CountInkCoverageTests : ScriptTestBase
{
    /// <summary>The most of a glyph's ink a number at its own size may hide: a corner, not a face.</summary>
    private const double Share = 0.16;

    private static string Host(string id, string icon, string size, string number, string badge = "") =>
        $"""<span class="count-host" id="{id}"><i class="{icon} {size}"></i><span class="count-badge {badge}" id="{id}-n">{number}</span></span>""";

    private const string Markup = """
        <div class="card" style="display:flex;gap:56px;align-items:center;padding:28px 72px 28px 28px">
          <span class="count-host" id="mail"><i class="ri-mail-line ri-xl"></i><span class="count-badge" id="mail-n">3</span></span>
          <span class="count-host" id="chat"><i class="ri-chat-3-line ri-xl"></i><span class="count-badge" id="chat-n">12</span></span>
          <span class="count-host" id="cart"><i class="ri-shopping-cart-2-line ri-xl"></i><span class="count-badge" id="cart-n">99+</span></span>
          <span class="count-host" id="bell"><i class="ri-notification-3-line ri-xl"></i><span class="count-badge count-badge--danger" id="bell-n">8</span></span>
          <span class="count-host" id="big"><i class="ri-mail-line ri-3x"></i><span class="count-badge count-badge--lg" id="big-n">3</span></span>
        </div>
        """;

    private async Task<IPage> Open()
    {
        var (page, _) = await OpenStyled(Markup, deviceScaleFactor: Ink.Scale);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);
        return page;
    }

    /// <summary>The share of the icon's ink the badge hides.</summary>
    private static async Task<double> Covered(IPage page, string id)
    {
        var icon = (await page.Locator($"#{id} > i").BoundingBoxAsync())!;
        var clip = new Clip { X = icon.X, Y = icon.Y, Width = icon.Width, Height = icon.Height };
        await page.EvaluateAsync($"() => document.getElementById('{id}-n').style.visibility = 'hidden'");
        var bare = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = clip }));
        await page.EvaluateAsync($"() => document.getElementById('{id}-n').style.visibility = ''");
        var marked = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = clip }));

        var counts = new Dictionary<(int, int, int), int>();
        for (var y = 0; y < bare.Height; y++)
            for (var x = 0; x < bare.Width; x++)
                counts[bare.Pixel(x, y)] = counts.GetValueOrDefault(bare.Pixel(x, y)) + 1;
        var bg = counts.MaxBy(c => c.Value).Key;

        int ink = 0, hidden = 0;
        for (var y = 0; y < bare.Height; y++)
            for (var x = 0; x < bare.Width; x++)
            {
                var a = bare.Pixel(x, y);
                if (Math.Abs(a.R - bg.Item1) + Math.Abs(a.G - bg.Item2) + Math.Abs(a.B - bg.Item3) <= 96) continue;
                ink++;
                var b = marked.Pixel(x, y);
                if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > 64) hidden++;
            }
        Assert.True(ink > 0, $"#{id}: the icon painted nothing.");
        return (double)hidden / ink;
    }

    [Theory]
    [InlineData("mail")]
    [InlineData("chat")]
    [InlineData("cart")]
    [InlineData("bell")]
    public async Task A_number_on_a_bare_icon_hides_no_more_than_the_corner_of_its_glyph(string id)
    {
        if (NoBrowser) return;
        var page = await Open();
        var covered = await Covered(page, id);
        Assert.True(covered <= Share, $"#{id}: the number hides {covered:P0} of the icon's ink, over {Share:P0}.");
    }

    [Fact]
    public async Task A_large_icon_keeps_its_badge_on_the_corner_and_it_is_a_small_share_of_the_glyph()
    {
        if (NoBrowser) return;
        var page = await Open();
        var covered = await Covered(page, "big");
        Assert.True(covered <= Share / 2, $"#big: the badge hides {covered:P0} of the icon's ink.");
    }

    [Fact]
    public async Task A_longer_number_grows_outward_and_starts_where_a_single_digit_does()
    {
        if (NoBrowser) return;
        var page = await Open();
        var starts = await page.EvaluateAsync<double[]>("""
            () => ['mail', 'chat', 'cart'].map(id => {
                const host = document.getElementById(id).getBoundingClientRect();
                return document.getElementById(id + '-n').getBoundingClientRect().left - host.right;
            })
            """);
        Assert.True(starts.Max() - starts.Min() < 0.5,
            $"A longer number starts {starts.Max() - starts.Min():0.##}px from where a single digit does: {string.Join(", ", starts)}.");
    }
}
