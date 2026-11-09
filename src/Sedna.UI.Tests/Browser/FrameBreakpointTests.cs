using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.layout--responsive</c> forces the rail below 900px, and <c>.layout--compact-at-sm</c> and
/// <c>-lg</c> move that width to 560px and 1200px — the one rail block, reached at three widths.
/// </summary>
public class FrameBreakpointTests : ScriptTestBase
{
    private static string Shell(string modifier) => $"""
        <div class="layout layout--responsive {modifier}" style="height:400px">
          <aside class="sidebar"><div class="brand"><span class="brand-text">Orders console</span></div>
            <nav class="nav"><a class="nav-link" href="#"><i class="ri-home-line"></i><span>Overview</span></a></nav></aside>
          <div class="content"><main class="page">Page</main></div>
        </div>
        """;

    [Theory]
    [InlineData("", 1000, false)]
    [InlineData("", 800, true)]
    [InlineData("layout--compact-at-sm", 800, false)]
    [InlineData("layout--compact-at-sm", 500, true)]
    [InlineData("layout--compact-at-lg", 1100, true)]
    [InlineData("layout--compact-at-lg", 1300, false)]
    public async Task The_rail_is_forced_below_the_width_the_layout_names(string modifier, int width, bool rail)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Shell(modifier));
        await page.SetViewportSizeAsync(width, 700);
        await Js.Settle(page);

        var sidebar = await page.EvaluateAsync<double>("() => document.querySelector('.sidebar').getBoundingClientRect().width");
        var collapsed = await page.EvaluateAsync<double>("""
            () => parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--sidebar-width-collapsed'))
                || parseFloat(getComputedStyle(document.querySelector('.sidebar')).getPropertyValue('--sidebar-width-collapsed'))
            """);

        if (rail) Assert.True(Math.Abs(collapsed - sidebar) < 1, $"At {width}px the sidebar is {sidebar}px, not the {collapsed}px rail.");
        else Assert.True(sidebar > collapsed + 50, $"At {width}px the sidebar is {sidebar}px, the rail's width.");
        Assert.Empty(errors);
    }

    /// <summary>
    /// A page banner's trailing actions sit beside its words down to 560px and go under them
    /// below it, while the dismiss stays on the first row and every slot stands on the first line.
    /// </summary>
    [Theory]
    [InlineData(1000, false)]
    [InlineData(600, false)]
    [InlineData(560, true)]
    [InlineData(360, true)]
    public async Task A_page_banners_actions_go_under_its_words_on_a_phone(int width, bool under)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <style>body { margin: 0; }</style>
            <div class="page-banner page-banner--info" id="banner">
              <i class="ri-information-line" id="icon"></i>
              <div class="page-banner-body" id="body"><span id="words">Exports pause on Saturday.</span></div>
              <div class="page-banner-actions" id="actions"><a class="btn btn--sm" href="#">Details</a></div>
              <button class="page-banner-dismiss" id="dismiss" type="button" aria-label="Dismiss"><i class="ri-close-line"></i></button>
            </div>
            """);
        await page.SetViewportSizeAsync(width, 700);
        await Js.Settle(page);

        var box = await page.EvaluateAsync<double[]>("""
            () => ['body', 'actions', 'dismiss', 'icon'].flatMap(id => { const r = document.getElementById(id).getBoundingClientRect(); return [r.top, r.bottom]; })
            """);
        double bodyTop = box[0], bodyBottom = box[1], actionsTop = box[2], dismissTop = box[4], dismissBottom = box[5], iconTop = box[6], iconBottom = box[7];
        var line = await page.EvaluateAsync<double>("""
            () => { const r = document.createRange(); r.selectNodeContents(document.getElementById('words'));
                    const b = r.getClientRects()[0]; return b.top + b.height / 2; }
            """);

        if (under) Assert.True(actionsTop >= bodyBottom, $"At {width}px the actions start at {actionsTop}, beside words that end at {bodyBottom}.");
        else Assert.True(actionsTop < bodyBottom, $"At {width}px the actions dropped under the words, to {actionsTop}.");
        Assert.True(dismissTop < bodyBottom, $"At {width}px the dismiss left the first row.");
        Assert.True(Math.Abs((dismissTop + dismissBottom) / 2 - line) <= 1.5, $"At {width}px the dismiss is off the first line ({line}).");
        Assert.True(Math.Abs((iconTop + iconBottom) / 2 - line) <= 1.5, $"At {width}px the icon is off the first line ({line}).");
        Assert.True(bodyTop >= 0);
        Assert.Empty(errors);
    }
}
