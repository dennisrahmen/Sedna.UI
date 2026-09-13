using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A list row measured in a layout engine: its density-driven padding, the line-count
/// knob, and the card presentation of the same markup.
/// </summary>
public class ListLayoutTests : ScriptTestBase
{
    private const string Long =
        "Refund above the approval limit for a damaged delivery of garden furniture, with "
        + "enough further words that it runs well past three lines at this width";

    private const string Fixture =
        $"""
        <div style="width:360px">
          <ul class="list">
            <li><span class="list-row" id="flush-row">
              <span class="avatar avatar-sm" id="flush-avatar">AF</span>
              <span class="list-main"><span class="list-title" id="short">Short</span><span class="list-sub">Sub</span></span>
            </span></li>
            <li><span class="list-row"><span class="list-main">
              <span class="list-title" id="long-default">{Long}</span>
            </span></span></li>
            <li style="--list-lines: 2"><span class="list-row">
              <span class="avatar avatar-sm" id="two-avatar">AF</span>
              <span class="list-main" id="two-main"><span class="list-title" id="long-two">{Long}</span></span>
            </span></li>
            <li style="--list-lines: none"><span class="list-row"><span class="list-main">
              <span class="list-title" id="long-none">{Long}</span>
            </span></span></li>
            <li><span class="list-row" id="own-row">
              <span class="avatar avatar-sm" id="own-avatar">AF</span>
              <span class="list-main"><span class="list-title" id="long-own" style="--list-lines: 2">{Long}</span></span>
            </span></li>
            <li><span class="list-row">
              <span class="avatar avatar-sm" id="meta-avatar">AF</span>
              <span class="list-main" id="meta-main">
                <span class="list-meta"><span>ORD-4211</span><span>12 min</span></span>
                <span class="list-title">Short</span>
              </span>
            </span></li>
            <li><span class="list-row"><span class="list-main">
              <span class="list-title" id="word">alex.fischer.notifications.for.every.approval.queue@example.com</span>
            </span></span></li>
          </ul>

          <ul class="list list--cards" id="cards">
            <li class="sticky-group-head" id="cards-head">Waiting</li>
            <li id="card-a-li"><button class="list-row" type="button" id="card-a"><span class="list-main"><span class="list-title">A</span></span></button></li>
            <li><button class="list-row" type="button" id="card-b" aria-current="true"><span class="list-main"><span class="list-title">B</span></span></button></li>
            <li><a class="list-row" href="#" id="card-c" aria-selected="true"><span class="list-main"><span class="list-title">C</span></span></a></li>
          </ul>

          <div class="card">
            <ul class="list list--cards" id="nested-cards">
              <li><span class="list-row" id="nested-card">N</span></li>
            </ul>
          </div>

          <div id="probe-raised-1" style="color: var(--surface-raised-1)"></div>
          <div id="probe-raised-2" style="color: var(--surface-raised-2)"></div>
          <div id="probe-brand" style="color: var(--brand)"></div>
          <div id="probe-hover" style="color: var(--bg-hover)"></div>
        </div>
        """;

    private const string Helpers =
        """
        const out = [];
        const el = id => document.getElementById(id);
        const cs = id => getComputedStyle(el(id));
        const box = id => el(id).getBoundingClientRect();
        const colour = id => cs(id).color;
        const want = (what, got, expected) => {
            if (got !== expected) out.push(what + ': expected ' + expected + ', measured ' + got);
        };
        """;

    [Fact]
    public async Task A_row_follows_density_outside_a_card()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var report = await page.EvaluateAsync<string[]>("() => {" + Helpers + """
            const measure = (label, block, inline, gap, head) => {
                want(label + ' row paddingTop', cs('flush-row').paddingTop, block);
                want(label + ' row paddingInlineStart', cs('flush-row').paddingInlineStart, inline);
                want(label + ' card gap', cs('cards').rowGap, gap);
                want(label + ' group head paddingInlineStart', cs('cards-head').paddingInlineStart, head);
            };
            // Comfortable is the literal the row carried before the tokens: --space-5 / --space-6.
            measure('comfortable', '10px', '12px', '8px', '13px');
            document.documentElement.setAttribute('data-density', 'compact');
            measure('compact', '6px', '10px', '4px', '11px');
            document.documentElement.removeAttribute('data-density');
            return out;
        }
        """);

        Assert.True(report.Length == 0,
            "A list row's padding and the gap between cards follow [data-density] wherever the "
            + "list sits:" + Environment.NewLine + string.Join(Environment.NewLine, report));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_line_count_knob_clamps_wraps_and_aligns_the_row()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var report = await page.EvaluateAsync<string[]>("() => {" + Helpers + """
            const line = box('short').height;
            const lines = id => Math.round(box(id).height / line);

            want('default lines', lines('long-default'), 1);
            want('--list-lines: 2 on the <li>', lines('long-two'), 2);
            want('--list-lines: 2 on the title alone', lines('long-own'), 2);
            if (lines('long-none') <= 2) out.push('--list-lines: none should wrap freely, measured ' + lines('long-none') + ' lines');

            // A one-word line breaks into the ellipsis rather than running past the edge.
            want('one-word line count', lines('word'), 1);
            want('one-word line overflow', el('word').scrollWidth <= el('word').clientWidth, true);

            // Default: the leading visual is centred on the row.
            const centre = r => r.top + r.height / 2;
            // Within a pixel: an odd leftover height splits unevenly either side.
            const near = (what, a, b) => { if (Math.abs(a - b) > 1) out.push(what + ': ' + a + ' vs ' + b); };
            near('default avatar centred', centre(box('flush-avatar')), centre(box('flush-row')));
            // The knob on the row or above aligns the slots to the first line.
            want('knob on the <li> aligns the avatar to the top', box('two-avatar').top, box('two-main').top);
            // A meta line above the title is a tall row too.
            want('meta line aligns the avatar to the top', box('meta-avatar').top, box('meta-main').top);
            // Set on one line alone, the knob shapes that line and leaves the row centred.
            near('knob on the title alone keeps the avatar centred', centre(box('own-avatar')), centre(box('own-row')));
            return out;
        }
        """);

        Assert.True(report.Length == 0,
            "--list-lines shapes .list-title and .list-sub:" + Environment.NewLine
            + string.Join(Environment.NewLine, report));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Cards_are_a_presentation_of_the_same_rows()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        const string Surface = """
            want('li + li divider', cs('card-a-li').borderTopWidth, '0px');
            want('card border', cs('card-a').borderTopWidth, '1px');
            want('card radius', cs('card-a').borderTopLeftRadius, getComputedStyle(document.documentElement).getPropertyValue('--radius-surface').trim());
            // A <button> row is reset to no background; the card surface has to win over it.
            want('button card surface', cs('card-a').backgroundColor, colour('probe-raised-1'));
            want('card in a card', cs('nested-card').backgroundColor, colour('probe-raised-2'));

            for (const id of ['card-b', 'card-c']) {
                want(id + ' selected border', cs(id).borderTopColor, colour('probe-brand'));
                want(id + ' selected surface stays opaque', cs(id).backgroundColor, colour('probe-raised-1'));
                if (!cs(id).boxShadow.includes('inset')) out.push(id + ': selected ring missing, box-shadow ' + cs(id).boxShadow);
                if (cs(id).backgroundImage === 'none') out.push(id + ': selected tint missing');
            }
            """;

        var report = await page.EvaluateAsync<string[]>("() => {" + Helpers + Surface + "return out; }");

        // Hover and pressed need a real pointer.
        await page.HoverAsync("#card-a");
        var hovered = await page.EvaluateAsync<string[]>("() => {" + Helpers + """
            want('hovered card', cs('card-a').backgroundColor, colour('probe-hover'));
            return out;
        }
        """);
        await page.HoverAsync("#card-b");
        var selectedHovered = await page.EvaluateAsync<string[]>("() => {" + Helpers + """
            want('hovered selected card keeps its brand border', cs('card-b').borderTopColor, colour('probe-brand'));
            return out;
        }
        """);

        var all = report.Concat(hovered).Concat(selectedHovered).ToList();
        Assert.True(all.Count == 0,
            ".list--cards gives each row a card surface and a selection as strong as a bordered "
            + "card:" + Environment.NewLine + string.Join(Environment.NewLine, all));
        Assert.Empty(errors);
    }
}
