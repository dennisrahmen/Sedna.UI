using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-carousel</c>: the app's previous, next and dot buttons move the track a slide at a
/// time, the slide in view and its dot carry <c>aria-current</c>, the button with nowhere to go
/// is <c>aria-disabled</c>, the focused track takes the arrow keys — mirrored right to left —,
/// the position is said in the app's words once the track rests, and a reader who asked for
/// reduced motion gets a track that lands at once.
/// </summary>
/// <remarks>
/// Four slides of one track's width each, 400px wide, so slide n starts n steps along: a
/// slide and the gap after it, measured on the page.
/// </remarks>
public class CarouselTests : ScriptTestBase
{
    private static string Fixture(string dir = "ltr") => $$"""
        <div dir="{{dir}}">
        <section class="carousel" aria-roledescription="carousel" aria-label="News" data-carousel id="c" style="width:400px">
          <div class="carousel-track" tabindex="0" aria-label="Slides" id="track">
            <div class="carousel-slide" role="group" aria-roledescription="slide" aria-label="1 of 4" style="height:80px">One</div>
            <div class="carousel-slide" role="group" aria-roledescription="slide" aria-label="2 of 4" style="height:80px">Two</div>
            <div class="carousel-slide" role="group" aria-roledescription="slide" aria-label="3 of 4" style="height:80px">Three <input id="field" /></div>
            <div class="carousel-slide" role="group" aria-roledescription="slide" aria-label="4 of 4" style="height:80px">Four</div>
          </div>
          <div class="carousel-controls">
            <button class="btn btn--icon btn--sm" type="button" data-carousel-prev aria-label="Previous" id="prev"><i class="ri-arrow-left-s-line"></i></button>
            <div class="deck-dots">
              <button class="deck-dot" type="button" data-carousel-to="1" aria-label="Slide 1" id="d1"></button>
              <button class="deck-dot" type="button" data-carousel-to="2" aria-label="Slide 2" id="d2"></button>
              <button class="deck-dot" type="button" data-carousel-to="3" aria-label="Slide 3" id="d3"></button>
              <button class="deck-dot" type="button" data-carousel-to="4" aria-label="Slide 4" id="d4"></button>
            </div>
            <button class="btn btn--icon btn--sm" type="button" data-carousel-next aria-label="Next" id="next"><i class="ri-arrow-right-s-line"></i></button>
          </div>
          <p class="visually-hidden" aria-live="polite" data-carousel-live data-carousel-say="Slide {position} of {count}: {label}" id="live"></p>
        </section>
        </div>
        """;

    private Task<IPage> OpenReduced(string dir = "ltr") =>
        Open(Fixture(dir), head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);

    private static Task<int> Current(IPage page) =>
        page.EvaluateAsync<int>("() => sednaUi.carousel.current('c')");

    /// <summary>Waits until slide <paramref name="n"/> (from 1) and only it is marked, with its dot.</summary>
    private static Task WaitForCurrent(IPage page, int n) =>
        page.WaitForFunctionAsync("""
            n => {
                const slides = [...document.querySelectorAll('#track > *')];
                const dots = [...document.querySelectorAll('[data-carousel-to]')];
                return slides.every((s, i) => (s.getAttribute('aria-current') === 'true') === (i === n - 1))
                    && dots.every((d, i) => (d.getAttribute('aria-current') === 'true') === (i === n - 1));
            }
            """, n);

    /// <summary>How far one slide is from the next along the track, whichever way it runs.</summary>
    private static Task<double> Step(IPage page) =>
        page.EvaluateAsync<double>("() => { const s = document.querySelectorAll('#track > *'); return Math.abs(s[1].getBoundingClientRect().left - s[0].getBoundingClientRect().left); }");

    private static Task<string?> Disabled(IPage page, string id) =>
        page.EvaluateAsync<string?>("id => document.getElementById(id).getAttribute('aria-disabled')", id);

    [Fact]
    public async Task The_first_slide_is_current_on_load_and_nothing_is_said()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());

        await WaitForCurrent(page, 1);
        Assert.Equal("true", await Disabled(page, "prev"));
        Assert.Null(await Disabled(page, "next"));
        Assert.Equal("", await page.EvaluateAsync<string>("() => document.getElementById('live').textContent"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Next_and_previous_move_one_slide_and_mark_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await WaitForCurrent(page, 1);

        var step = await Step(page);
        var from = await Js.ScrollPosition(page, "#track");
        await page.ClickAsync("#next");
        await Js.ScrollAtRest(page, "#track", from);
        await WaitForCurrent(page, 2);
        Assert.Equal(step, await page.EvaluateAsync<double>("() => document.getElementById('track').scrollLeft"), 1);
        Assert.Null(await Disabled(page, "prev"));

        from = await Js.ScrollPosition(page, "#track");
        await page.ClickAsync("#prev");
        await Js.ScrollAtRest(page, "#track", from);
        await WaitForCurrent(page, 1);
        Assert.Equal("true", await Disabled(page, "prev"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_dot_goes_to_its_slide_and_the_last_disables_next()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture());
        await WaitForCurrent(page, 1);

        var from = await Js.ScrollPosition(page, "#track");
        await page.ClickAsync("#d4");
        await Js.ScrollAtRest(page, "#track", from);
        await WaitForCurrent(page, 4);
        await page.WaitForFunctionAsync("() => document.getElementById('next').getAttribute('aria-disabled') === 'true'");

        // A press on the disabled button goes nowhere, and keeps the focus where it was.
        await page.FocusAsync("#next");
        await page.Keyboard.PressAsync("Enter");
        await Js.Frames(page);
        Assert.Equal(3, await Current(page));
        Assert.Equal("next", await page.EvaluateAsync<string>("() => document.activeElement.id"));
    }

    [Fact]
    public async Task The_position_is_said_in_the_apps_words_once_the_track_rests()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture());
        await WaitForCurrent(page, 1);

        await page.ClickAsync("#d3");
        await page.WaitForFunctionAsync("() => document.getElementById('live').textContent === 'Slide 3 of 4: 3 of 4'");
    }

    [Fact]
    public async Task The_focused_track_takes_the_arrow_keys_home_and_end()
    {
        if (NoBrowser) return;
        var page = await OpenReduced();
        await WaitForCurrent(page, 1);
        await page.FocusAsync("#track");

        await page.Keyboard.PressAsync("ArrowRight");
        await WaitForCurrent(page, 2);
        await page.Keyboard.PressAsync("End");
        await WaitForCurrent(page, 4);
        await page.Keyboard.PressAsync("ArrowLeft");
        await WaitForCurrent(page, 3);
        await page.Keyboard.PressAsync("Home");
        await WaitForCurrent(page, 1);

        // An arrow key in a field inside a slide is the field's.
        await Js.Run(page, "() => sednaUi.carousel.go('c', 2)");
        await WaitForCurrent(page, 3);
        await page.FocusAsync("#field");
        await page.Keyboard.PressAsync("ArrowRight");
        await Js.Frames(page);
        Assert.Equal(2, await Current(page));
    }

    [Fact]
    public async Task Right_to_left_the_slides_run_from_the_right_and_the_arrow_keys_mirror()
    {
        if (NoBrowser) return;
        var page = await OpenReduced("rtl");
        await WaitForCurrent(page, 1);

        // The first slide sits against the right edge of the track.
        var edges = await page.EvaluateAsync<double[]>("""
            () => { const t = document.getElementById('track').getBoundingClientRect(), s = document.querySelector('#track > *').getBoundingClientRect(); return [t.right, s.right]; }
            """);
        Assert.Equal(edges[0], edges[1], 1);

        var step = await Step(page);
        await page.ClickAsync("#next");
        await WaitForCurrent(page, 2);
        // On towards the left, which in RTL is a negative scroll position.
        Assert.Equal(-step, await page.EvaluateAsync<double>("() => document.getElementById('track').scrollLeft"), 1);

        await page.FocusAsync("#track");
        await page.Keyboard.PressAsync("ArrowLeft");
        await WaitForCurrent(page, 3);
        await page.Keyboard.PressAsync("ArrowRight");
        await WaitForCurrent(page, 2);

        // The arrows on the buttons turn round.
        var flipped = await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#next > i')).transform");
        Assert.StartsWith("matrix(-1", flipped, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Under_reduced_motion_the_track_lands_at_once()
    {
        if (NoBrowser) return;
        var page = await OpenReduced();
        await WaitForCurrent(page, 1);

        var step = await Step(page);
        // Read in the same task as the press: an instant scroll has landed, a smooth one has not begun.
        var at = await page.EvaluateAsync<double>("() => { document.getElementById('d3').click(); return document.getElementById('track').scrollLeft; }");
        Assert.Equal(step * 2, at, 1);
        await WaitForCurrent(page, 3);
    }

    [Fact]
    public async Task Without_reduced_motion_the_track_moves_smoothly()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture());
        await WaitForCurrent(page, 1);

        var step = await Step(page);
        var at = await page.EvaluateAsync<double>("() => { document.getElementById('d3').click(); return document.getElementById('track').scrollLeft; }");
        Assert.True(at < step * 2 - 1, $"The track was at {at} straight after the press, as an instant scroll would leave it.");
        await Js.ScrollAtRest(page, "#track", "0,0");
        await WaitForCurrent(page, 3);
    }

    [Fact]
    public async Task A_swipe_that_lands_on_a_slide_marks_it()
    {
        if (NoBrowser) return;
        var page = await OpenReduced();
        await WaitForCurrent(page, 1);

        // The reader's own scroll, as a swipe or a trackpad leaves it.
        await Js.Run(page, "n => { document.getElementById('track').scrollLeft = n; }", await Step(page));
        await WaitForCurrent(page, 2);
        await page.WaitForFunctionAsync("() => document.getElementById('live').textContent === 'Slide 2 of 4: 2 of 4'");
    }

    [Fact]
    public async Task The_script_writes_no_word_of_its_own()
    {
        if (NoBrowser) return;
        var page = await OpenReduced();
        await WaitForCurrent(page, 1);
        await Js.Run(page, "() => document.getElementById('live').removeAttribute('data-carousel-say')");

        await page.ClickAsync("#next");
        // Without a sentence the slide's own label is said.
        await page.WaitForFunctionAsync("() => document.getElementById('live').textContent === '2 of 4'");
    }
}
