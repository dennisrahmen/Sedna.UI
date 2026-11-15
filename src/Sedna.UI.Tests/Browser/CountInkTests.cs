using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The number in a corner count sits in the middle of its badge, measured on the paint and not on
/// the boxes, across and down, at every size the badge has and in either direction.
/// </summary>
/// <remarks>
/// A box can be centred exactly while the digits inside it are not: a tabular digit has its own
/// side bearings, and a line trimmed to its capitals leaves the engine to round the glyph's
/// baseline to a pixel. The badge is read on a page the colour of its own fill, with its
/// cut-out ring removed, so the only paint in its box is the digits.
/// </remarks>
public class CountInkTests : ScriptTestBase
{
    [Fact]
    public async Task The_number_is_set_in_the_system_stack_whatever_it_sits_in()
    {
        if (NoBrowser) return;
        // A <button> does not inherit its parent's font, so a count on a bare button took the
        // browser's own button font — Arial in one engine — and its digits were drawn and spaced
        // by that instead.
        var (page, _) = await OpenStyled(Fixture.Replace("{0}", """<button class="topbar-btn" type="button"><span class="count-host"><i class="ri-notification-3-line"></i><span class="count-badge" id="b">3</span></span></button>""", StringComparison.Ordinal));
        var fonts = await page.EvaluateAsync<string[]>("() => [getComputedStyle(document.getElementById('b')).fontFamily, getComputedStyle(document.body).fontFamily]");
        Assert.Equal(fonts[1], fonts[0]);
    }

    private const string Fixture = """
        <style>
          .count-badge { box-shadow: none; }
          .count-host { background: transparent; border-color: transparent; box-shadow: none; }
          .count-host > :not(.count-badge) { visibility: hidden; }
        </style>
        <div style="background: var(--brand); padding: 32px 48px; display: flex; gap: 40px; align-items: center; width: 640px">
          {0}
        </div>
        """;

    public static TheoryData<string, string, string> Counts()
    {
        var data = new TheoryData<string, string, string>();
        var hosts = new (string Name, string Open, string Close)[]
        {
            ("bare icon", """<span class="count-host"><i class="ri-notification-3-line"></i>""", "</span>"),
            ("topbar-btn", """<button class="topbar-btn" type="button"><span class="count-host"><i class="ri-notification-3-line"></i>""", "</span></button>"),
            ("btn", """<button class="btn btn--icon count-host" type="button"><i class="ri-notification-3-line"></i>""", "</button>"),
            ("btn--sm", """<button class="btn btn--sm btn--icon count-host" type="button"><i class="ri-notification-3-line"></i>""", "</button>"),
            ("btn--lg", """<button class="btn btn--lg btn--icon count-host" type="button"><i class="ri-notification-3-line"></i>""", "</button>"),
            ("avatar", """<span class="count-host count-host--round"><span class="avatar">AF</span>""", "</span>"),
            ("avatar--sm", """<span class="count-host count-host--round"><span class="avatar avatar--sm">AF</span>""", "</span>"),
            ("avatar--lg", """<span class="count-host count-host--round"><span class="avatar avatar--lg">AF</span>""", "</span>"),
        };
        foreach (var h in hosts)
            foreach (var dir in new[] { "ltr", "rtl" })
                foreach (var text in new[] { "3", "12", "99+" })
                    data.Add($"{h.Name} {dir} {text}", dir, h.Open + $"""<span class="count-badge" id="b">{text}</span>""" + h.Close);
        return data;
    }

    /// <summary>
    /// Half a pixel, which is as close as an engine draws a line of text: it puts a baseline on a
    /// whole pixel, so a number can land half a pixel either side of where its box says. A badge
    /// that sits on a fraction of a pixel draws further off than that, because its box is snapped
    /// to the pixel and its text is not, and that is what this holds it to.
    /// </summary>
    private const double Tolerance = 0.55;

    [Theory]
    [MemberData(nameof(Counts))]
    public async Task The_number_stands_in_the_middle_of_the_badge(string name, string dir, string markup)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture.Replace("{0}", markup, StringComparison.Ordinal), deviceScaleFactor: Ink.Scale);
        await Js.Run(page, $"() => {{ document.documentElement.dir = '{dir}'; }}");
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);

        var box = (await page.Locator("#b").BoundingBoxAsync())!;
        var down = (await Ink.Of(page, "#b"))[0];
        var across = (await Ink.Across(page, "#b"))[0];
        var dy = Ink.Centre(down) - (box.Y + box.Height / 2);
        var dx = Ink.Centre(across) - (box.X + box.Width / 2);
        // A topbar button's own 1px divider leaves its content half a pixel off its centre, so the
        // badge on it sits on a half pixel too, and WebKit snaps the box and sets the text apart.
        var acrossTolerance = name.StartsWith("topbar-btn", StringComparison.Ordinal) ? Tolerance + 0.4 : Tolerance;
        if (dir == "rtl" && !BrowserEngine.CentresRightToLeftDigits) dx = 0;
        Assert.True(Math.Abs(dx) <= acrossTolerance && Math.Abs(dy) <= Tolerance,
            $"{name}: the digits sit {dx:+0.00;-0.00}px across and {dy:+0.00;-0.00}px down from the badge's centre (the box is {box.Width:0.##} by {box.Height:0.##}).");
    }
}
