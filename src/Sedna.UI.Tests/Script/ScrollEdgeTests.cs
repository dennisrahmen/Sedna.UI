using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The scroll-edge marks a pinned table column reads where the engine has no
/// <c>scroll-state()</c> query.
/// </summary>
public class ScrollEdgeTests : ScriptTestBase
{
    private const string Wide = """
        <div class="sedna-scroll-x" id="s" style="width:200px">
            <table class="table table--pin-end" style="min-width:600px">
                <thead><tr><th>A</th><th>B</th><th>C</th></tr></thead>
                <tbody><tr><td>1</td><td>2</td><td>3</td></tr></tbody>
            </table>
        </div>
        """;

    private const string Fits = """
        <div class="sedna-scroll-x" id="s" style="width:600px">
            <table class="table table--pin-end" style="width:300px">
                <thead><tr><th>A</th><th>B</th></tr></thead>
                <tbody><tr><td>1</td><td>2</td></tr></tbody>
            </table>
        </div>
        """;

    private static Task<string[]> Marks(Microsoft.Playwright.IPage page) =>
        page.EvaluateAsync<string[]>("""
            () => ['data-scroll-start', 'data-scroll-end']
                .filter(a => document.getElementById('s').hasAttribute(a))
            """);

    [Fact]
    public async Task A_scroller_that_fits_is_at_both_edges()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fits);
        await page.WaitForFunctionAsync("() => document.getElementById('s').hasAttribute('data-scroll-end')");
        Assert.Equal(["data-scroll-start", "data-scroll-end"], await Marks(page));
    }

    [Fact]
    public async Task The_marks_follow_the_scroll()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Wide);
        await page.WaitForFunctionAsync("() => document.getElementById('s').hasAttribute('data-scroll-start')");
        Assert.Equal(["data-scroll-start"], await Marks(page));

        await page.EvaluateAsync("() => { document.getElementById('s').scrollLeft = 100; }");
        await page.WaitForFunctionAsync("() => !document.getElementById('s').hasAttribute('data-scroll-start')");
        Assert.Empty(await Marks(page));

        await page.EvaluateAsync("() => { const s = document.getElementById('s'); s.scrollLeft = s.scrollWidth; }");
        await page.WaitForFunctionAsync("() => document.getElementById('s').hasAttribute('data-scroll-end')");
        Assert.Equal(["data-scroll-end"], await Marks(page));
    }

    [Fact]
    public async Task A_scroller_rendered_later_is_marked_too()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("<div id=\"host\"></div>");
        await page.EvaluateAsync($$"""
            () => { document.getElementById('host').innerHTML = `{{Fits}}`; }
            """);
        await page.WaitForFunctionAsync("() => document.getElementById('s').hasAttribute('data-scroll-end')");
    }
}
