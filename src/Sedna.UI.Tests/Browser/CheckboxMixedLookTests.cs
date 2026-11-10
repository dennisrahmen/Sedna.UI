using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A half-ticked checkbox, measured: filled like a ticked one in every theme, contrast setting and
/// forced colours, with a level dash where the tick would be — whether the platform's
/// <c>indeterminate</c> says so or the markup's <c>aria-checked="mixed"</c>, and over a box that is
/// also ticked. <see cref="CheckboxMixedTests"/> finds every drawn checkbox; this holds what they draw.
/// </summary>
public class CheckboxMixedLookTests : ScriptTestBase
{
    private const string Fixture = """
        <div style="padding:20px; display:flex; flex-direction:column; gap:8px">
          <label class="form-check"><input type="checkbox" id="off" /> <span>Off</span></label>
          <label class="form-check"><input type="checkbox" id="on" checked /> <span>On</span></label>
          <label class="form-check"><input type="checkbox" id="prop" /> <span>Indeterminate</span></label>
          <label class="form-check"><input type="checkbox" id="aria" aria-checked="mixed" /> <span>Mixed</span></label>
          <label class="form-check"><input type="checkbox" id="both" checked aria-checked="mixed" /> <span>Both</span></label>
          <ul class="tree" role="tree" aria-label="Locations" aria-multiselectable="true">
            <li role="treeitem" id="t-on" aria-checked="true"><span class="tree-row"><span class="tree-check"></span><span class="tree-label">North hub</span></span></li>
            <li role="treeitem" id="t-mixed" aria-checked="mixed"><span class="tree-row"><span class="tree-check"></span><span class="tree-label">South hub</span></span></li>
          </ul>
        </div>
        """;

    public static TheoryData<string, string, bool> Settings() => new()
    {
        { "dark", "", false },
        { "light", "", false },
        { "dark", "more", false },
        { "light", "more", false },
        { "light", "", true },
    };

    [Theory]
    [MemberData(nameof(Settings))]
    public async Task A_half_ticked_box_is_filled_like_a_ticked_one_with_a_dash_for_its_tick(string variant, string contrast, bool forced)
    {
        if (NoBrowser || (forced && !BrowserEngine.EmulatesForcedColors)) return;
        var (page, errors) = await OpenStyled(Fixture,
            "<style>*, *::before, *::after { transition: none !important; }</style>");
        if (forced) await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        await Js.Run(page, """
            ([variant, contrast]) => {
                document.documentElement.dataset.variant = variant;
                if (contrast) document.documentElement.dataset.contrast = contrast;
                document.getElementById('prop').indeterminate = true;
            }
            """, new[] { variant, contrast });

        var failures = await page.EvaluateAsync<string[]>("""
            () => {
                const out = [];
                const box = el => { const s = getComputedStyle(el); return { bg: s.backgroundColor, edge: s.borderTopColor }; };
                const mark = el => {
                    const s = getComputedStyle(el, '::after');
                    const m = new DOMMatrixReadOnly(s.transform === 'none' ? undefined : s.transform);
                    return { content: s.content, w: parseFloat(s.width), h: parseFloat(s.height), ink: s.backgroundColor,
                             tickInk: s.borderBottomColor, level: Math.abs(m.b) < 1e-6 && Math.abs(m.c) < 1e-6 };
                };
                const on = document.getElementById('on'), off = document.getElementById('off');
                const tick = mark(on);
                if (tick.level) out.push('the ticked box draws no rotated tick to compare with');
                for (const id of ['prop', 'aria', 'both']) {
                    const el = document.getElementById(id);
                    const b = box(el), want = box(on);
                    if (b.bg !== want.bg || b.edge !== want.edge)
                        out.push(`${id}: the box is ${b.bg} edged ${b.edge}, a ticked one ${want.bg} edged ${want.edge}`);
                    if (b.bg === box(off).bg && b.edge === box(off).edge) out.push(`${id}: the box looks unticked`);
                    const m = mark(el);
                    if (m.content === 'none') out.push(`${id}: no mark is drawn`);
                    if (!m.level) out.push(`${id}: the mark is turned, a tick rather than a dash`);
                    if (!(m.w > m.h * 2)) out.push(`${id}: the mark is ${m.w}x${m.h}, not a dash`);
                    if (m.ink !== tick.tickInk) out.push(`${id}: the dash is ${m.ink}, the tick ${tick.tickInk}`);
                }
                const tOn = document.querySelector('#t-on .tree-check'), tMixed = document.querySelector('#t-mixed .tree-check');
                const tb = box(tMixed), tw = box(tOn);
                if (tb.bg !== tw.bg || tb.edge !== tw.edge) out.push(`tree: the half box is ${tb.bg} edged ${tb.edge}, the ticked ${tw.bg} edged ${tw.edge}`);
                const tm = mark(tMixed);
                if (tm.content === 'none' || !(tm.w > tm.h * 2)) out.push(`tree: the half box draws no dash (${tm.w}x${tm.h})`);
                return out;
            }
            """);

        Assert.True(failures.Length == 0,
            $"{Engine}, {variant}{(contrast.Length > 0 ? ", more contrast" : "")}{(forced ? ", forced colours" : "")}:\n  "
            + string.Join("\n  ", failures));
        Assert.Empty(errors);
    }
}
