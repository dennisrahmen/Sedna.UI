using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Where a <c>&lt;dialog&gt;</c> overlay actually lands once the platform has had its
/// say.
/// </summary>
/// <remarks>
/// <para>
/// The UA stylesheet gives every <c>&lt;dialog&gt;</c> <c>margin: auto</c> and
/// <c>width</c>/<c>height: fit-content</c>. For a modal that is exactly right — it is
/// what centres it. For an edge-anchored panel it is the opposite of right, and it
/// loses silently: every rule anchoring the drawer parsed, applied, and was overruled
/// by two declarations nobody wrote.
/// </para>
/// <para>
/// The symptom was a "full-height drawer" sized to its own text, vertically centred,
/// and ten pixels short of the edge it is flush with — and a sheet that grew past the
/// bottom of the viewport, because <c>dialog.drawer { max-height: none }</c> outranked
/// <c>.sheet</c>'s own cap. Neither is visible to a source scan; both are one
/// measurement away.
/// </para>
/// </remarks>
public class OverlayLayoutTests : ScriptTestBase
{
    private const string Panels =
        """
        <div style="padding:20px">
          <button class="btn" type="button" onclick="document.getElementById('side').showModal()">Filters</button>
          <button class="btn" type="button" onclick="document.getElementById('start').showModal()">Start edge</button>
          <button class="btn" type="button" onclick="document.getElementById('sheet').showModal()">Sheet</button>
        </div>

        <dialog class="drawer" id="side">
          <div class="drawer-header"><h3>Filter</h3></div>
          <div class="drawer-body"><p>One short line.</p></div>
        </dialog>

        <dialog class="drawer drawer--start" id="start">
          <div class="drawer-header"><h3>Queues</h3></div>
          <div class="drawer-body"><p>One short line.</p></div>
        </dialog>

        <dialog class="drawer sheet" id="sheet">
          <div class="sheet-handle"></div>
          <div class="drawer-header"><h3>Reassign</h3></div>
          <div class="drawer-body">
            <p>Enough content that an uncapped sheet would run off the bottom.</p>
            <p style="height:1200px">tall</p>
          </div>
        </dialog>
        """;

    [Fact]
    public async Task A_drawer_fills_the_edge_it_is_anchored_to()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Panels);

        await page.ClickAsync("button:has-text('Filters')");

        // [left, right, top, bottom, viewportWidth, viewportHeight]
        var box = await Box(page, "side");

        Assert.Equal(box[4], box[1], 1);          // flush with the inline end
        Assert.Equal(0, box[2], 1);               // flush with the top
        Assert.Equal(box[5], box[3], 1);          // and the bottom — full height
        Assert.Equal(420, box[1] - box[0], 1);    // the width the part declares

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_start_edge_drawer_fills_the_other_edge()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Panels);

        await page.ClickAsync("button:has-text('Start edge')");
        var box = await Box(page, "start");

        Assert.Equal(0, box[0], 1);
        Assert.Equal(0, box[2], 1);
        Assert.Equal(box[5], box[3], 1);

        Assert.Empty(errors);
    }

    /// <summary>
    /// A sheet is capped at <c>min(480px, 100vw)</c> and centred, so one rule has to
    /// serve both ends of that <c>min()</c>: edge to edge where the viewport is the
    /// smaller value, 480px centred where it is not.
    /// </summary>
    /// <remarks>
    /// Both widths are measured because only one of them exercises the cap. A test at
    /// desktop width alone passes with <c>width: 480px</c> and no <c>min()</c> at all,
    /// which is a sheet cropped to 480px on a phone; a test at phone width alone passes
    /// with the old full-bleed rule.
    /// </remarks>
    [Theory]
    [InlineData(1280, 480)]
    [InlineData(375, 375)]
    public async Task A_sheet_sits_on_the_bottom_edge_capped_and_centred(int viewport, int expected)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Panels);
        await page.SetViewportSizeAsync(viewport, 800);

        await page.ClickAsync("button:has-text('Sheet')");
        var box = await Box(page, "sheet");

        var width = box[1] - box[0];
        Assert.Equal(expected, width, 1);
        // Centred, which on the narrow case is the same statement as flush to both
        // edges — the arithmetic covers both without a branch. Measured against the
        // initial containing block rather than `innerWidth`, which counts a classic
        // scrollbar the fixed-position box is not laid out over.
        var icb = await page.EvaluateAsync<double>("() => document.documentElement.clientWidth");
        Assert.Equal((icb - width) / 2, box[0], 1);

        Assert.Equal(box[5], box[3], 1);                  // on the bottom edge
        Assert.True(box[2] >= 0,
            $"The sheet starts at {box[2]}px, above the top of the viewport — its cap is not applying.");
        Assert.True(box[3] - box[2] <= box[5] * 0.85 + 1,
            $"The sheet is {box[3] - box[2]}px tall, past the 85vh cap on a {box[5]}px viewport.");

        Assert.Empty(errors);
    }

    /// <summary>
    /// A panel draws the edges that face the page and leaves the ones lying against
    /// the viewport bare.
    /// </summary>
    /// <remarks>
    /// A full-height drawer has one such edge. A sheet is capped and centred, so it has
    /// three — top and both sides — and only its block-end edge is off-screen. The
    /// count is per panel for that reason rather than being one number for all three.
    /// </remarks>
    [Theory]
    [InlineData("side", 1)]
    [InlineData("start", 1)]
    [InlineData("sheet", 3)]
    public async Task A_drawer_draws_only_the_edges_that_face_the_page(string id, int expected)
    {
        if (NoBrowser) return;
        // The UA gives every <dialog> `border: solid`, which computes to 3px of
        // currentColor. `.drawer` only ever set the inline-start edge, so the other
        // three kept the UA's — a near-white frame along three viewport edges in the
        // dark theme, which is what "the drawer has white borders" was.
        var (page, errors) = await OpenStyled(Panels);

        var widths = await page.EvaluateAsync<double[]>(
            $"() => {{ const s = getComputedStyle(document.getElementById('{id}')); "
            + "return [s.borderTopWidth, s.borderRightWidth, s.borderBottomWidth, s.borderLeftWidth]"
            + ".map(parseFloat); }");

        var drawn = widths.Count(w => w > 0);
        Assert.True(drawn == expected,
            $"#{id} draws {drawn} borders ({string.Join(", ", widths)}), expected {expected}. "
            + "A panel draws the edges facing the page and none of the ones against the viewport.");
        Assert.True(widths.Max() <= 1.5, $"#{id}'s edge is {widths.Max()}px — that is the UA's, not ours.");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_closed_overlay_leaves_the_document()
    {
        if (NoBrowser) return;
        // `.drawer`, `.palette` and `.modal` each give a <dialog> a display of their own,
        // which overrides the UA's `display: none` for the closed state as well unless it
        // is gated. The palette is built once and reused, so an ungated rule left a 560px
        // panel sitting below the page content for the rest of the session after the first
        // Ctrl-K.
        //
        // `.modal` was NOT in this list, and shipped with exactly that bug: `.modal {
        // display: flex }` is gated on nothing, so every closed modal rendered as the UA's
        // `inset: 0; margin: auto` box, over the page content and behind whatever followed
        // it in source — before its trigger was ever clicked and again after it closed.
        // Every new dialog class belongs here on the day it is written.
        var (page, errors) = await OpenStyled(
            Panels
            + """
              <dialog class="palette" id="p"><input class="palette-input" /></dialog>
              <dialog class="modal" id="m"><div class="modal-body"><p>Take over this order?</p></div></dialog>
              """);

        // Opened and closed again, because the closed state after an open is the one that
        // regressed — a dialog that has never been opened is trivially hidden.
        await page.ClickAsync("button:has-text('Filters')");
        await page.EvaluateAsync("() => document.getElementById('side').close()");
        await page.EvaluateAsync("() => document.getElementById('m').showModal()");
        await page.EvaluateAsync("() => document.getElementById('m').close()");

        foreach (var id in new[] { "side", "start", "sheet", "p", "m" })
        {
            await page.WaitForFunctionAsync(
                $"() => getComputedStyle(document.getElementById('{id}')).display === 'none'",
                null, new() { Timeout = 5_000 });
        }

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_dialog_overlay_has_no_user_agent_padding()
    {
        if (NoBrowser) return;
        // `padding: 1em` is the UA's, and it is 16px of nothing inside a panel whose own
        // header and body already carry their spacing.
        var (page, errors) = await OpenStyled(
            Panels
            + """
              <button class="btn" type="button" onclick="document.getElementById('m').showModal()">Modal</button>
              <dialog class="modal" id="m"><div class="modal-body"><p>x</p></div></dialog>
              """);

        foreach (var id in new[] { "side", "start", "sheet", "m" })
        {
            var padding = await page.EvaluateAsync<double[]>(
                $"() => {{ const s = getComputedStyle(document.getElementById('{id}')); "
                + "return [s.paddingTop, s.paddingRight, s.paddingBottom, s.paddingLeft].map(parseFloat); }");

            Assert.All(padding, p => Assert.Equal(0, p));
        }

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_modal_dialog_stays_centred()
    {
        if (NoBrowser) return;
        // The counterpart. `margin: auto` is the UA behaviour the drawer has to undo and
        // the modal has to keep, so undoing it globally would break this instead.
        var (page, errors) = await OpenStyled(
            """
            <button class="btn" type="button" onclick="document.getElementById('m').showModal()">Open</button>
            <dialog class="modal" id="m"><div class="modal-body"><p>Centred.</p></div></dialog>
            """);

        await page.ClickAsync("button");
        var box = await Box(page, "m");

        // Equal gutters left and right, and not touching either edge.
        Assert.Equal(box[4] - box[1], box[0], 1);
        Assert.True(box[0] > 0, "The modal is flush with the viewport edge instead of centred.");

        Assert.Empty(errors);
    }

    /// <summary>
    /// The panel's box, once it has finished sliding in.
    /// </summary>
    /// <remarks>
    /// Waiting for the transform to settle is not politeness, it is the difference
    /// between measuring the panel and measuring the animation: a drawer opens from
    /// <c>translateX(100%)</c>, so a rect read on the click reports it one full width
    /// off-screen. The resting state is <c>transform: none</c>, which is what makes
    /// this an exact wait rather than a sleep.
    /// </remarks>
    /// <summary>
    /// A sheet travels along the block axis in a right-to-left document too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A sheet is <c>&lt;div class="drawer sheet"&gt;</c>, so
    /// <c>[dir="rtl"] .drawer { transform: translateX(-100%) }</c> in 70-rtl.css
    /// matches it at (0,2,0) and outranked <c>.sheet</c>'s own (0,1,0)
    /// <c>translateY(100%)</c>. An RTL sheet slid in from the inline start edge, and
    /// nothing errored: there is no logical translate, so every inline-axis transform
    /// in this library needs its sign inverted by hand and it is easy to invert one
    /// that should not move at all.
    /// </para>
    /// <para>
    /// Measured rather than grepped, and in both directions from one statement: the
    /// block axis does not flip, so the assertion is the same sentence for LTR and RTL
    /// and any rule that mirrors it fails one of the two.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_closed_sheet_is_translated_down_the_block_axis(string dir)
    {
        if (NoBrowser) return;
        // The div form, whose closed state is a plain rule. It is `visibility: hidden`
        // rather than `display: none`, so it has a box and the percentage resolves.
        var (page, errors) = await OpenStyled(
            Panels + """
                     <div class="drawer sheet" id="divsheet">
                       <div class="drawer-header"><h3>Reassign</h3></div>
                       <div class="drawer-body"><p>One short line.</p></div>
                     </div>
                     """);

        var m = await page.EvaluateAsync<double[]>(
            @"(dir) => {
                document.documentElement.dir = dir;
                const el = document.getElementById('divsheet');
                const t = getComputedStyle(el).transform;
                const n = t.slice(t.indexOf('(') + 1, -1).split(',').map(parseFloat);
                return [n[4], n[5], el.getBoundingClientRect().height];
            }", dir);

        Assert.Equal(0, m[0], 1);
        Assert.True(m[1] > 0,
            $"dir={dir}: a closed sheet is translated ({m[0]}, {m[1]})px. It has to leave along the "
            + "block axis — a non-zero x is the drawer's inline-axis rule winning against `.sheet`.");
        // The full height, not some fraction of it, or the closed sheet's edge shows.
        Assert.Equal(m[2], m[1], 1);

        Assert.Empty(errors);
    }

    /// <summary>
    /// The same for the <c>&lt;dialog&gt;</c> form, whose closed state is a
    /// <c>@starting-style</c> rule rather than a plain one.
    /// </summary>
    /// <remarks>
    /// A second (0,3,1) selector pair with the same problem, and one the first test
    /// cannot reach: a closed <c>&lt;dialog&gt;</c> is <c>display: none</c>, so there
    /// is no box and no resolved percentage to read. So this measures the transition
    /// in flight instead — <c>--motion-mid</c> is stretched to four seconds and the
    /// panel is sampled while it is still almost entirely at its start value. That is
    /// the behaviour a reader sees, which is the thing worth pinning.
    /// </remarks>
    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_dialog_sheet_opens_from_the_bottom_edge(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            Panels, extraHead: "<style>:root { --motion-mid: 4s }</style>");

        await page.EvaluateAsync("(dir) => { document.documentElement.dir = dir; }", dir);
        await page.ClickAsync("button:has-text('Sheet')");
        // Sampling before the dialog is open reads a `display: none` box, where the
        // percentage has nothing to resolve against and the matrix comes back as the
        // identity — a pass or a failure decided by a race rather than by the rule.
        await page.WaitForFunctionAsync("() => document.getElementById('sheet').open");

        var m = await page.EvaluateAsync<double[]>(
            @"() => {
                const t = getComputedStyle(document.getElementById('sheet')).transform;
                if (t === 'none') return [0, 0];
                const n = t.slice(t.indexOf('(') + 1, -1).split(',').map(parseFloat);
                return [n[4], n[5]];
            }");

        Assert.True(m[1] > 1,
            $"dir={dir}: the sheet is at ({m[0]}, {m[1]})px a moment into a four-second open. It has "
            + "to still be below the viewport — 0 means no @starting-style applied at all.");
        Assert.True(Math.Abs(m[0]) < Math.Abs(m[1]),
            $"dir={dir}: the sheet is opening at ({m[0]}, {m[1]})px — mostly sideways. The "
            + "@starting-style rule for the drawer's inline axis is winning against the sheet's.");

        Assert.Empty(errors);
    }

    private static async Task<double[]> Box(Microsoft.Playwright.IPage page, string id)
    {
        await page.WaitForFunctionAsync(
            $"() => getComputedStyle(document.getElementById('{id}')).transform === 'none'",
            null, new() { Timeout = 5_000 });

        return await page.EvaluateAsync<double[]>(
            $"() => {{ const r = document.getElementById('{id}').getBoundingClientRect(); "
            + "return [r.left, r.right, r.top, r.bottom, innerWidth, innerHeight]; }");
    }
}
