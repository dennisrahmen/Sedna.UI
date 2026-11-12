using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A size an app reasonably changes is a custom property on the element: the output pane's
/// height, and every skeleton shape's width and height.
/// </summary>
/// <remarks>
/// Without a knob an app writes <c>max-height</c> or <c>width</c> inline, which is a value
/// the library cannot see: the next release that changes how the shape is sized leaves it
/// behind. The skeleton shapes are read from the stylesheet, so a shape added without the
/// knobs fails here.
/// </remarks>
public class SizeKnobTests : ScriptTestBase
{
    [Fact]
    public async Task The_output_pane_takes_its_height_from_its_knob()
    {
        if (NoBrowser) return;
        var lines = string.Concat(Enumerable.Range(1, 60).Select(i => $"<li class=\"output-line\">06:12:{i:00} order ORD-{4100 + i} synced</li>"));
        var (page, errors) = await OpenStyled(
            $"""
            <ul class="output" id="plain" aria-label="Sync log">{lines}</ul>
            <ul class="output" id="tall" aria-label="Sync log" style="--output-max-height: 32rem">{lines}</ul>
            <ul class="output" id="all" aria-label="Sync log" style="--output-max-height: none">{lines}</ul>
            """);

        var heights = await page.EvaluateAsync<double[]>(
            """
            () => ['plain', 'tall', 'all'].map(id => document.getElementById(id).getBoundingClientRect().height)
            """);
        var rem = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.documentElement).fontSize)");

        Assert.Equal(320, heights[0], 0.5);
        Assert.Equal(32 * rem, heights[1], 0.5);
        Assert.True(heights[2] > 32 * rem, $"With --output-max-height: none the pane stopped at {heights[2]}px.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Every_skeleton_shape_is_sized_by_the_same_knobs()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""<div id="host" style="width:600px"></div>""");

        var failures = await page.EvaluateAsync<string[]>(
            """
            () => {
              const sheet = [...document.styleSheets].find(s => s.href && s.href.endsWith('/css/Sedna.UI.css'));
              const shapes = new Set();
              const visit = list => {
                for (const r of list) {
                  if (r instanceof CSSStyleRule) for (const m of r.selectorText.matchAll(/\.(skeleton-[a-z]+)(?![\w-])/g)) shapes.add(m[1]);
                  else if (r.cssRules && !(r instanceof CSSKeyframesRule)) visit(r.cssRules);
                }
              };
              visit(sheet.cssRules);
              if (shapes.size < 6) return [`only ${shapes.size} skeleton shapes were found`];

              const host = document.getElementById('host');
              const failures = [];
              for (const shape of shapes) {
                const el = document.createElement('span');
                el.className = `skeleton ${shape}`;
                host.replaceChildren(el);
                const round = getComputedStyle(el).borderRadius === '50%';
                const ratio = getComputedStyle(el).aspectRatio !== 'auto';
                if (round) {
                  el.style.setProperty('--skeleton-size', '41px');
                  const r = el.getBoundingClientRect();
                  if (Math.abs(r.width - 41) > 0.5 || Math.abs(r.height - 41) > 0.5) failures.push(`.${shape} ignores --skeleton-size: ${r.width}×${r.height}`);
                  continue;
                }
                el.style.setProperty('--skeleton-width', '137px');
                if (!ratio) el.style.setProperty('--skeleton-height', '23px');
                const r = el.getBoundingClientRect();
                if (Math.abs(r.width - 137) > 0.5) failures.push(`.${shape} ignores --skeleton-width: ${r.width}px wide`);
                if (!ratio && Math.abs(r.height - 23) > 0.5) failures.push(`.${shape} ignores --skeleton-height: ${r.height}px tall`);
              }
              return failures;
            }
            """);

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
        Assert.Empty(errors);
    }
}
