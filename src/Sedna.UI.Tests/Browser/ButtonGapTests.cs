using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The room between an icon and its label is <c>--btn-gap</c> in every button, at every size, on
/// one line and on two, and the icon's own side of the padding is balanced against it.
/// </summary>
/// <remarks>
/// Measured from the boxes: the gap is the label's start minus the icon's end, and the room to
/// the edge is the icon's start minus the button's. A wrapped label is the case that looked
/// wrong — 16px of padding on one side of the icon and 4px of gap on the other.
/// </remarks>
public class ButtonGapTests : ScriptTestBase
{
    private const string Fixture = """
        <div style="display:flex; flex-direction:column; align-items:flex-start; gap:12px; padding:16px; width:360px">
          <button class="btn" id="count"><i class="ri-notification-3-line"></i> <span>12</span></button>
          <button class="btn" id="md"><i class="ri-save-line"></i> <span>Save changes</span></button>
          <button class="btn btn--sm" id="sm"><i class="ri-download-2-line"></i> <span>Export</span></button>
          <button class="btn btn--lg" id="lg"><i class="ri-check-line"></i> <span>Large</span></button>
          <button class="btn" id="trail"><span>Open the guide</span> <i class="ri-arrow-right-line"></i></button>
          <button class="btn btn--wrap" id="wrap1" style="width:220px"><i class="ri-user-line"></i><span>Shared mailbox for the regional fulfilment team</span></button>
          <button class="btn btn--wrap" id="wrap2" style="width:220px"><i class="ri-user-line"></i><span>Alex Fischer<br><span class="text-muted text-sm">Northwind Retail, accounts payable and reconciliation</span></span></button>
          <button class="btn btn--icon" id="only" aria-label="Refresh"><i class="ri-refresh-line"></i></button>
          <button class="fab" id="fab" style="position:static"><i class="ri-add-line"></i> <span>New order</span></button>
          <button class="fab fab--lg" id="fablg" style="position:static"><i class="ri-add-line"></i> <span>New order</span></button>
        </div>
        """;

    private const string Measure = """
        id => {
            const b = document.getElementById(id), r = b.getBoundingClientRect();
            const icon = b.querySelector('i'), label = [...b.children].find(c => c !== icon && c.tagName === 'SPAN');
            const i = icon.getBoundingClientRect(), l = label ? label.getBoundingClientRect() : null;
            const trailing = l && l.left < i.left;
            return {
                gap: l ? (trailing ? i.left - l.right : l.left - i.right) : null,
                edge: trailing ? r.right - i.right : i.left - r.left,
                labelHeight: l ? l.height : 0,
                padX: parseFloat(getComputedStyle(b).getPropertyValue('--btn-pad-x') || '0'),
                width: r.width,
            };
        }
        """;

    private static double D(JsonElement e, string name) => e.GetProperty(name).GetDouble();

    [Fact]
    public async Task The_gap_between_an_icon_and_its_label_is_the_token_in_every_button()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);
        var token = await page.EvaluateAsync<double>("""
            () => { const p = document.createElement('div'); p.style.width = 'var(--btn-gap)'; document.body.append(p);
                    const w = p.getBoundingClientRect().width; p.remove(); return w; }
            """);
        Assert.True(token > 0, "--btn-gap is not declared.");

        foreach (var id in new[] { "count", "md", "sm", "lg", "trail", "wrap1", "wrap2", "fab", "fablg" })
        {
            var m = await page.EvaluateAsync<JsonElement>(Measure, id);
            Assert.True(Math.Abs(D(m, "gap") - token) <= 0.5, $"#{id}: the icon is {D(m, "gap")}px from its label, not {token}px.");
        }
    }

    [Fact]
    public async Task The_icons_own_side_of_the_padding_is_four_pixels_less_so_a_wrapped_label_is_balanced()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);

        foreach (var id in new[] { "count", "md", "sm", "lg", "trail", "wrap1", "wrap2" })
        {
            var m = await page.EvaluateAsync<JsonElement>(Measure, id);
            // The border is 1px of the room to the edge.
            Assert.True(Math.Abs(D(m, "edge") - 1 - (D(m, "padX") - 4)) <= 0.5,
                $"#{id}: the icon is {D(m, "edge")}px from the edge; its side of {D(m, "padX")}px padding should give back 4px.");
        }
        foreach (var id in new[] { "wrap1", "wrap2" })
        {
            var m = await page.EvaluateAsync<JsonElement>(Measure, id);
            Assert.True(D(m, "labelHeight") >= 30, $"#{id} was meant to be a label on two lines.");
        }
    }

    [Fact]
    public async Task An_icon_only_button_keeps_its_square()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);

        var box = await page.Locator("#only").BoundingBoxAsync();

        Assert.Equal(box!.Height, box.Width, 1);
    }
}
