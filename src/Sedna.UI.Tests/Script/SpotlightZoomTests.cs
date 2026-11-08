using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The spotlight inside a CSS-zoomed frame — a wall screen scaled up — covers its target
/// exactly, and <c>scroll</c> brings a target below the fold into view once.
/// </summary>
public class SpotlightZoomTests : ScriptTestBase
{
    [Fact]
    public async Task The_hole_covers_its_target_inside_a_zoomed_frame()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="zoom:1.5">
              <div id="stage" style="position:relative; width:600px; height:300px">
                <div id="target" style="position:absolute; left:100px; top:60px; width:120px; height:40px"></div>
                <div id="hole" class="spotlight-hole spotlight-ring"></div>
              </div>
            </div>
            """);

        await Js.Run(page, "() => sednaUi.spotlight.at(document.getElementById('hole'), document.getElementById('target'), { pad: 0 })");
        // Where the hole comes to rest: it slides between placements.
        await Js.Settle(page);
        var drawn = await page.EvaluateAsync<double[]>("""
            () => {
              const h = document.getElementById('hole').getBoundingClientRect();
              const t = document.getElementById('target').getBoundingClientRect();
              return [h.left - t.left, h.top - t.top, h.width - t.width, h.height - t.height];
            }
            """);

        foreach (var d in drawn) Assert.InRange(d, -1, 1);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_target_below_the_fold_is_scrolled_into_view()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="height:3000px; position:relative">
              <div id="target" style="position:absolute; top:2400px; left:20px; width:100px; height:30px"></div>
              <div id="hole" class="spotlight-hole spotlight-ring"></div>
            </div>
            """);

        var top = await page.EvaluateAsync<double>("""
            () => {
              sednaUi.spotlight.at(document.getElementById('hole'), document.getElementById('target'), { scroll: 'nearest' });
              return document.getElementById('target').getBoundingClientRect().top;
            }
            """);

        Assert.InRange(top, 0, await page.EvaluateAsync<double>("() => window.innerHeight"));
        Assert.Empty(errors);
    }
}
