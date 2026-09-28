using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Between the header search and <c>.topbar-btn--search</c> there is always exactly one
/// way into search, at every width — and under <c>.layout--bottombar</c>, the bar takes
/// over where it shows.
/// </summary>
/// <remarks>
/// <c>.search</c> is hidden below 720px and the bottom bar only shows below 560px, so a
/// phone layout built from the catalogue had no search between the two.
/// </remarks>
public class SearchEntryTests : ScriptTestBase
{
    private static string Frame(string layoutClass) => $"""
        <div class="layout {layoutClass}">
          <aside class="sidebar"><div class="nav-scroll"><a class="nav-link" href="#">Orders</a></div></aside>
          <div class="content">
            <header class="topbar">
              <div class="search" id="box"><input class="search-input" type="search" placeholder="Search…" aria-label="Search"></div>
              <div class="topbar-spacer"></div>
              <button class="topbar-btn topbar-btn--search" id="btn" type="button" aria-label="Search"><i class="ri-search-line"></i></button>
            </header>
            <main class="page">Orders</main>
            <nav class="bottombar" id="bar" aria-label="Main">
              <div class="bottombar-items">
                <a class="bottombar-item active" href="#" aria-current="page"><span class="bottombar-icon"><i class="ri-inbox-line"></i></span><span class="bottombar-label">Orders</span></a>
                <button class="bottombar-item bottombar-item--end" type="button"><span class="bottombar-icon"><i class="ri-search-line"></i></span><span class="bottombar-label">Search</span></button>
              </div>
            </nav>
          </div>
        </div>
        """;

    private static async Task<string> Visible(Microsoft.Playwright.IPage page, int width)
    {
        await page.SetViewportSizeAsync(width, 700);
        return await page.EvaluateAsync<string>("""
            () => ['box', 'btn', 'bar']
              .filter(id => document.getElementById(id).getBoundingClientRect().height > 0)
              .join(' ')
            """);
    }

    [Theory]
    [InlineData(1280, "box bar")]
    [InlineData(721, "box bar")]
    [InlineData(720, "btn bar")]
    [InlineData(375, "btn bar")]
    public async Task Without_a_bottombar_layout_the_button_stands_in_for_the_box(int width, string expected)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame(""));

        Assert.Equal(expected, await Visible(page, width));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(1280, "box")]
    [InlineData(721, "box")]
    [InlineData(720, "btn")]
    [InlineData(600, "btn")]
    [InlineData(561, "btn")]
    [InlineData(560, "bar")]
    [InlineData(375, "bar")]
    public async Task Under_a_bottombar_layout_each_width_has_exactly_one_way_in(int width, string expected)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Frame("layout--bottombar"));

        Assert.Equal(expected, await Visible(page, width));
    }
}
