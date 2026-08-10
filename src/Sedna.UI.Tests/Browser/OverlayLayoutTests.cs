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

    [Fact]
    public async Task A_sheet_sits_on_the_bottom_edge_and_stays_inside_the_viewport()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Panels);

        await page.ClickAsync("button:has-text('Sheet')");
        var box = await Box(page, "sheet");

        Assert.Equal(0, box[0], 1);                       // full width
        Assert.Equal(box[4], box[1], 1);
        Assert.Equal(box[5], box[3], 1);                  // on the bottom edge
        Assert.True(box[2] >= 0,
            $"The sheet starts at {box[2]}px, above the top of the viewport — its cap is not applying.");
        Assert.True(box[3] - box[2] <= box[5] * 0.85 + 1,
            $"The sheet is {box[3] - box[2]}px tall, past the 85vh cap on a {box[5]}px viewport.");

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("side")]
    [InlineData("start")]
    [InlineData("sheet")]
    public async Task A_drawer_draws_exactly_one_edge(string id)
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
        Assert.True(drawn == 1,
            $"#{id} draws {drawn} borders ({string.Join(", ", widths)}). A drawer has one edge "
            + "facing the content and three against the viewport.");
        Assert.True(widths.Max() <= 1.5, $"#{id}'s edge is {widths.Max()}px — that is the UA's, not ours.");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_closed_overlay_leaves_the_document()
    {
        if (NoBrowser) return;
        // `.drawer` and `.palette` give a <dialog> a display of their own, which overrides
        // the UA's `display: none` for the closed state as well unless it is gated. The
        // palette is built once and reused, so an ungated rule left a 560px panel sitting
        // below the page content for the rest of the session after the first Ctrl-K.
        var (page, errors) = await OpenStyled(
            Panels
            + """
              <dialog class="palette" id="p"><input class="palette-input" /></dialog>
              """);

        // Opened and closed again, because the closed state after an open is the one that
        // regressed — a dialog that has never been opened is trivially hidden.
        await page.ClickAsync("button:has-text('Filters')");
        await page.EvaluateAsync("() => document.getElementById('side').close()");

        foreach (var id in (string[])["side", "start", "sheet", "p"])
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

        foreach (var id in (string[])["side", "start", "sheet", "m"])
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
