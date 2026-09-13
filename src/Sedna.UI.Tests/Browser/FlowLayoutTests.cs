using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.layout--flow</c>: on a phone the document scrolls and the topbar sticks; on a
/// desktop the frame keeps its one scroller.
/// </summary>
public class FlowLayoutTests : ScriptTestBase
{
    private const string Shell = """
        <div class="layout layout--flow" id="layout">
            <div class="content">
                <header class="topbar" id="topbar"><span class="topbar-spacer"></span></header>
                <div class="page" id="page">
                    <div class="card"><div class="card-body" style="height: 3000px">Long</div></div>
                </div>
            </div>
        </div>
        """;

    [Fact]
    public async Task On_a_phone_the_document_scrolls_and_the_topbar_sticks()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Shell);
        await page.SetViewportSizeAsync(375, 700);

        Assert.True(await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollHeight > window.innerHeight"),
            "The document did not grow with its content.");
        Assert.Equal("visible", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('page')).overflowY"));
        Assert.Equal("sticky", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('topbar')).position"));

        await page.EvaluateAsync("() => window.scrollTo(0, 400)");
        var top = await page.EvaluateAsync<double>("() => document.getElementById('topbar').getBoundingClientRect().top");
        Assert.True(Math.Abs(top) < 1, $"The topbar scrolled away: top {top}.");
    }

    [Fact]
    public async Task On_a_desktop_the_page_is_still_the_scroller()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Shell);
        await page.SetViewportSizeAsync(1280, 700);

        Assert.False(await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollHeight > window.innerHeight"),
            "The document scrolls on a desktop.");
        Assert.Equal("auto", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('page')).overflowY"));
    }
}
