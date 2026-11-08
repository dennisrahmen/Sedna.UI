using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The assistant panels: a <c>.drawer--dock</c> that pushes the content from 900px wide and is
/// a drawer over the page below it, in both directions and inside the safe area; its toggle's
/// <c>aria-expanded</c> kept in step; an <c>.artifact</c>'s head on one line with its actions;
/// and the AI label in a field, a cell and a line of text.
/// </summary>
public class AssistantPanelLayoutTests : ScriptTestBase
{
    private static string Frame(string dir = "ltr", string head = "") => $$"""
        {{head}}
        <div dir="{{dir}}">
          <div class="layout" id="shell">
            <div class="content" id="content">
              <header class="topbar">
                <button class="topbar-btn" id="toggle" type="button" aria-label="Assistant"
                        aria-controls="dock" aria-expanded="false"><i class="ri-sparkling-2-line"></i></button>
              </header>
              <div class="page"><p>Order queue</p></div>
            </div>
            <dialog class="drawer drawer--dock" data-modeless id="dock" aria-label="Assistant">
              <div class="drawer-header" id="dock-head"><h3>Ops agent</h3>
                <button class="modal-close" id="dock-close" type="button" aria-label="Close"
                        onclick="this.closest('dialog').close()"><i class="ri-close-line"></i></button></div>
              <div class="chat-pane" id="pane">
                <div class="chat" role="log" aria-label="Conversation"><p>One line.</p></div>
                <form class="chat-composer" id="composer"><div class="chat-composer-row">
                  <textarea class="form-input" rows="1" aria-label="Message"></textarea></div></form>
              </div>
            </dialog>
          </div>
        </div>
        """;

    private const string Notched = """
        <style>
          :root { --safe-block-start: 17px; --safe-block-end: 34px; --safe-inline-start: 11px; --safe-inline-end: 13px; }
        </style>
        """;

    private sealed class Geometry
    {
        public double ContentWidth { get; set; }
        public double DockLeft { get; set; }
        public double DockRight { get; set; }
        public double DockWidth { get; set; }
        public string DockPosition { get; set; } = "";
        public double ShellWidth { get; set; }
        public double ViewportWidth { get; set; }
        public double PaddingEnd { get; set; }
    }

    private static Task<Geometry> Measure(IPage page) =>
        page.EvaluateAsync<Geometry>("""
            () => {
              const c = document.getElementById('content').getBoundingClientRect();
              const d = document.getElementById('dock');
              const r = d.getBoundingClientRect();
              const s = getComputedStyle(d);
              return {
                contentWidth: c.width, dockLeft: r.left, dockRight: r.right, dockWidth: r.width,
                dockPosition: s.position, shellWidth: document.getElementById('shell').getBoundingClientRect().width,
                viewportWidth: document.documentElement.clientWidth, paddingEnd: parseFloat(s.paddingBottom)
              };
            }
            """);

    /// <summary>Opens the dock the way an app does, with every transition finished.</summary>
    private static async Task<IPage> OpenDock(IPage page, int width)
    {
        await page.SetViewportSizeAsync(width, 700);
        await Js.Run(page, "() => { sednaUi.modal.show('dock'); }");
        await page.WaitForFunctionAsync("() => document.getElementById('dock').open");
        await Js.Settle(page);
        return page;
    }

    [Fact]
    public async Task From_900px_the_open_dock_is_a_column_that_narrows_the_content()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame());
        await page.SetViewportSizeAsync(1200, 700);
        var closed = await Measure(page);
        Assert.Equal(closed.ShellWidth, closed.ContentWidth, 0.5);

        await OpenDock(page, 1200);
        var open = await Measure(page);
        Assert.Equal("relative", open.DockPosition);
        Assert.Equal(420, open.DockWidth, 0.5);
        Assert.Equal(open.ShellWidth - open.DockWidth, open.ContentWidth, 0.5);
        Assert.Equal(open.ViewportWidth, open.DockRight, 0.5);

        // Closed again, it takes no room.
        await page.ClickAsync("#dock-close");
        await page.WaitForFunctionAsync("() => !document.getElementById('dock').open");
        Assert.Equal(closed.ShellWidth, (await Measure(page)).ContentWidth, 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Below_900px_the_dock_is_a_drawer_over_the_page_and_the_content_keeps_its_width()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame());
        await OpenDock(page, 700);
        var g = await Measure(page);
        Assert.Equal("fixed", g.DockPosition);
        Assert.Equal(g.ShellWidth, g.ContentWidth, 0.5);
        Assert.Equal(g.ViewportWidth, g.DockRight, 0.5);
        Assert.True(g.DockLeft < g.ViewportWidth, "The drawer is on screen.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task On_a_phone_the_dock_is_the_width_of_the_screen()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame());
        await OpenDock(page, 390);
        var g = await Measure(page);
        Assert.Equal("fixed", g.DockPosition);
        Assert.Equal(0, g.DockLeft, 0.5);
        Assert.Equal(g.ViewportWidth, g.DockRight, 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Right_to_left_the_docked_column_is_on_the_left()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame("rtl"));
        await OpenDock(page, 1200);
        var g = await Measure(page);
        Assert.Equal(0, g.DockLeft, 0.5);
        Assert.Equal(g.ShellWidth - g.DockWidth, g.ContentWidth, 0.5);
        var contentLeft = await page.EvaluateAsync<double>("() => document.getElementById('content').getBoundingClientRect().left");
        Assert.Equal(g.DockRight, contentLeft, 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Docked_it_stays_inside_the_safe_area_and_keeps_the_home_indicator_clear()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame(head: Notched));
        await OpenDock(page, 1200);
        var g = await Measure(page);
        Assert.Equal(g.ViewportWidth - 13, g.DockRight, 0.5);
        Assert.Equal(34, g.PaddingEnd, 0.5);
        var top = await page.EvaluateAsync<double>("() => document.getElementById('dock').getBoundingClientRect().top");
        Assert.Equal(17, top, 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_chat_takes_the_height_under_the_header_with_the_composer_at_the_foot()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame());
        await OpenDock(page, 1200);
        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const d = document.getElementById('dock').getBoundingClientRect();
              const h = document.getElementById('dock-head').getBoundingClientRect();
              const p = document.getElementById('pane').getBoundingClientRect();
              const c = document.getElementById('composer').getBoundingClientRect();
              return [h.bottom, p.top, p.bottom, d.bottom, c.bottom];
            }
            """);
        Assert.Equal(m[0], m[1], 0.5);
        Assert.Equal(m[3], m[2], 0.5);
        Assert.Equal(m[2], m[4], 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_toggle_says_whether_the_dock_is_open_however_it_closes()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame());
        await OpenDock(page, 1200);
        await page.WaitForFunctionAsync("() => document.getElementById('toggle').getAttribute('aria-expanded') === 'true'");

        await page.ClickAsync("#dock-close");
        await page.WaitForFunctionAsync("() => document.getElementById('toggle').getAttribute('aria-expanded') === 'false'");

        await Js.Run(page, "() => { sednaUi.modal.show('dock'); }");
        await page.WaitForFunctionAsync("() => document.getElementById('toggle').getAttribute('aria-expanded') === 'true'");
        await page.FocusAsync("#dock textarea");
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForFunctionAsync("() => document.getElementById('toggle').getAttribute('aria-expanded') === 'false'");
        Assert.Empty(errors);
    }

    private const string Artifact = """
        <section class="artifact" id="art" style="width:520px; height:260px">
          <header class="artifact-head" id="art-head">
            <div class="artifact-head-text">
              <h3 class="artifact-title" id="art-title">release_old_holds.sql</h3>
              <p id="art-desc">Releases holds older than 48 hours</p>
            </div>
            <div class="artifact-actions" id="art-actions">
              <span class="chat-versions">
                <button class="btn btn--sm btn--ghost btn--icon" type="button" aria-label="Previous"><i class="ri-arrow-left-s-line"></i></button>
                <span>2 / 3</span>
                <button class="btn btn--sm btn--ghost btn--icon" type="button" aria-label="Next"><i class="ri-arrow-right-s-line"></i></button>
              </span>
              <button class="btn btn--sm btn--ghost btn--icon" id="art-copy" type="button" aria-label="Copy"><i class="ri-file-copy-line"></i></button>
            </div>
          </header>
          <div class="artifact-body artifact-body--flush" id="art-body">
            <div class="code-block code-block--flush"><pre><code>SELECT 1;
        SELECT 2;
        SELECT 3;
        SELECT 4;
        SELECT 5;
        SELECT 6;
        SELECT 7;
        SELECT 8;
        SELECT 9;
        SELECT 10;
        SELECT 11;
        SELECT 12;</code></pre></div>
          </div>
        </section>
        """;

    [Fact]
    public async Task An_artifacts_title_stands_on_the_line_of_its_actions_and_its_body_scrolls()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Artifact);
        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const mid = el => { const r = el.getBoundingClientRect(); return r.top + r.height / 2; };
              const t = document.getElementById('art-title');
              const b = document.getElementById('art-body');
              const a = document.getElementById('art').getBoundingClientRect();
              return [mid(t), mid(document.getElementById('art-copy')), t.getBoundingClientRect().height,
                      b.scrollHeight > b.clientHeight ? 1 : 0, getComputedStyle(b).overflowY === 'auto' ? 1 : 0,
                      b.getBoundingClientRect().bottom, a.bottom - 1];
            }
            """);
        Assert.Equal(m[1], m[0], 0.5);
        Assert.True(m[2] >= 28 - 0.5, $"The title's line is a small control tall, not {m[2]}px.");
        Assert.Equal(1, m[3]);
        Assert.Equal(1, m[4]);
        Assert.Equal(m[6], m[5], 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task In_a_narrow_artifact_the_actions_drop_under_the_title_at_the_end_edge()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Artifact.Replace("width:520px", "width:300px", StringComparison.Ordinal));
        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const t = document.getElementById('art-desc').getBoundingClientRect();
              const a = document.getElementById('art-actions').getBoundingClientRect();
              const h = document.getElementById('art-head');
              const hr = h.getBoundingClientRect();
              return [a.top, t.bottom, a.right, hr.right - parseFloat(getComputedStyle(h).paddingRight)];
            }
            """);
        Assert.True(m[0] >= m[1], "The actions wrap under the title.");
        Assert.Equal(m[3], m[2], 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task In_a_drawer_an_artifact_gives_up_its_frame_and_fills_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<dialog class="drawer" data-modeless id="d">{Artifact}</dialog>""");
        await Js.Run(page, "() => { sednaUi.modal.show('d'); }");
        await page.WaitForFunctionAsync("() => document.getElementById('d').open");
        await Js.Settle(page);
        var m = await page.EvaluateAsync<string[]>("""
            () => {
              const s = getComputedStyle(document.getElementById('art'));
              const a = document.getElementById('art').getBoundingClientRect();
              const d = document.getElementById('d');
              const r = d.getBoundingClientRect();
              return [s.borderTopStyle, String(Math.abs(a.bottom - (r.bottom - parseFloat(getComputedStyle(d).paddingBottom))) < 0.5)];
            }
            """);
        Assert.Equal("none", m[0]);
        Assert.Equal("True", m[1], ignoreCase: true);
        Assert.Empty(errors);
    }

    private const string Labels = """
        <div style="width:420px">
          <div class="input-group" id="group">
            <input class="form-input" type="text" value="Thu 14 Nov, 09:00–11:00" aria-label="Delivery">
            <button class="badge badge-ai badge-ai--icon" id="in-field" type="button" popovertarget="why">
              <i class="ri-sparkling-2-line" aria-hidden="true"></i><span class="visually-hidden">AI-generated: why</span>
            </button>
          </div>
          <table class="table"><tbody><tr>
            <td id="cell"><span id="cell-text">Fragile goods</span>
              <button class="badge badge-ai badge-ai--icon" id="in-cell" type="button" popovertarget="why">
                <i class="ri-sparkling-2-line" aria-hidden="true"></i><span class="visually-hidden">AI-generated: why</span>
              </button></td>
          </tr></tbody></table>
          <p><span class="badge badge-ai" id="word"><i class="ri-sparkling-2-line"></i><span>AI</span></span>
             <span class="badge badge--sm badge-ai" id="word-sm"><i class="ri-sparkling-2-line"></i><span>AI</span></span>
             <span class="badge badge--sm badge-ai badge-ai--icon" id="icon-sm"><i class="ri-sparkling-2-line"></i></span>
             <span class="badge badge--lg badge-ai" id="word-lg"><i class="ri-sparkling-2-line"></i><span>AI</span></span>
             <span class="badge badge--lg badge-ai badge-ai--icon" id="icon-lg"><i class="ri-sparkling-2-line"></i></span></p>
          <div class="popover" id="why" popover><p>Suggested by the dispatch agent.</p></div>
        </div>
        """;

    [Fact]
    public async Task The_icon_label_is_a_square_the_height_of_a_word_badge_at_every_size()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Labels);
        var m = await page.EvaluateAsync<double[]>("""
            () => ['in-field', 'word', 'word-sm', 'icon-sm', 'word-lg', 'icon-lg'].flatMap(id => {
              const r = document.getElementById(id).getBoundingClientRect(); return [r.width, r.height]; })
            """);
        Assert.Equal(m[0], m[1], 0.5);       // square
        Assert.Equal(m[3], m[1], 0.5);       // as tall as a word badge
        Assert.Equal(m[6], m[7], 0.5);
        Assert.Equal(m[5], m[7], 0.5);
        Assert.Equal(m[10], m[11], 0.5);
        Assert.Equal(m[9], m[11], 0.5);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_label_sits_inside_a_field_and_on_a_cells_line_and_opens_its_explanation()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Labels);
        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const r = id => document.getElementById(id).getBoundingClientRect();
              const g = r('group'), f = r('in-field'), c = r('in-cell'), t = r('cell-text');
              return [g.top + g.height / 2, f.top + f.height / 2, g.right - f.right,
                      t.top + t.height / 2, c.top + c.height / 2];
            }
            """);
        Assert.Equal(m[0], m[1], 0.5);
        Assert.True(m[2] > 1, "The label sits inside the field's border, inset from its end.");
        // A badge in a line of text is `vertical-align: middle`, which centres it half an x-height
        // above the baseline; the text's box centres on its font's ascent and descent, which sit
        // apart from that by a fraction of a pixel up to a pixel and a half at body sizes.
        Assert.True(Math.Abs(m[3] - m[4]) <= 1.5, $"The label sits on the cell's line: {m[3]} against {m[4]}.");

        await page.ClickAsync("#in-cell");
        await page.WaitForFunctionAsync("() => document.getElementById('why').matches(':popover-open')");
        var name = await page.EvaluateAsync<string>("() => document.getElementById('in-cell').textContent.trim()");
        Assert.Contains("AI-generated", name, StringComparison.Ordinal);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Right_to_left_the_label_sits_at_the_fields_start_side_end()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div dir="rtl">{Labels}</div>""");
        var m = await page.EvaluateAsync<double[]>("""
            () => {
              const g = document.getElementById('group').getBoundingClientRect();
              const f = document.getElementById('in-field').getBoundingClientRect();
              return [f.left - g.left, g.right - f.right];
            }
            """);
        Assert.True(m[0] < m[1], "In RTL the label is at the field's left, its inline end.");
        Assert.Empty(errors);
    }
}
