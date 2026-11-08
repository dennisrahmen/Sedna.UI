using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The separator keeps the same distance from the item after it whether that item holds
/// bare text or elements.
/// </summary>
public class BreadcrumbGapTests : ScriptTestBase
{
    [Fact]
    public async Task A_bare_text_item_keeps_the_gap_after_the_separator()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <nav class="breadcrumb" aria-label="Breadcrumb">
              <a class="breadcrumb-item" href="#">Orders</a>
              <a class="breadcrumb-item" href="#" id="bare">ORD-4209</a>
              <span class="breadcrumb-item" aria-current="page" id="wrapped"><span>Lines</span></span>
            </nav>
            """);

        var gaps = await page.EvaluateAsync<double[]>("""
            () => ['bare', 'wrapped'].map(id => {
                const item = document.getElementById(id);
                const text = document.createRange();
                const node = id === 'bare' ? item.lastChild : item.firstElementChild.firstChild;
                text.selectNodeContents(node);
                return text.getBoundingClientRect().left - item.getBoundingClientRect().left;
            })
            """);
        Assert.True(Math.Abs(gaps[0] - gaps[1]) < 1,
            $"The text after the separator starts {gaps[0]:0.0}px into a bare item and {gaps[1]:0.0}px into a wrapped one.");
    }
}
