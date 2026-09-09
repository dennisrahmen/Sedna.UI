using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Two layouts whose defect is invisible to a source scan and obvious to a layout
/// engine: a modal taller than the window, and a pinned column in a table scrolled
/// sideways.
/// </summary>
/// <remarks>
/// <para>
/// The modal case was not submittable. A backdrop centres its panel, so a panel taller
/// than the viewport grew past both edges — the header off the top and
/// <c>.modal-footer</c>, which is where every example puts the primary action, off the
/// bottom. Nothing indicated why, because no element had a scrollbar.
/// </para>
/// <para>
/// The pinned column has three failure modes and every one of them parses: a
/// transparent sticky cell shows the columns sliding underneath it; the corner cell,
/// sticky on both axes, paints under the sticky header when the two share a z-index;
/// and a row state painted as a translucent tint disappears on the one cell that needs
/// an opaque background.
/// </para>
/// </remarks>
public class TableModalLayoutTests : ScriptTestBase
{
    [Theory]
    [InlineData("div")]
    [InlineData("dialog")]
    public async Task A_tall_modal_keeps_its_header_and_footer_and_scrolls_only_the_body(string form)
    {
        if (NoBrowser) return;

        // Both markup forms, because they fail differently: the div is centred by the
        // backdrop's `align-items: center`, and the <dialog> arrives with a UA
        // `max-height` of its own plus a <form> wrapper whose box would otherwise leave
        // the panel with one flex item and nothing to scroll.
        var rows = string.Concat(Enumerable.Range(1, 40)
            .Select(i => $"""<div class="form-field"><label class="form-label">Field {i}</label>"""
                       + """<input class="form-input" /></div>"""));

        var panel = $"""
            <div class="modal-header"><h3 id="t">Edit source</h3></div>
            <div class="modal-body">{rows}</div>
            <div class="modal-footer"><button class="btn btn-primary" id="save" type="button">Save</button></div>
            """;

        var body = form == "dialog"
            ? $"""<dialog class="modal" id="m" aria-labelledby="t"><form method="dialog">{panel}</form></dialog>"""
            : $"""<div class="modal-backdrop"><div class="modal" id="m">{panel}</div></div>""";

        var (page, _) = await OpenStyled(body);
        await page.SetViewportSizeAsync(1000, 700);
        if (form == "dialog") await page.EvaluateAsync("() => sednaUi.modal.show('m')");

        var viewport = page.ViewportSize!;
        var header = (await page.Locator(".modal-header").BoundingBoxAsync())!;
        var footer = (await page.Locator(".modal-footer").BoundingBoxAsync())!;

        Assert.True(header.Y >= -2, $"{form}: the header is {header.Y}px above the viewport.");
        Assert.True(footer.Y + footer.Height <= viewport.Height + 2,
            $"{form}: the footer ends {footer.Y + footer.Height}px down a {viewport.Height}px viewport.");

        // The Save button has to be reachable without scrolling the page, which is the
        // user-visible form of the bug.
        Assert.True(await page.Locator("#save").IsVisibleAsync());

        // And the body is the part that gives way, rather than the header or the footer
        // being squeezed. `scrollHeight > clientHeight` is the assertion that it is a
        // real scroller and not merely clipped.
        var scrolls = await page.EvaluateAsync<bool>("""
            () => { const b = document.querySelector('.modal-body');
                    return b.scrollHeight > b.clientHeight + 1; }
            """);
        Assert.True(scrolls, $"{form}: the modal body is not scrollable.");
    }

    [Fact]
    public async Task A_pinned_cell_is_opaque_and_the_corner_paints_over_the_header()
    {
        if (NoBrowser) return;

        var (page, _) = await OpenStyled("""
            <div class="sedna-scroll-x" id="scroller" style="width:320px">
                <table class="table table--sticky table--zebra table--pin-start">
                    <thead><tr><th id="corner">Staff ID</th><th>Surname</th><th>E-mail</th><th>Location</th><th>Cost centre</th></tr></thead>
                    <tbody>
                        <tr><td id="odd">10020266</td><td>Aebischer</td><td>a@example.com</td><td>Zürich</td><td>4210</td></tr>
                        <tr><td id="even">10020412</td><td>Baumgartner</td><td>b@example.com</td><td>Bern</td><td>4180</td></tr>
                    </tbody>
                </table>
            </div>
            """);
        await page.EvaluateAsync("() => { document.getElementById('scroller').scrollLeft = 200; }");

        // Opaque, on both a plain and a striped row. A pinned cell that inherited the
        // zebra stripe's translucent tint without an opaque base under it is the defect
        // here, and `background-color` is where it shows: an alpha below 1 means the
        // columns scrolling past are visible through the cell.
        foreach (var id in new[] { "odd", "even" })
        {
            var alpha = await page.EvaluateAsync<double>($$"""
                () => {
                    const c = getComputedStyle(document.getElementById('{{id}}')).backgroundColor;
                    const m = c.match(/rgba?\(([^)]+)\)/);
                    const parts = m[1].split(',').map(s => parseFloat(s));
                    return parts.length > 3 ? parts[3] : 1;
                }
                """);
            Assert.Equal(1, alpha);
        }

        // The stripe still reads: the two rows must not have the same background, or
        // the pinned column is a flat strip beside a striped table.
        var samePaint = await page.EvaluateAsync<bool>("""
            () => getComputedStyle(document.getElementById('odd')).backgroundImage
               === getComputedStyle(document.getElementById('even')).backgroundImage
            """);
        Assert.False(samePaint, "The zebra stripe does not survive under the pinned cell.");

        // The corner cell is the one to get right: sticky on both axes, and it must
        // paint OVER the pinned column below it. With both at the same z-index the
        // tbody cells win on DOM order and cover the header — so the element at the
        // corner's own centre has to be the corner.
        var topmost = await page.EvaluateAsync<string>("""
            () => {
                const r = document.getElementById('corner').getBoundingClientRect();
                const el = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
                return el.closest('th, td').id || el.closest('th, td').tagName;
            }
            """);
        Assert.Equal("corner", topmost);
    }

    [Fact]
    public async Task A_pinned_column_stays_at_the_inline_start_under_rtl()
    {
        if (NoBrowser) return;

        // `inset-inline-start` mirrors on its own; the edge SHADOW does not, because
        // box-shadow has no logical form. 70-rtl.css swaps which token the column
        // reaches for, and the observable consequence is that the shadow falls away
        // from the pinned cell rather than back across it.
        var (page, _) = await OpenStyled("""
            <div class="sedna-scroll-x" id="scroller" style="width:320px">
                <table class="table table--pin-start">
                    <thead><tr><th>Staff ID</th><th>Surname</th><th>E-mail</th><th>Location</th></tr></thead>
                    <tbody><tr><td id="pinned">10020266</td><td>Aebischer</td><td>a@example.com</td><td>Zürich</td></tr></tbody>
                </table>
            </div>
            """);

        var offsets = new Dictionary<string, double>();
        foreach (var dir in new[] { "ltr", "rtl" })
        {
            await page.EvaluateAsync("d => document.documentElement.setAttribute('dir', d)", dir);
            // Scrolled away from the inline start, or there is no shadow to measure —
            // see the test below, which is the other half of this one.
            await page.EvaluateAsync(
                "() => { const s = document.getElementById('scroller'); "
                + "s.scrollLeft = s.scrollWidth * (getComputedStyle(s).direction === 'rtl' ? -1 : 1); }");
            // A scroll-state query is re-evaluated after scroll processing, at a frame
            // boundary, so a getComputedStyle straight after the assignment can still
            // read the previous frame's answer.
            await page.EvaluateAsync(
                "() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
            var shadow = await page.EvaluateAsync<string>(
                "() => getComputedStyle(document.getElementById('pinned')).boxShadow");
            // The x offset is the first length in the resolved value, after the colour.
            var x = System.Text.RegularExpressions.Regex.Match(shadow, @"(-?\d+(?:\.\d+)?)px");
            Assert.True(x.Success, $"{dir}: no box-shadow on the pinned cell — got '{shadow}'.");
            offsets[dir] = double.Parse(x.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.True(offsets["ltr"] > 0, $"ltr: the edge shadow should fall towards the end, got {offsets["ltr"]}px.");
        Assert.True(offsets["rtl"] < 0, $"rtl: the edge shadow should mirror, got {offsets["rtl"]}px.");
    }

    [Theory]
    [InlineData(320, 0, false)]
    [InlineData(320, 200, true)]
    [InlineData(2000, 0, false)]
    public async Task A_pinned_columns_edge_shadow_is_painted_only_while_a_column_is_behind_it(
        int width, int scrollLeft, bool expected)
    {
        if (NoBrowser) return;

        // The shadow means "columns are hidden this way". It was painted whenever the
        // table carried the class — on a table that fits its container, and on one
        // sitting at the start of its scroll, where it says that about nothing. The
        // three cases are the three a reader meets: too narrow and unscrolled, too
        // narrow and scrolled, and wide enough that there is nothing to scroll.
        var (page, _) = await OpenStyled($$"""
            <div class="sedna-scroll-x" id="scroller" style="width:{{width}}px">
                <table class="table table--pin-start">
                    <thead><tr><th>Staff ID</th><th>Surname</th><th>E-mail</th><th>Location</th></tr></thead>
                    <tbody><tr><td id="pinned">10020266</td><td>Aebischer</td><td>a@example.com</td><td>Zürich</td></tr></tbody>
                </table>
            </div>
            """);
        await page.EvaluateAsync(
            $"() => {{ document.getElementById('scroller').scrollLeft = {scrollLeft}; }}");
        // A scroll-state query is re-evaluated after scroll processing, at a frame
        // boundary, so a getComputedStyle straight after the assignment can still read
        // the previous frame's answer.
        await page.EvaluateAsync(
            "() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

        // A transparent shadow is still a box-shadow, so the test is whether it PAINTS,
        // not whether the property is `none`: an alpha of 0 and an offset of 0 are what
        // switching it off looks like.
        var painted = await page.EvaluateAsync<bool>("""
            () => {
                const s = getComputedStyle(document.getElementById('pinned')).boxShadow;
                if (s === 'none') return false;
                return s.split(/,(?![^(]*\))/).some(part => {
                    const alpha = part.match(/rgba\([^)]*,\s*([\d.]+)\s*\)/);
                    if (alpha && parseFloat(alpha[1]) === 0) return false;
                    return /[1-9]/.test(part.replace(/rgba?\([^)]*\)/g, ''));
                });
            }
            """);

        Assert.Equal(expected, painted);
    }

    [Fact]
    public async Task Nothing_in_a_scrolling_column_paints_over_the_pinned_one()
    {
        if (NoBrowser) return;

        // The pinned cell was at z-index 0, which is not above `auto`: a POSITIONED
        // child of an ordinary cell paints in the same step of the stacking order as a
        // z-index: 0 stacking context, and the tie goes to tree order. So every
        // library control that positions itself — `.segmented-option` here, and
        // `.switch` and `.menu-anchor` alike — slid over the top of the pinned column
        // as the table scrolled, which reads as the pinned cell being transparent.
        // A narrow container, because the defect needs the last column to reach the
        // pinned one.
        var (page, _) = await OpenStyled("""
            <div class="sedna-scroll-x" id="scroller" style="width:200px">
              <table class="table table--sticky table--zebra table--pin-start">
                <thead><tr><th>Capability</th><th>Description</th><th>Offered</th></tr></thead>
                <tbody>
                  <tr><td id="pinned">github_add_comment</td>
                      <td>Add a review comment to the requester's latest pending review</td>
                      <td><span class="segmented"><label class="segmented-option">On</label><label class="segmented-option">Off</label></span></td></tr>
                </tbody>
              </table>
            </div>
            """);

        var topmost = await page.EvaluateAsync<string>("""
            () => {
                const s = document.getElementById('scroller');
                s.scrollLeft = s.scrollWidth;
                const c = document.getElementById('pinned').getBoundingClientRect();
                // Just inside the trailing edge, which is the part a scrolled column
                // reaches first and the last part it leaves.
                const el = document.elementFromPoint(c.right - 4, c.top + c.height / 2);
                const cell = el ? el.closest('th, td') : null;
                return cell ? (cell.id || cell.tagName) : (el ? el.tagName : 'none');
            }
            """);

        Assert.Equal("pinned", topmost);
    }
}
