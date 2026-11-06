using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.sedna-split--stack-*</c> stacks at its own width and not the viewport's, and
/// <c>--aside-first</c> and <c>--sticky</c> act on the state it is in.
/// </summary>
public class SplitStackTests : ScriptTestBase
{
    private static string Split(int width) => $"""
        <div style="width:{width}px">
          <div class="sedna-split sedna-split--stack-md sedna-split--aside-first sedna-split--sticky" id="s">
            <div id="main" style="height:600px">Main</div>
            <aside class="sedna-split-aside" id="aside">Aside</aside>
          </div>
        </div>
        """;

    private static Task<double[]> Measure(Microsoft.Playwright.IPage page) =>
        page.EvaluateAsync<double[]>("""
            () => {
              const m = document.getElementById('main').getBoundingClientRect();
              const a = document.getElementById('aside').getBoundingClientRect();
              return [a.top < m.top ? 1 : 0, a.left >= m.right - 1 ? 1 : 0,
                      getComputedStyle(document.getElementById('aside')).position === 'sticky' ? 1 : 0];
            }
            """);

    [Fact]
    public async Task Below_its_width_it_stacks_with_the_aside_first_and_nothing_sticks()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Split(800));
        var m = await Measure(page);
        Assert.Equal(1, m[0]);
        Assert.Equal(0, m[2]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Above_its_width_the_two_sit_side_by_side_and_the_aside_sticks()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Split(1000));
        var m = await Measure(page);
        Assert.Equal(1, m[1]);
        Assert.Equal(1, m[2]);
        Assert.Empty(errors);
    }
}
