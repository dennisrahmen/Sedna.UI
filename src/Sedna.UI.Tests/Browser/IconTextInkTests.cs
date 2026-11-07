using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// What a component draws sits on its centre line: an icon beside a label on the label's
/// capitals, and a word on its own in the middle of its box — measured on the paint, in
/// every size the component offers.
/// </summary>
/// <remarks>
/// <para>
/// Centring the boxes is not enough: a glyph sits where its font puts it inside its box, an
/// icon font and a text font differently, so two centred boxes can draw a pixel apart. The
/// rules that close that gap are in <c>06-base-elements.css</c>, "The line a row is centred
/// on", and every page of the catalogue is held to them by <c>AlignmentScanTests</c>; these
/// are the components on their own, where a failure names the rule.
/// </para>
/// <para>
/// The icon is a filled square and the label capitals and digits, so the measurement is of
/// the line each is set on, not of one icon's drawing or one word's descenders.
/// </para>
/// </remarks>
public class IconTextInkTests : ScriptTestBase
{
    /// <summary>
    /// A pixel. Both engines trim a line to its font's cap height rounded to a pixel and draw
    /// the glyph at the exact one, and both set a line of text on a whole pixel where its box
    /// falls between pixels — Chromium always, WebKit on Linux and in some layouts on Apple's
    /// platforms (a 10px number in a 16px box: 0.82px off, centred any way) — so a word lands up
    /// to a pixel from where the rules put it, and no rule can put it closer.
    /// </summary>
    internal static double Tolerance => 1.0 + Measured;

    /// <summary>What the measurement itself is good to: a sixteenth of a pixel, as the page scan allows.</summary>
    private const double Measured = 1.0 / 16;

    private const string Icon = """<i class="ri-checkbox-blank-fill" id="i"></i>""";

    public static TheoryData<string, string> Rows() => new()
    {
        { "btn", $"""<button class="btn">{Icon}<span id="t">REFRESH 42</span></button>""" },
        { "btn--sm", $"""<button class="btn btn--sm">{Icon}<span id="t">REFRESH 42</span></button>""" },
        { "btn--lg", $"""<button class="btn btn--lg">{Icon}<span id="t">REFRESH 42</span></button>""" },
        { "btn trailing", $"""<button class="btn"><span id="t">NEXT 42</span>{Icon}</button>""" },
        { "btn-bare", $"""<button class="btn-bare">{Icon}<span id="t">EXPORT 42</span></button>""" },
        { "badge", $"""<span class="badge">{Icon}<span id="t">PENDING 42</span></span>""" },
        { "badge--sm", $"""<span class="badge badge--sm">{Icon}<span id="t">PENDING 42</span></span>""" },
        { "badge--lg", $"""<span class="badge badge--lg">{Icon}<span id="t">PENDING 42</span></span>""" },
        { "chip", $"""<span class="chip">{Icon}<span id="t">ALEX 42</span></span>""" },
        { "chip--sm", $"""<span class="chip chip--sm">{Icon}<span id="t">ALEX 42</span></span>""" },
        { "chip--lg", $"""<span class="chip chip--lg">{Icon}<span id="t">ALEX 42</span></span>""" },
        { "chip-label", $"""<span class="chip">{Icon}<span class="chip-label" id="t">ALEX 42</span></span>""" },
        { "menu-item", $"""<div class="menu" style="position:static"><button class="menu-item">{Icon}<span id="t">REASSIGN 42</span></button></div>""" },
        { "tab", $"""<div class="tabs" role="tablist"><button class="tab" role="tab">{Icon}<span id="t">ORDERS 42</span></button></div>""" },
        { "nav-link", $"""<nav class="nav"><a class="nav-link" href="#">{Icon}<span id="t">OVERVIEW 42</span></a></nav>""" },
        { "segmented-option", $"""<div class="segmented"><label class="segmented-option"><input type="radio" name="s" checked>{Icon}<span id="t">LIST 42</span></label></div>""" },
        { "cal-mark", $"""<span class="cal-marks"><button class="cal-mark" type="button">{Icon}<span id="t">09:00 +1</span></button></span>""" },
        { "cal-holiday", $"""<span class="cal-holiday">{Icon}<span class="cal-holiday-name" id="t">HARVEST 42</span></span>""" },
        { "alert", $"""<div class="alert alert--info">{Icon}<span id="t">RUN DONE 42</span></div>""" },
        { "page-link", $"""<nav class="pagination"><a class="page-link" href="#">{Icon}<span id="t">PREVIOUS 42</span></a></nav>""" },
        { "form-check", """<label class="form-check"><input type="checkbox" id="i" checked> <span id="t">NOTIFY 42</span></label>""" },
        { "toast", $"""<div class="toast toast--go" style="position:static">{Icon}<span class="toast-body"><strong id="t">DISPATCHED 42</strong>ORD-4182 HAS LEFT.</span></div>""" },
        { "toast, one line", $"""<div class="toast toast--go" style="position:static">{Icon}<span class="toast-body" id="t">DISPATCHED 42</span></div>""" },
        { "callout", $"""<div class="callout">{Icon}<div class="callout-body" id="t">TIMES ARE UTC 42</div></div>""" },
        { "callout, titled", $"""<div class="callout">{Icon}<div class="callout-body"><span class="callout-title" id="t">NOTE 42</span>TIMES ARE UTC.</div></div>""" },
        { "form-error", $"""<p class="form-error">{Icon}<span id="t">NOT A COMPLETE ADDRESS 42</span></p>""" },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task The_icon_stands_on_the_label_s_capitals(string component, string markup)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div style="padding:24px; width:480px">{markup}</div>""", deviceScaleFactor: Ink.Scale);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        // An entrance — a toast's — run to its end: the measurement is of where it comes to rest.
        await Js.Settle(page);

        var ink = await Ink.Of(page, "#i", "#t");
        var text = await FirstLine(page, ink[1]);
        var delta = Ink.Centre(ink[0]) - text;

        Assert.True(Math.Abs(delta) <= Tolerance,
            $"{component}: the icon is drawn {delta:+0.00;-0.00}px from the middle of the label's capitals.");
        Assert.Empty(errors);
    }

    public static TheoryData<string, string> Words() => new()
    {
        { "btn", """<button class="btn" id="w">SAVE 42</button>""" },
        { "btn--sm", """<button class="btn btn--sm" id="w">SAVE 42</button>""" },
        { "btn--lg", """<button class="btn btn--lg" id="w">SAVE 42</button>""" },
        { "badge", """<span class="badge" id="w">OPEN 42</span>""" },
        { "badge--sm", """<span class="badge badge--sm" id="w">OPEN 42</span>""" },
        { "badge--lg", """<span class="badge badge--lg" id="w">OPEN 42</span>""" },
        { "chip", """<span class="chip" id="w">ALEX 42</span>""" },
        { "chip--sm", """<span class="chip chip--sm" id="w">ALEX 42</span>""" },
        { "chip--lg", """<span class="chip chip--lg" id="w">ALEX 42</span>""" },
        { "kbd", """<kbd class="kbd" id="w">K</kbd>""" },
        { "tab", """<div class="tabs" role="tablist"><button class="tab" role="tab" id="w">ORDERS 42</button></div>""" },
        { "menu-item", """<div class="menu" style="position:static"><button class="menu-item" id="w">REASSIGN 42</button></div>""" },
        { "page-link", """<nav class="pagination"><a class="page-link" href="#" id="w">42</a></nav>""" },
        { "cal-day-num", """<span class="cal-day-num" id="w">17</span>""" },
        { "avatar", """<span class="avatar" id="w">AF</span>""" },
        { "nav-count", """<nav class="nav"><a class="nav-link" href="#"><span>INBOX</span><span class="nav-count" id="w">5</span></a></nav>""" },
        { "tab-count", """<div class="tabs" role="tablist"><button class="tab" role="tab"><span>LINES</span> <span class="tab-count" id="w">5</span></button></div>""" },
        { "topbar-version", """<span class="topbar-version" id="w">V4.2.1</span>""" },
        { "citation", """<button class="citation" type="button" id="w">2</button>""" },
        { "source-card-n", """<a class="source-card" href="#"><span class="source-card-host"><span class="source-card-n" id="w">2</span><span>RUNBOOK</span></span></a>""" },
    };

    [Theory]
    [MemberData(nameof(Words))]
    public async Task A_word_on_its_own_is_drawn_in_the_middle_of_its_box(string component, string markup)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div style="padding:24px; display:flex; align-items:flex-start">{markup}</div>""", deviceScaleFactor: Ink.Scale);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        // An entrance — a toast's — run to its end: the measurement is of where it comes to rest.
        await Js.Settle(page);

        // Inside the border, a pixel in: the border's own antialiased edge reaches into it.
        var box = await page.EvaluateAsync<double[]>("""
            () => { const e = document.getElementById('w'), r = e.getBoundingClientRect(), c = getComputedStyle(e);
                    const t = parseFloat(c.borderTopWidth), b = parseFloat(c.borderBottomWidth);
                    return [r.left + parseFloat(c.borderLeftWidth), r.top + t, r.width - parseFloat(c.borderLeftWidth) - parseFloat(c.borderRightWidth), r.height - t - b]; }
            """);
        var png = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = new() { X = (float)box[0], Y = (float)box[1], Width = (float)box[2], Height = (float)box[3] } }));
        var ink = Ink.Paint(png, box[0], box[1], box[0] + 1, box[1] + 1, box[2] - 2, box[3] - 2);
        Assert.NotNull(ink);
        var delta = Ink.Centre(ink) - (box[1] + box[3] / 2);

        Assert.True(Math.Abs(delta) <= Tolerance,
            $".{component}: its word is drawn {delta:+0.00;-0.00}px from the middle of its box.");
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(Words))]
    public async Task A_word_on_its_own_takes_the_height_a_labelled_one_does(string component, string markup)
    {
        if (NoBrowser) return;
        // The same box with its word in a <span>: the flex row the rules started from.
        var labelled = System.Text.RegularExpressions.Regex.Replace(markup, @"id=""w"">([^<]+)<", @"id=""w""><span>$1</span><");
        var (page, errors) = await OpenStyled($"""
            <div style="padding:24px; display:flex; align-items:flex-start; gap:12px">{markup}</div>
            <div style="padding:24px; display:flex; align-items:flex-start; gap:12px">{labelled.Replace("id=\"w\"", "id=\"l\"", StringComparison.Ordinal)}</div>
            """);
        var word = (await page.Locator("#w").BoundingBoxAsync())!;
        var label = (await page.Locator("#l").BoundingBoxAsync())!;

        Assert.True(Math.Abs(word.Height - label.Height) <= 0.5,
            $".{component}: on its own its box is {word.Height:0.00}px tall, with a <span> label {label.Height:0.00}px.");
        Assert.Empty(errors);
    }

    /// <summary>
    /// The middle of the capitals of a label's first line: the label's paint, cut at its
    /// first line when it runs to more than one.
    /// </summary>
    private static async Task<double> FirstLine(Microsoft.Playwright.IPage page, double[] paint)
    {
        var lineHeight = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.getElementById('t')).lineHeight) || 0");
        var capHeight = await page.EvaluateAsync<double>("""
            () => { const p = document.createElement('span'); p.style.cssText = 'position:absolute; height:1cap'; document.getElementById('t').appendChild(p);
                    const h = p.getBoundingClientRect().height; p.remove(); return h; }
            """);
        return paint[1] - paint[0] <= lineHeight ? Ink.Centre(paint) : paint[0] + capHeight / 2;
    }
}
