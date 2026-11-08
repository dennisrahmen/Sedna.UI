using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The developer output blocks: a terminal's prompt and the command beside it, a stack
/// trace's frames and the fold that hides the external ones, a test run's suites and tests
/// lined up by icon and time, and an environment variable masked until it is revealed — in
/// either direction.
/// </summary>
public class OutputLayoutTests : ScriptTestBase
{
    private const string Terminal = """
        <ul class="output output--terminal" id="term" aria-label="Terminal" style="width:360px; max-height:none">
          <li class="output-line output-line--prompt" id="p1">
            <span class="output-prompt" id="sign" aria-hidden="true">$</span>
            <span class="output-cmd" id="cmd">dotnet test src/Orders.Tests --filter "FullyQualifiedName~Sync" --logger "console;verbosity=normal"</span>
          </li>
          <li class="output-line" id="out">  All projects are up-to-date for restore.</li>
          <li class="output-line output-line--prompt" id="p2">
            <span class="output-prompt" aria-hidden="true">$</span><span class="output-cmd">git status</span>
          </li>
        </ul>
        """;

    [Fact]
    public async Task A_terminal_command_stands_on_the_prompts_line_and_wraps_under_itself()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Terminal);
        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const sign = document.getElementById('sign'), cmd = document.getElementById('cmd');
              const range = document.createRange(); range.selectNodeContents(cmd);
              const lines = [...range.getClientRects()];
              const signRange = document.createRange(); signRange.selectNodeContents(sign);
              const s = signRange.getBoundingClientRect(), c = cmd.getBoundingClientRect();
              const style = getComputedStyle(sign);
              return [s.top, lines[0].top, lines.length, Math.min(...lines.map(r => r.left)), c.left, s.right,
                      (style.userSelect || style.webkitUserSelect) === 'none' ? 1 : 0,
                      parseFloat(getComputedStyle(document.getElementById('p2')).marginTop),
                      parseFloat(getComputedStyle(document.getElementById('p1')).marginTop)];
            }
            """);
        Assert.Equal(m[0], m[1], 0.5);                  // the prompt and the command share a line
        Assert.True(m[2] > 1, "The command wraps in a 360px pane.");
        Assert.Equal(m[4], m[3], 0.5);                  // every line starts under the command's first character
        Assert.True(m[4] > m[5], "The command starts after the prompt.");
        Assert.Equal(1, m[6]);                          // the prompt is not selected with the command
        Assert.True(m[7] > 0, "A command after output stands apart from it.");
        Assert.Equal(0, m[8]);                          // the first does not
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_terminal_line_is_not_tinted_on_hover()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Terminal + """
            <ul class="output" style="width:360px"><li class="output-line" id="log">a log line</li></ul>
            """);
        await page.HoverAsync("#out");
        var term = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('out')).backgroundColor");
        await page.HoverAsync("#log");
        var log = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('log')).backgroundColor");
        Assert.Equal("rgba(0, 0, 0, 0)", term);
        Assert.NotEqual("rgba(0, 0, 0, 0)", log);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Right_to_left_the_prompt_is_on_the_right_of_the_command()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div dir="rtl">{Terminal}</div>""");
        var m = await page.EvaluateAsync<double[]>("""
            () => [document.getElementById('sign').getBoundingClientRect().left,
                   document.getElementById('cmd').getBoundingClientRect().right]
            """);
        Assert.True(m[0] > m[1], "In RTL the prompt is at the inline start, on the right.");
        Assert.Empty(errors);
    }

    private static string Trace(int width) => $$"""
        <div class="output-trace" style="width:{{width}}px; font-family: var(--font-mono)">
          <p class="output-exception"><strong>System.InvalidOperationException</strong>: No price list is active.</p>
          <ol class="output-frames">
            <li class="output-frame" id="own"><span id="fn">Orders.Pricing.PriceListResolver.Resolve(String currency)</span> <a class="output-frame-loc" id="loc" href="#">PriceListResolver.cs:58</a></li>
          </ol>
          <details class="output-frames-fold" id="fold">
            <summary id="fold-summary">2 framework frames</summary>
            <ol class="output-frames">
              <li class="output-frame output-frame--external" id="ext"><span>System.Threading.Tasks.Task.InnerInvoke()</span> <span class="output-frame-loc">Task.cs:2357</span></li>
            </ol>
          </details>
        </div>
        """;

    [Fact]
    public async Task External_frames_fold_away_and_open_from_their_summary()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Trace(720));
        await Assertions.Expect(page.Locator("#ext")).ToBeHiddenAsync();
        var closed = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('fold-summary'), '::before').transform");

        await page.ClickAsync("#fold-summary");
        await Assertions.Expect(page.Locator("#ext")).ToBeVisibleAsync();
        var open = await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('fold-summary'), '::before').transform");
        Assert.NotEqual(closed, open);

        var colours = await page.EvaluateAsync<string[]>("""
            () => [getComputedStyle(document.getElementById('own')).color, getComputedStyle(document.getElementById('ext')).color]
            """);
        Assert.NotEqual(colours[0], colours[1]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_frames_location_stands_at_its_end_and_drops_under_the_call_when_narrow()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Trace(720) + Trace(240).Replace("id=\"", "id=\"n-", StringComparison.Ordinal));
        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const r = id => document.getElementById(id).getBoundingClientRect();
              return [r('loc').right, r('own').right, r('loc').top, r('fn').top,
                      r('n-loc').top, r('n-fn').bottom, r('n-loc').right, r('n-own').right];
            }
            """);
        Assert.Equal(m[1], m[0], 0.5);
        Assert.True(Math.Abs(m[2] - m[3]) < 2, "Wide, the location shares the call's line.");
        Assert.True(m[4] >= m[5] - 0.5, "Narrow, the location drops under the call.");
        Assert.Equal(m[7], m[6], 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Right_to_left_a_frames_location_stands_at_its_left()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div dir="rtl">{Trace(720)}</div>""");
        var m = await page.EvaluateAsync<double[]>("""
            () => [document.getElementById('loc').getBoundingClientRect().left, document.getElementById('own').getBoundingClientRect().left]
            """);
        Assert.Equal(m[1], m[0], 0.5);
        Assert.Empty(errors);
    }

    private const string Run = """
        <div class="card" style="width:560px">
          <div class="card-body">
            <div class="stack-bar" id="bar" role="img" aria-label="9 tests: 5 passed, 2 failed, 2 skipped">
              <span class="stack-bar-segment series-go" id="seg-go" style="--value: 55.6%"></span>
              <span class="stack-bar-segment series-danger" id="seg-fail" style="--value: 22.2%"></span>
              <span class="stack-bar-segment series-muted" id="seg-skip" style="--value: 22.2%"></span>
            </div>
          </div>
          <details class="test-suite test-suite--fail" id="suite" open>
            <summary id="suite-row">
              <i class="ri-close-circle-fill" id="suite-icon" aria-hidden="true"></i>
              <span class="test-suite-name">Orders.Tests.SyncTests</span>
              <span class="test-suite-count">1 passed, 1 failed</span>
              <span class="test-time" id="suite-time">1.84 s</span>
            </summary>
            <ul class="test-cases">
              <li class="test-case test-case--pass" id="pass">
                <i class="ri-checkbox-circle-fill" id="pass-icon" aria-hidden="true"></i>
                <span class="test-case-name">Reconcile_releases_a_hold</span>
                <span class="visually-hidden">passed</span>
                <span class="test-time" id="pass-time">41 ms</span>
              </li>
              <li class="test-case test-case--fail" id="fail">
                <i class="ri-close-circle-fill" id="fail-icon" aria-hidden="true"></i>
                <span class="test-case-name" id="fail-name">Reconcile_throws_on_an_empty_batch</span>
                <span class="visually-hidden">failed</span>
                <span class="test-time" id="fail-time">12 ms</span>
                <pre class="output-trace" id="fail-trace">Assert.Throws() Failure: No exception was thrown</pre>
              </li>
            </ul>
          </details>
          <details class="test-suite test-suite--skip" id="skipped">
            <summary><i class="ri-indeterminate-circle-line" aria-hidden="true"></i>
              <span class="test-suite-name">Orders.Tests.CarrierTests</span><span class="test-suite-count">1 skipped</span><span class="test-time">0 s</span></summary>
            <ul class="test-cases"><li class="test-case test-case--skip" id="skip"><i class="ri-indeterminate-circle-line" aria-hidden="true"></i>
              <span class="test-case-name">Label_prints</span><span class="visually-hidden">skipped</span><span class="test-time">0 ms</span></li></ul>
          </details>
        </div>
        """;

    [Fact]
    public async Task A_test_run_lines_its_tests_up_under_the_suite_with_the_times_down_one_edge()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Run);
        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const r = id => document.getElementById(id).getBoundingClientRect();
              const mid = id => { const b = r(id); return b.top + b.height / 2; };
              return [r('suite-icon').left, r('pass-icon').left, r('fail-icon').left,
                      r('suite-time').right, r('pass-time').right, r('fail-time').right,
                      r('fail-trace').top, r('fail-name').bottom, r('fail-trace').left, r('fail-icon').left,
                      mid('pass-icon'), mid('pass-time')];
            }
            """);
        Assert.Equal(m[0], m[1], 0.5);
        Assert.Equal(m[0], m[2], 0.5);
        Assert.Equal(m[3], m[4], 0.5);
        Assert.Equal(m[3], m[5], 0.5);
        Assert.True(m[6] >= m[7], "A failed test's trace is under its name.");
        Assert.Equal(m[9], m[8], 0.5);
        Assert.True(Math.Abs(m[10] - m[11]) <= 1, "A test's icon and its time share a centre line.");

        await Assertions.Expect(page.Locator("#skip")).ToBeHiddenAsync();
        await page.ClickAsync("#skipped > summary");
        await Assertions.Expect(page.Locator("#skip")).ToBeVisibleAsync();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_test_runs_tones_follow_the_outcome_and_the_bar_its_shares()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Run + """
            <span id="go" style="color: var(--go-fg)">x</span><span id="danger" style="color: var(--danger-fg)">x</span>
            """);
        var c = await page.EvaluateAsync<string[]>("""
            () => ['pass-icon', 'go', 'fail-icon', 'danger', 'suite-icon'].map(id => getComputedStyle(document.getElementById(id)).color)
            """);
        Assert.Equal(c[1], c[0]);
        Assert.Equal(c[3], c[2]);
        Assert.Equal(c[3], c[4]);

        var w = await page.EvaluateAsync<double[]>("""
            () => ['seg-go', 'seg-fail', 'seg-skip'].map(id => document.getElementById(id).getBoundingClientRect().width)
            """);
        Assert.True(w[0] > w[1] * 2, "The passed share is the longest.");
        Assert.Equal(w[1], w[2], 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Right_to_left_a_closed_suites_caret_points_toward_the_inline_end()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div id="ltr">{Run}</div><div dir="rtl">{Run.Replace("id=\"", "id=\"r-", StringComparison.Ordinal)}</div>""");
        var t = await page.EvaluateAsync<string[]>("""
            () => ['skipped', 'r-skipped'].map(id => getComputedStyle(document.querySelector('#' + id + ' > summary'), '::before').transform)
            """);
        Assert.NotEqual(t[0], t[1]);
        Assert.NotEqual("none", t[1]);
        Assert.Empty(errors);
    }

    private const string Env = """
        <dl style="width:520px">
          <div class="kv">
            <dt class="k text-mono">ORDERS_DB</dt>
            <dd class="v"><span class="secret-value" id="secret">Host=203.0.113.14;Password=example-only</span></dd>
            <dd class="kv-action">
              <button class="btn btn--sm btn--ghost btn--icon" id="reveal" type="button" data-reveal aria-controls="secret" aria-pressed="false" aria-label="Show ORDERS_DB">
                <i class="ri-eye-line" id="eye"></i><i class="ri-eye-off-line" id="eye-off"></i></button>
              <button class="btn btn--sm btn--ghost btn--icon" id="copy" type="button" data-copy="Host=203.0.113.14;Password=example-only" aria-label="Copy ORDERS_DB"><i class="ri-file-copy-line"></i></button>
            </dd>
          </div>
        </dl>
        """;

    private static Task<string> Security(IPage page) =>
        page.EvaluateAsync<string>("() => { const s = getComputedStyle(document.getElementById('secret')); return s.webkitTextSecurity || s.getPropertyValue('-webkit-text-security'); }");

    [Fact]
    public async Task A_secret_value_is_masked_until_its_reveal_is_pressed_and_masked_again_after()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Env);
        Assert.Equal("disc", await Security(page));
        await Assertions.Expect(page.Locator("#eye")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#eye-off")).ToBeHiddenAsync();

        await page.ClickAsync("#reveal");
        await Assertions.Expect(page.Locator("#reveal")).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Equal("none", await Security(page));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('secret').hasAttribute('data-sedna-revealed')"));
        await Assertions.Expect(page.Locator("#eye")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#eye-off")).ToBeVisibleAsync();

        await page.ClickAsync("#reveal");
        await Assertions.Expect(page.Locator("#reveal")).ToHaveAttributeAsync("aria-pressed", "false");
        Assert.Equal("disc", await Security(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_masked_value_still_copies_in_full()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Env);
        await page.ClickAsync("#copy");
        await Assertions.Expect(page.Locator("#copy")).ToHaveAttributeAsync("data-copied", "ok");
        Assert.Equal("Host=203.0.113.14;Password=example-only", await ClipboardText(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_actions_column_stays_at_the_rows_end_in_either_direction()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Env + $"""<div dir="rtl">{Env.Replace("id=\"", "id=\"r-", StringComparison.Ordinal).Replace("aria-controls=\"secret\"", "aria-controls=\"r-secret\"", StringComparison.Ordinal)}</div>""");
        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const r = s => document.querySelector(s).getBoundingClientRect();
              return [r('#copy').right, r('dl .kv').right, r('#r-copy').left, r('[dir=rtl] .kv').left];
            }
            """);
        Assert.Equal(m[1], m[0], 0.5);
        Assert.Equal(m[3], m[2], 0.5);
        Assert.Empty(errors);
    }
}
