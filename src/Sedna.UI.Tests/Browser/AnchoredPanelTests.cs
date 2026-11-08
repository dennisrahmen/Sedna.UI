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
          const frames = n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); });
          document.getElementById('toggle').click();
          await frames(2);
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
              const frames = n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); });
              const host = document.getElementById('host');
              // Waits for the scroll itself rather than a fixed time: under load the event
              // can come later than any sleep that is short enough to keep the suite fast.
              const scrolled = new Promise(r => host.addEventListener('scroll', () => setTimeout(r, 0), { once: true }));
              host.scrollTop = 60;
              await scrolled;
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
              const frames = n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); });
              const other = document.getElementById('elsewhere');
              // Waits for the scroll itself rather than a fixed time: under load the event
              // can come later than any sleep that is short enough to keep the suite fast.
              const scrolled = new Promise(r => other.addEventListener('scroll', () => setTimeout(r, 0), { once: true }));
              other.scrollTop = 200;
              await scrolled;
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
              const frames = n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); });
              const panel = document.getElementById('panel');
              // Waits for the scroll itself rather than a fixed time: under load the event
              // can come later than any sleep that is short enough to keep the suite fast.
              const scrolled = new Promise(r => panel.addEventListener('scroll', () => setTimeout(r, 0), { once: true }));
              panel.scrollTop = 30;
              await scrolled;
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
              const frames = n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); });
              const pop = document.getElementById('detail');
              document.getElementById('invoker').click();
              await frames(2);
              if (!pop.matches(':popover-open')) throw new Error('the popover did not open');

              const host = document.getElementById('host');
              // Waits for the scroll itself rather than a fixed time: under load the event
              // can come later than any sleep that is short enough to keep the suite fast.
              const scrolled = new Promise(r => host.addEventListener('scroll', () => setTimeout(r, 0), { once: true }));
              host.scrollTop = 60;
              await scrolled;
              if (host.scrollTop !== 60) throw new Error('the fixture did not scroll');
              return pop.matches(':popover-open') ? 0 : 1;
            }
            """);

        Assert.Equal(1, closed);
        Assert.Empty(errors);
    }

    // ── On a phone ──────────────────────────────────────────────────────────

    private const string PhoneFixture =
        """
        <div style="height:120px"></div>
        <div class="menu-anchor" id="anchor">
          <button class="btn" id="toggle" data-menu-toggle aria-expanded="false">Actions</button>
          <div class="menu menu--start" id="panel" hidden>
            <button class="menu-item" type="button">Reassign</button>
            <button class="menu-item" type="button">Snooze</button>
          </div>
        </div>
        <button class="btn" id="under" type="button" style="margin-top:40px"
                onclick="window.underClicks = (window.underClicks || 0) + 1">Under the dim</button>
        <div id="row" data-menu-context="ctx" style="margin-top:40px">ORD-4209</div>
        <div class="menu" id="ctx" popover>
          <button class="menu-item" type="button">Copy the number</button>
        </div>
        <div class="form-combo" data-combo="single" id="combo" style="margin-top:40px; max-width:200px">
          <div class="form-combo-box">
            <input class="form-combo-input" id="combo-input" type="text" role="combobox" placeholder=" "
                   aria-expanded="false" aria-controls="combo-list" aria-autocomplete="list" autocomplete="off" aria-label="Product">
          </div>
          <div class="form-combo-panel" id="combo-panel">
            <div class="form-combo-list" id="combo-list" role="listbox" aria-label="Products">
              <div class="form-combo-option" role="option" id="chai" data-value="chai" aria-selected="false">Chai</div>
              <div class="form-combo-option" role="option" id="chang" data-value="chang" aria-selected="false">Chang</div>
            </div>
          </div>
        </div>
        <div style="height:1200px"></div>
        """;

    /// <summary>The element's box against the viewport: its start, end and bottom gaps, and its share of the width.</summary>
    private static Task<double[]> Edges(Microsoft.Playwright.IPage page, string id) =>
        page.EvaluateAsync<double[]>("""
            id => {
              const r = document.getElementById(id).getBoundingClientRect();
              const w = document.documentElement.clientWidth;
              return [r.left, w - r.right, innerHeight - r.bottom, r.width / w];
            }
            """, id);

    [Fact]
    public async Task On_a_phone_a_dropdown_is_a_sheet_on_the_bottom_edge_and_a_press_above_it_closes_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(PhoneFixture);
        await page.SetViewportSizeAsync(375, 700);

        await page.Locator("#toggle").ClickAsync();
        await Js.Frames(page);
        var edges = await Edges(page, "panel");
        Assert.Equal(0, edges[0], 0);
        Assert.Equal(0, edges[1], 0);
        Assert.Equal(0, edges[2], 0);
        Assert.Equal(1, edges[3], 2);

        // The press on the dimmed page lands on the menu's own scrim, not on the button under it.
        var under = await page.Locator("#under").BoundingBoxAsync();
        Assert.NotNull(under);
        await page.Mouse.ClickAsync(under!.X + under.Width / 2, under.Y + under.Height / 2);
        Assert.Equal("false", await page.Locator("#toggle").GetAttributeAsync("aria-expanded"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.underClicks || 0"));
        // With the menu closed the same press reaches the page again.
        await page.Locator("#under").ClickAsync();
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.underClicks || 0"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task On_a_phone_a_menu_at_the_pointer_and_a_combo_panel_are_sheets_too()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(PhoneFixture);
        await page.SetViewportSizeAsync(375, 700);

        await page.Locator("#row").ClickAsync(new() { Button = Microsoft.Playwright.MouseButton.Right });
        await page.WaitForFunctionAsync("() => document.getElementById('ctx').matches(':popover-open')");
        var edges = await Edges(page, "ctx");
        Assert.Equal(0, edges[0], 0);
        Assert.Equal(0, edges[1], 0);
        Assert.Equal(0, edges[2], 0);
        await page.Keyboard.PressAsync("Escape");

        await page.Locator("#combo-input").ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(page.Locator("#combo-panel")).ToBeVisibleAsync();
        edges = await Edges(page, "combo-panel");
        // The full width of the screen, though the field is 200px.
        Assert.Equal(0, edges[0], 0);
        Assert.Equal(0, edges[1], 0);
        Assert.Equal(0, edges[2], 0);
        Assert.Equal(1, edges[3], 2);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task On_a_wide_screen_the_same_dropdown_stays_anchored_under_its_trigger()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(PhoneFixture);
        await page.SetViewportSizeAsync(1024, 700);

        await page.Locator("#toggle").ClickAsync();
        await Js.Frames(page);
        var gap = await page.EvaluateAsync<double>("""
            () => document.getElementById('panel').getBoundingClientRect().top
                - document.getElementById('toggle').getBoundingClientRect().bottom
            """);
        Assert.InRange(gap, 0, 8);
        Assert.True((await Edges(page, "panel"))[3] < 0.5, "A wide screen's dropdown spans the screen.");
        Assert.Empty(errors);
    }

    // ── A speed dial ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_speed_dial_fans_its_actions_out_above_the_button_at_its_end_edge(int width)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="menu-anchor fab-dial">
              <button class="fab fab--icon" id="fab" type="button" data-menu-toggle aria-expanded="false" aria-label="Create"><i class="ri-add-line"></i></button>
              <div class="menu" id="dial" hidden>
                <button class="menu-item" type="button" id="first"><i class="ri-shopping-bag-3-line"></i> <span>Order</span></button>
                <button class="menu-item" type="button"><i class="ri-truck-line"></i> <span>Shipment</span></button>
              </div>
            </div>
            """);
        await page.SetViewportSizeAsync(width, 700);

        await page.Locator("#fab").ClickAsync();
        await Js.Frames(page);
        var box = await page.EvaluateAsync<double[]>("""
            () => {
              const fab = document.getElementById('fab').getBoundingClientRect();
              const dial = document.getElementById('dial').getBoundingClientRect();
              const item = document.getElementById('first').getBoundingClientRect();
              return [fab.top - dial.bottom, fab.right - dial.right, fab.right - item.right,
                      document.documentElement.clientWidth - fab.right];
            }
            """);
        Assert.InRange(box[0], 0, 24);        // above the button, close to it
        Assert.Equal(0, box[1], 0);           // lined up on its end edge
        Assert.Equal(0, box[2], 0);
        Assert.True(box[3] > 0 && box[3] < 64, "The dial is not in the corner.");

        // A menu's keyboard: Escape gives the focus back to the button.
        await page.Locator("#first").FocusAsync();
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("false", await page.Locator("#fab").GetAttributeAsync("aria-expanded"));
        Assert.Equal("fab", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Empty(errors);
    }
}
