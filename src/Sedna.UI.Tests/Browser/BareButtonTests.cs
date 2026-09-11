using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.btn-bare</c> is a button with its chrome taken away, so the two things worth
/// measuring are that none of it is left and that the focus ring still is.
/// </summary>
/// <remarks>
/// The ring is the point of the class existing in the library rather than in each app.
/// A hand-written inline reset sets <c>border: none</c>, which takes the browser's own
/// focus outline box with it, and the button then says nothing at all about where the
/// keyboard is — a defect that is invisible to whoever wrote it, because they were
/// using a mouse.
/// </remarks>
public class BareButtonTests : ScriptTestBase
{
    private const string Fixture =
        """
        <p style="padding:20px" id="text">
          Reserved against order
          <button class="btn-bare" type="button" id="bare">10482</button>
          and
          <button class="btn-bare btn-bare--link" type="button" id="link">10488</button>
          by Alex Fischer.
        </p>
        <p style="padding:20px"><button class="btn" type="button" id="chromed">Open</button></p>
        """;

    [Fact]
    public async Task It_keeps_none_of_the_buttons_chrome()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var report = await page.EvaluateAsync<string[]>(@"() => {
            const bare = getComputedStyle(document.getElementById('bare'));
            const text = getComputedStyle(document.getElementById('text'));
            const out = [];
            const want = (what, got, expected) => {
                if (got !== expected) out.push(what + ': expected ' + expected + ', measured ' + got);
            };

            for (const side of ['Top', 'Right', 'Bottom', 'Left']) {
                want('padding' + side, bare['padding' + side], '0px');
                want('border' + side + 'Width', bare['border' + side + 'Width'], '0px');
            }
            want('backgroundColor', bare.backgroundColor, 'rgba(0, 0, 0, 0)');
            // No control height. It is a run of text, not a target lined up with the
            // fields and buttons beside it.
            if (parseFloat(bare.minHeight) > 0) out.push('minHeight: ' + bare.minHeight);

            // font: inherit and color: inherit — it has to read as part of the sentence
            // it is in, whatever that sentence is set in.
            want('fontFamily', bare.fontFamily, text.fontFamily);
            want('fontSize', bare.fontSize, text.fontSize);
            want('fontWeight', bare.fontWeight, text.fontWeight);
            want('lineHeight', bare.lineHeight, text.lineHeight);
            want('color', bare.color, text.color);
            want('cursor', bare.cursor, 'pointer');

            // The link variant is the one thing that does NOT inherit its colour.
            const link = getComputedStyle(document.getElementById('link'));
            if (link.color === text.color) out.push('btn-bare--link did not take the link colour');
            return out;
        }");

        Assert.True(report.Length == 0,
            "A .btn-bare is a reset: chrome left on it is chrome every app strips by hand again."
            + Environment.NewLine + string.Join(Environment.NewLine, report));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task It_keeps_the_librarys_focus_ring()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        // Tab rather than focus(): :focus-visible is a heuristic about how focus
        // arrived, and a scripted focus() is not reliably keyboard focus.
        await page.Keyboard.PressAsync("Tab");

        var focused = await page.EvaluateAsync<string>("() => document.activeElement.id");
        Assert.Equal("bare", focused);

        var (bareRing, bareOutline) = await Ring(page, "bare");

        Assert.NotEqual("none", bareRing);
        Assert.Equal("none", bareOutline);

        // The same ring the chromed button wears, rather than a second one invented
        // here: one focus style is what makes it recognisable as focus.
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("chromed", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        // `.btn` transitions its box-shadow, so a value read the instant focus lands is
        // mid-transition — transparent and zero-sized — and compares against nothing.
        // `.btn-bare` has no transition, which is why its own ring reads final at once.
        await page.WaitForTimeoutAsync(250);

        var (chromedRing, _) = await Ring(page, "chromed");
        Assert.Equal(chromedRing, bareRing);
        Assert.Empty(errors);
    }

    private static async Task<(string Shadow, string Outline)> Ring(
        Microsoft.Playwright.IPage page, string id)
    {
        var pair = await page.EvaluateAsync<string[]>(
            "id => { const cs = getComputedStyle(document.getElementById(id));"
            + " return [cs.boxShadow, cs.outlineStyle]; }", id);
        return (pair[0], pair[1]);
    }

    [Fact]
    public void The_ring_is_restated_as_an_outline_for_forced_colours()
    {
        // box-shadow is not painted in forced colours, so every ring in the library is
        // restated there as an outline. A chromeless button that misses that
        // restatement has no border, no fill and no ring: nothing whatsoever marks it.
        var css = Assets.StripComments(Assets.Css);

        var forced = Assets.MediaBlocks(css)
            .Where(b => b.Condition.Contains("forced-colors", StringComparison.OrdinalIgnoreCase))
            .Select(b => b.Body)
            .ToList();

        Assert.True(
            forced.Any(body => Regex.IsMatch(body, @"\.btn-bare:focus-visible\s*[,{]")),
            "`.btn-bare:focus-visible` declares its ring as a box-shadow, which forced colours does "
            + "not paint. Restate it as an outline in 71-forced-colors.css, beside the other rings.");
    }
}
