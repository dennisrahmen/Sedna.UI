using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Every control on every page that hides its native input — a dropzone, a file field, a
/// choice tile, a segmented option — is that input's containing block, and draws the edge
/// that marks it as a control at 3:1, in the dark theme and the light one.
/// </summary>
/// <remarks>
/// <para>
/// Unpositioned, a hidden input is laid out against the next positioned ancestor: in a
/// modal that is the dialog, and focusing the input scrolls the dialog instead of the body
/// it sits in. <c>HiddenInputTests</c> shows that in both engines; this finds the next
/// control that would do it.
/// </para>
/// <para>
/// With its input not drawn, the control's edge is the only thing that says it is one — the
/// label's own edge, or for an option that draws none, the group's around it. WCAG 1.4.11
/// asks 3:1 of it against what it sits on. A checked or disabled control is not measured:
/// its state draws its own edge, and a disabled one is exempt. A label that shows a
/// <c>.btn</c> — a file field — is that button, which its words identify. A rating's star is
/// marked by its own drawn outline, which is measured instead of an edge.
/// </para>
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class HiddenInputScanTests(CatalogueAppFixture app)
{
    public static TheoryData<string> Pages() => RoutedPages.AsTheoryData();

    private const string Scan = """
        () => {
          const hidden = el => {
            const s = getComputedStyle(el);
            if (s.display === 'none') return false;
            const r = el.getBoundingClientRect();
            return s.opacity === '0' || s.clipPath.startsWith('inset(50%') || (r.width <= 1 && r.height <= 1);
          };
          const name = el => {
            const cls = [...el.classList].filter(c => !c.startsWith('cat-') && !c.startsWith('ex-')).join('.');
            return el.tagName.toLowerCase() + (el.id ? '#' + el.id : '') + (cls ? '.' + cls : '');
          };
          const where = el => {
            const ex = el.closest('[id]');
            return name(el) + (ex && ex !== el ? ' in #' + ex.id : '');
          };
          const out = [];
          for (const input of document.querySelectorAll('input, select, textarea')) {
            if (input.closest('.cat-sidebar, .cat-topbar, .ex-code, pre')) continue;
            // Not rendered at all — inside a closed popover or a hidden panel — has no box.
            if (input.getClientRects().length === 0) continue;
            const s = getComputedStyle(input);
            if (s.position !== 'absolute' || !hidden(input)) continue;
            const own = input.closest('label') ?? input.parentElement;
            if (input.offsetParent !== own)
              out.push(where(input) + ': laid out against ' + (input.offsetParent ? name(input.offsetParent) : 'the page') + ', not ' + name(own));
          }
          return out;
        }
        """;

    private const string Edges = """
        () => {
          const rgb = c => { const m = c.match(/[\d.]+/g); return m ? m.slice(0, 4).map(Number) : null; };
          const opaque = c => { const v = rgb(c); return v && (v.length < 4 || v[3] > 0.99); };
          const lum = ([r, g, b]) => {
            const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
            return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
          };
          const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };
          const behind = el => {
            for (let n = el; n; n = n.parentElement) {
              const c = getComputedStyle(n).backgroundColor;
              if (opaque(c)) return rgb(c);
            }
            return rgb(getComputedStyle(document.documentElement).backgroundColor) ?? [255, 255, 255];
          };
          const edged = el => {
            const s = getComputedStyle(el);
            return parseFloat(s.borderTopWidth) > 0 && s.borderTopStyle !== 'none' && opaque(s.borderTopColor);
          };
          const out = [];
          for (const input of document.querySelectorAll('input[type=radio], input[type=checkbox], input[type=file]')) {
            if (input.closest('.cat-sidebar, .cat-topbar, .ex-code, pre')) continue;
            const s = getComputedStyle(input);
            const r = input.getBoundingClientRect();
            const notDrawn = s.opacity === '0' || s.clipPath.startsWith('inset(50%') || (r.width <= 1 && r.height <= 1);
            if (!notDrawn || s.display === 'none' || input.disabled || input.checked) continue;
            const label = input.closest('label');
            if (!label || label.getClientRects().length === 0) continue;
            // A label that shows a button is that button, held to the buttons' own rules: its
            // words say what it is.
            if (label.querySelector('.btn')) continue;
            // A rating's star draws no edge: its outline IS the control, drawn as a clipped
            // background on the star's ::before, and it is held to 3:1 the same way.
            const star = label.closest('.rating-star');
            if (star) {
              const outline = getComputedStyle(star, '::before').backgroundColor;
              const ink = opaque(outline) ? ratio(rgb(outline), behind(star.parentElement)) : 0;
              if (ink < 3) out.push('rating-star around ' + (label.className || 'label') + ': its outline is ' + ink.toFixed(2) + ':1');
              continue;
            }
            const control = edged(label) ? label : edged(label.parentElement) ? label.parentElement : null;
            const id = label.className || label.tagName.toLowerCase();
            if (!control) { out.push(id + ': draws no edge, and nor does its group'); continue; }
            const edge = rgb(getComputedStyle(control).borderTopColor);
            const outside = behind(control.parentElement);
            const insideColour = getComputedStyle(control).backgroundColor;
            const inside = opaque(insideColour) ? rgb(insideColour) : outside;
            const worst = Math.min(ratio(edge, outside), ratio(edge, inside));
            if (worst < 3) out.push((control === label ? id : control.className + ' around ' + id) + ': its edge is ' + worst.toFixed(2) + ':1');
          }
          return [...new Set(out)];
        }
        """;

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Every_control_that_hides_its_input_holds_it_and_marks_itself_at_3_to_1(string route)
    {
        if (app.NoBrowser) return;
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var problems = new List<string>();
        try
        {
            var page = await context.NewPageAsync();
            await page.GotoAsync(app.Url(route), new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Js.Settle(page);
            problems.AddRange(await page.EvaluateAsync<string[]>(Scan));
            foreach (var variant in new[] { "dark", "light" })
            {
                await Js.Run(page, "v => v === 'light' ? document.documentElement.setAttribute('data-variant', 'light') : document.documentElement.removeAttribute('data-variant')", variant);
                await Js.Settle(page);
                problems.AddRange((await page.EvaluateAsync<string[]>(Edges)).Select(p => $"{variant}: {p}"));
            }
        }
        finally { await context.CloseAsync(); }

        Assert.True(problems.Count == 0, $"{route}:{Environment.NewLine}{string.Join(Environment.NewLine, problems.Distinct())}");
    }
}
