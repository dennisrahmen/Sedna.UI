using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The dot in front of an unread notification's time stands on the centre of the time's own
/// figures, in either direction — measured on the paint, as a mark beside a line of text is
/// everywhere else.
/// </summary>
public class InboxUnreadDotTests : ScriptTestBase
{
    private const string Markup = """
        <div class="inbox" style="--inbox-width: 360px; position: static">
          <ul class="inbox-list">
            <li class="inbox-item inbox-item--unread">
              <div class="inbox-item-main">
                <a class="inbox-item-title" href="#">Two of five lines on ORD-4209 are short</a>
                <time class="inbox-item-time" id="t" datetime="2026-11-08T09:40">09:40</time>
              </div>
            </li>
          </ul>
        </div>
        """;

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_dot_stands_on_the_centre_of_the_time(string dir)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Markup, deviceScaleFactor: Ink.Scale);
        await Js.Run(page, $"() => {{ document.documentElement.dir = '{dir}'; }}");
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);

        var colours = await page.EvaluateAsync<int[][]>("""
            () => {
                const rgb = value => {
                    const probe = document.createElement('i');
                    probe.style.color = value;
                    document.body.append(probe);
                    const c = getComputedStyle(probe).color.match(/\d+/g).map(Number).slice(0, 3);
                    probe.remove();
                    return c;
                };
                return [rgb('var(--brand)'), rgb(getComputedStyle(document.getElementById('t')).color)];
            }
            """);
        var box = (await page.Locator("#t").BoundingBoxAsync())!;
        var left = box.X - 2;
        var top = box.Y - 2;
        var png = Ink.Png.Decode(await page.ScreenshotAsync(new()
        {
            Clip = new() { X = left, Y = top, Width = box.Width + 4, Height = box.Height + 4 },
        }));
        var dot = Ink.Paint(png, left, top, box.X, box.Y, box.Width, box.Height, (colours[0][0], colours[0][1], colours[0][2]), columns: false);
        var figures = Ink.Paint(png, left, top, box.X, box.Y, box.Width, box.Height, (colours[1][0], colours[1][1], colours[1][2]), columns: false);
        Assert.NotNull(dot);
        Assert.NotNull(figures);

        var off = Ink.Centre(dot!) - Ink.Centre(figures!);
        Assert.True(Math.Abs(off) <= 0.55,
            $"The unread dot's centre is {off:+0.00;-0.00}px from the centre of the time's figures.");
        Assert.True(dot![1] - dot[0] is > 5 and < 7, $"The dot is {dot[1] - dot[0]:0.0}px tall, not 6.");
    }
}
