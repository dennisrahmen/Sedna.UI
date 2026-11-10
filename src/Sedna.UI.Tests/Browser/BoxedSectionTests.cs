using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A boxed section, measured: the legend and a folding box's title on the same frame line at the
/// same inset, the action centred on that line at the far end, a checkable box whose body follows
/// the switch in its legend, the frame of a box in error, a filled box's paint, and the title kept
/// clear of the line under forced colours.
/// </summary>
public class BoxedSectionTests : ScriptTestBase
{
    private const string NoTransitions = "<style>*, *::before, *::after { transition: none !important; }</style>";

    private static string Boxes(string dir = "ltr") => $"""
        <div dir="{dir}" style="padding:24px; width:900px">
          <div class="sedna-grid sedna-grid--fit">
            <fieldset class="form-section form-section--box" id="set">
              <legend id="set-title">Connection</legend>
              <a class="form-section-action" id="set-action" href="#">Reset to defaults</a>
              <p class="form-section-hint" id="hint">Where the reports are read from.</p>
              <div class="form-section-body"><div class="form-field"><label class="form-label" for="h">Host</label><input class="form-input" id="h" value="src-db-14.example.com"></div></div>
            </fieldset>
            <details class="form-section form-section--box" id="fold">
              <summary id="fold-summary">
                <span class="form-section-legend" id="fold-title">Advanced</span>
                <span class="form-section-summary" id="fold-line">Timeout 30 s · Audit log on</span>
                <span class="form-section-action" id="fold-action"><a href="#">Defaults</a></span>
              </summary>
              <div class="form-section-body"><div class="form-field"><label class="form-label" for="t">Timeout</label><input class="form-input" id="t" value="30 s"></div></div>
            </details>
            <fieldset class="form-section form-section--box" id="badged">
              <legend id="badged-title"><i class="ri-lock-line"></i> <span>Access</span> <span class="badge">2</span></legend>
              <div class="form-section-body"><label class="form-check"><input type="checkbox" checked> <span>Members</span></label></div>
            </fieldset>
          </div>
        </div>
        """;

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_legend_and_a_folding_title_sit_on_the_same_frame_line_at_the_same_inset_with_the_action_on_it_at_the_far_end(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Boxes(dir), NoTransitions);

        var failures = await page.EvaluateAsync<string[]>("""
            dir => {
                const out = [];
                const r = id => document.getElementById(id).getBoundingClientRect();
                const mid = b => b.top + b.height / 2;
                const set = r('set'), fold = r('fold'), title = r('set-title'), foldTitle = r('fold-title');
                // A details' frame starts at its box; its line is the frame's 1px.
                const line = fold.top + 0.5;
                if (Math.abs(mid(foldTitle) - line) > 0.5) out.push(`the folding title is centred at ${mid(foldTitle)}, its frame's line at ${line}`);
                if (Math.abs(mid(title) - line) > 0.5) out.push(`the legend is centred at ${mid(title)}, the folding box's line at ${line}`);
                if (Math.abs(title.top - foldTitle.top) > 1) out.push(`the legend's top is ${title.top}, the folding title's ${foldTitle.top}`);
                const inset = (box, t) => dir === 'rtl' ? box.right - t.right : t.left - box.left;
                if (Math.abs(inset(set, title) - inset(fold, foldTitle)) > 1) out.push(`the legend is ${inset(set, title)}px in, the folding title ${inset(fold, foldTitle)}px`);
                // The badge is taller than the line and overhangs it evenly; the frame does not move.
                const badged = mid(r('badged-title')) - r('badged').top, plain = mid(title) - set.top;
                if (Math.abs(badged - plain) > 0.5) out.push(`a legend holding a badge is centred ${badged}px into its box, a plain one ${plain}px`);
                for (const [box, action] of [[set, r('set-action')], [fold, r('fold-action')]]) {
                    if (Math.abs(mid(action) - line) > 0.5) out.push(`an action is centred at ${mid(action)}, the line at ${line}`);
                    const end = dir === 'rtl' ? action.left - box.left : box.right - action.right;
                    if (Math.abs(end - inset(set, title)) > 1) out.push(`an action is ${end}px from the far end, the title ${inset(set, title)}px from the near one`);
                }
                // The hint sits under the title, inside the frame.
                if (r('hint').top < title.bottom) out.push('the hint overlaps the legend');
                return out;
            }
            """, dir);

        Assert.True(failures.Length == 0, $"{Engine}, {dir}:\n  " + string.Join("\n  ", failures));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_folding_box_shows_its_summary_line_only_while_closed_and_its_caret_turns_to_point_down()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Boxes(), NoTransitions);

        var closed = await page.EvaluateAsync<string[]>("""
            () => [getComputedStyle(document.getElementById('fold-line')).display,
                   getComputedStyle(document.getElementById('fold-title'), '::before').rotate]
            """);
        Assert.Equal(["block", "-90deg"], closed);

        await page.Locator("#fold-title").ClickAsync();
        var open = await page.EvaluateAsync<string[]>("""
            () => [String(document.getElementById('fold').open),
                   getComputedStyle(document.getElementById('fold-line')).display,
                   getComputedStyle(document.getElementById('fold-title'), '::before').rotate]
            """);
        Assert.Equal(["true", "none", "0deg"], open);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("switch")]
    [InlineData("form-check")]
    public async Task A_checkable_box_dims_its_body_and_makes_it_inert_while_the_control_in_its_legend_is_off(string control)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <fieldset class="form-section form-section--box" style="width:420px">
              <legend><label class="{control}" id="toggle"><input type="checkbox" id="on" checked> <span>Proxy</span></label></legend>
              <div class="form-section-body choice-dependent choice-dependent--dim" id="body">
                <div class="form-field"><label class="form-label" for="p">Address</label><input class="form-input" id="p" value="198.51.100.20:3128"></div>
              </div>
            </fieldset>
            <fieldset class="form-section form-section--box" style="width:420px">
              <legend><label class="{control}"><input type="checkbox" id="off2"> <span>Every agent</span></label></legend>
              <div class="form-section-body choice-dependent choice-dependent--off choice-dependent--hide" id="list"><p>build-runner-01</p></div>
            </fieldset>
            """, NoTransitions);

        const string State = """
            () => {
                const b = document.getElementById('body');
                return [getComputedStyle(b).opacity, String(b.hasAttribute('inert')), getComputedStyle(document.getElementById('list')).display];
            }
            """;
        Assert.Equal(["1", "false", "flex"], await page.EvaluateAsync<string[]>(State));

        await page.Locator("#on").UncheckAsync();
        await page.Locator("#off2").CheckAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('body').hasAttribute('inert')");
        Assert.Equal(["0.45", "true", "none"], await page.EvaluateAsync<string[]>(State));

        // The control in the legend is not in the inert body: it still turns the box back on.
        await page.Locator("#on").CheckAsync();
        await page.WaitForFunctionAsync("() => !document.getElementById('body').hasAttribute('inert')");
        Assert.Equal("1", (await page.EvaluateAsync<string[]>(State))[0]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_box_in_error_turns_its_frame_a_disabled_box_greys_its_legend_and_a_filled_box_tints_inside()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="card" style="width:460px"><div class="card-body">
              <fieldset class="form-section form-section--box" id="bad"><legend>Schedule</legend>
                <p class="form-error"><i class="ri-error-warning-line"></i><span>Pick at least one day.</span></p>
                <div class="form-section-body"><label class="form-check"><input type="checkbox"> <span>Weekdays</span></label></div>
              </fieldset>
              <fieldset class="form-section form-section--box" id="off" disabled><legend id="off-title">Billing</legend>
                <div class="form-section-body"><input class="form-input" id="off-input" value="x"></div>
              </fieldset>
              <fieldset class="form-section form-section--box form-section--filled" id="filled"><legend>Profile</legend>
                <div class="form-section-body">
                  <details class="form-section form-section--box" id="inner"><summary><span class="form-section-legend" id="inner-title">Advanced</span></summary><div class="form-section-body">x</div></details>
                </div>
              </fieldset>
              <details class="form-section form-section--box" id="carded"><summary><span class="form-section-legend" id="card-title">On a card</span></summary><div class="form-section-body">x</div></details>
            </div></div>
            """);

        var failures = await page.EvaluateAsync<string[]>("""
            () => {
                const out = [];
                const probe = document.createElement('i');
                document.body.append(probe);
                const resolve = t => { probe.style.color = `var(${t})`; return getComputedStyle(probe).color; };
                const s = id => getComputedStyle(document.getElementById(id));
                if (s('bad').borderTopColor !== resolve('--field-border-danger')) out.push(`the frame in error is ${s('bad').borderTopColor}`);
                if (s('off').borderTopColor !== resolve('--border')) out.push(`a disabled frame is ${s('off').borderTopColor}`);
                if (s('off-title').color !== resolve('--muted')) out.push(`a disabled legend is ${s('off-title').color}`);
                if (!document.getElementById('off-input').matches(':disabled')) out.push('a control in a disabled box is not disabled');
                if (s('filled').backgroundColor !== resolve('--surface-soft')) out.push(`a filled box is ${s('filled').backgroundColor}`);
                // A title on the frame paints what is behind the box: the card, and inside a filled
                // box the card with the tint over it.
                const inner = s('inner-title').backgroundImage, card = s('card-title').backgroundImage;
                if (!card.includes(resolve('--card-bg'))) out.push(`a title on a card paints ${card}`);
                if (!inner.includes(resolve('--card-bg')) || !inner.includes(resolve('--surface-soft'))) out.push(`a title inside a filled box paints ${inner}`);
                return out;
            }
            """);

        Assert.True(failures.Length == 0, Engine + ":\n  " + string.Join("\n  ", failures));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Under_forced_colours_a_folding_title_and_an_action_still_hide_the_frame_line_behind_them()
    {
        if (NoBrowser || !BrowserEngine.EmulatesForcedColors) return;
        var page = await Open(Boxes(), head: StylesheetTag, forcedColors: ForcedColors.Active);

        var painted = await page.EvaluateAsync<string[]>("""
            () => ['fold-title', 'set-action'].map(id => {
                const s = getComputedStyle(document.getElementById(id));
                return s.backgroundImage.includes('linear-gradient') ? 'painted' : s.backgroundImage;
            })
            """);
        Assert.Equal(["painted", "painted"], painted);
    }
}
