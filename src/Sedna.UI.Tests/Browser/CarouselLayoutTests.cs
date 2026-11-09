using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The carousel measured in a layout engine: a track that snaps a slide at a time on the inline
/// axis with no scrollbar, a slide the track's width by default, several in view with
/// <c>--carousel-per-view</c>, the next one showing past the end with <c>.carousel--peek</c>,
/// and a track that starts at the right edge right to left.
/// </summary>
public class CarouselLayoutTests : ScriptTestBase
{
    private static string Track(string id, string classes = "", string style = "") => $"""
        <section class="carousel {classes}" id="{id}" style="width:400px; {style}">
          <div class="carousel-track">
            <div class="carousel-slide" style="height:60px">One</div>
            <div class="carousel-slide" style="height:60px">Two</div>
            <div class="carousel-slide" style="height:60px">Three</div>
          </div>
        </section>
        """;

    private static string Fixture(string dir) => $"""
        <div dir="{dir}">
          {Track("plain")}
          {Track("two", style: "--carousel-per-view: 2")}
          {Track("peek", "carousel--peek")}
          {Track("both", "carousel--peek", "--carousel-per-view: 2")}
        </div>
        """;

    private const string Measure = """
        id => {
            const c = document.getElementById(id), t = c.querySelector('.carousel-track');
            const cs = getComputedStyle(t), slides = [...t.children].map(s => s.getBoundingClientRect());
            const tr = t.getBoundingClientRect();
            return {
                snapType: cs.scrollSnapType,
                align: getComputedStyle(t.children[0]).scrollSnapAlign,
                stop: getComputedStyle(t.children[0]).scrollSnapStop,
                overflowX: cs.overflowX,
                scrollbar: cs.scrollbarWidth,
                track: { left: tr.left, right: tr.right, width: tr.width, height: tr.height, clientHeight: t.clientHeight },
                slides: slides.map(s => ({ left: s.left, right: s.right, width: s.width })),
                gap: parseFloat(cs.columnGap)
            };
        }
        """;

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_track_snaps_a_slide_at_a_time_and_each_variant_sizes_its_slides(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture(dir));

        var plain = await page.EvaluateAsync<JsonElement>(Measure, "plain");
        var snap = plain.GetProperty("snapType").GetString()!;
        Assert.Contains("mandatory", snap, StringComparison.Ordinal);
        Assert.Matches("^(inline|x) ", snap);
        Assert.Equal("start", plain.GetProperty("align").GetString());
        Assert.Equal("always", plain.GetProperty("stop").GetString());
        Assert.Equal("auto", plain.GetProperty("overflowX").GetString());
        Assert.Equal("none", plain.GetProperty("scrollbar").GetString());
        // No scrollbar takes height from the track.
        var track = plain.GetProperty("track");
        Assert.Equal(track.GetProperty("height").GetDouble(), track.GetProperty("clientHeight").GetDouble(), 0.5);

        var trackWidth = track.GetProperty("width").GetDouble();
        var gap = plain.GetProperty("gap").GetDouble();
        Assert.True(gap > 0);

        // One per view: a slide is the track's width, and starts at the track's inline start.
        var first = plain.GetProperty("slides")[0];
        Assert.Equal(trackWidth, first.GetProperty("width").GetDouble(), 0.5);
        var startEdge = dir == "ltr" ? "left" : "right";
        Assert.Equal(track.GetProperty(startEdge).GetDouble(), first.GetProperty(startEdge).GetDouble(), 0.5);

        // Two per view: two slides and the gap between them fill the track.
        var two = await page.EvaluateAsync<JsonElement>(Measure, "two");
        Assert.Equal((trackWidth - gap) / 2, two.GetProperty("slides")[0].GetProperty("width").GetDouble(), 0.5);

        // A peek: the next slide shows past the end, by the peek less the gap — 15% of the track.
        var peek = await page.EvaluateAsync<JsonElement>(Measure, "peek");
        var peekSlide = peek.GetProperty("slides")[0].GetProperty("width").GetDouble();
        Assert.Equal(trackWidth * 0.85, peekSlide, 0.5);
        var second = peek.GetProperty("slides")[1];
        var shown = dir == "ltr"
            ? track.GetProperty("right").GetDouble() - second.GetProperty("left").GetDouble()
            : second.GetProperty("right").GetDouble() - track.GetProperty("left").GetDouble();
        Assert.Equal(trackWidth * 0.15 - gap, shown, 0.5);

        // Both: two in view and a peek of the third.
        var both = await page.EvaluateAsync<JsonElement>(Measure, "both");
        Assert.Equal((trackWidth * 0.85 - gap) / 2, both.GetProperty("slides")[0].GetProperty("width").GetDouble(), 0.5);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_carousel_in_a_narrow_column_keeps_its_slides_inside_the_track()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""<div style="width:240px">{Track("narrow", "carousel--peek", "width:auto")}</div>""");

        var m = await page.EvaluateAsync<JsonElement>(Measure, "narrow");
        Assert.Equal(240, m.GetProperty("track").GetProperty("width").GetDouble(), 0.5);
        Assert.Equal(240 * 0.85, m.GetProperty("slides")[0].GetProperty("width").GetDouble(), 0.5);
        // The page itself does not scroll sideways.
        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"));
    }
}
