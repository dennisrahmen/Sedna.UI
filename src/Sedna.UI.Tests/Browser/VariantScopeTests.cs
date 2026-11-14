using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A subtree that names its own variant — <c>&lt;section data-variant="dark"&gt;</c> on a light
/// page — reads exactly as that variant does on a page of its own, in the colour-blind palette
/// and in high contrast too, and keeps everything that is not a variant: density, type, an app's
/// own override of a token that does not differ by variant.
/// </summary>
/// <remarks>
/// The measurement is a comparison, not a list of colours: the same markup is read inside the
/// subtree and on a page set to that variant, and every colour the controls paint must match.
/// A token the subtree failed to redeclare, or a derived token that went stale from the page's
/// variant, shows as a difference on some control.
/// </remarks>
public class VariantScopeTests : ScriptTestBase
{
    private const string Sample =
        """
        <h2 data-probe>Title</h2>
        <p data-probe>Body text <a href="#" data-probe>a link</a> and <span class="muted" data-probe>a muted run</span>.</p>
        <p>
          <a class="btn" href="#" data-probe>Default</a>
          <a class="btn btn--ghost" href="#" data-probe>Ghost</a>
          <a class="btn btn--solid" href="#" data-probe>Solid</a>
          <a class="btn btn--brand" href="#" data-probe>Brand</a>
          <a class="btn btn--go btn--solid" href="#" data-probe>Go solid</a>
          <a class="btn btn--danger" href="#" data-probe>Danger</a>
          <span class="badge" data-probe>Neutral</span>
          <span class="badge badge--go" data-probe>Go</span>
          <span class="badge badge--cyan" data-probe>Cyan</span>
        </p>
        <div class="card" data-probe><div class="card-head" data-probe><h3>Card</h3></div><div class="card-body" data-probe>Card body</div></div>
        <input class="form-input" value="Field" data-probe>
        <div class="alert alert--info" data-probe>An alert</div>
        <table class="table"><thead><tr><th data-probe>Head</th></tr></thead><tbody><tr><td data-probe>Cell</td></tr></tbody></table>
        """;

    private const string Read =
        """
        (root) => [...root.querySelectorAll('[data-probe]')].map(el => {
            const s = getComputedStyle(el);
            return el.tagName.toLowerCase() + '.' + (el.className || '-') + ' ' + [
                s.color, s.backgroundColor, s.borderTopColor, s.boxShadow, s.textDecorationLine, s.colorScheme
            ].join(' | ');
        })
        """;

    // A control eases between its colours, and the page changes variant after it has loaded;
    // measured at once, a plain page reads halfway there.
    private const string NoEasing = "<style>*, *::before, *::after { transition: none !important; }</style>";

    private async Task<string[]> PlainPage(string variant, string attrs)
    {
        var (page, _) = await OpenStyled($"<div id=\"here\">{Sample}</div>", NoEasing);
        await page.EvaluateAsync($"() => {{ const h = document.documentElement; h.dataset.variant = '{variant}'; for (const a of '{attrs}'.split(' ').filter(Boolean)) {{ const [k, v] = a.split('='); h.setAttribute(k, v); }} }}");
        return await page.EvaluateAsync<string[]>($"() => ({Read})(document.getElementById('here'))");
    }

    private async Task<string[]> Subtree(string pageVariant, string scopeVariant, string attrs)
    {
        var (page, _) = await OpenStyled($"<div id=\"here\" data-variant=\"{scopeVariant}\">{Sample}</div>", NoEasing);
        await page.EvaluateAsync($"() => {{ const h = document.documentElement; h.dataset.variant = '{pageVariant}'; for (const a of '{attrs}'.split(' ').filter(Boolean)) {{ const [k, v] = a.split('='); h.setAttribute(k, v); }} }}");
        return await page.EvaluateAsync<string[]>($"() => ({Read})(document.getElementById('here'))");
    }

    [Theory]
    [InlineData("")]
    [InlineData("data-cvd=1")]
    [InlineData("data-contrast=more")]
    [InlineData("data-cvd=1 data-contrast=more")]
    public async Task A_dark_subtree_on_a_light_page_reads_as_a_dark_page(string attrs)
    {
        if (NoBrowser) return;
        var reference = await PlainPage("dark", attrs);
        var inside = await Subtree("light", "dark", attrs);
        Assert.Equal(reference, inside);
    }

    [Theory]
    [InlineData("")]
    [InlineData("data-cvd=1")]
    [InlineData("data-contrast=more")]
    [InlineData("data-cvd=1 data-contrast=more")]
    public async Task A_light_subtree_on_a_dark_page_reads_as_a_light_page(string attrs)
    {
        if (NoBrowser) return;
        var reference = await PlainPage("light", attrs);
        var inside = await Subtree("dark", "light", attrs);
        Assert.Equal(reference, inside);
    }

    [Fact]
    public async Task A_subtree_in_the_pages_own_variant_changes_nothing()
    {
        if (NoBrowser) return;
        Assert.Equal(await PlainPage("light", ""), await Subtree("light", "light", ""));
        Assert.Equal(await PlainPage("dark", ""), await Subtree("dark", "dark", ""));
    }

    [Fact]
    public async Task It_is_the_variants_colours_and_nothing_the_variant_does_not_touch()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            $"<div id=\"here\" data-variant=\"dark\"><a class=\"btn\" id=\"b\" href=\"#\">Open</a><p id=\"p\">Text</p></div><a class=\"btn\" id=\"plain\" href=\"#\">Open</a>",
            // An app's override of a token that is the same in both variants.
            extraHead: "<style>:root { --radius-control: 3px; --font-sans: Georgia, serif; }</style>");
        await page.EvaluateAsync("() => { document.documentElement.dataset.variant = 'light'; document.documentElement.dataset.density = 'compact'; }");

        var report = await page.EvaluateAsync<string[]>(@"() => {
            const inside = getComputedStyle(document.getElementById('b'));
            const outside = getComputedStyle(document.getElementById('plain'));
            const out = [];
            for (const p of ['height', 'paddingLeft', 'borderTopLeftRadius', 'fontFamily', 'fontSize'])
                if (inside[p] !== outside[p]) out.push(p + ': ' + outside[p] + ' outside, ' + inside[p] + ' inside');
            if (inside.borderTopLeftRadius !== '3px') out.push('the app radius did not reach the subtree: ' + inside.borderTopLeftRadius);
            if (inside.color === outside.color) out.push('colour did not change');
            return out;
        }");

        Assert.True(report.Length == 0,
            "A subtree names a variant, not a density or an app's own token: "
            + string.Join(Environment.NewLine, report));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Its_native_controls_and_text_follow_its_variant()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("<div id=\"here\" data-variant=\"dark\"><p id=\"p\">Text</p></div><p id=\"plain\">Text</p>");
        await page.EvaluateAsync("() => { document.documentElement.dataset.variant = 'light'; }");
        var report = await page.EvaluateAsync<string[]>(@"() => {
            const a = getComputedStyle(document.getElementById('here'));
            const b = getComputedStyle(document.getElementById('plain'));
            return [a.colorScheme, b.colorScheme, a.color, b.color];
        }");
        Assert.Contains("dark", report[0], StringComparison.Ordinal);
        Assert.Contains("light", report[1], StringComparison.Ordinal);
        Assert.NotEqual(report[2], report[3]);
    }

    [Fact]
    public async Task Forced_colours_reach_a_subtree_too()
    {
        if (NoBrowser || !BrowserEngine.EmulatesForcedColors) return;
        var page = await Open(
            "<div id=\"here\" data-variant=\"dark\"><div class=\"card\" id=\"card\">Card</div></div><div class=\"card\" id=\"plain\">Card</div>",
            head: StylesheetTag, forcedColors: ForcedColors.Active);
        var report = await page.EvaluateAsync<string[]>(@"() => {
            const a = getComputedStyle(document.getElementById('card'));
            const b = getComputedStyle(document.getElementById('plain'));
            return [a.backgroundColor, b.backgroundColor, a.borderTopColor, b.borderTopColor, a.color, b.color];
        }");
        Assert.Equal(report[1], report[0]);
        Assert.Equal(report[3], report[2]);
        Assert.Equal(report[5], report[4]);
    }
}
