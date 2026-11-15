using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A chip that is a button or a link has a disabled state, as a button does: it loses its tone
/// and its fill, and the pointer no longer lifts it.
/// </summary>
public class ChipStateTests : ScriptTestBase
{
    [Fact]
    public async Task A_disabled_chip_loses_its_tone_and_does_not_lift_under_the_pointer()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="display:flex;gap:16px;padding:24px">
              <button class="chip chip--go" id="on" type="button"><i class="ri-check-line"></i><span class="chip-label">Open</span></button>
              <button class="chip chip--go" id="off" type="button" disabled><i class="ri-check-line"></i><span class="chip-label">Open</span></button>
              <button class="chip" id="pressed-off" type="button" aria-pressed="true" disabled><span class="chip-label">Mine</span></button>
              <a class="chip" id="link-off" href="#" aria-disabled="true"><span class="chip-label">All</span></a>
            </div>
            """);
        var look = "id => { const s = getComputedStyle(document.getElementById(id)); return [s.backgroundColor, s.borderTopColor, s.color, s.cursor]; }";
        var on = await page.EvaluateAsync<string[]>(look, "on");
        foreach (var id in new[] { "off", "pressed-off", "link-off" })
        {
            var rest = await page.EvaluateAsync<string[]>(look, id);
            Assert.NotEqual(on[0], rest[0]);
            Assert.Equal("not-allowed", rest[3]);
            await page.Locator("#" + id).HoverAsync(new() { Force = true });
            var hover = await page.EvaluateAsync<string[]>(look, id);
            Assert.Equal(rest, hover);
        }
    }
}
