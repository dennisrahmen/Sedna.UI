using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A container that holds the app's own content resets a bare paragraph's margin, and a
/// classed paragraph inside it keeps its own spacing.
/// </summary>
public class ContainerParagraphTests : ScriptTestBase
{
    public static TheoryData<string, string> Containers() => new()
    {
        { "<div class=\"modal-body\">", "</div>" },
        { "<div class=\"drawer-body\">", "</div>" },
        { "<div class=\"card\"><div class=\"card-body\">", "</div></div>" },
        { "<div class=\"chat-bubble\">", "</div>" },
    };

    [Theory]
    [MemberData(nameof(Containers))]
    public async Task A_classed_paragraph_keeps_its_own_margin(string open, string close)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <fieldset class="form-section"><legend>Outside</legend><p class="form-section-hint" id="outside">Hint</p></fieldset>
            {open}<fieldset class="form-section"><legend>Inside</legend><p class="form-section-hint" id="inside">Hint</p></fieldset>{close}
            """);

        var margins = await page.EvaluateAsync<string[]>("""
            () => ['outside', 'inside'].map(id => {
                const s = getComputedStyle(document.getElementById(id));
                return s.marginTop + ' ' + s.marginBottom;
            })
            """);
        Assert.True(margins[0] == margins[1],
            $"A .form-section-hint has margins {margins[1]} inside {open} and {margins[0]} outside it.");
    }
}
