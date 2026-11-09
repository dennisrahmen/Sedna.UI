using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.diff</c> (56-diff.css): the gutters, the + and − markers that say a line's side without its
/// tint, the two halves of a side-by-side row staying level, wrapping against scrolling, code staying
/// left to right on a right-to-left page, and forced colours.
/// </summary>
public class DiffLayoutTests : ScriptTestBase
{
    private const string Long =
        "export-orders --since yesterday --format parquet --out /var/exports/orders.parquet --notify ops@example.com --retry 3";

    private static string Fixture(string dir = "ltr") => $$"""
        <div dir="{{dir}}" style="width:560px; padding:16px">
          <div class="diff" id="unified">
            <div class="diff-head"><span class="diff-file">deploy/orders.yaml</span></div>
            <div class="diff-body" id="unified-body">
              <table class="diff-table">
                <tbody>
                  <tr class="diff-fold" id="fold"><td colspan="3"><button class="btn-bare" type="button">Show 9 unchanged lines</button></td></tr>
                  <tr id="ctx"><td class="diff-num">10</td><td class="diff-num">10</td><td class="diff-code">service: orders</td></tr>
                  <tr class="diff-del" id="del"><td class="diff-num">11</td><td class="diff-num"></td><td class="diff-code">image: orders:<del id="word-del">1.4.2</del></td></tr>
                  <tr class="diff-add" id="add"><td class="diff-num"></td><td class="diff-num">11</td><td class="diff-code">image: orders:<ins id="word-ins">1.5.0</ins></td></tr>
                  <tr class="diff-add" id="long"><td class="diff-num"></td><td class="diff-num">12</td><td class="diff-code">{{Long}}</td></tr>
                </tbody>
              </table>
            </div>
          </div>
          <div class="diff diff--wrap" id="wrapped" style="margin-top:16px">
            <div class="diff-body" id="wrapped-body">
              <table class="diff-table">
                <tbody>
                  <tr class="diff-add" id="wrap-long"><td class="diff-num"></td><td class="diff-num">12</td><td class="diff-code">{{Long}}</td></tr>
                </tbody>
              </table>
            </div>
          </div>
          <div class="diff diff--split" id="split" style="margin-top:16px">
            <div class="diff-body">
              <table class="diff-table">
                <tbody>
                  <tr class="diff-fold"><td colspan="4"><button class="btn-bare" type="button">Show 2 unchanged lines</button></td></tr>
                  <tr id="s-ctx">
                    <td class="diff-num">1</td><td class="diff-code">Hello {first_name},</td>
                    <td class="diff-num">1</td><td class="diff-code">Hello {first_name},</td>
                  </tr>
                  <tr id="s-change">
                    <td class="diff-num diff-del">3</td><td class="diff-code diff-del" id="s-old">arrives within five days.</td>
                    <td class="diff-num diff-add">3</td><td class="diff-code diff-add" id="s-new">arrives on {delivery_date}, between {window_start} and {window_end}, at the address on the order.</td>
                  </tr>
                  <tr id="s-gap">
                    <td class="diff-num diff-gap"></td><td class="diff-code diff-gap" id="s-gap-code"></td>
                    <td class="diff-num diff-add">4</td><td class="diff-code diff-add">Track it at https://example.com/track/{order_id}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
        """;

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task Each_changed_line_has_a_marker_a_tint_and_an_unselectable_gutter(string variant)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());
        await Js.Run(page, "v => { document.documentElement.dataset.variant = v; }", variant);

        var result = await page.EvaluateAsync<string[]>("""
            () => {
                const probe = document.createElement('i');
                document.body.append(probe);
                const fg = t => { probe.style.color = `var(${t})`; return getComputedStyle(probe).color; };
                const bg = t => { probe.style.backgroundColor = `var(${t})`; return getComputedStyle(probe).backgroundColor; };
                const out = [];
                const code = id => document.querySelector(`#${id} > .diff-code`);
                const mark = id => getComputedStyle(code(id), '::before');
                for (const [id, sign, tone] of [['add', '"+"', 'go'], ['del', '"−"', 'danger']]) {
                    const m = mark(id);
                    if (m.content !== sign) out.push(`${id} marker ${m.content}`);
                    if (m.color !== fg(`--${tone}-fg`)) out.push(`${id} marker ink ${m.color}`);
                    for (const td of document.querySelectorAll(`#${id} > td`))
                        if (getComputedStyle(td).backgroundColor !== bg(`--${tone}-bg`)) out.push(`${id} tint ${getComputedStyle(td).backgroundColor}`);
                    const word = document.getElementById(id === 'add' ? 'word-ins' : 'word-del');
                    const w = getComputedStyle(word);
                    if (w.backgroundColor !== bg(`--${tone}-bg-active`)) out.push(`${id} word tint ${w.backgroundColor}`);
                    if (w.textDecorationLine !== 'none') out.push(`${id} word line ${w.textDecorationLine}`);
                    if (w.paddingLeft !== '0px' || w.paddingRight !== '0px') out.push(`${id} word padding ${w.paddingLeft}`);
                }
                if (mark('ctx').content !== 'none') out.push(`context marker ${mark('ctx').content}`);
                for (const n of document.querySelectorAll('#unified .diff-num')) {
                    const s = getComputedStyle(n);
                    const select = s.userSelect || s.webkitUserSelect;
                    if (select !== 'none') out.push(`number selectable ${select}`);
                    if (s.textAlign !== 'end' && s.textAlign !== 'right') out.push(`number align ${s.textAlign}`);
                }
                // Every number column is as wide as the next, and the marker sits in the code's
                // own gutter, before the text.
                const widths = [...document.querySelectorAll('#ctx > .diff-num')].map(n => n.getBoundingClientRect().width);
                if (Math.abs(widths[0] - widths[1]) > 0.5) out.push(`gutters ${widths}`);
                const c = code('add').getBoundingClientRect();
                const range = document.createRange();
                range.selectNodeContents(code('add'));
                const text = range.getClientRects()[0];
                if (!(text.left - c.left >= 16)) out.push(`no room for the marker: ${text.left - c.left}`);
                return out;
            }
            """);

        Assert.True(result.Length == 0, $"{Engine}, {variant}: " + string.Join("; ", result));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_unified_diff_scrolls_and_a_wrapped_one_wraps()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const lh = parseFloat(getComputedStyle(document.querySelector('#unified .diff-table')).lineHeight);
                const h = id => document.querySelector(`#${id} > .diff-code`).getBoundingClientRect().height;
                const body = document.getElementById('unified-body'), wrapped = document.getElementById('wrapped-body');
                return [lh, h('long'), h('ctx'), body.scrollWidth - body.clientWidth,
                        h('wrap-long'), wrapped.scrollWidth - wrapped.clientWidth,
                        document.getElementById('unified').getBoundingClientRect().width];
            }
            """);
        var (lineHeight, longRow, contextRow, overflow, wrappedRow, wrappedOverflow) = (m[0], m[1], m[2], m[3], m[4], m[5]);

        // A line height of whole pixels, and a long line on one line that scrolls the body only.
        Assert.Equal(0, lineHeight % 2, 0.01);
        Assert.Equal(contextRow, longRow, 0.5);
        Assert.True(overflow > 50, $"The long line should overflow the unified body, overflow {overflow}.");
        // The diff keeps to its container's width; only its body scrolls.
        Assert.Equal(528, m[6], 1.0);

        // Wrapped: several lines, nothing to scroll.
        Assert.True(wrappedRow >= 2 * lineHeight - 0.5, $"The wrapped line is {wrappedRow}px, line {lineHeight}px.");
        Assert.Equal(0, wrappedOverflow, 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Side_by_side_halves_are_equal_and_a_change_stays_level()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture());

        var r = await page.EvaluateAsync<double[]>("""
            () => {
                const box = id => document.getElementById(id).getBoundingClientRect();
                const old = box('s-old'), neu = box('s-new');
                const codes = [...document.querySelectorAll('#s-ctx > .diff-code')].map(c => c.clientWidth);
                const nums = [...document.querySelectorAll('#s-ctx > .diff-num')].map(c => c.clientWidth);
                const lh = parseFloat(getComputedStyle(document.querySelector('#split .diff-table')).lineHeight);
                const gap = getComputedStyle(document.getElementById('s-gap-code')).backgroundColor;
                const probe = document.createElement('i');
                document.body.append(probe);
                probe.style.backgroundColor = 'var(--surface-soft)';
                return [old.top, neu.top, old.height, neu.height, codes[0], codes[1], nums[0], nums[1], lh,
                        gap === getComputedStyle(probe).backgroundColor ? 1 : 0];
            }
            """);

        Assert.Equal(r[0], r[1], 0.5);
        Assert.Equal(r[2], r[3], 0.5);
        Assert.True(r[3] >= 2 * r[8] - 0.5, "The new half did not wrap, so the fixture proves nothing about staying level.");
        Assert.Equal(r[4], r[5], 1.0);
        Assert.Equal(r[6], r[7], 0.5);
        Assert.Equal(1, r[9], 0.001);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Code_stays_left_to_right_on_a_right_to_left_page()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture("rtl"));

        var r = await page.EvaluateAsync<string[]>("""
            () => {
                const nums = document.querySelectorAll('#ctx > .diff-num');
                const code = document.querySelector('#ctx > .diff-code');
                const fold = document.querySelector('#fold > td');
                const head = getComputedStyle(document.querySelector('#unified .diff-head'));
                return [getComputedStyle(code).direction,
                        String(nums[1].getBoundingClientRect().right <= code.getBoundingClientRect().left + 0.5),
                        String(nums[0].getBoundingClientRect().right <= nums[1].getBoundingClientRect().left + 0.5),
                        getComputedStyle(fold).direction, head.direction];
            }
            """);

        Assert.Equal(["ltr", "true", "true", "rtl", "rtl"], r);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Forced_colours_keep_the_markers_and_box_a_changed_word()
    {
        // Forced colours exist in Chromium only (see BrowserEngine).
        if (NoBrowser || !BrowserEngine.EmulatesForcedColors) return;
        var (page, errors) = await OpenStyled(Fixture());
        await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });

        var r = await page.EvaluateAsync<string[]>("""
            () => {
                const mark = id => getComputedStyle(document.querySelector(`#${id} > .diff-code`), '::before').content;
                const word = id => { const s = getComputedStyle(document.getElementById(id)); return s.outlineStyle + ' ' + s.outlineWidth; };
                return [mark('add'), mark('del'), word('word-ins'), word('word-del')];
            }
            """);

        Assert.Equal(["\"+\"", "\"−\"", "solid 1px", "solid 1px"], r);
        Assert.Empty(errors);
    }
}
