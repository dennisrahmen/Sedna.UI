using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Everything that has to move when the device takes part of the viewport.
/// </summary>
/// <remarks>
/// <para>
/// The values come from <c>env(safe-area-inset-*)</c>, which no browser this test can
/// drive will ever report as anything but <c>0px</c> — there is no headless flag for a
/// notch, and Playwright cannot emulate one. A test that only asserted the frame is
/// correct on a desktop would therefore pass with the whole feature deleted.
/// </para>
/// <para>
/// The indirection through four tokens is what makes it measurable: overriding them on
/// <c>:root</c> is exactly the substitution the browser would make on a phone, so every
/// rule downstream is exercised for real. Four distinct values rather than one, so a
/// rule reading the wrong edge fails instead of coinciding.
/// </para>
/// </remarks>
public class SafeAreaTests : ScriptTestBase
{
    private const double Top = 17, Bottom = 34, Start = 11, End = 13;

    /// <summary>Stands in for a device with an inset on all four edges.</summary>
    private const string Notched =
        """
        <style>
          :root {
            --safe-block-start: 17px;
            --safe-block-end: 34px;
            --safe-inline-start: 11px;
            --safe-inline-end: 13px;
          }
        </style>
        """;

    private const string Frame =
        """
        <div class="layout" id="shell">
          <aside class="sidebar"><div class="nav-scroll"><a class="nav-link" href="#">Queue</a></div></aside>
          <div class="content">
            <header class="topbar"></header>
            <main class="page" id="page"><p>One short line.</p></main>
          </div>
        </div>

        <div class="toast-stack" id="toasts"><div class="toast"><span>Saved</span></div></div>
        <button class="fab" id="fab" type="button">New</button>
        <div id="blazor-error-ui">
          <div class="status-bar status-bar--failed" id="bar"><i class="ri-wifi-off-line"></i><span>Lost</span></div>
        </div>

        <a class="skip-link" id="skip" href="#page">Skip to content</a>

        <dialog class="drawer sheet" id="sheet">
          <div class="drawer-header"><h3>Reassign</h3></div>
          <div class="drawer-body"><p>One short line.</p></div>
        </dialog>
        <dialog class="drawer" id="side">
          <div class="drawer-header"><h3>Filter</h3></div>
          <div class="drawer-body"><p>One short line.</p></div>
        </dialog>
        <dialog class="modal" id="modal">
          <div class="modal-header"><h3>Confirm</h3></div>
          <div class="modal-body"><p style="height:2000px">tall</p></div>
          <div class="modal-footer"><button class="btn btn-go" type="button">Go</button></div>
        </dialog>
        """;

    private static Task<double> Px(IPage page, string id, string property) =>
        page.EvaluateAsync<double>(
            $"() => parseFloat(getComputedStyle(document.getElementById('{id}')).{property})");

    /// <summary>
    /// The shell takes the top and the two sides, because nothing in the library is
    /// pinned to those edges — padding it moves the sidebar, the topbar and the page
    /// at once.
    /// </summary>
    [Theory]
    [InlineData("paddingTop", Top)]
    [InlineData("paddingLeft", Start)]
    [InlineData("paddingRight", End)]
    public async Task The_shell_holds_its_content_off_the_top_and_the_sides(string property, double expected)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame, Notched);

        Assert.Equal(expected, await Px(page, "shell", property), 1);
        Assert.Empty(errors);
    }

    /// <summary>
    /// The shell must not grow past the viewport paying for it. <c>height: 100dvh</c>
    /// with <c>box-sizing: border-box</c> means the padding eats inwards; anything else
    /// puts a scrollbar on the document and scrolls the whole frame off the top.
    /// </summary>
    [Fact]
    public async Task The_shell_still_fits_the_viewport()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame, Notched);

        var overflow = await page.EvaluateAsync<double>(
            "() => document.documentElement.scrollHeight - document.documentElement.clientHeight");

        Assert.True(overflow <= 1,
            $"The document scrolls by {overflow}px. The shell's safe-area padding is adding to "
            + "100dvh instead of eating into it.");
        Assert.Empty(errors);
    }

    /// <summary>
    /// The bottom edge is the other half of the split: four things are pinned to it
    /// with <c>position: fixed</c> and are outside the shell's padding entirely, so each
    /// takes the inset itself — which is also what lets each paint its own surface into
    /// the strip rather than leaving the page showing through.
    /// </summary>
    /// <remarks>
    /// The gutter and the offsets are ADDED to, never replaced: a page on a phone still
    /// wants its 24px of breathing room below the last row, and a desktop must keep the
    /// whole 24px when the inset is nothing.
    /// </remarks>
    [Theory]
    [InlineData("page", "paddingBottom", 24 + Bottom)]     // --space-9 + the inset
    [InlineData("bar", "paddingBottom", 10 + Bottom)]      // --space-5 + the inset
    [InlineData("toasts", "bottom", 20 + Bottom)]          // --space-8 + the inset
    [InlineData("toasts", "right", 20 + End)]
    [InlineData("fab", "bottom", 20 + Bottom)]
    [InlineData("fab", "right", 20 + End)]
    public async Task Everything_on_the_bottom_edge_clears_the_home_indicator(
        string id, string property, double expected)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame, Notched);

        Assert.Equal(expected, await Px(page, id, property), 1);
        Assert.Empty(errors);
    }

    /// <summary>
    /// A panel reaches the bottom edge in both its forms — a side drawer spans the block
    /// axis, a sheet sits on it — and the dialog rule's own <c>padding</c> shorthand
    /// outranks the class that sets the inset, so both are measured.
    /// </summary>
    [Theory]
    [InlineData("sheet")]
    [InlineData("side")]
    public async Task A_panel_pads_its_own_surface_into_the_inset(string id)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame, Notched);

        await page.EvaluateAsync($"() => document.getElementById('{id}').showModal()");

        Assert.Equal(Bottom, await Px(page, id, "paddingBottom"), 1);
        // Padding, not margin — the panel's own elevated surface has to fill the strip,
        // or the page shows through under it in a band the eye reads as the panel not
        // quite landing.
        Assert.Equal(0, await Px(page, id, "marginBottom"), 1);
        Assert.Empty(errors);
    }

    /// <summary>
    /// The skip link is the one thing pinned to the TOP edge, so the shell's padding
    /// never reaches it.
    /// </summary>
    /// <remarks>
    /// It is also the one element that must be readable whatever else is on screen —
    /// that is what the 1000 rung is for — and in a standalone window it would have
    /// appeared under the notch. Grown rather than offset, so the brand fill still
    /// meets both edges.
    /// </remarks>
    [Theory]
    [InlineData("paddingTop", 10 + Top)]        // --space-5 + the inset
    [InlineData("paddingLeft", 16 + Start)]     // --space-7 + the inset
    public async Task The_skip_link_clears_the_notch(string property, double expected)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame, Notched);

        Assert.Equal(expected, await Px(page, "skip", property), 1);
        Assert.Empty(errors);
    }

    /// <summary>
    /// A modal is centred, so the thing that keeps its footer — where the primary
    /// action is — off the home indicator is its cap, not padding.
    /// </summary>
    /// <remarks>
    /// Padding would be wrong: the gap belongs outside the panel, or the elevated
    /// surface is drawn across a strip the device owns. Measured as a rectangle rather
    /// than as a computed cap, because the cap is only correct if the centring then
    /// leaves the panel clear of both edges.
    /// </remarks>
    [Fact]
    public async Task A_tall_modal_keeps_its_footer_off_both_edges()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame, Notched);

        await page.EvaluateAsync("() => document.getElementById('modal').showModal()");

        var box = await page.EvaluateAsync<double[]>(
            "() => { const r = document.getElementById('modal').getBoundingClientRect(); "
            + "return [r.top, r.bottom, document.documentElement.clientHeight]; }");

        Assert.True(box[0] >= Top,
            $"The modal starts {box[0]}px down a viewport with a {Top}px inset at the top.");
        Assert.True(box[2] - box[1] >= Bottom,
            $"The modal ends {box[2] - box[1]}px from the bottom, inside a {Bottom}px home indicator.");
        Assert.Empty(errors);
    }

    /// <summary>
    /// The four tokens read <c>env()</c> with a <c>0px</c> fallback, which is what makes
    /// every rule downstream correct on a desktop without a second rule for it.
    /// </summary>
    [Theory]
    [InlineData("--safe-block-start", "top")]
    [InlineData("--safe-block-end", "bottom")]
    [InlineData("--safe-inline-start", "left")]
    [InlineData("--safe-inline-end", "right")]
    public void Each_token_reads_its_own_edge_with_a_zero_fallback(string token, string edge)
    {
        var css = Assets.StripComments(Assets.Css);
        var declaration = Regex.Match(css, Regex.Escape(token) + @":\s*([^;]+);").Groups[1].Value.Trim();

        Assert.Equal($"env(safe-area-inset-{edge}, 0px)", declaration);
    }

    /// <summary>
    /// <c>env()</c>'s four values are physical and the library is written in logical
    /// properties, so the inline pair is swapped for <c>dir="rtl"</c> in the token layer.
    /// </summary>
    /// <remarks>
    /// That swap is why <c>70-rtl.css</c> needs nothing for any of this: every use site
    /// stays logical and is correct in both directions. It cannot be measured the way the
    /// rest of this file is — overriding the tokens to simulate a device is exactly what
    /// would replace the thing under test — so it is read from the source.
    /// </remarks>
    [Fact]
    public void The_inline_pair_is_mirrored_for_rtl()
    {
        var css = Assets.StripComments(Assets.Css);
        var block = Regex.Match(css, ":root\\[dir=\"rtl\"\\]\\s*\\{([^}]*)\\}").Groups[1].Value;

        // Collapsed, because the declarations in the part are column-aligned and the
        // gap between the colon and the value is a reading choice, not a contract.
        var declarations = Regex.Replace(block, @"\s+", " ");

        Assert.Contains("--safe-inline-start: env(safe-area-inset-right, 0px)", declarations, StringComparison.Ordinal);
        Assert.Contains("--safe-inline-end: env(safe-area-inset-left, 0px)", declarations, StringComparison.Ordinal);
    }
}
