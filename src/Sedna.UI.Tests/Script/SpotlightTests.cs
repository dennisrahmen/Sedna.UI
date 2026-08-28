using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The spotlight's geometry and its interaction lock. A source scan cannot test any of
/// this: every answer here is the browser's — where a laid-out bubble fits, whether an
/// element has client rects, and whether a capture-phase guard actually stops a click.
/// </summary>
public class SpotlightTests : ScriptTestBase
{
    /// <summary>
    /// A stage with a positioned offset parent, the hole, the bubble, and three
    /// targets — one of them rendered but not shown, which is the state a framework
    /// leaves a node in before it reveals it.
    /// </summary>
    private const string Stage = """
        <div id="stage" style="position:relative; width:600px; height:400px; margin:40px">
            <div id="one"   style="position:absolute; left:20px;  top:20px;  width:100px; height:40px; border-radius:999px"></div>
            <div id="two"   style="position:absolute; left:200px; top:120px; width:80px;  height:30px"></div>
            <div id="gone"  style="display:none; width:50px; height:50px"></div>
            <div id="hole" class="spotlight-hole spotlight-ring"></div>
            <div id="tip" class="spotlight-tip" style="width:200px">Step text</div>
        </div>
        """;

    [Fact]
    public async Task A_spotlight_over_several_targets_covers_their_union_and_skips_the_invisible_ones()
    {
        if (NoBrowser) return;
        // A step frequently has to reveal a control *and* what it produced — a search box
        // plus its results. `#gone` is the case that used to drag the hole to the origin:
        // an element the framework has rendered but not shown has a 0×0 box at (0,0),
        // and unioning it silently ruins the geometry with no error anywhere.
        var (page, errors) = await OpenStyled(Stage);

        var rect = await page.EvaluateAsync<double[]>("""
            () => {
                const r = sednaUi.spotlight.at(
                    document.getElementById('hole'),
                    [document.getElementById('one'), document.getElementById('two'), document.getElementById('gone')],
                    { pad: 0 });
                return [r.left, r.top, r.width, r.height];
            }
            """);

        Assert.Equal(new double[] { 20, 20, 260, 130 }, rect);   // 20 → 280 across, 20 → 150 down
        Assert.Empty(errors);
    }

    [Fact]
    public async Task An_included_element_widens_the_hole_and_a_selector_is_resolved_at_call_time()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Stage);

        var rect = await page.EvaluateAsync<double[]>("""
            () => {
                const r = sednaUi.spotlight.at(document.getElementById('hole'), '#one', { pad: 0, include: '#two' });
                return [r.width, r.height];
            }
            """);

        Assert.Equal(new double[] { 260, 130 }, rect);
    }

    [Fact]
    public async Task A_single_target_lends_the_hole_its_own_corners_and_a_union_does_not()
    {
        if (NoBrowser) return;
        // The pill-shaped button must not be highlighted with a rectangle. A union is not
        // any one of their shapes, so the inline value has to be cleared rather than left
        // carrying the previous step's radius.
        var (page, _) = await OpenStyled(Stage);

        var single = await page.EvaluateAsync<string>("""
            () => { sednaUi.spotlight.at(document.getElementById('hole'), '#one');
                    return document.getElementById('hole').style.borderRadius; }
            """);
        var union = await page.EvaluateAsync<string>("""
            () => { sednaUi.spotlight.at(document.getElementById('hole'), '#one, #two');
                    return document.getElementById('hole').style.borderRadius; }
            """);

        Assert.Equal("999px", single);
        Assert.Equal("", union);
    }

    [Fact]
    public async Task Nothing_visible_returns_null_rather_than_parking_the_hole_at_the_origin()
    {
        if (NoBrowser) return;
        // null is the signal an app needs: hide the hole. A zero box at the offset
        // parent's corner is a highlight over nothing, which reads as a bug in the app.
        var (page, _) = await OpenStyled(Stage);

        var missing = await page.EvaluateAsync<object?>(
            "() => sednaUi.spotlight.at(document.getElementById('hole'), '#gone')");
        var absent = await page.EvaluateAsync<object?>(
            "() => sednaUi.spotlight.at(document.getElementById('hole'), '#no-such-thing')");

        Assert.Null(missing);
        Assert.Null(absent);
    }

    [Theory]
    [InlineData("bottom", "bottom")]
    [InlineData("top", "top")]
    [InlineData("left", "left")]
    [InlineData("right", "right")]
    public async Task The_bubble_goes_to_the_side_it_was_asked_for_when_that_side_fits(
        string asked, string expected)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Stage);

        var side = await page.EvaluateAsync<string>($$"""
            () => {
                const rect = sednaUi.spotlight.at(document.getElementById('hole'), '#two');
                return sednaUi.spotlight.tipAt(document.getElementById('tip'), rect, { placement: '{{asked}}' });
            }
            """);

        Assert.Equal(expected, side);
    }

    [Fact]
    public async Task A_side_with_no_room_flips_to_its_opposite()
    {
        if (NoBrowser) return;
        // `#one` sits 60px from the top of the viewport, and the bubble is taller than
        // that. Asking for 'top' has to yield 'bottom' rather than a bubble half off the
        // screen — which is the whole reason a tour cannot just always place below.
        var (page, _) = await OpenStyled(Stage);

        var side = await page.EvaluateAsync<string>("""
            () => {
                const tip = document.getElementById('tip');
                tip.style.height = '200px';
                const rect = sednaUi.spotlight.at(document.getElementById('hole'), '#one');
                return sednaUi.spotlight.tipAt(tip, rect, { placement: 'top' });
            }
            """);

        Assert.Equal("bottom", side);
    }

    [Fact]
    public async Task The_bubble_is_clamped_into_the_viewport_beside_an_edge_anchor()
    {
        if (NoBrowser) return;
        // A bubble centred on an anchor near the top-left corner overhangs both edges.
        // Without the clamp the reader loses the first words of every step, and the
        // library's own tipAt() had exactly that shape before follow() existed.
        var (page, _) = await OpenStyled("""
            <div id="stage" style="position:relative">
                <div id="edge" style="position:absolute; left:0; top:0; width:20px; height:20px"></div>
                <div id="hole" class="spotlight-hole"></div>
                <div id="tip" class="spotlight-tip" style="width:260px">Step text</div>
            </div>
            """);

        var box = await page.EvaluateAsync<double[]>("""
            () => {
                const rect = sednaUi.spotlight.at(document.getElementById('hole'), '#edge');
                sednaUi.spotlight.tipAt(document.getElementById('tip'), rect, { placement: 'bottom', margin: 10 });
                const b = document.getElementById('tip').getBoundingClientRect();
                return [b.left, b.top];
            }
            """);

        Assert.True(box[0] >= 10, $"the bubble overhangs the left edge at {box[0]}");
        Assert.True(box[1] >= 10, $"the bubble overhangs the top edge at {box[1]}");
    }

    [Fact]
    public async Task A_bubble_with_no_width_of_its_own_is_measured_at_its_full_size()
    {
        if (NoBrowser) return;
        // An absolutely positioned box with only `left` set is shrink-to-fit against what
        // is left of its containing block. Measuring it beside a right-hand target
        // therefore returns a box far narrower than the one that will be drawn, and the
        // placement puts the real bubble straight through the thing it is explaining.
        // `.spotlight-tip` has a max-width and no width, so this is the ordinary case.
        var (page, _) = await OpenStyled("""
            <div id="stage" style="position:relative; height:300px; width:800px; border:1px solid var(--border)">
                <div id="far" style="position:absolute; right:20px; top:120px; width:120px; height:40px"></div>
                <div id="hole" class="spotlight-hole"></div>
                <div id="tip" class="spotlight-tip">A step with enough words in it to run the bubble out to its full width.</div>
            </div>
            """);

        var gap = await page.EvaluateAsync<double>("""
            () => {
                sednaUi.spotlight.tipAt('#tip', sednaUi.spotlight.at('#hole', '#far'),
                    { placement: 'left', gap: 12, boundary: '#stage' });
                const t = document.getElementById('tip').getBoundingClientRect();
                const h = document.getElementById('hole').getBoundingClientRect();
                return Math.round(h.left - t.right);
            }
            """);

        Assert.Equal(12, gap);
    }

    [Fact]
    public async Task A_side_with_no_room_does_not_flip_onto_a_side_with_even_less()
    {
        if (NoBrowser) return;
        // A one-way flip lands the bubble on whatever is opposite, worse or not, and the
        // clamp then shoves it back over the thing the step is explaining. Neither side
        // fits here, and the reader is better served by the roomier one.
        var (page, _) = await OpenStyled("""
            <div id="stage" style="position:relative; height:200px; width:400px; border:1px solid var(--border)">
                <div id="high" style="position:absolute; left:150px; top:10px; width:60px; height:20px"></div>
                <div id="hole" class="spotlight-hole"></div>
                <div id="tip" class="spotlight-tip" style="width:120px; height:300px">Step</div>
            </div>
            """);

        var side = await page.EvaluateAsync<string>("""
            () => sednaUi.spotlight.tipAt('#tip', sednaUi.spotlight.at('#hole', '#high'),
                { placement: 'bottom', boundary: '#stage' })
            """);

        Assert.Equal("bottom", side);
    }

    [Fact]
    public async Task A_boundary_stands_in_for_the_viewport_when_the_tour_runs_inside_a_panel()
    {
        if (NoBrowser) return;
        // Clamping to the viewport is right for a tour that runs across the page and
        // wrong for one inside a box: the bubble lands where the browser can see it and
        // the box has already clipped it away. Naming the box fixes both the clamp and
        // which side 'auto' picks.
        var (page, _) = await OpenStyled("""
            <div style="height:40px"></div>
            <div id="stage" style="position:relative; height:300px; width:400px; overflow:hidden;
                                   border:1px solid var(--border)">
                <div id="mid" style="position:absolute; left:170px; top:140px; width:60px; height:20px"></div>
                <div id="hole" class="spotlight-hole"></div>
                <div id="tip" class="spotlight-tip" style="width:140px; height:60px">Step</div>
            </div>
            """);

        var box = await page.EvaluateAsync<double[]>("""
            () => {
                const rect = sednaUi.spotlight.at('#hole', '#mid');
                sednaUi.spotlight.tipAt('#tip', rect, { placement: 'auto', boundary: '#stage', margin: 8 });
                const b = document.getElementById('tip').getBoundingClientRect();
                const s = document.getElementById('stage').getBoundingClientRect();
                return [b.left - s.left, b.top - s.top, s.right - b.right, s.bottom - b.bottom];
            }
            """);

        Assert.All(box, edge => Assert.True(edge >= 8, $"the bubble left the stage by {edge}px"));
    }

    [Fact]
    public async Task Auto_picks_the_side_with_the_most_room()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div id="stage" style="position:relative; height:2000px">
                <div id="hugger" style="position:absolute; left:0; top:0; width:40px; height:40px"></div>
                <div id="hole" class="spotlight-hole"></div>
                <div id="tip" class="spotlight-tip" style="width:120px">Step</div>
            </div>
            """);

        // Hard against the top-left corner, so 'right' has the whole viewport width and
        // 'left' and 'top' have nothing.
        var side = await page.EvaluateAsync<string>("""
            () => {
                const rect = sednaUi.spotlight.at(document.getElementById('hole'), '#hugger');
                return sednaUi.spotlight.tipAt(document.getElementById('tip'), rect, { placement: 'auto' });
            }
            """);

        Assert.Equal("right", side);
    }

    [Fact]
    public async Task A_number_still_means_the_gap()
    {
        if (NoBrowser) return;
        // The 0.2.0 signature. Widening a function's third parameter must not break the
        // calls already written against it.
        var (page, _) = await OpenStyled(Stage);

        var gap = await page.EvaluateAsync<double>("""
            () => {
                const rect = sednaUi.spotlight.at(document.getElementById('hole'), '#two', { pad: 0 });
                sednaUi.spotlight.tipAt(document.getElementById('tip'), rect, 30);
                const target = document.getElementById('two').getBoundingClientRect();
                return Math.round(document.getElementById('tip').getBoundingClientRect().top - target.bottom);
            }
            """);

        Assert.Equal(30, gap);
    }

    [Fact]
    public async Task Follow_re_places_the_hole_when_a_re_render_moves_the_page()
    {
        if (NoBrowser) return;
        // Neither scroll nor resize fires when a framework re-renders and the DOM shifts
        // under a live step. The MutationObserver is the only thing that notices, and it
        // must watch childList only — placing writes `style`, and observing attributes
        // would loop it.
        // An in-flow target, because an absolutely positioned one would not move when a
        // node is inserted above it, and the test would pass on a broken observer.
        var (page, errors) = await OpenStyled("""
            <div id="stage" style="position:relative">
                <div id="flow" style="width:80px; height:30px">Target</div>
                <div id="hole" class="spotlight-hole"></div>
                <div id="tip" class="spotlight-tip">Step text</div>
            </div>
            """);

        await page.EvaluateAsync("""
            () => {
                window.step = sednaUi.spotlight.follow(
                    document.getElementById('hole'), '#flow', { pad: 0, tip: document.getElementById('tip') });
            }
            """);
        var before = await page.EvaluateAsync<double>("() => parseFloat(document.getElementById('hole').style.top)");

        // A node inserted above the target: exactly what a re-render does, and no scroll
        // or resize event comes with it.
        await page.EvaluateAsync("""
            () => document.getElementById('stage').insertAdjacentHTML(
                'afterbegin', '<div id="pushed" style="height:60px"></div>')
            """);
        await page.WaitForFunctionAsync(
            $"() => parseFloat(document.getElementById('hole').style.top) !== {before.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

        var after = await page.EvaluateAsync<double>("() => parseFloat(document.getElementById('hole').style.top)");
        Assert.Equal(before + 60, after);

        // And stop() lets go: the same insertion no longer moves anything.
        await page.EvaluateAsync("() => window.step.stop()");
        await page.EvaluateAsync("""
            () => document.getElementById('stage').insertAdjacentHTML(
                'afterbegin', '<div style="height:60px"></div>')
            """);
        Assert.Equal("none", await page.EvaluateAsync<string>("() => document.getElementById('hole').style.display"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Every_element_may_be_named_by_selector_instead_of_passed_in()
    {
        if (NoBrowser) return;
        // The hole and the bubble are nodes a framework replaces on any render just like
        // the anchor is, so a reference captured once goes stale. Naming them also lets a
        // Blazor app drive the whole thing from C# without threading an ElementReference
        // through every call.
        var (page, _) = await OpenStyled(Stage);

        var side = await page.EvaluateAsync<string>(
            "() => sednaUi.spotlight.tipAt('#tip', sednaUi.spotlight.at('#hole', '#two'), { placement: 'right' })");
        Assert.Equal("right", side);

        await page.EvaluateAsync(
            "() => window.step = sednaUi.spotlight.follow('#hole', '#two', { tip: '#tip', root: '#stage' })");
        Assert.NotEqual("none", await page.EvaluateAsync<string>("() => document.getElementById('hole').style.display"));
        await page.EvaluateAsync("() => window.step.stop()");
    }

    [Fact]
    public async Task Follow_hides_the_hole_when_its_target_goes_away_and_shows_it_again_when_it_returns()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Stage);

        await page.EvaluateAsync(
            "() => window.step = sednaUi.spotlight.follow(document.getElementById('hole'), '#two')");
        Assert.NotEqual("none", await page.EvaluateAsync<string>("() => document.getElementById('hole').style.display"));

        await page.EvaluateAsync("() => document.getElementById('two').style.display = 'none'");
        Assert.Null(await page.EvaluateAsync<object?>("() => window.step.update()"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => document.getElementById('hole').style.display"));

        await page.EvaluateAsync("() => document.getElementById('two').style.display = ''");
        await page.EvaluateAsync("() => window.step.update()");
        Assert.NotEqual("none", await page.EvaluateAsync<string>("() => document.getElementById('hole').style.display"));
    }

    // ── the interaction lock ────────────────────────────────────────────────

    /// <summary>
    /// A page with something to press outside the step, an anchor with a usable control
    /// and a blocked one inside it, and a text box to prove typing survives.
    /// </summary>
    private const string LockStage = """
        <div id="stage" style="position:relative">
            <button type="button" id="outside" onclick="window.hits = (window.hits || 0) + 1">Elsewhere</button>
            <div id="anchor">
                <button type="button" id="inside" onclick="window.hits = (window.hits || 0) + 1">Do it</button>
                <span data-spotlight-block>
                    <button type="button" id="blocked" onclick="window.hits = (window.hits || 0) + 1">Cancel</button>
                </span>
            </div>
            <input id="field" type="text">
            <div id="hole" class="spotlight-hole"></div>
            <div id="tip" class="spotlight-tip">
                <button type="button" id="next" onclick="window.hits = (window.hits || 0) + 1">Next</button>
            </div>
        </div>
        """;

    [Fact]
    public async Task The_lock_stops_everything_except_the_bubble_and_the_live_anchor()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(LockStage);

        await page.EvaluateAsync("""
            () => window.step = sednaUi.spotlight.follow(
                document.getElementById('hole'), '#anchor', { lock: { interactive: true } })
            """);

        // The bubble and the marked anchor answer; everything else does not. Force: the
        // point is what the capture-phase guard does with a real click, and Playwright's
        // actionability check would refuse a pointer-events:none control before the guard
        // ever saw it.
        await page.Locator("#next").ClickAsync(new() { Force = true });
        await page.Locator("#inside").ClickAsync(new() { Force = true });
        Assert.Equal(2, await page.EvaluateAsync<int>("() => window.hits || 0"));

        await page.Locator("#outside").ClickAsync(new() { Force = true });
        Assert.Equal(2, await page.EvaluateAsync<int>("() => window.hits || 0"));

        // …and a control the step explicitly blocks stays dead inside the live anchor.
        await page.Locator("#blocked").ClickAsync(new() { Force = true });
        Assert.Equal(2, await page.EvaluateAsync<int>("() => window.hits || 0"));

        await page.EvaluateAsync("() => window.step.stop()");
        await page.Locator("#outside").ClickAsync();
        Assert.Equal(3, await page.EvaluateAsync<int>("() => window.hits || 0"));
    }

    [Fact]
    public async Task Without_interactive_the_anchor_is_shown_but_not_usable()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(LockStage);

        await page.EvaluateAsync("""
            () => sednaUi.spotlight.follow(document.getElementById('hole'), '#anchor', { lock: true })
            """);

        await page.Locator("#inside").ClickAsync(new() { Force = true });
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.hits || 0"));
        Assert.False(await page.EvaluateAsync<bool>(
            "() => document.getElementById('anchor').classList.contains('spotlight-allowed')"));
    }

    [Fact]
    public async Task Typing_and_space_to_scroll_survive_the_lock()
    {
        if (NoBrowser) return;
        // A lock that swallowed every key would take the space bar out of a text box and
        // stop the page scrolling. Only Enter and Space *on a focusable control* are
        // activation, and only those are blocked.
        var (page, _) = await OpenStyled(LockStage);

        await page.EvaluateAsync(
            "() => sednaUi.spotlight.follow(document.getElementById('hole'), '#anchor', { lock: true })");

        await page.Locator("#field").FocusAsync();
        await page.Keyboard.TypeAsync("two words");
        Assert.Equal("two words", await page.InputValueAsync("#field"));

        // Enter on a button outside the step is activation, and is refused.
        await page.Locator("#outside").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.hits || 0"));
    }

    [Fact]
    public async Task The_lock_gates_hover_hints_and_hands_the_app_its_own_gate_back()
    {
        if (NoBrowser) return;
        // A control the reader cannot press must not still explain itself. Every app
        // building a tour has had to discover this coupling for itself; the lock owns it
        // now — and it chains the app's gate rather than replacing it, because an app
        // that suppresses hints for its own reasons must keep doing so.
        var (page, _) = await OpenStyled(LockStage);

        await page.EvaluateAsync("() => { window.asked = []; sednaUi.tips.gate = el => { window.asked.push(el.id); return true; }; }");
        await page.EvaluateAsync("() => sednaUi.spotlight.lock({ allow: '#anchor' })");

        var outside = await page.EvaluateAsync<bool>("() => sednaUi.tips.gate(document.getElementById('outside'))");
        var inside = await page.EvaluateAsync<bool>("() => sednaUi.tips.gate(document.getElementById('inside'))");
        Assert.False(outside);
        Assert.True(inside);
        // Chained, not replaced: the app's gate was still consulted for the allowed one.
        Assert.Equal(new[] { "inside" }, await page.EvaluateAsync<string[]>("() => window.asked"));

        await page.EvaluateAsync("() => sednaUi.spotlight.unlock()");
        Assert.True(await page.EvaluateAsync<bool>("() => sednaUi.tips.gate(document.getElementById('outside'))"));
        Assert.False(await page.EvaluateAsync<bool>("() => document.body.classList.contains('spotlight-lock')"));
    }

    [Fact]
    public async Task Locking_twice_re_aims_the_lock_and_still_restores_the_app_gate()
    {
        if (NoBrowser) return;
        // A tour calls lock() per step. Wiring a second gate on top of our own would
        // leave the app's original unreachable, and unlock() would restore the library's
        // wrapper instead of the app's function.
        var (page, _) = await OpenStyled(LockStage);

        await page.EvaluateAsync("() => { window.mine = el => true; sednaUi.tips.gate = window.mine; }");
        await page.EvaluateAsync("() => sednaUi.spotlight.lock({ allow: '#anchor' })");
        await page.EvaluateAsync("() => sednaUi.spotlight.lock({ allow: '#outside' })");

        Assert.True(await page.EvaluateAsync<bool>("() => sednaUi.tips.gate(document.getElementById('outside'))"));
        Assert.False(await page.EvaluateAsync<bool>("() => sednaUi.tips.gate(document.getElementById('inside'))"));

        await page.EvaluateAsync("() => sednaUi.spotlight.unlock()");
        Assert.True(await page.EvaluateAsync<bool>("() => sednaUi.tips.gate === window.mine"));
    }
}
