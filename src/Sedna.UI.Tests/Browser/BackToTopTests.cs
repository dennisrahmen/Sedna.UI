using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.back-to-top</c>: out of sight and out of the tab order until the page has scrolled
/// <c>--back-to-top-after</c>, then stuck to the end corner of what scrolls the page, and a
/// press takes that scroller back to its start and moves focus there.
/// </summary>
public class BackToTopTests : ScriptTestBase
{
    private const string Button =
        """<a class="back-to-top" id="top-btn" href="#top" aria-label="Back to top"><i class="ri-arrow-up-line"></i></a>""";

    private static string InBox(string dir = "ltr") => $"""
        <div dir="{dir}">
          <div class="sedna-scroll" id="box" style="max-height:400px; width:600px">
            <button class="btn" type="button" id="first">First control</button>
            <div style="height:2400px"></div>
            {Button.Replace("href=", "style=\"--back-to-top-after: 300px\" href=", StringComparison.Ordinal)}
          </div>
        </div>
        """;

    private const string InFrame = $"""
        <div class="layout">
          <div class="content">
            <header class="topbar"><span class="topbar-spacer"></span></header>
            <main class="page" id="box">
              <div style="height:3000px"></div>
              {Button}
            </main>
          </div>
        </div>
        """;

    private sealed record Reading(
        double Opacity, string Visibility, double[] Button, double[] Box, double PadEnd, double Space8, double ScrollTop);

    private static async Task<Reading> At(IPage page, int scrollTop)
    {
        await Js.Run(page, "y => { document.getElementById('box').scrollTop = y; }", scrollTop);
        await Js.Frames(page);
        return await Read(page);
    }

    private static async Task<Reading> Read(IPage page) =>
        System.Text.Json.JsonSerializer.Deserialize<Reading>(await page.EvaluateAsync<string>("""
            () => {
              const b = document.getElementById('top-btn'), box = document.getElementById('box');
              const r = e => { const x = e.getBoundingClientRect(); return [x.left, x.right, x.top, x.bottom]; };
              const probe = document.createElement('div');
              probe.style.paddingTop = 'var(--space-8)';
              document.body.appendChild(probe);
              const space8 = parseFloat(getComputedStyle(probe).paddingTop);
              probe.remove();
              const cs = getComputedStyle(b);
              return JSON.stringify({
                opacity: parseFloat(cs.opacity), visibility: cs.visibility,
                button: r(b), box: r(box), padEnd: parseFloat(getComputedStyle(box).paddingBottom),
                space8, scrollTop: box.scrollTop,
              });
            }
            """), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

    [Fact]
    public async Task It_comes_in_only_once_the_page_has_scrolled_far_enough()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(InBox());

        var top = await At(page, 0);
        Assert.Equal("hidden", top.Visibility);
        Assert.Equal(0, top.Opacity, 2);

        var near = await At(page, 200);
        Assert.Equal("hidden", near.Visibility);

        var far = await At(page, 800);
        Assert.Equal("visible", far.Visibility);
        Assert.Equal(1, far.Opacity, 2);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task It_sticks_to_the_end_corner_of_its_scroller(string dir)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(InBox(dir));

        var r = await At(page, 800);
        Assert.Equal(r.Box[3] - r.Space8, r.Button[3], 1.0);
        if (dir == "ltr") Assert.Equal(r.Box[1], r.Button[1], 1.0);
        else Assert.Equal(r.Box[0], r.Button[0], 1.0);
        Assert.Equal(40, r.Button[1] - r.Button[0], 0.5);
        Assert.Equal(40, r.Button[3] - r.Button[2], 0.5);
    }

    [Fact]
    public async Task In_the_frame_it_stands_clear_of_the_page_padding()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(InFrame);
        await page.SetViewportSizeAsync(1280, 700);

        var r = await At(page, 1500);
        Assert.Equal("visible", r.Visibility);
        Assert.Equal(r.Box[3] - r.PadEnd - r.Space8, r.Button[3], 1.0);
    }

    [Fact]
    public async Task Pressing_it_scrolls_its_scroller_to_the_start_and_moves_focus_there()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(InBox(), """<base href="/">""");
        await Js.Run(page, "() => { window.__stay = true; }");

        await At(page, 1200);
        await page.Locator("#top-btn").ClickAsync();
        await Js.ScrollAtRest(page, "#box");

        Assert.Equal(0, (await Read(page)).ScrollTop, 1);
        Assert.Equal("/fixture.html", await page.EvaluateAsync<string>("() => location.pathname"));
        Assert.True(await page.EvaluateAsync<bool>("() => window.__stay === true"),
            "#top reloaded the page: it resolved against <base href=\"/\">.");
        Assert.Equal("box", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        // The next Tab is the first control at the top, not the one after the button.
        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("first", await page.EvaluateAsync<string>("() => document.activeElement.id"));
    }

    [Fact]
    public async Task In_the_frame_pressing_it_scrolls_the_page_to_the_start()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(InFrame, """<base href="/">""");
        await page.SetViewportSizeAsync(1280, 700);

        await At(page, 1500);
        await page.Locator("#top-btn").ClickAsync();
        await Js.ScrollAtRest(page, "#box");

        Assert.Equal(0, (await Read(page)).ScrollTop, 1);
        Assert.Equal("box", await page.EvaluateAsync<string>("() => document.activeElement.id"));
    }

    [Fact]
    public async Task Under_reduced_motion_it_comes_in_without_a_fade()
    {
        if (NoBrowser) return;
        var page = await Open(InBox(), head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);

        // Part way through the stretch it would fade over: either there or not, never between.
        foreach (var y in new[] { 310, 320, 330 })
        {
            var r = await At(page, y);
            Assert.True(r.Opacity is 0 or 1, $"At {y}px the button was part faded in ({r.Opacity}).");
        }
        Assert.Equal("visible", (await At(page, 800)).Visibility);
    }
}
