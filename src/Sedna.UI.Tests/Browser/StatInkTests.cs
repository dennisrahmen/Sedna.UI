using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A stat's words stand the same distance from the start of their tile as from its top and its
/// foot — measured on the ink, not the box.
/// </summary>
/// <remarks>
/// A line of text sits inside its box with leading above and below its capitals, so a tile padded
/// the same on every side still reads taller than it is wide: the first label stood a
/// half-leading further from the top than from the start, and the figure the same from the foot.
/// The first and last lines of a stat give that leading back, so the padding is what the eye sees.
/// Every case is the label's capitals and the figure's digits, whose paint is the line they are set
/// on and not a descender; the start edge is good to the side bearing of the first glyph.
/// </remarks>
public class StatInkTests : ScriptTestBase
{
    /// <summary>What the eye cannot tell apart: a side bearing is up to a pixel, and the measurement is a sixteenth.</summary>
    private const double Tolerance = 1.5;

    /// <summary>
    /// The foot is read off digits, which overshoot their baseline by a fraction of a pixel, and WebKit
    /// sets a line whose box falls between pixels on a whole one — up to a pixel the other way. The
    /// leading it replaces was five, so the test still fails on it.
    /// </summary>
    private const double FootTolerance = 2.25;

    private const string Tile = """<span class="stat-label" id="l">HELD ORDERS</span><span class="stat-value" id="v">1204</span>""";

    public static TheoryData<string, string, string> Cases() => new()
    {
        { "plain tile in a row of tones", $"""<div class="stat-row" style="width:640px"><div class="stat stat--warn" id="s">{Tile}</div><div class="stat">{Tile}</div></div>""", "#s" },
        { "every tone", $"""<div class="stat-row" style="width:900px"><div class="stat stat--brand" id="s">{Tile}</div><div class="stat stat--go">{Tile}</div><div class="stat stat--danger">{Tile}</div><div class="stat stat--info">{Tile}</div></div>""", "#s" },
        { "toned tile in a card", $"""<div class="card" style="width:640px"><div class="card-body"><div class="stat-row"><div class="stat stat--danger" id="s">{Tile}</div><div class="stat">{Tile}</div></div></div></div>""", "#s" },
        { "tile in a card", $"""<div class="card" style="width:640px"><div class="card-body" id="s"><div class="stat-row"><div class="stat">{Tile}</div><div class="stat">{Tile}</div></div></div></div>""", "#s" },
        { "linked tile", $"""<div class="stat-row" style="width:640px"><a class="stat" href="#" id="s">{Tile}</a><a class="stat" href="#">{Tile}</a></div>""", "#s" },
        { "linked tile in a card", $"""<div class="card" style="width:640px"><div class="card-body"><div class="stat-row"><a class="stat" href="#" id="s">{Tile}</a><a class="stat" href="#">{Tile}</a></div></div></div>""", "#s" },
        { "small value, toned", $"""<div class="stat-row" style="width:640px; align-items:start"><div class="stat stat--sm stat--info" id="s">{Tile}</div><div class="stat">{Tile}</div></div>""", "#s" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_label_stands_as_far_from_the_top_as_from_the_start_and_the_figure_from_the_foot(string name, string markup, string box)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div style="padding:32px">{markup}</div>""", deviceScaleFactor: Ink.Scale);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);

        var edge = await page.EvaluateAsync<double[]>($$"""
            () => { const b = document.querySelector('{{box}}').getBoundingClientRect(), s = getComputedStyle(document.querySelector('{{box}}'));
                    // The tile's own edge: a tint's border and an app's padding are inside it, a card's rule is not.
                    return [b.left + parseFloat(s.borderLeftWidth), b.top + parseFloat(s.borderTopWidth), b.right - parseFloat(s.borderRightWidth), b.bottom - parseFloat(s.borderBottomWidth)]; }
            """);
        var ink = await Ink.Extent(page, 1, "#l", "#v");
        var start = ink[0][0] - edge[0];
        var top = ink[0][1] - edge[1];
        var foot = edge[3] - ink[1][3];

        Assert.True(Math.Abs(start - top) <= Tolerance, $"{name}: the label is {start:0.00}px from the start of its tile and {top:0.00}px from the top.");
        Assert.True(Math.Abs(start - foot) <= FootTolerance, $"{name}: the label is {start:0.00}px from the start of its tile and the figure {foot:0.00}px from the foot.");
        Assert.Empty(errors);
    }
}
