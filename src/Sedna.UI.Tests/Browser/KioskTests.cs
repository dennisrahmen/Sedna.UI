using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.kiosk</c> fills the screen at every width with the same canvas scaled, never scrolls,
/// and keeps the stage in its row while the strip is empty.
/// </summary>
public class KioskTests : ScriptTestBase
{
    private const string Wall = """
        <div class="kiosk" id="k">
          <div class="kiosk-strip" id="strip"></div>
          <main class="kiosk-stage" id="stage"><div style="height:3000px">Runs</div></main>
          <aside class="kiosk-side" id="side">Queue</aside>
        </div>
        """;

    [Theory]
    [InlineData(1280, 720, 1.0)]
    [InlineData(1920, 1080, 1.5)]
    [InlineData(2560, 1440, 2.0)]
    public async Task It_fills_the_screen_with_the_same_canvas_scaled(int width, int height, double zoom)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Wall, extraHead: "<style>body{margin:0}</style>");
        await page.SetViewportSizeAsync(width, height);

        var m = await page.EvaluateAsync<double[]>("""
            () => { const k = document.getElementById('k').getBoundingClientRect();
                    const s = document.getElementById('stage').getBoundingClientRect();
                    return [k.width, k.height, document.documentElement.scrollHeight, s.top - k.top,
                            parseFloat(getComputedStyle(document.getElementById('k')).getPropertyValue('--kiosk-zoom'))]; }
            """);

        Assert.InRange(m[0], width - 1, width + 1);
        Assert.InRange(m[1], height - 1, height + 1);
        Assert.InRange(m[2], height - 1, height + 1);     // nothing scrolls the document
        Assert.Equal(zoom, m[4], 3);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_empty_strip_leaves_the_stage_below_it_and_the_side_beside_it()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Wall, extraHead: "<style>body{margin:0}</style>");
        await page.SetViewportSizeAsync(1280, 720);
        var r = await page.EvaluateAsync<double[]>("""
            () => { const st = document.getElementById('stage').getBoundingClientRect();
                    const sd = document.getElementById('side').getBoundingClientRect();
                    return [Math.abs(st.top - sd.top), sd.left >= st.right ? 1 : 0]; }
            """);
        Assert.InRange(r[0], 0, 1);
        Assert.Equal(1, r[1]);
    }
}
