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
}
