using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The toast stack and a <c>.fab</c> rise above the frame's bottom bar while it shows, and
/// sit on the corner when it does not.
/// </summary>
/// <remarks>
/// Both read the bar's height through <c>anchor-size()</c>, because that height is not one
/// number: an accessory, the card variant's padding and the home indicator all add to it.
/// The height and not the position: under <c>.layout--flow</c> the bar is sticky, and
/// <c>anchor()</c> resolved to where it would sit unstuck, which put the toast thousands of
/// pixels below the screen. The flow cases pin that.
/// </remarks>
public class BottombarOverlayTests : ScriptTestBase
{
    private const double Gap = 20; // --space-8

    private static string Frame(string layoutClass, string barClass = "bottombar--card", string pageExtra = "") => $"""
        <div class="layout {layoutClass}">
          <aside class="sidebar"><div class="nav-scroll"><a class="nav-link" href="#">Orders</a></div></aside>
          <div class="content">
            <header class="topbar"><span class="topbar-spacer"></span></header>
            <main class="page">{pageExtra}<div class="card"><div class="card-body" style="height:3000px">Long</div></div></main>
            <nav class="bottombar {barClass}" id="bar" aria-label="Main">
              <div class="bottombar-accessory"><span class="bottombar-accessory-text">Syncing</span></div>
              <div class="bottombar-items" id="items">
                <a class="bottombar-item active" href="#" aria-current="page"><span class="bottombar-icon"><i class="ri-inbox-line"></i></span><span class="bottombar-label">Orders</span></a>
                <a class="bottombar-item" href="#"><span class="bottombar-icon"><i class="ri-archive-line"></i></span><span class="bottombar-label">Stock</span></a>
              </div>
            </nav>
          </div>
        </div>
        <button class="fab" id="fab" type="button">New</button>
        """;

    private sealed class Edges
    {
        public double Toast { get; set; }
        public double Fab { get; set; }
        public double Bar { get; set; }
        public double Viewport { get; set; }
    }

    private static async Task<Edges> Measure(Microsoft.Playwright.IPage page) =>
        await page.EvaluateAsync<Edges>("""
            () => {
              if (!document.querySelector('.toast')) sednaUi.toast.show('Saved', { timeout: 0 });
              const bar = document.getElementById('bar').getBoundingClientRect();
              return {
                toast: document.querySelector('.toast-stack').getBoundingClientRect().bottom,
                fab: document.getElementById('fab').getBoundingClientRect().bottom,
                bar: bar.height ? bar.top : NaN,
                viewport: window.innerHeight,
              };
            }
            """);

    [Theory]
    [InlineData("layout--bottombar", "bottombar--card")]
    [InlineData("layout--bottombar", "")]
    [InlineData("layout--bottombar layout--flow", "bottombar--card")]
    public async Task On_a_phone_both_sit_above_the_bar(string layoutClass, string barClass)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame(layoutClass, barClass));
        await page.SetViewportSizeAsync(375, 700);

        var edges = await Measure(page);

        Assert.False(double.IsNaN(edges.Bar), "The bar should show on a phone.");
        Assert.Equal(edges.Bar - Gap, edges.Toast, 1.0);
        Assert.Equal(edges.Bar - Gap, edges.Fab, 1.0);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_scrolled_document_leaves_them_above_the_bar()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Frame("layout--bottombar layout--flow"));
        await page.SetViewportSizeAsync(375, 700);

        await Measure(page); // the toast shows before the scroll
        await page.EvaluateAsync("() => window.scrollTo(0, 1200)");
        await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
        var edges = await Measure(page);

        Assert.Equal(edges.Bar - Gap, edges.Toast, 1.0);
        Assert.Equal(edges.Bar - Gap, edges.Fab, 1.0);
    }

    [Fact]
    public async Task With_the_bar_hidden_on_a_wide_screen_both_sit_on_the_corner()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Frame("layout--bottombar"));
        await page.SetViewportSizeAsync(1280, 700);

        var edges = await Measure(page);

        Assert.True(double.IsNaN(edges.Bar), "The bar should be hidden above 560px.");
        Assert.Equal(edges.Viewport - Gap, edges.Toast, 1.0);
        Assert.Equal(edges.Viewport - Gap, edges.Fab, 1.0);
    }

    // The catalogue draws bars inside its pages, each in a .content of its own; the last
    // one in the document must not become every toast's floor.
    [Fact]
    public async Task A_bar_drawn_inside_a_page_is_not_the_anchor()
    {
        if (NoBrowser) return;
        const string demo = """
            <div class="content" style="height:200px"><main class="page">Demo</main>
              <nav class="bottombar" aria-label="Demo"><div class="bottombar-items"><a class="bottombar-item" href="#">A</a></div></nav>
            </div>
            """;
        var (page, _) = await OpenStyled($"""
            <div class="layout"><div class="content"><main class="page">{demo}</main></div></div>
            <button class="fab" id="fab" type="button">New</button>
            <nav id="bar" hidden></nav>
            """);
        await page.SetViewportSizeAsync(1280, 700);

        var edges = await Measure(page);

        Assert.Equal(edges.Viewport - Gap, edges.Toast, 1.0);
        Assert.Equal(edges.Viewport - Gap, edges.Fab, 1.0);
    }
}
