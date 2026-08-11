using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Interactive states really repaint. Transitions are frozen first: getComputedStyle read at t=0 returns the value being transitioned away from, which once looked like a broken selector.
/// </summary>
public class RepaintTests : ScriptTestBase
{
    [Fact]
    public async Task Interactive_states_repaint_when_the_state_changes()
    {
        if (OptedOut) return;

        // A state that only paints correctly on first render is the worst failure
        // shape there is — the control looks operable and is not — so it is worth a
        // real browser.
        //
        // The trap this test exists to avoid, having fallen into it: nearly everything
        // interactive in this library has a `transition`, and getComputedStyle read
        // immediately after a class or checkedness change returns the value at t=0,
        // which is the OLD one. That reads exactly like a broken selector. It nearly
        // caused a redesign of the segmented control away from :has(), and the tell
        // was the control below: a plain `.sidebar.collapsed` toggle appearing not to
        // work, which no browser has got wrong since 1998.
        //
        // So transitions are switched off first, and the sentinel stays in as the
        // thing that catches the mistake next time.
        var (page, errors) = await OpenStyled("<p>fixture</p>");
        Assert.Empty(errors);

        var report = await page.EvaluateAsync<string[]>(@"() => {
            const problems = [];

            // Kill every transition, or each reading below is the value at t=0 — the
            // one being transitioned AWAY from. `* !important` is fine here: it is a
            // test harness, not the library, which uses none.
            const freeze = document.createElement('style');
            freeze.textContent = '*, *::before, *::after { transition: none !important; ' +
                                 'animation: none !important; }';
            document.head.appendChild(freeze);

            const host = document.createElement('div');
            host.innerHTML = `
                <aside class='sidebar' id='sb'></aside>
                <div class='segmented'>
                  <label class='segmented-option' id='s1'><input type='radio' name='v' checked> A</label>
                  <label class='segmented-option' id='s2'><input type='radio' name='v'> B</label>
                </div>
                <label class='form-check'><input type='checkbox' id='cb'><span>t</span></label>`;
            document.body.appendChild(host);
            const q = id => host.querySelector('#' + id);
            const bg = el => getComputedStyle(el).backgroundColor;

            // Control: if a plain class toggle does not take effect, the environment is
            // broken and nothing below can be trusted — so say that, rather than
            // reporting a library fault.
            const sb = q('sb');
            const w0 = getComputedStyle(sb).width;
            sb.classList.add('collapsed');
            if (getComputedStyle(sb).width === w0)
                problems.push('ENVIRONMENT: a .sidebar.collapsed class toggle did not repaint');

            // The chosen segmented option must move with the radio. The LABEL is what
            // is painted — :has(input:checked) on it — because the label is also the
            // whole hit area.
            const s1 = q('s1'), s2 = q('s2');
            const before = [bg(s1), bg(s2)];
            s2.querySelector('input').click();
            const after = [bg(s1), bg(s2)];
            if (after[0] === before[0] || after[1] === before[1])
                problems.push('segmented: chosen option did not move — ' +
                    JSON.stringify({ before, after }));

            // The checkbox fills with the brand colour when checked.
            const cb = q('cb');
            const cbBefore = bg(cb);
            cb.click();
            if (bg(cb) === cbBefore)
                problems.push('checkbox: :checked did not repaint — stayed ' + cbBefore);

            host.remove();
            freeze.remove();
            return problems;
        }");

        Assert.True(report.Length == 0, string.Join(Environment.NewLine, report));
        await page.CloseAsync();
    }
}
