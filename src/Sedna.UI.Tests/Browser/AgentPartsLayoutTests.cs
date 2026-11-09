using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// The agent's thread parts: a tool call that opens on what it sent and got back, a badge
/// per state, a question the reader answers, a queue and a todo list with their actions,
/// a thought still running, what the context holds, a checkpoint, and a message edited or
/// failed — in either direction and at a phone's width.
/// </summary>
public class AgentPartsLayoutTests : ScriptTestBase
{
    private static string Tool(string state, string id, bool open = false, string name = "query_locks") => $$"""
        <details class="card agent-tool" id="{{id}}" data-state="{{state}}"{{(open ? " open" : "")}}>
          <summary>
            <span class="agent-tool-icon"><i class="ri-database-2-line"></i></span>
            <span class="agent-tool-name" id="{{id}}-name">{{name}}</span>
            <span class="badge agent-tool-status" id="{{id}}-status"><span>{{state}}</span></span>
            <span class="agent-tool-meta">0.8 s</span>
            <i class="ri-arrow-down-s-line agent-tool-chevron" id="{{id}}-chevron"></i>
          </summary>
          <div class="agent-tool-body" id="{{id}}-body">
            <div class="code-block"><div class="code-block-head"><span>Parameters</span></div><pre><code>{ "host": "src-db-14" }</code></pre></div>
            <div class="code-block agent-tool-error"><div class="code-block-head"><span>Error</span></div><pre><code>permission denied</code></pre></div>
          </div>
        </details>
        """;

    private static readonly string[] ToolStates = ["streaming", "running", "waiting", "approved", "done", "failed", "denied"];

    private static string Style(string selector, string property) =>
        $"() => getComputedStyle(document.querySelector('{selector}')).getPropertyValue('{property}')";

    [Fact]
    public async Task A_tool_call_opens_on_its_input_and_a_failed_one_is_open_from_the_markup()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div style="width:560px">{Tool("done", "ok")}{Tool("failed", "bad", open: true)}</div>
            """);

        await Assertions.Expect(page.Locator("#ok-body")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#bad-body")).ToBeVisibleAsync();
        Assert.Equal("180deg", await page.EvaluateAsync<string>(Style("#bad-chevron", "rotate")));
        Assert.Equal("none", await page.EvaluateAsync<string>(Style("#ok-chevron", "rotate")));

        await page.Locator("#ok summary").ClickAsync();
        await Assertions.Expect(page.Locator("#ok-body")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#ok-chevron")).ToHaveCSSAsync("rotate", "180deg");

        // The error is told apart from the input by its edge, not only by its heading.
        Assert.NotEqual(
            await page.EvaluateAsync<string>(Style("#bad-body > .code-block:first-child", "border-top-color")),
            await page.EvaluateAsync<string>(Style("#bad-body > .agent-tool-error", "border-top-color")));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Every_tool_state_paints_its_badge_its_own_way()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div style="width:560px">{string.Concat(ToolStates.Select(s => Tool(s, s)))}</div>
            """);

        var looks = new Dictionary<string, string>();
        foreach (var state in ToolStates)
            looks[state] = await page.EvaluateAsync<string>($$"""
                () => { const s = getComputedStyle(document.getElementById('{{state}}-status'));
                        return [s.backgroundColor, s.borderTopColor, s.borderTopStyle, s.color].join(' | '); }
                """);

        Assert.Equal(ToolStates.Length, looks.Values.Distinct().Count());
        Assert.Equal("dashed", await page.EvaluateAsync<string>(Style("#streaming-status", "border-top-style")));
        Assert.Contains("line-through", await page.EvaluateAsync<string>(Style("#denied-name", "text-decoration-line")), StringComparison.Ordinal);

        // A call that needs the reader, or failed, also draws the card's edge in its colour.
        var plain = await page.EvaluateAsync<string>(Style("#done", "border-top-color"));
        Assert.NotEqual(plain, await page.EvaluateAsync<string>(Style("#waiting", "border-top-color")));
        Assert.NotEqual(plain, await page.EvaluateAsync<string>(Style("#failed", "border-top-color")));
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_long_tool_name_is_cut_short_and_the_chevron_stays_at_the_end(string dir)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <div dir="{dir}" style="width:300px">{Tool("running", "t", open: true, name: "query_lock_history_for_every_replica_in_the_region")}</div>
            """);

        Assert.True(await page.EvaluateAsync<bool>("() => { const c = document.getElementById('t'); return c.scrollWidth <= c.clientWidth; }"),
            "The card grew a scrollbar for its tool name.");
        Assert.True(await page.EvaluateAsync<bool>("() => { const n = document.getElementById('t-name'); return n.scrollWidth > n.clientWidth; }"),
            "The name should be cut short with an ellipsis.");

        var card = (await page.Locator("#t").BoundingBoxAsync())!;
        var name = (await page.Locator("#t-name").BoundingBoxAsync())!;
        var status = (await page.Locator("#t-status").BoundingBoxAsync())!;
        var chevron = (await page.Locator("#t-chevron").BoundingBoxAsync())!;
        Assert.True(status.Width > 0 && chevron.Width > 0);
        if (dir == "ltr")
        {
            Assert.True(status.X >= name.X + name.Width, "The badge should follow the name.");
            Assert.True(chevron.X + chevron.Width <= card.X + card.Width && chevron.X > status.X, "The chevron is not at the end.");
        }
        else
        {
            Assert.True(status.X + status.Width <= name.X, "The badge should follow the name, leftwards.");
            Assert.True(chevron.X >= card.X && chevron.X < status.X, "The chevron is not at the end.");
        }

        // The code scrolls inside its block rather than widening the card.
        Assert.True(await page.EvaluateAsync<bool>("() => { const b = document.getElementById('t-body'); return b.scrollWidth <= b.clientWidth + 1; }"));
    }

    private const string Question = """
        <form class="card agent-approval agent-approval--question" id="q" style="width:480px">
          <div class="card-head"><div class="agent-approval-head">
            <span class="agent-approval-icon"><i class="ri-question-answer-line"></i></span>
            <span class="agent-approval-title"><strong>Before I draft the change</strong><span>Two answers</span></span>
          </div></div>
          <div class="card-body sedna-col sedna-gap-5">
            <fieldset class="agent-question" id="single">
              <legend id="legend">Where should it go first?</legend>
              <div class="choice-tiles" role="radiogroup" aria-label="Where" id="tiles">
                <label class="choice-tile" id="staging"><input type="radio" name="t" value="staging"><span class="choice-tile-title">Staging</span></label>
                <label class="choice-tile"><input type="radio" name="t" value="production"><span class="choice-tile-title">Production</span></label>
              </div>
            </fieldset>
            <fieldset class="agent-question">
              <legend>Who should hear about it?</legend>
              <label class="form-check"><input type="checkbox" id="ops"> <span>The ops channel</span></label>
            </fieldset>
            <div class="agent-question" id="free">
              <label for="note" id="note-label">Anything else?</label>
              <textarea class="form-input" id="note" rows="2"></textarea>
            </div>
          </div>
          <div class="card-foot">
            <div class="agent-approval-actions" id="actions"><button class="btn btn--solid sedna-push" id="send" type="submit" disabled>Send answers</button></div>
            <div class="agent-approval-result" id="result"><span>Answered</span></div>
          </div>
        </form>
        <div class="card agent-approval" id="plain" style="width:480px"><div class="card-body">x</div></div>
        """;

    [Fact]
    public async Task A_question_sets_its_legend_over_its_choices_and_waits_for_an_answer()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Question);

        var legend = (await page.Locator("#legend").BoundingBoxAsync())!;
        var tiles = (await page.Locator("#tiles").BoundingBoxAsync())!;
        var single = (await page.Locator("#single").BoundingBoxAsync())!;
        Assert.True(legend.Y + legend.Height <= tiles.Y, "The legend overlaps the choices.");
        Assert.True(Math.Abs(legend.X - single.X) < 1, "The legend does not start at the question's edge.");
        Assert.True(Math.Abs(legend.X - (await page.Locator("#note-label").BoundingBoxAsync())!.X) < 1,
            "A free-text question's label does not line up with a legend.");
        var note = (await page.Locator("#note").BoundingBoxAsync())!;
        Assert.True(Math.Abs(note.Width - (await page.Locator("#free").BoundingBoxAsync())!.Width) < 1, "The field does not fill its question.");

        // Asking, not asking leave: not the approval's warn edge.
        Assert.NotEqual(await page.EvaluateAsync<string>(Style("#plain", "border-top-color")),
                        await page.EvaluateAsync<string>(Style("#q", "border-top-color")));

        // Disabled is the platform's state, and the app lifts it once answered.
        await Assertions.Expect(page.Locator("#send")).ToBeDisabledAsync();
        await page.Locator("#staging").ClickAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => document.querySelector('#staging input').checked"));
        Assert.NotEqual("none", await page.EvaluateAsync<string>(Style("#staging", "box-shadow")));

        await Js.Run(page, "() => { document.getElementById('q').dataset.state = 'answered'; }");
        await Assertions.Expect(page.Locator("#actions")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#result")).ToBeVisibleAsync();
    }

    private static string Queue(string dir = "ltr", int width = 420) => $$"""
        <div dir="{{dir}}" style="width:{{width}}px">
          <ul class="agent-plan agent-plan--todo" id="todo">
            <li data-state="done" id="done"><span class="agent-plan-mark"><i class="ri-check-line"></i></span><span class="agent-plan-label" id="done-label">Find when the sync stopped</span></li>
            <li id="plain"><span class="agent-plan-mark"></span><span class="agent-plan-label" id="plain-label">Ask for approval</span></li>
            <li id="queued">
              <span class="agent-plan-mark"><i class="ri-time-line"></i></span>
              <span class="agent-plan-label" id="queued-label">Also check the EU-North sync</span>
              <span class="agent-plan-actions" id="actions">
                <button class="btn btn--sm btn--ghost btn--icon" id="edit" type="button" aria-label="Edit"><i class="ri-pencil-line"></i></button>
                <button class="btn btn--sm btn--ghost btn--icon" type="button" aria-label="Remove"><i class="ri-close-line"></i></button>
              </span>
            </li>
          </ul>
          <ul class="agent-plan"><li data-state="done" id="plan-done"><span class="agent-plan-mark"></span><span class="agent-plan-label" id="plan-done-label">Done in a plan</span></li></ul>
        </div>
        """;

    [Fact]
    public async Task A_todo_strikes_its_done_items_and_shows_its_actions_on_hover_and_focus()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Queue());

        Assert.Contains("line-through", await page.EvaluateAsync<string>(Style("#done-label", "text-decoration-line")), StringComparison.Ordinal);
        Assert.DoesNotContain("line-through", await page.EvaluateAsync<string>(Style("#plain-label", "text-decoration-line")), StringComparison.Ordinal);
        // A plan without the modifier keeps its done items as they were.
        Assert.DoesNotContain("line-through", await page.EvaluateAsync<string>(Style("#plan-done-label", "text-decoration-line")), StringComparison.Ordinal);
        Assert.NotEqual(await page.EvaluateAsync<string>(Style("#plain", "color")),
                        await page.EvaluateAsync<string>(Style("#done", "color")));

        // The buttons are there for the keyboard, unseen until wanted, and add no height.
        Assert.Equal("0", await page.EvaluateAsync<string>(Style("#actions", "opacity")));
        var plain = (await page.Locator("#plain").BoundingBoxAsync())!.Height;
        var queued = (await page.Locator("#queued").BoundingBoxAsync())!.Height;
        Assert.True(Math.Abs(plain - queued) < 0.5, $"An item with actions is {queued}px tall, one without {plain}px.");

        await page.Locator("#queued-label").HoverAsync();
        await Assertions.Expect(page.Locator("#actions")).ToHaveCSSAsync("opacity", "1");
        await page.Locator("#plain-label").HoverAsync();
        await Assertions.Expect(page.Locator("#actions")).ToHaveCSSAsync("opacity", "0");
        await page.Locator("#edit").FocusAsync();
        await Assertions.Expect(page.Locator("#actions")).ToHaveCSSAsync("opacity", "1");
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task On_a_phone_a_queued_message_wraps_and_its_actions_stay_at_the_end(string dir)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Queue(dir, 260));
        await page.SetViewportSizeAsync(320, 700);

        Assert.True(await page.EvaluateAsync<bool>("() => { const l = document.getElementById('todo'); return l.scrollWidth <= l.clientWidth; }"),
            "The list grew a scrollbar.");
        var item = (await page.Locator("#queued").BoundingBoxAsync())!;
        var label = (await page.Locator("#queued-label").BoundingBoxAsync())!;
        var actions = (await page.Locator("#actions").BoundingBoxAsync())!;
        if (dir == "ltr")
            Assert.True(actions.X >= label.X + label.Width && Math.Abs(actions.X + actions.Width - (item.X + item.Width)) < 1, "The actions are not at the end.");
        else
            Assert.True(actions.X + actions.Width <= label.X && Math.Abs(actions.X - item.X) < 1, "The actions are not at the end.");
    }

    private const string Thinking = """
        <div style="width:480px">
          <div class="reasoning-bar" id="bar">
            <details class="reasoning" data-state="running" id="running">
              <summary id="summary"><i class="ri-arrow-right-s-line reasoning-chevron"></i><span class="reasoning-label" id="label">Thinking…</span></summary>
              <div class="reasoning-body"><p>The sync log first.</p></div>
            </details>
            <button class="btn btn--sm btn--ghost" id="stop" type="button">Stop</button>
          </div>
          <details class="reasoning" id="done"><summary><i class="ri-arrow-right-s-line reasoning-chevron"></i><span class="reasoning-label" id="done-label">Thought for 4 s, 3 tools</span></summary></details>
        </div>
        """;

    [Fact]
    public async Task A_running_thought_shimmers_beside_its_stop_and_rests_once_done()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Thinking);

        Assert.Equal("text-shimmer", await page.EvaluateAsync<string>(Style("#label", "animation-name")));
        Assert.Equal("none", await page.EvaluateAsync<string>(Style("#done-label", "animation-name")));

        var summary = (await page.Locator("#summary").BoundingBoxAsync())!;
        var stop = (await page.Locator("#stop").BoundingBoxAsync())!;
        var bar = (await page.Locator("#bar").BoundingBoxAsync())!;
        Assert.True(Math.Abs(summary.Y + summary.Height / 2 - (stop.Y + stop.Height / 2)) < 1,
            "The stop button is not on the summary's line.");
        Assert.True(Math.Abs(stop.X + stop.Width - (bar.X + bar.Width)) < 1, "The stop button is not at the end.");

        // Opened, the thought grows under the summary and the button stays where it was.
        await page.Locator("#summary").ClickAsync();
        Assert.True(Math.Abs(stop.Y - (await page.Locator("#stop").BoundingBoxAsync())!.Y) < 1, "The stop button moved when the thought opened.");

        // Done: the app drops the state, and the same words hold still.
        await Js.Run(page, "() => { document.getElementById('running').removeAttribute('data-state'); }");
        Assert.Equal("none", await page.EvaluateAsync<string>(Style("#label", "animation-name")));
    }

    [Fact]
    public async Task Under_reduced_motion_a_running_thought_holds_still()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Thinking + Tool("done", "tool"));
        await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });

        Assert.Equal("none", await page.EvaluateAsync<string>(Style("#label", "animation-name")));
        Assert.Equal("0s", await page.EvaluateAsync<string>(Style(".agent-tool-chevron", "transition-duration")));
        Assert.Equal("0s", await page.EvaluateAsync<string>(Style(".agent-tool > summary", "transition-duration")));
    }

    private const string Context = """
        <button class="btn btn--sm btn--ghost" id="ring" type="button" data-popover="ctx" data-popover-hover aria-label="Context: 62% used">
          <span class="context-ring" style="--value: 62" aria-hidden="true"></span><span>62%</span>
        </button>
        <div class="popover context-usage" id="ctx" popover>
          <div class="context-usage-head"><strong>Context</strong><span>124k / 200k tokens</span></div>
          <div class="meter meter--sm"><span class="meter-fill" style="--value: 62%"></span></div>
          <dl class="context-usage-rows" id="rows">
            <div><dt>Input</dt><dd>92,410</dd><dd>$0.28</dd></div>
            <div><dt>Reasoning</dt><dd>3,960</dd><dd>$0.06</dd></div>
            <div><dt>Cached</dt><dd>19,640</dd><dd>$0.001</dd></div>
          </dl>
          <div class="context-usage-foot" id="foot"><span>Total cost</span><strong>$0.34</strong></div>
        </div>
        """;

    [Fact]
    public async Task The_context_card_stays_closed_until_asked_and_lines_its_figures_up()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Context);

        // No `display` of its own: a closed popover stays closed.
        Assert.Equal("none", await page.EvaluateAsync<string>(Style("#ctx", "display")));
        await page.Locator("#ring").ClickAsync();
        await Assertions.Expect(page.Locator("#ctx")).ToBeVisibleAsync();

        var ends = await page.EvaluateAsync<double[][]>("""
            () => [...document.querySelectorAll('#rows > div')].map(r =>
                [...r.querySelectorAll('dd')].map(d => d.getBoundingClientRect().right))
            """);
        foreach (var column in Enumerable.Range(0, 2))
            Assert.True(ends.Select(r => r[column]).Max() - ends.Select(r => r[column]).Min() < 0.5,
                $"The figures in column {column + 1} do not end on one line.");

        // The bar is the ring's colour, so the two read as one measure.
        var probe = await page.EvaluateAsync<string>("""
            () => { const p = document.createElement('span'); p.style.color = 'var(--agent-to)'; document.body.append(p);
                    const c = getComputedStyle(p).color; p.remove(); return c; }
            """);
        Assert.Equal(probe, await page.EvaluateAsync<string>(Style("#ctx .meter-fill", "color")));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_checkpoint_button_keeps_its_case_and_an_edited_message_takes_its_width()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div style="width:600px">
              <div class="divider-labelled" id="cp"><span class="sedna-row sedna-gap-2"><i class="ri-bookmark-line"></i><span>Checkpoint</span></span>
                <button class="btn btn--sm btn--ghost" id="restore" type="button">Restore to here</button></div>
              <div class="chat" role="log">
                <div class="chat-message chat-message--mine" id="editing"><div class="chat-body">
                  <form class="chat-bubble chat-bubble--editing" id="bubble"><textarea class="form-input" id="field" rows="2">Hi</textarea>
                    <div class="sedna-row sedna-gap-2"><button class="btn btn--sm btn--ghost sedna-push" type="button">Cancel</button><button class="btn btn--sm btn--solid" type="submit">Send</button></div></form>
                </div></div>
                <div class="chat-message chat-message--mine" id="short"><div class="chat-body"><div class="chat-bubble">Hi</div></div></div>
                <div class="chat-message chat-message--agent"><span class="agent-avatar"></span><div class="chat-body">
                  <div class="chat-bubble chat-bubble--failed" id="failed">The answer stopped.</div>
                  <div class="chat-bubble" id="answer"><p>An answer.</p></div>
                </div></div>
              </div>
            </div>
            """);

        Assert.Equal("uppercase", await page.EvaluateAsync<string>(Style("#cp", "text-transform")));
        Assert.Equal("none", await page.EvaluateAsync<string>(Style("#restore", "text-transform")));

        var editing = (await page.Locator("#editing").BoundingBoxAsync())!;
        var shortOne = (await page.Locator("#short").BoundingBoxAsync())!;
        var field = (await page.Locator("#field").BoundingBoxAsync())!;
        var bubble = (await page.Locator("#bubble").BoundingBoxAsync())!;
        Assert.True(editing.Width > shortOne.Width * 3, $"The message being edited is {editing.Width}px wide.");
        Assert.True(Math.Abs(editing.X + editing.Width - (shortOne.X + shortOne.Width)) < 1, "The edited message left the end.");
        Assert.True(field.Width > bubble.Width - 20, "The field does not fill the bubble.");

        // An agent's failed answer gets back the ground an agent's answer does not have.
        Assert.Equal("0px", await page.EvaluateAsync<string>(Style("#answer", "padding-left")));
        Assert.NotEqual("0px", await page.EvaluateAsync<string>(Style("#failed", "padding-left")));
    }
}
