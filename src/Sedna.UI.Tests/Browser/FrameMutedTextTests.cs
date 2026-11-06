using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Quiet text follows what it sits on: a frame region with a surface of its own — the topbar, the
/// sidebar, the bottom bar — gives its own muted ink as <c>--muted-here</c>, every other surface
/// gives the page's, and <c>.text-muted</c>, <c>.eyebrow</c> and <c>.nav-section-label</c> read it.
/// </summary>
/// <remarks>
/// <c>.nav-section-label</c> in a topbar an app had coloured coral measured 1.14:1: it kept the
/// sidebar's muted while the topbar's buttons followed <c>--topbar-*</c>.
/// </remarks>
public class FrameMutedTextTests : ScriptTestBase
{
    [Fact]
    public void Every_surface_says_which_muted_ink_its_text_reads()
    {
        var css = Assets.StripComments(Assets.Css);
        var silent = Regex.Matches(css, @"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}")
            .Where(m => m.Groups["body"].Value.Contains("--surface-here:", StringComparison.Ordinal)
                        && !m.Groups["body"].Value.Contains("--muted-here:", StringComparison.Ordinal))
            .Select(m => Assets.Squash(m.Groups["sel"].Value))
            .ToList();

        Assert.True(silent.Count == 0,
            "These declare a surface and not its muted ink, so quiet text on them takes the ink of whatever is "
            + "around them — a coloured topbar's, for a panel opened from it: " + string.Join(" | ", silent));
    }

    private const string Quiet = """
        <span class="text-muted" data-q>Running since 08:14</span>
        <span class="eyebrow" data-q>Overview</span>
        <span class="nav-section-label" data-q>Operations</span>
        """;

    private static readonly string Frame = $"""
        <div class="layout" style="height:400px">
          <aside class="sidebar" data-region="sidebar"><nav class="nav"><div class="nav-section">{Quiet}</div></nav></aside>
          <div class="content">
            <header class="topbar" data-region="topbar">
              <div class="topbar-section">{Quiet}</div>
              <div class="menu-anchor"><div class="menu" id="panel" data-panel>{Quiet}</div></div>
            </header>
            <main class="page"><div class="card"><div class="card-body" data-region="card">{Quiet}</div></div></main>
            <nav class="bottombar" data-region="bottombar" aria-label="Main"><div class="bottombar-items">{Quiet}</div></nav>
          </div>
        </div>
        """;

    private const string Measure = """
        () => {
            const lum = c => {
                const [r, g, b] = c.match(/[\d.]+/g).slice(0, 3).map(v => {
                    v = +v / 255;
                    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
                });
                return 0.2126 * r + 0.7152 * g + 0.0722 * b;
            };
            const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
            const groundOf = el => {
                for (let n = el; n; n = n.parentElement) {
                    const bg = getComputedStyle(n).backgroundColor;
                    if (bg !== 'rgba(0, 0, 0, 0)' && bg !== 'transparent') return bg;
                }
                return getComputedStyle(document.body).backgroundColor;
            };
            const out = [];
            for (const q of document.querySelectorAll('[data-q]')) {
                const holder = q.closest('[data-region], [data-panel]');
                const name = (holder.dataset.region || 'menu in the topbar') + ' .' + q.className;
                const ink = getComputedStyle(q).color, ground = groundOf(q);
                out.push([name, ink, ground, ratio(ink, ground).toFixed(2)]);
            }
            return out;
        }
        """;

    [Theory]
    [InlineData("dark", "", "")]
    [InlineData("light", "", "")]
    [InlineData("dark", "more", "")]
    [InlineData("light", "more", "")]
    [InlineData("dark", "", "1")]
    [InlineData("light", "", "1")]
    public async Task Quiet_text_reads_at_AA_on_every_frame_region(string variant, string contrast, string cvd)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame);
        await page.EvaluateAsync("""
            ([v, c, d]) => {
                const h = document.documentElement;
                h.dataset.variant = v;
                if (c) h.dataset.contrast = c;
                if (d) h.dataset.cvd = d;
                document.getElementById('panel').hidden = false;
            }
            """, new[] { variant, contrast, cvd });

        var rows = await page.EvaluateAsync<string[][]>(Measure);
        Assert.Equal(15, rows.Length);
        var failures = rows.Where(r => double.Parse(r[3], System.Globalization.CultureInfo.InvariantCulture) < 4.5)
            .Select(r => $"{r[0]}: {r[1]} on {r[2]} is {r[3]}:1").ToList();
        Assert.True(failures.Count == 0, $"{Engine}, {variant} {contrast} {cvd}:\n  " + string.Join("\n  ", failures));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_coloured_region_paints_quiet_text_in_its_own_muted_and_a_panel_on_it_in_the_pages()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame, """
            <style>
              :root {
                --topbar-bg: var(--coral-600); --topbar-fg: var(--white); --topbar-muted: var(--coral-100);
                --sidebar-bg: var(--slate-950); --sidebar-fg: var(--white); --sidebar-muted: var(--slate-300);
              }
              .bottombar { --bottombar-surface: var(--orbit-800); --bottombar-fg: var(--white); --bottombar-muted: var(--orbit-100); }
            </style>
            """);
        await page.EvaluateAsync("() => { document.documentElement.dataset.variant = 'light'; document.getElementById('panel').hidden = false; }");

        var inks = await page.EvaluateAsync<Dictionary<string, string>>("""
            () => {
                const probe = document.createElement('i');
                document.body.append(probe);
                const resolve = (t, on) => { (on || document.body).append(probe); probe.style.color = `var(${t})`; return getComputedStyle(probe).color; };
                const bar = document.querySelector('.bottombar');
                const ink = sel => [...document.querySelectorAll(sel + ' [data-q]')].map(q => getComputedStyle(q).color);
                const same = (list, want) => list.every(c => c === want) ? 'ok' : list.join(' / ') + ' want ' + want;
                return {
                    topbar: same(ink('.topbar-section'), resolve('--topbar-muted')),
                    sidebar: same(ink('.sidebar'), resolve('--sidebar-muted')),
                    bottombar: same(ink('.bottombar'), resolve('--bottombar-muted', bar)),
                    menu: same(ink('#panel'), resolve('--muted')),
                };
            }
            """);

        foreach (var (region, verdict) in inks)
            Assert.True(verdict == "ok", $"{Engine}: quiet text in the {region} is {verdict}.");
        Assert.Empty(errors);
    }
}
