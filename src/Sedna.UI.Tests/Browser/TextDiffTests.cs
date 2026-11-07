using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>&lt;ins&gt;</c> and <c>&lt;del&gt;</c> mark what changed in running text (06-base-elements.css):
/// the go and danger tints, said again by an underline and a strike-through, on a line whose height
/// they do not move, with both ends on every line of a change that wraps.
/// </summary>
public class TextDiffTests : ScriptTestBase
{
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task A_change_is_tinted_and_marked_and_leaves_the_line_alone(string variant)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div style="width:260px">
              <p id="plain">Orders ship within two working days of payment, from the nearest warehouse.</p>
              <p id="diff">Orders ship within <del>two</del> <ins>three</ins> working days of payment, <ins>from the nearest warehouse that holds every line</ins>.</p>
              <div class="card"><div class="card-body prose"><p>Returns are <del>free</del> <ins>free within 30 days</ins>.</p></div></div>
            </div>
            """);
        await page.EvaluateAsync<string>("v => document.documentElement.dataset.variant = v", variant);

        var result = await page.EvaluateAsync<string[]>("""
            () => {
                const probe = document.createElement('i');
                document.body.append(probe);
                const fg = t => { probe.style.color = `var(${t})`; return getComputedStyle(probe).color; };
                const bg = t => { probe.style.backgroundColor = `var(${t})`; return getComputedStyle(probe).backgroundColor; };
                const out = [];
                for (const el of document.querySelectorAll('ins, del')) {
                    const s = getComputedStyle(el), added = el.tagName === 'INS';
                    const tone = added ? 'go' : 'danger';
                    if (s.color !== fg(`--${tone}-fg`)) out.push(`${el.tagName} ink ${s.color}`);
                    if (s.backgroundColor !== bg(`--${tone}-bg`)) out.push(`${el.tagName} tint ${s.backgroundColor}`);
                    if (!s.textDecorationLine.includes(added ? 'underline' : 'line-through')) out.push(`${el.tagName} line ${s.textDecorationLine}`);
                    const clone = s.boxDecorationBreak || s.webkitBoxDecorationBreak;
                    if (clone !== 'clone') out.push(`${el.tagName} break ${clone}`);
                }
                const h = id => document.getElementById(id).getBoundingClientRect().height;
                const lines = document.querySelector('#diff ins:last-of-type').getClientRects().length;
                out.push(`heights ${h('plain')} ${h('diff')} lines ${lines}`);
                return out;
            }
            """);

        var report = result[^1].Split(' ');
        Assert.True(result.Length == 1, $"{Engine}, {variant}: " + string.Join("; ", result[..^1]));
        // The same words with the change in them wrap the same way and stand as tall: the marks
        // add no block padding to a line.
        Assert.True(int.Parse(report[4], System.Globalization.CultureInfo.InvariantCulture) > 1, "The long change did not wrap, so the fixture tests nothing about wrapping.");
        var plainLines = double.Parse(report[1], System.Globalization.CultureInfo.InvariantCulture);
        var diffLines = double.Parse(report[2], System.Globalization.CultureInfo.InvariantCulture);
        var lineHeight = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.getElementById('diff')).lineHeight)");
        Assert.Equal(0, Math.IEEERemainder(diffLines, lineHeight), 0.5);
        Assert.True(diffLines >= plainLines);
        Assert.Empty(errors);
    }
}
