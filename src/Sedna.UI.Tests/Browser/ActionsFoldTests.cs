using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.page-head-actions--fold</c> and <c>.toolbar-actions--fold</c> fold their actions into the
/// app's <c>.actions-more</c> menu below 40rem of the row's own width, keeping what is marked
/// <c>.actions-keep</c>.
/// </summary>
public class ActionsFoldTests : ScriptTestBase
{
    private static string PageHead(int width) => $"""
        <div style="width:{width}px">
          <div class="page-head">
            <div class="page-head-text"><h1>Approval queue</h1></div>
            <div class="page-head-actions page-head-actions--fold">
              <button class="btn" type="button" id="refresh">Refresh</button>
              <button class="btn btn--brand btn--solid actions-keep" type="button" id="keep">Approve all</button>
              <div class="menu-anchor actions-more" id="more">
                <button class="btn btn--icon" type="button" data-menu-toggle aria-expanded="false" aria-label="More actions" id="toggle"><i class="ri-more-2-fill"></i></button>
                <div class="menu" hidden id="menu"><button class="menu-item" type="button">Refresh</button></div>
              </div>
            </div>
          </div>
        </div>
        """;

    private static string Toolbar(int width) => $"""
        <div style="width:{width}px">
          <div class="toolbar">
            <div class="toolbar-filters"><input class="form-input toolbar-input" aria-label="Filter"></div>
            <div class="toolbar-actions toolbar-actions--fold">
              <span class="toolbar-count actions-keep" id="keep">6 selected</span>
              <button class="btn btn--sm" type="button" id="refresh">Export</button>
              <div class="menu-anchor actions-more" id="more">
                <button class="btn btn--sm btn--icon" type="button" data-menu-toggle aria-expanded="false" aria-label="More actions"><i class="ri-more-2-fill"></i></button>
                <div class="menu" hidden><button class="menu-item" type="button">Export</button></div>
              </div>
            </div>
          </div>
        </div>
        """;

    private const string Shown = """
        () => ['refresh', 'keep', 'more'].map(id => document.getElementById(id).getBoundingClientRect().width > 0)
        """;

    public static TheoryData<string> Groups() => new() { "page head", "toolbar" };

    private static string Fixture(string group, int width) => group == "toolbar" ? Toolbar(width) : PageHead(width);

    [Theory]
    [MemberData(nameof(Groups))]
    public async Task A_narrow_row_folds_its_actions_into_the_menu_and_keeps_the_marked_one(string group)
    {
        if (NoBrowser) return;
        // A narrow column on a wide window: the fold reads the row's width, not the viewport's.
        var (page, errors) = await OpenStyled(Fixture(group, 360));
        await page.SetViewportSizeAsync(1280, 800);

        var shown = await page.EvaluateAsync<bool[]>(Shown);
        Assert.False(shown[0], $"The {group}'s folded button still shows.");
        Assert.True(shown[1], $"The {group}'s kept action was folded away.");
        Assert.True(shown[2], $"The {group}'s menu button does not show.");
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(Groups))]
    public async Task A_wide_row_shows_its_buttons_and_no_menu(string group)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture(group, 900));
        await page.SetViewportSizeAsync(1280, 800);

        var shown = await page.EvaluateAsync<bool[]>(Shown);
        Assert.True(shown[0] && shown[1], $"The {group}'s buttons are hidden at 900px.");
        Assert.False(shown[2], $"The {group}'s menu button shows at 900px.");
    }

    [Fact]
    public async Task The_folded_menu_opens_from_its_button()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(PageHead(360));
        await page.ClickAsync("#toggle");
        await page.WaitForFunctionAsync("() => !document.getElementById('menu').hidden");
        Assert.Equal("true", await page.GetAttributeAsync("#toggle", "aria-expanded"));
    }
}
