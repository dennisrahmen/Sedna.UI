using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// 29-viewport.js: a visual viewport left offset under <c>.layout--flow</c> at phone width is
/// nudged once, and nothing is touched while a reader types, has zoomed in, or is on a wide
/// screen.
/// </summary>
public class ViewportNudgeTests : ScriptTestBase
{
    // A visual viewport stuck 240px down, as iOS 26 leaves it, and a record of every scrollBy.
    private const string Stuck = """
        <script>
          window.nudges = [];
          const fake = new EventTarget();
          fake.offsetTop = 240; fake.scale = 1;
          Object.defineProperty(window, 'visualViewport', { value: fake, configurable: true });
          const real = window.scrollBy.bind(window);
          window.scrollBy = (x, y) => { window.nudges.push(y); real(x, y); };
        </script>
        """;

    private const string Flow = """<div class="layout layout--flow"><div class="content"><main class="page"><input id="f"><div style="height:2000px"></div></main></div></div>""";

    private static async Task<double[]> After(Microsoft.Playwright.IPage page)
    {
        await Js.Run(page, "() => window.dispatchEvent(new Event('scroll'))");
        await page.WaitForTimeoutAsync(700);
        return await page.EvaluateAsync<double[]>("() => window.nudges");
    }

    [Fact]
    public async Task A_stuck_viewport_at_phone_width_is_nudged_once()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Flow, extraHead: Stuck);
        await page.SetViewportSizeAsync(390, 800);

        Assert.Equal(new double[] { 1, -1 }, await After(page));
        Assert.Equal(new double[] { 1, -1 }, await After(page));   // once per occurrence
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("typing")]
    [InlineData("zoomed")]
    [InlineData("wide")]
    public async Task Nothing_is_touched_where_the_offset_is_the_reader_s(string why)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Flow, extraHead: Stuck);
        await page.SetViewportSizeAsync(why == "wide" ? 1200 : 390, 800);
        if (why == "typing") await page.FocusAsync("#f");
        if (why == "zoomed") await Js.Run(page, "() => { window.visualViewport.scale = 2; }");

        Assert.Empty(await After(page));
        Assert.Empty(errors);
    }
}
