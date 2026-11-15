using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// A label set inside a line of text — a badge after a word, the AI label, a freshness note after a
/// figure — is centred on the capitals of that line.
/// </summary>
/// <remarks>
/// Measured on the geometry, not on a screenshot: the line's baseline is where a zero-size inline
/// box lands, and the capitals' centre is half the font's capital height above it. <c>middle</c>
/// puts a label's centre half an x-height above the baseline, which is a pixel under the capitals'.
/// </remarks>
public class InlineLabelAlignmentTests : ScriptTestBase
{
    /// <summary>A sixteenth of a pixel is what the layout is good to; a quarter allows an engine's rounding.</summary>
    private const double Tolerance = 0.3;

    private const string Lines = """
        <div style="width:640px;padding:24px;font-size:var(--text-4)">
          <p id="p-word">Fragile goods <span class="badge" id="word">General</span></p>
          <p id="p-icon">Fragile goods <span class="badge" id="icon"><i class="ri-flag-line"></i><span>Priority</span></span></p>
          <p id="p-sm">Fragile goods <span class="badge badge--sm" id="sm"><i class="ri-flag-line"></i><span>Priority</span></span></p>
          <p id="p-lg">Fragile goods <span class="badge badge--lg" id="lg"><i class="ri-flag-line"></i><span>Priority</span></span></p>
          <p id="p-swatch">Fragile goods <span class="badge badge--swatch" id="swatch">design</span></p>
          <p id="p-scoped">Fragile goods <span class="badge badge--scoped badge--danger" id="scoped"><span class="badge-key">env</span>prod</span></p>
          <p id="p-ai">Forecast: 1,240 orders <button class="badge badge-ai" id="ai" type="button"><i class="ri-sparkling-2-line"></i><span>AI</span></button></p>
          <p id="p-ai-icon">Fragile goods <button class="badge badge-ai badge-ai--icon" id="ai-icon" type="button"><i class="ri-sparkling-2-line"></i><span class="visually-hidden">why</span></button></p>
          <p id="p-ai-sm">Fragile goods <button class="badge badge--sm badge-ai badge-ai--icon" id="ai-sm" type="button"><i class="ri-sparkling-2-line"></i></button></p>
          <p id="p-ai-lg">Fragile goods <button class="badge badge--lg badge-ai" id="ai-lg" type="button"><i class="ri-sparkling-2-line"></i><span>AI</span></button></p>
          <h2 id="p-big" style="font-size:28px;margin:0">Orders <span class="badge" id="big">Open</span></h2>
          <table class="table"><tbody><tr><td class="col-num" id="p-stale">312 <span class="stale stale--warn" id="stale"><i class="ri-time-line"></i> <span>6m ago</span></span></td></tr></tbody></table>
        </div>
        """;

    public static TheoryData<string> Labels() => new()
    {
        "word", "icon", "sm", "lg", "swatch", "scoped", "ai", "ai-icon", "ai-sm", "ai-lg", "big", "stale",
    };

    [Theory]
    [MemberData(nameof(Labels))]
    public async Task A_label_in_a_line_is_centred_on_the_line_s_capitals(string id)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Lines);
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");
        await Js.Settle(page);

        var off = await page.EvaluateAsync<double>($$"""
            () => {
                const el = document.getElementById('{{id}}');
                const line = el.parentElement;
                const cs = getComputedStyle(line);
                const ctx = document.createElement('canvas').getContext('2d');
                ctx.font = `${cs.fontStyle} ${cs.fontWeight} ${cs.fontSize} ${cs.fontFamily}`;
                const cap = ctx.measureText('H').actualBoundingBoxAscent;
                const probe = document.createElement('span');
                probe.style.cssText = 'display:inline-block;width:0;height:0';
                line.insertBefore(probe, el);
                const baseline = probe.getBoundingClientRect().bottom;
                probe.remove();
                const r = el.getBoundingClientRect();
                return (r.top + r.height / 2) - (baseline - cap / 2);
            }
            """);

        Assert.True(Math.Abs(off) <= Tolerance, $"#{id} is centred {off:0.00}px from the centre of its line's capitals.");
    }

    [Fact]
    public async Task A_badge_set_inside_a_field_is_a_pill_all_round()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:320px;padding:24px">
              <div class="input-group"><input class="form-input" value="Thu 14 Nov" readonly><button class="badge badge-ai badge-ai--icon" id="ai" type="button"><i class="ri-sparkling-2-line"></i></button></div>
            </div>
            """);
        var radii = await page.EvaluateAsync<double[]>("""
            () => { const cs = getComputedStyle(document.getElementById('ai')); const h = document.getElementById('ai').getBoundingClientRect().height;
                    return [cs.borderStartStartRadius, cs.borderStartEndRadius, cs.borderEndEndRadius, cs.borderEndStartRadius].map(v => Math.min(parseFloat(v), h / 2)).concat([h / 2]); }
            """);
        Assert.All(radii.Take(4), r => Assert.True(Math.Abs(r - radii[4]) < 0.5, $"A corner of the badge is rounded {r}px, not a pill's {radii[4]}px."));
    }
}
