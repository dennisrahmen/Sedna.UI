using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// An anchored panel is closed by a scroll that moves its trigger, and left alone
/// by one that does not.
/// </summary>
/// <remarks>
/// <para>
/// The defect this pins: Chromium computes an anchored <c>position: fixed</c> panel's
/// offset when the panel becomes visible and does not recompute it while the anchor
/// scrolls. Measured here before the fix — a <c>.menu</c> 4px under its trigger sat
/// 64px under it after a 60px scroll, and the panel's own viewport <c>top</c> never
/// changed at all.
/// </para>
/// <para>
/// It has to be measured in a layout engine, and it has to be measured with the scroll
/// <b>asserted</b>. Five earlier fixtures reported this working, and every one of them
/// had failed to scroll: assigning <c>scrollTop</c> to an element that cannot scroll is
/// a silent no-op, so the panel stayed correct because nothing had moved. Hence
/// <see cref="A_scroll_that_does_not_move_the_trigger_leaves_the_menu_open"/>, which
/// would pass just as well if the whole mechanism were broken — its job is to stop
/// this file from being satisfied by a fixture that never scrolls.
/// </para>
/// </remarks>
public class AnchoredPanelTests : ScriptTestBase
{
    /// <summary>
    /// Two independent scrollers: the one holding the menu, and one beside it that
    /// must not affect it.
    /// </summary>
    private const string Fixture =
        """
        <div id="host" style="height:180px; overflow-y:auto; border:1px solid #888">
          <div style="height:60px"></div>
          <div class="menu-anchor" id="anchor">
            <button class="btn" id="toggle" data-menu-toggle aria-expanded="false">Actions</button>
            <div class="menu menu--start" id="panel" hidden>
              <button class="menu-item" type="button">Reassign</button>
              <button class="menu-item" type="button">Snooze</button>
            </div>
          </div>
          <div style="height:900px"></div>
        </div>

        <div id="elsewhere" style="height:120px; overflow-y:auto; border:1px solid #888">
          <div style="height:900px"></div>
        </div>
        """;

    private const string ClickTheToggle =
        """
        async () => {
          const sleep = ms => new Promise(r => setTimeout(r, ms));
          document.getElementById('toggle').click();
          await sleep(150);
          return document.getElementById('panel').hidden === false;
        }
        """;

    [Fact]
    public async Task A_scroll_that_moves_the_trigger_closes_the_menu()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        Assert.True(await page.EvaluateAsync<bool>(ClickTheToggle), "The menu did not open.");

        var moved = await page.EvaluateAsync<double>(
            """
            async () => {
              const sleep = ms => new Promise(r => setTimeout(r, ms));
              const host = document.getElementById('host');
              host.scrollTop = 60;
              await sleep(250);
              // The assertion the earlier fixtures were missing: prove it scrolled.
              if (host.scrollTop !== 60) throw new Error('the fixture did not scroll');
              return document.getElementById('panel').hidden ? 1 : 0;
            }
            """);

        Assert.Equal(1, moved);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_scroll_that_does_not_move_the_trigger_leaves_the_menu_open()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        Assert.True(await page.EvaluateAsync<bool>(ClickTheToggle), "The menu did not open.");

        var stillOpen = await page.EvaluateAsync<double>(
            """
            async () => {
              const sleep = ms => new Promise(r => setTimeout(r, ms));
              const other = document.getElementById('elsewhere');
              other.scrollTop = 200;
              await sleep(250);
              if (other.scrollTop !== 200) throw new Error('the fixture did not scroll');
              return document.getElementById('panel').hidden ? 0 : 1;
            }
            """);

        Assert.Equal(1, stillOpen);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_panels_own_scrolling_leaves_it_open()
    {
        if (NoBrowser) return;

        // A menu tall enough to scroll itself. Without the guard in 22-anchored.js
        // the first wheel inside a long menu would dismiss it.
        var (page, errors) = await OpenStyled(
            """
            <div class="menu-anchor" id="anchor">
              <button class="btn" id="toggle" data-menu-toggle aria-expanded="false">Actions</button>
              <div class="menu menu--start" id="panel" hidden style="max-height:80px; overflow-y:auto">
                <button class="menu-item" type="button">One</button>
                <button class="menu-item" type="button">Two</button>
                <button class="menu-item" type="button">Three</button>
                <button class="menu-item" type="button">Four</button>
                <button class="menu-item" type="button">Five</button>
                <button class="menu-item" type="button">Six</button>
              </div>
            </div>
            """);

        Assert.True(await page.EvaluateAsync<bool>(ClickTheToggle), "The menu did not open.");

        var stillOpen = await page.EvaluateAsync<double>(
            """
            async () => {
              const sleep = ms => new Promise(r => setTimeout(r, ms));
              const panel = document.getElementById('panel');
              panel.scrollTop = 30;
              await sleep(250);
              if (panel.scrollTop !== 30) throw new Error('the panel did not scroll');
              return panel.hidden ? 0 : 1;
            }
            """);

        Assert.Equal(1, stillOpen);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_popover_closes_on_a_scroll_that_moves_its_invoker()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <div id="host" style="height:180px; overflow-y:auto; border:1px solid #888">
              <div style="height:60px"></div>
              <button class="btn" id="invoker" popovertarget="detail">Why is this overdue?</button>
              <div class="popover" id="detail" popover>
                <strong class="popover-title">Breached 2h ago</strong>
                <p>Priority 2 carries a four-hour response target.</p>
              </div>
              <div style="height:900px"></div>
            </div>
            """);

        var closed = await page.EvaluateAsync<double>(
            """
            async () => {
              const sleep = ms => new Promise(r => setTimeout(r, ms));
              const pop = document.getElementById('detail');
              document.getElementById('invoker').click();
              await sleep(200);
              if (!pop.matches(':popover-open')) throw new Error('the popover did not open');

              const host = document.getElementById('host');
              host.scrollTop = 60;
              await sleep(250);
              if (host.scrollTop !== 60) throw new Error('the fixture did not scroll');
              return pop.matches(':popover-open') ? 0 : 1;
            }
            """);

        Assert.Equal(1, closed);
        Assert.Empty(errors);
    }
}
