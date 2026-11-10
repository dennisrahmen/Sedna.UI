using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A footer row of actions wraps when its buttons do not fit, so every one of them stays
/// inside the footer and reachable on a phone.
/// </summary>
public class FooterWrapTests : ScriptTestBase
{
    public static TheoryData<string> Footers() => new()
    {
        "modal-footer", "drawer-footer", "deck-footer", "card-foot", "form-actions",
    };

    [Theory]
    [MemberData(nameof(Footers))]
    public async Task Four_labelled_buttons_stay_inside_a_narrow_footer(string footer)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div style="width: 300px">
              <div class="{footer}" id="f">
                <button class="btn btn--danger" type="button"><i class="ri-delete-bin-line"></i> <span>Delete</span></button>
                <button class="btn" type="button"><i class="ri-edit-line"></i> <span>Edit</span></button>
                <button class="btn btn--brand btn--solid" type="button"><i class="ri-external-link-line"></i> <span>Full view</span></button>
                <button class="btn" type="button"><i class="ri-close-line"></i> <span>Close</span></button>
              </div>
            </div>
            """);

        var outside = await page.EvaluateAsync<int>("""
            () => {
                const f = document.getElementById('f').getBoundingClientRect();
                return [...document.querySelectorAll('#f .btn')]
                    .map(b => b.getBoundingClientRect())
                    .filter(r => r.left < f.left - 0.5 || r.right > f.right + 0.5).length;
            }
            """);
        Assert.True(outside == 0, $"{outside} button(s) run past the edge of a 300px .{footer}.");
    }
}
