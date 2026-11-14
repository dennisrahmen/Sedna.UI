using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>&lt;a class="card"&gt;</c>: a card that is entirely one link. It keeps the box and the text
/// colours of a card, underlines nothing, answers hover on its frame and the keyboard with the
/// library's ring, and holds still for a reader who asked for less motion.
/// </summary>
public class CardLinkTests : ScriptTestBase
{
    private const string Fixture =
        """
        <div style="width:420px;display:grid;gap:16px;padding:16px">
          <a class="card" id="link" href="#">
            <div class="card-head"><h3 id="title">Web development</h3></div>
            <div class="card-body"><p id="body">Sites and shops, built and kept running.</p><p class="muted" id="muted">From 3 weeks</p></div>
          </a>
          <div class="card" id="plain">
            <div class="card-head"><h3 id="ptitle">Web development</h3></div>
            <div class="card-body"><p id="pbody">Sites and shops, built and kept running.</p><p class="muted" id="pmuted">From 3 weeks</p></div>
          </div>
          <a class="card card--fill" id="fill" href="#" style="height:200px"><div class="card-body">Fill</div></a>
        </div>
        """;

    private const string NoEasing = "<style>*, *::before, *::after { transition: none !important; }</style>";

    private static Task Variant(IPage page, string variant) =>
        page.EvaluateAsync($"() => document.documentElement.dataset.variant = '{variant}'");

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task It_is_a_card_in_every_respect_but_the_link(string variant)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture, NoEasing);
        await Variant(page, variant);

        var report = await page.EvaluateAsync<string[]>(@"() => {
            const out = [];
            const s = id => getComputedStyle(document.getElementById(id));
            const a = s('link'), b = s('plain');
            for (const p of ['display', 'width', 'height', 'backgroundColor', 'borderTopColor', 'borderTopLeftRadius', 'color', 'textDecorationLine'])
                if (a[p] !== b[p]) out.push(p + ': a div card has ' + b[p] + ', the link has ' + a[p]);
            // The colours inside are the card's, not the link colour.
            for (const [x, y] of [['title', 'ptitle'], ['body', 'pbody'], ['muted', 'pmuted']])
                if (s(x).color !== s(y).color) out.push(x + ' colour ' + s(x).color + ' is not the card text colour ' + s(y).color);
            if (s('fill').display !== 'flex') out.push('card--fill lost its column: ' + s('fill').display);
            return out;
        }");

        Assert.True(report.Length == 0, string.Join(Environment.NewLine, report));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task Hover_steps_the_frame_and_underlines_nothing(string variant)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture, NoEasing);
        await Variant(page, variant);

        var before = await page.EvaluateAsync<string[]>("() => { const s = getComputedStyle(document.getElementById('link')); return [s.backgroundColor, s.borderTopColor]; }");
        await page.Locator("#link").HoverAsync();
        var after = await page.EvaluateAsync<string[]>(@"() => {
            const s = getComputedStyle(document.getElementById('link'));
            const t = getComputedStyle(document.getElementById('title'));
            const b = getComputedStyle(document.getElementById('body'));
            return [s.backgroundColor, s.borderTopColor, t.textDecorationLine, b.textDecorationLine, s.textDecorationLine];
        }");

        Assert.NotEqual(before[0], after[0]);
        Assert.NotEqual(before[1], after[1]);
        Assert.All(after.Skip(2), d => Assert.Equal("none", d));
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task The_keyboard_gets_the_ring_on_the_frame(string variant)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture, NoEasing);
        await Variant(page, variant);

        await page.Keyboard.PressAsync(Keys.Tab);
        Assert.Equal("link", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        var ring = await page.EvaluateAsync<string[]>(@"() => {
            const s = getComputedStyle(document.getElementById('link'));
            const probe = document.createElement('i');
            probe.style.color = getComputedStyle(document.documentElement).getPropertyValue('--focus-ring');
            document.body.append(probe);
            const want = getComputedStyle(probe).color;
            probe.remove();
            return [s.boxShadow, s.outlineStyle, want];
        }");

        Assert.Contains(ring[2], ring[0], StringComparison.Ordinal);
        Assert.Equal("none", ring[1]);
    }

    [Fact]
    public async Task Forced_colours_paint_an_outline_where_the_ring_cannot_show()
    {
        if (NoBrowser || !BrowserEngine.EmulatesForcedColors) return;
        var page = await Open(Fixture, head: StylesheetTag, forcedColors: ForcedColors.Active);

        await page.Keyboard.PressAsync(Keys.Tab);
        var outline = await page.EvaluateAsync<string[]>(@"() => {
            const s = getComputedStyle(document.getElementById('link'));
            return [s.outlineStyle, s.outlineWidth];
        }");

        Assert.NotEqual("none", outline[0]);
        Assert.NotEqual("0px", outline[1]);
    }

    [Fact]
    public async Task Reduced_motion_holds_the_frame_still()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: StylesheetTag, reducedMotion: ReducedMotion.Reduce);
        var duration = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('link')).transitionDuration");
        Assert.All(duration.Split(',', StringSplitOptions.TrimEntries), d => Assert.Equal("0s", d));
    }
}
