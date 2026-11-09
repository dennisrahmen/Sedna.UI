using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Drag and drop over the app's own items: the events carry the move, and the script moves
/// no node. Mouse, a real touch screen and the keyboard each reach the same event.
/// </summary>
/// <remarks>
/// A MutationObserver on every zone counts added and removed nodes for the whole test. The
/// rule under test is that the script changes attributes only — Blazor holds references to
/// the items it rendered, so a node moved here would be diffed against the wrong sibling on
/// the next render.
/// </remarks>
public class DragTests : ScriptTestBase
{
    private const string Live = """
        <p class="visually-hidden" aria-live="assertive" data-drag-live
           data-drag-pickup="Picked up {item}."
           data-drag-move="{item}: {position} of {count} in {zone}."
           data-drag-drop="Dropped {item} at {position} of {count} in {zone}."
           data-drag-cancel="{item} is back at {position} in {zone}."></p>
        """;

    private const string Queue = Live + """
        <ul class="drag-list" style="width: 320px" data-drag-zone="queue" aria-label="Queue">
          <li class="drag-item" data-drag-item="a" tabindex="0">Alpha</li>
          <li class="drag-item" data-drag-item="b" tabindex="0">Bravo <button id="open-b" type="button">Open</button></li>
          <li class="drag-item" data-drag-item="c" tabindex="0">Charlie</li>
          <li class="drag-item" data-drag-item="d" tabindex="0" aria-disabled="true">Delta</li>
        </ul>
        """;

    private const string Board = Live + """
        <div style="display: flex; gap: 24px">
          <ul class="drag-list" style="width: 220px" data-drag-zone="todo" data-drag-group="board" aria-label="To do">
            <li class="drag-item" data-drag-item="t1" data-drag-type="task" tabindex="0">Rotate keys</li>
            <li class="drag-item" data-drag-item="b1" data-drag-type="bug" tabindex="0">Fix login</li>
          </ul>
          <ul class="drag-list" style="width: 220px; min-height: 120px" data-drag-zone="doing" data-drag-group="board"
              data-drag-accept="task" aria-label="Doing">
            <li class="drag-empty">Nothing in progress</li>
          </ul>
          <ul class="drag-list" style="width: 220px" data-drag-zone="elsewhere" aria-label="Elsewhere">
            <li class="drag-item" data-drag-item="x1" tabindex="0">Unrelated</li>
          </ul>
        </div>
        """;

    private const string Nested = Live + """
        <ul class="drag-list" style="width: 360px" data-drag-zone="sections" data-drag-group="plan" data-drag-accept="section" aria-label="Sections">
          <li class="drag-item" data-drag-item="s1" data-drag-type="section" style="display: block">
            <button class="drag-handle" id="grip-s1" type="button" data-drag-handle aria-label="Move Build">::</button> Build
            <ul class="drag-list" data-drag-zone="s1-tasks" data-drag-group="plan" data-drag-accept="task" aria-label="Build tasks">
              <li class="drag-item" data-drag-item="k1" data-drag-type="task">Compile</li>
              <li class="drag-item" data-drag-item="k2" data-drag-type="task">Package</li>
            </ul>
          </li>
          <li class="drag-item" data-drag-item="s2" data-drag-type="section" style="display: block">
            <button class="drag-handle" id="grip-s2" type="button" data-drag-handle aria-label="Move Release">::</button> Release
            <ul class="drag-list" data-drag-zone="s2-tasks" data-drag-group="plan" data-drag-accept="task" aria-label="Release tasks">
              <li class="drag-item" data-drag-item="k3" data-drag-type="task">Tag</li>
            </ul>
          </li>
        </ul>
        """;

    private const string Grid = """
        <ul class="drag-grid" style="width: 390px; grid-template-columns: repeat(3, 1fr)" data-drag-zone="tiles" data-drag-axis="grid">
          <li class="drag-item" data-drag-item="g0">0</li>
          <li class="drag-item" data-drag-item="g1">1</li>
          <li class="drag-item" data-drag-item="g2">2</li>
          <li class="drag-item" data-drag-item="g3">3</li>
          <li class="drag-item" data-drag-item="g4">4</li>
          <li class="drag-item" data-drag-item="g5">5</li>
        </ul>
        """;

    private static string Scroller()
    {
        var items = string.Concat(Enumerable.Range(0, 30).Select(i =>
            $"""<li class="drag-item" data-drag-item="r{i}" style="height: 40px">Row {i}</li>"""));
        return $"""
            <div id="scroller" style="height: 240px; width: 320px; overflow: auto">
              <ul class="drag-list" data-drag-zone="rows">{items}</ul>
            </div>
            """;
    }

    private sealed record Logged(string Name, JsonElement Detail);

    private async Task<IPage> OpenDrag(string body, bool hasTouch = false)
    {
        var page = await Open(body, head: StylesheetTag, hasTouch: hasTouch);
        await Js.Run(page, """
            () => {
                window.dragLog = [];
                for (const name of ['sedna-dragstart', 'sedna-drop', 'sedna-dragend'])
                    document.addEventListener(name, e => dragLog.push({ name, detail: e.detail }));
                window.nodesMoved = 0;
                const observer = new MutationObserver(records => {
                    for (const r of records) nodesMoved += r.addedNodes.length + r.removedNodes.length;
                });
                for (const zone of document.querySelectorAll('[data-drag-zone]'))
                    observer.observe(zone, { childList: true, subtree: true });
            }
            """);
        return page;
    }

    private static async Task<List<Logged>> Log(IPage page)
    {
        var json = await page.EvaluateAsync<string>("() => JSON.stringify(dragLog)");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray()
            .Select(e => new Logged(e.GetProperty("name").GetString()!, e.GetProperty("detail").Clone()))
            .ToList();
    }

    private static async Task<(float X, float Y)> Point(IPage page, string selector, float fx = 0.5f, float fy = 0.5f)
    {
        var box = await page.Locator(selector).BoundingBoxAsync();
        Assert.NotNull(box);
        return (box!.X + box.Width * fx, box.Y + box.Height * fy);
    }

    private static async Task MouseDrag(IPage page, (float X, float Y) from, (float X, float Y) to, bool release = true)
    {
        await page.Mouse.MoveAsync(from.X, from.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(from.X, from.Y + 8, new() { Steps = 2 });
        await page.Mouse.MoveAsync(to.X, to.Y, new() { Steps = 12 });
        await Js.Frames(page);
        if (release) await page.Mouse.UpAsync();
    }

    private static async Task AssertNothingLeftBehind(IPage page)
    {
        Assert.Equal(0, await page.EvaluateAsync<int>("() => nodesMoved"));
        Assert.Equal(0, await page.EvaluateAsync<int>(
            "() => document.querySelectorAll('[data-dragging], [data-drop-state], [data-drop-over], [data-drop-edge], [popover], [data-drag-slot], [data-drag-carried], [data-drag-count], [data-drag-copy]').length"));
        Assert.Equal(0, await page.EvaluateAsync<int>(
            "() => [...document.querySelectorAll('[data-drag-item]')].filter(el => el.hasAttribute('style') && (el.style.getPropertyValue('--drag-x') || el.style.getPropertyValue('--drag-name'))).length"));
    }

    private static Task<string> RowTops(IPage page, string selector = "[data-drag-item]:not([data-dragging])") =>
        page.EvaluateAsync<string>(
            $"() => [...document.querySelectorAll('{selector}')].map(el => {{ const r = el.getBoundingClientRect(); return el.getAttribute('data-drag-item') + '@' + Math.round(r.left) + ',' + Math.round(r.top); }}).filter(s => !s.startsWith('a@')).join(' ')");

    private static async Task<string> Order(IPage page, string zone) =>
        await page.EvaluateAsync<string>(
            $"() => [...document.querySelector('[data-drag-zone=\"{zone}\"]').children].map(el => el.getAttribute('data-drag-item')).filter(Boolean).join(',')");

    private static void AssertDrop(Logged drop, string item, string from, string to, int index, int fromIndex, bool keyboard, string effect = "move")
    {
        Assert.Equal(effect, drop.Detail.GetProperty("effect").GetString());
        Assert.Equal("sedna-drop", drop.Name);
        Assert.Equal(item, drop.Detail.GetProperty("item").GetString());
        Assert.Equal(from, drop.Detail.GetProperty("from").GetString());
        Assert.Equal(to, drop.Detail.GetProperty("to").GetString());
        Assert.Equal(index, drop.Detail.GetProperty("index").GetInt32());
        Assert.Equal(fromIndex, drop.Detail.GetProperty("fromIndex").GetInt32());
        Assert.Equal(keyboard, drop.Detail.GetProperty("keyboard").GetBoolean());
    }

    // ── Mouse ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_mouse_drag_reports_the_move_and_moves_no_node()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Queue);
        var tops = await RowTops(page);

        await MouseDrag(page, await Point(page, "[data-drag-item=a]"),
            await Point(page, "[data-drag-item=c]", fy: 0.8f), release: false);

        // Mid-drag: every state is an attribute on the app's own elements.
        await Assertions.Expect(page.Locator("[data-drag-item=a]")).ToHaveAttributeAsync("data-dragging", "pointer");
        await Assertions.Expect(page.Locator("[data-drag-zone=queue]")).ToHaveAttributeAsync("data-drop-state", "valid");
        await Assertions.Expect(page.Locator("[data-drag-zone=queue]")).ToHaveAttributeAsync("data-drop-over", "");
        await Assertions.Expect(page.Locator("[data-drag-item=d]")).ToHaveAttributeAsync("data-drop-edge", "before");
        var translate = await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('[data-drag-item=a]')).translate");
        Assert.NotEqual("none", translate);
        // Carried in the top layer, where nothing can clip it, with its slot held open so
        // no row the pointer was aimed at has moved.
        Assert.True(await page.EvaluateAsync<bool>("() => document.querySelector('[data-drag-item=a]').matches(':popover-open')"));
        Assert.Equal(tops, await RowTops(page));

        await page.Mouse.UpAsync();

        var log = await Log(page);
        Assert.Equal(["sedna-dragstart", "sedna-drop", "sedna-dragend"], log.Select(l => l.Name));
        AssertDrop(log[1], "a", "queue", "queue", index: 2, fromIndex: 0, keyboard: false);
        Assert.True(log[2].Detail.GetProperty("dropped").GetBoolean());

        // The app did not handle it, so nothing moved: the list is still the app's order.
        Assert.Equal("a,b,c,d", await Order(page, "queue"));
        await AssertNothingLeftBehind(page);
        Assert.False(await page.EvaluateAsync<bool>("() => document.querySelector('[data-drag-item=a]').hasAttribute('style')"));
        Assert.Equal("", await page.EvaluateAsync<string>("() => getSelection().toString()"));
    }

    [Fact]
    public async Task A_click_stays_a_click_and_controls_and_disabled_items_do_not_lift()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Queue);
        await Js.Run(page, "() => { window.clicks = 0; document.getElementById('open-b').addEventListener('click', () => clicks++); }");

        await page.Locator("#open-b").ClickAsync();
        Assert.Equal(1, await page.EvaluateAsync<int>("() => clicks"));

        await MouseDrag(page, await Point(page, "#open-b"), await Point(page, "[data-drag-item=d]"));
        await MouseDrag(page, await Point(page, "[data-drag-item=d]"), await Point(page, "[data-drag-item=a]"));
        await page.Locator("[data-drag-item=c]").ClickAsync();

        Assert.Empty(await Log(page));
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task The_app_can_refuse_a_drag_from_its_start_event()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Queue);
        await Js.Run(page, "() => document.addEventListener('sedna-dragstart', e => e.preventDefault())");

        await MouseDrag(page, await Point(page, "[data-drag-item=a]"),
            await Point(page, "[data-drag-item=c]"), release: false);
        Assert.Null(await page.Locator("[data-drag-item=a]").GetAttributeAsync("data-dragging"));
        await page.Mouse.UpAsync();

        Assert.Equal(["sedna-dragstart"], (await Log(page)).Select(l => l.Name));
    }

    [Fact]
    public async Task A_card_crosses_to_a_zone_in_its_group_and_a_refusing_zone_is_marked()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Board);

        // A bug over a lane that takes only tasks.
        await MouseDrag(page, await Point(page, "[data-drag-item=b1]"),
            await Point(page, "[data-drag-zone=doing]"), release: false);
        await Assertions.Expect(page.Locator("[data-drag-zone=doing]")).ToHaveAttributeAsync("data-drop-state", "invalid");
        await Assertions.Expect(page.Locator("[data-drag-zone=doing]")).ToHaveAttributeAsync("data-drop-over", "");
        await Assertions.Expect(page.Locator("[data-drag-zone=todo]")).ToHaveAttributeAsync("data-drop-state", "valid");
        // A zone in no shared group is not part of this drag at all.
        Assert.Null(await page.Locator("[data-drag-zone=elsewhere]").GetAttributeAsync("data-drop-state"));
        await page.Mouse.UpAsync();

        var refused = await Log(page);
        Assert.Equal(["sedna-dragstart", "sedna-dragend"], refused.Select(l => l.Name));
        Assert.False(refused[1].Detail.GetProperty("dropped").GetBoolean());

        // A task into the same empty lane.
        await Js.Run(page, "() => dragLog.length = 0");
        await MouseDrag(page, await Point(page, "[data-drag-item=t1]"), await Point(page, "[data-drag-zone=doing]"));

        var log = await Log(page);
        AssertDrop(log.Single(l => l.Name == "sedna-drop"), "t1", "todo", "doing", index: 0, fromIndex: 0, keyboard: false);
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_nested_zone_that_refuses_the_item_hands_the_drop_to_the_zone_around_it()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Nested);

        // Release, by its handle, over the top of Build's own task list.
        await MouseDrag(page, await Point(page, "#grip-s2"),
            await Point(page, "[data-drag-item=s1]", fy: 0.3f), release: false);

        // The pointer is over Build's task list, which takes only tasks: the section lands
        // in the list of sections around it, in front of Build.
        await Assertions.Expect(page.Locator("[data-drag-zone=sections]")).ToHaveAttributeAsync("data-drop-over", "");
        await Assertions.Expect(page.Locator("[data-drag-zone=s1-tasks]")).ToHaveAttributeAsync("data-drop-state", "invalid");
        await Assertions.Expect(page.Locator("[data-drag-zone=s2-tasks]")).ToHaveAttributeAsync("data-drop-state", "invalid");
        await Assertions.Expect(page.Locator("[data-drag-item=s1]")).ToHaveAttributeAsync("data-drop-edge", "before");
        await page.Mouse.UpAsync();

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "s2", "sections", "sections", index: 0, fromIndex: 1, keyboard: false);

        // And a task goes between the inner lists, which share a group.
        await Js.Run(page, "() => dragLog.length = 0");
        await MouseDrag(page, await Point(page, "[data-drag-item=k3]"), await Point(page, "[data-drag-item=k2]", fy: 0.8f));
        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "k3", "s2-tasks", "s1-tasks", index: 2, fromIndex: 0, keyboard: false);
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_grid_counts_positions_in_reading_order_and_draws_the_line_on_the_pointers_row()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Grid);
        const string others = "[data-drag-item]:not([data-drag-item=g0])";
        var cells = await RowTops(page, others);

        // The end of the first row: the slot is in front of tile 3, which starts the next
        // row, so the line is drawn after tile 2 where the pointer is.
        await MouseDrag(page, await Point(page, "[data-drag-item=g0]"),
            await Point(page, "[data-drag-item=g2]", fx: 0.85f), release: false);
        await Assertions.Expect(page.Locator("[data-drag-item=g2]")).ToHaveAttributeAsync("data-drop-edge", "after");
        // Every other tile is still in its cell while tile 0 is carried.
        Assert.Equal(cells, await RowTops(page, others));
        await page.Mouse.UpAsync();
        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "g0", "tiles", "tiles", index: 2, fromIndex: 0, keyboard: false);

        await Js.Run(page, "() => dragLog.length = 0");
        await MouseDrag(page, await Point(page, "[data-drag-item=g0]"), await Point(page, "[data-drag-item=g4]", fx: 0.2f));
        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "g0", "tiles", "tiles", index: 3, fromIndex: 0, keyboard: false);
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_drag_held_near_the_edge_of_a_scroller_scrolls_it()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Scroller());

        var bottom = await Point(page, "#scroller", fy: 0.97f);
        await MouseDrag(page, await Point(page, "[data-drag-item=r1]"), bottom, release: false);
        // Until it has scrolled and the place it would drop has followed past the seventh row
        // (the dragged row leaves the count, so that is an index past 6) — not for a fixed
        // time: the scrolling runs on frames, and a loaded machine paints fewer of them.
        await page.WaitForFunctionAsync("""
            () => document.getElementById('scroller').scrollTop > 120
               && [...document.querySelectorAll('#scroller [data-drag-item]')].findIndex(e => e.hasAttribute('data-drop-edge')) > 7
            """);

        var scrolled = await page.EvaluateAsync<double>("() => document.getElementById('scroller').scrollTop");
        Assert.True(scrolled > 120, $"The scroller moved {scrolled}px under a drag held at its edge.");

        await page.Mouse.UpAsync();
        var drop = (await Log(page)).Single(l => l.Name == "sedna-drop");
        Assert.True(drop.Detail.GetProperty("index").GetInt32() > 6,
            "The drop landed where the scroller was when the drag started, not where it scrolled to.");
        await AssertNothingLeftBehind(page);
    }

    // ── Touch ──────────────────────────────────────────────────────────────────

    private static async Task Touch(ICDPSession cdp, string type, (float X, float Y)? at)
    {
        var points = at is { } p
            ? new object[] { new Dictionary<string, object> { ["x"] = p.X, ["y"] = p.Y, ["id"] = 1 } }
            : Array.Empty<object>();
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = type,
            ["touchPoints"] = points,
        });
    }

    private static async Task TouchMove(ICDPSession cdp, (float X, float Y) from, (float X, float Y) to, int steps)
    {
        for (var i = 1; i <= steps; i++)
        {
            var t = (float)i / steps;
            await Touch(cdp, "touchMove", (from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t));
        }
    }

    [Fact]
    public async Task A_finger_held_still_lifts_the_item_and_carries_it()
    {
        // Raw touch input needs CDP (see BrowserEngine).
        if (NoBrowser || !BrowserEngine.HasRawTouch) return;
        var page = await OpenDrag(Queue, hasTouch: true);
        var cdp = await page.Context.NewCDPSessionAsync(page);

        var from = await Point(page, "[data-drag-item=a]");
        var to = await Point(page, "[data-drag-item=c]", fy: 0.8f);

        await Touch(cdp, "touchStart", from);
        await Assertions.Expect(page.Locator("[data-drag-item=a]")).ToHaveAttributeAsync("data-dragging", "pointer");

        await TouchMove(cdp, from, to, steps: 10);
        await Assertions.Expect(page.Locator("[data-drag-item=d]")).ToHaveAttributeAsync("data-drop-edge", "before");
        await Touch(cdp, "touchEnd", null);

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "a", "queue", "queue", index: 2, fromIndex: 0, keyboard: false);
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_finger_that_moves_before_the_hold_is_scrolling_and_lifts_nothing()
    {
        // Raw touch input needs CDP (see BrowserEngine).
        if (NoBrowser || !BrowserEngine.HasRawTouch) return;
        var page = await OpenDrag(Queue, hasTouch: true);
        var cdp = await page.Context.NewCDPSessionAsync(page);

        var from = await Point(page, "[data-drag-item=a]");
        await Touch(cdp, "touchStart", from);
        await TouchMove(cdp, from, (from.X, from.Y + 60), steps: 6);
        await Js.Elapse(page, 450);
        await Touch(cdp, "touchEnd", null);

        Assert.Empty(await Log(page));
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_finger_on_a_handle_lifts_at_once()
    {
        // Raw touch input needs CDP (see BrowserEngine).
        if (NoBrowser || !BrowserEngine.HasRawTouch) return;
        var page = await OpenDrag(Nested, hasTouch: true);
        var cdp = await page.Context.NewCDPSessionAsync(page);

        var from = await Point(page, "#grip-s2");
        var to = await Point(page, "[data-drag-item=s1]", fy: 0.2f);
        await Touch(cdp, "touchStart", from);
        await TouchMove(cdp, from, to, steps: 10);
        await Assertions.Expect(page.Locator("[data-drag-item=s2]")).ToHaveAttributeAsync("data-dragging", "pointer");
        await Touch(cdp, "touchEnd", null);

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "s2", "sections", "sections", index: 0, fromIndex: 1, keyboard: false);
        await AssertNothingLeftBehind(page);
    }

    // ── Keyboard ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Space_picks_up_the_arrows_move_and_Enter_drops_in_the_apps_words()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Queue);
        var live = page.Locator("[data-drag-live]");

        await page.Locator("[data-drag-item=a]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(page.Locator("[data-drag-item=a]")).ToHaveAttributeAsync("data-dragging", "keyboard");
        await Assertions.Expect(live).ToHaveTextAsync("Picked up Alpha.");

        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(live).ToHaveTextAsync(new Regex(@"^Alpha: 3 of 4 in Queue\.\s?$"));
        await Assertions.Expect(page.Locator("[data-drag-item=d]")).ToHaveAttributeAsync("data-drop-edge", "before");

        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(live).ToHaveTextAsync("Dropped Alpha at 3 of 4 in Queue.");

        var log = await Log(page);
        Assert.Equal(["sedna-dragstart", "sedna-drop", "sedna-dragend"], log.Select(l => l.Name));
        AssertDrop(log[1], "a", "queue", "queue", index: 2, fromIndex: 0, keyboard: true);
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task Escape_puts_the_item_back_and_sends_no_drop()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Queue);

        await page.Locator("[data-drag-item=b]").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.Locator("[data-drag-live]")).ToHaveTextAsync("Bravo Open is back at 2 in Queue.");
        var log = await Log(page);
        Assert.Equal(["sedna-dragstart", "sedna-dragend"], log.Select(l => l.Name));
        Assert.False(log[1].Detail.GetProperty("dropped").GetBoolean());
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task The_arrows_across_a_list_move_to_the_next_zone_that_takes_the_item()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Board);

        await page.Locator("[data-drag-item=t1]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(page.Locator("[data-drag-zone=doing]")).ToHaveAttributeAsync("data-drop-over", "");
        await Assertions.Expect(page.Locator("[data-drag-live]")).ToHaveTextAsync("Rotate keys: 1 of 1 in Doing.");
        // Past the last zone in the group there is nowhere further to go.
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("Space");

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "t1", "todo", "doing", index: 0, fromIndex: 0, keyboard: true);
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task Focus_follows_the_item_once_the_app_has_moved_it()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Nested);
        // What a Blazor render does: the item is taken out and put back in its new place,
        // which drops focus to <body>.
        await Js.Run(page, """
            () => document.addEventListener('sedna-drop', e => {
                const zone = document.querySelector(`[data-drag-zone="${e.detail.to}"]`);
                const item = document.querySelector(`[data-drag-item="${e.detail.item}"]`);
                const rest = [...zone.children].filter(el => el !== item && el.hasAttribute('data-drag-item'));
                setTimeout(() => zone.insertBefore(item, rest[e.detail.index] || null), 30); // page-timer: an app that moves the item a moment later
            })
            """);

        await page.Locator("#grip-s2").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("Space");

        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-drag-zone=sections]').firstElementChild.getAttribute('data-drag-item') === 's2'");
        await Assertions.Expect(page.Locator("#grip-s2")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task An_item_is_never_offered_a_zone_inside_itself()
    {
        if (NoBrowser) return;
        // A tree: every folder's children are a zone in the same group as the folders.
        var page = await OpenDrag("""
            <ul class="drag-list" style="width: 360px" data-drag-zone="root" data-drag-group="tree">
              <li class="drag-item" data-drag-item="f1" style="display: block" tabindex="0">Reports
                <ul class="drag-list" data-drag-zone="f1-children" data-drag-group="tree"></ul>
              </li>
              <li class="drag-item" data-drag-item="f2" style="display: block" tabindex="0">Archive
                <ul class="drag-list" data-drag-zone="f2-children" data-drag-group="tree"></ul>
              </li>
            </ul>
            """);

        await page.Locator("[data-drag-item=f1]").FocusAsync();
        await page.Keyboard.PressAsync("Space");

        await Assertions.Expect(page.Locator("[data-drag-zone=f1-children]")).ToHaveAttributeAsync("data-drop-state", "invalid");
        await Assertions.Expect(page.Locator("[data-drag-zone=f2-children]")).ToHaveAttributeAsync("data-drop-state", "valid");
        await page.Keyboard.PressAsync("Escape");
        await AssertNothingLeftBehind(page);
    }

    // ── Several at once ────────────────────────────────────────────────────────

    private const string Picked = """
        <p class="visually-hidden" aria-live="assertive" data-drag-live
           data-drag-pickup="Picked up {items}."
           data-drag-drop="Dropped {items} at {position} of {count}."></p>
        <ul class="drag-list" style="width: 320px" data-drag-zone="picked" aria-label="Picked">
          <li class="drag-item" data-drag-item="p1" tabindex="0" aria-selected="true">One</li>
          <li class="drag-item" data-drag-item="p2" tabindex="0">Two</li>
          <li class="drag-item" data-drag-item="p3" tabindex="0" aria-selected="true">Three</li>
          <li class="drag-item" data-drag-item="p4" tabindex="0">Four</li>
        </ul>
        """;

    private static string[] Items(Logged logged) =>
        logged.Detail.GetProperty("items").EnumerateArray().Select(e => e.GetString()!).ToArray();

    [Fact]
    public async Task A_selected_item_carries_the_rest_of_the_selection_and_counts_them()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Picked);

        await MouseDrag(page, await Point(page, "[data-drag-item=p3]"),
            await Point(page, "[data-drag-item=p4]", fy: 0.8f), release: false);

        // The others wait where they are, faded; the one in hand says how many travel.
        await Assertions.Expect(page.Locator("[data-drag-item=p1]")).ToHaveAttributeAsync("data-drag-carried", "");
        await Assertions.Expect(page.Locator("[data-drag-item=p3]")).ToHaveAttributeAsync("data-drag-count", "2");
        Assert.Equal("\"2\"", await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('[data-drag-item=p3]'), '::after').content"));
        // The line is drawn among what stays, never against an item that is travelling.
        await Assertions.Expect(page.Locator("[data-drag-item=p4]")).ToHaveAttributeAsync("data-drop-edge", "after");

        await page.Mouse.UpAsync();

        var log = await Log(page);
        Assert.Equal(["p1", "p3"], Items(log[0]));
        var drop = log.Single(l => l.Name == "sedna-drop");
        // Two stay, so the end of the list is position 2 — counted without either of them.
        AssertDrop(drop, "p3", "picked", "picked", index: 2, fromIndex: 2, keyboard: false);
        Assert.Equal(["p1", "p3"], Items(drop));
        Assert.Equal(["p1", "p3"], Items(log[^1]));
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task An_item_outside_the_selection_travels_alone()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Picked);

        await MouseDrag(page, await Point(page, "[data-drag-item=p2]"),
            await Point(page, "[data-drag-item=p4]", fy: 0.8f));

        var drop = (await Log(page)).Single(l => l.Name == "sedna-drop");
        AssertDrop(drop, "p2", "picked", "picked", index: 3, fromIndex: 1, keyboard: false);
        Assert.Equal(["p2"], Items(drop));
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_selection_is_read_from_a_checkbox_and_goes_only_where_every_item_may()
    {
        if (NoBrowser) return;
        // A tile has a checkbox, not aria-selected, which a list item may not carry.
        var page = await OpenDrag("""
            <div style="display: flex; gap: 24px">
              <ul class="drag-list" style="width: 220px" data-drag-zone="todo" data-drag-group="board" aria-label="To do">
                <li class="drag-item" data-drag-item="t1" data-drag-type="task"><input type="checkbox" data-drag-select checked aria-label="Select Rotate keys"> Rotate keys</li>
                <li class="drag-item" data-drag-item="t2" data-drag-type="task"><input type="checkbox" data-drag-select checked aria-label="Select Renew certificate"> Renew certificate</li>
                <li class="drag-item" data-drag-item="b1" data-drag-type="bug"><input type="checkbox" data-drag-select id="pick-b1" aria-label="Select Fix login"> Fix login</li>
              </ul>
              <ul class="drag-list" style="width: 220px; min-height: 160px" data-drag-zone="doing" data-drag-group="board"
                  data-drag-accept="task" aria-label="Doing">
                <li class="drag-empty">Nothing in progress</li>
              </ul>
            </div>
            """);

        await MouseDrag(page, await Point(page, "[data-drag-item=t1]", fx: 0.7f), await Point(page, "[data-drag-zone=doing]"));
        var drop = (await Log(page)).Single(l => l.Name == "sedna-drop");
        AssertDrop(drop, "t1", "todo", "doing", index: 0, fromIndex: 0, keyboard: false);
        Assert.Equal(["t1", "t2"], Items(drop));

        // With a bug in the selection, a lane that takes only tasks takes none of it.
        await Js.Run(page, "() => { dragLog.length = 0; document.getElementById('pick-b1').checked = true; }");
        await MouseDrag(page, await Point(page, "[data-drag-item=t1]", fx: 0.7f),
            await Point(page, "[data-drag-zone=doing]"), release: false);
        await Assertions.Expect(page.Locator("[data-drag-zone=doing]")).ToHaveAttributeAsync("data-drop-state", "invalid");
        await page.Mouse.UpAsync();

        Assert.DoesNotContain(await Log(page), l => l.Name == "sedna-drop");
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_selection_moves_from_the_keyboard_and_is_announced_by_its_size()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Picked);
        var live = page.Locator("[data-drag-live]");

        await page.Locator("[data-drag-item=p1]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(live).ToHaveTextAsync("Picked up 2.");
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(live).ToHaveTextAsync("Dropped 2 at 3 of 3.");

        var drop = (await Log(page)).Single(l => l.Name == "sedna-drop");
        AssertDrop(drop, "p1", "picked", "picked", index: 2, fromIndex: 0, keyboard: true);
        Assert.Equal(["p1", "p3"], Items(drop));
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_selection_set_down_where_it_already_is_sends_no_drop_unless_it_was_scattered()
    {
        if (NoBrowser) return;
        // One and Three are apart: dropping them at One's place gathers them, which is a move.
        var page = await OpenDrag(Picked);
        await page.Locator("[data-drag-item=p1]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("Enter");
        var scattered = (await Log(page)).Single(l => l.Name == "sedna-drop");
        AssertDrop(scattered, "p1", "picked", "picked", index: 0, fromIndex: 0, keyboard: true);

        // One and Two together, set down at once: nothing changes, so nothing is sent.
        await Js.Run(page, """
            () => {
                dragLog.length = 0;
                document.querySelector('[data-drag-item=p3]').removeAttribute('aria-selected');
                document.querySelector('[data-drag-item=p2]').setAttribute('aria-selected', 'true');
            }
            """);
        await page.Locator("[data-drag-item=p2]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("Enter");

        Assert.Equal(["sedna-dragstart", "sedna-dragend"], (await Log(page)).Select(l => l.Name));
        await AssertNothingLeftBehind(page);
    }

    // ── Table columns ──────────────────────────────────────────────────────────

    /// <summary>
    /// A header row that reorders its columns. Order stays put and is no item; Customer and
    /// State drag from their sort buttons under a pointer, Total from anywhere in it, and
    /// Customer has a resize grip.
    /// </summary>
    private static string Columns(string dir = "ltr", string role = "", string tab = "tabindex=\"0\"") => Live + $$"""
        <div dir="{{dir}}">
          <table class="table table--fixed" id="t" {{role}} aria-label="Orders" style="--col-width: 140px">
            <thead><tr data-drag-zone="cols" data-drag-axis="x" aria-label="Columns">
              <th id="h-order">Order</th>
              <th id="h-customer" data-drag-item="customer" data-drag-label="Customer" {{tab}} aria-sort="none">
                <button class="th-sort" id="sort-customer" type="button" data-drag-handle="pointer">Customer</button>
                <div class="col-resize" id="grip" data-resize="--col-width" data-resize-key="customer" role="separator"
                     aria-orientation="vertical" aria-label="Resize Customer" tabindex="0"></div>
              </th>
              <th id="h-state" data-drag-item="state" data-drag-label="State" {{tab}} aria-sort="none">
                <button class="th-sort" id="sort-state" type="button" data-drag-handle="pointer">State</button>
              </th>
              <th id="h-total" data-drag-item="total" data-drag-label="Total" {{tab}}>Total</th>
            </tr></thead>
            <tbody>
              <tr><td id="c-order">ORD-4209</td><td id="c-customer">Northwind Retail</td><td>Pending</td><td>1,248.00</td></tr>
              <tr><td>ORD-4204</td><td>Alex Fischer</td><td>Approved</td><td>86.40</td></tr>
            </tbody>
          </table>
        </div>
        <script>window.sorts = []; document.addEventListener('click', e => { const b = e.target.closest('.th-sort'); if (b) sorts.push(b.id); });</script>
        """;

    private static Task<string> Focused(IPage page) => page.EvaluateAsync<string>("() => document.activeElement.id");

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_column_is_carried_along_its_row_and_its_index_counts_only_the_columns_that_move(string dir)
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Columns(dir));
        var from = await Point(page, "#h-total", fx: 0.5f);
        var start = await page.EvaluateAsync<double>("() => document.getElementById('h-total').getBoundingClientRect().top");

        // Total, carried over the first half of Customer — the inline-start half, on either side.
        await MouseDrag(page, from, await Point(page, "#h-customer", fx: dir == "rtl" ? 0.8f : 0.2f, fy: 0.9f), release: false);

        await Assertions.Expect(page.Locator("#h-total")).ToHaveAttributeAsync("data-dragging", "pointer");
        await Assertions.Expect(page.Locator("#h-customer")).ToHaveAttributeAsync("data-drop-edge", "before");
        // Still a cell in its row, moved along it and not up or down.
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('h-total').matches(':popover-open')"));
        Assert.Equal(start, await page.EvaluateAsync<double>("() => document.getElementById('h-total').getBoundingClientRect().top"), 0.5);
        var travelled = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.getElementById('h-total')).translate)");
        Assert.True(dir == "rtl" ? travelled > 100 : travelled < -100, $"The heading travelled {travelled}px.");

        await page.Mouse.UpAsync();
        var log = await Log(page);
        // In front of Customer: position 0 among the columns that move, not 1 among the cells.
        AssertDrop(log.Single(l => l.Name == "sedna-drop"), "total", "cols", "cols", index: 0, fromIndex: 2, keyboard: false);
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task Carried_off_its_row_a_column_fades_and_letting_go_puts_it_back()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Columns());
        await MouseDrag(page, await Point(page, "#h-total"), await Point(page, "#c-customer"), release: false);

        await Assertions.Expect(page.Locator("#h-total")).ToHaveAttributeAsync("data-dragging", "pointer");
        Assert.Null(await page.Locator("[data-drag-zone=cols]").GetAttributeAsync("data-drop-over"));
        Assert.Equal(0, await page.Locator("[data-drop-edge]").CountAsync());
        Assert.Equal("0.5", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('h-total')).opacity"));

        await page.Mouse.UpAsync();
        Assert.Equal(["sedna-dragstart", "sedna-dragend"], (await Log(page)).Select(l => l.Name));
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_sort_button_that_is_a_pointer_handle_drags_the_column_and_a_click_still_sorts()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Columns());

        await page.ClickAsync("#sort-state");
        Assert.Equal("[\"sort-state\"]", await page.EvaluateAsync<string>("() => JSON.stringify(sorts)"));
        Assert.Empty(await Log(page));

        await MouseDrag(page, await Point(page, "#sort-state"), await Point(page, "#h-customer", fx: 0.2f));
        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "state", "cols", "cols", index: 0, fromIndex: 1, keyboard: false);
        // The click that ends a drag is not a sort.
        Assert.Equal("[\"sort-state\"]", await page.EvaluateAsync<string>("() => JSON.stringify(sorts)"));

        // A press on the resize grip resizes, and lifts nothing.
        await Js.Run(page, "() => dragLog.length = 0");
        await MouseDrag(page, await Point(page, "#grip"), await Point(page, "#h-total"));
        Assert.Empty(await Log(page));
        Assert.NotEqual("", await page.EvaluateAsync<string>("() => document.getElementById('h-customer').style.getPropertyValue('--col-width')"));
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task From_the_keyboard_Space_on_the_heading_picks_it_up_and_Enter_on_its_sort_button_sorts()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Columns());

        await page.FocusAsync("#sort-customer");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("[\"sort-customer\"]", await page.EvaluateAsync<string>("() => JSON.stringify(sorts)"));
        Assert.Empty(await Log(page));

        await page.FocusAsync("#h-customer");
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(page.Locator("#h-customer")).ToHaveAttributeAsync("data-dragging", "keyboard");
        await Assertions.Expect(page.Locator("[data-drag-live]")).ToHaveTextAsync("Picked up Customer.");
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(page.Locator("[data-drag-live]")).ToHaveTextAsync(new Regex(@"^Customer: 3 of 3 in Columns\.\s?$"));
        await page.Keyboard.PressAsync("Space");

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "customer", "cols", "cols", index: 2, fromIndex: 0, keyboard: true);
        Assert.Equal("h-customer", await Focused(page));
        await AssertNothingLeftBehind(page);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task In_a_keyboard_grid_Space_carries_the_heading_and_the_arrows_are_the_drags(string dir)
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Columns(dir, role: "role=\"grid\"", tab: ""));
        await Js.Run(page, "() => document.getElementById('h-state').focus()");

        // Enter is the grid's: it moves into the cell's sort button and picks nothing up.
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("sort-state", await Focused(page));
        Assert.Empty(await Log(page));
        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("h-state", await Focused(page));

        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(page.Locator("#h-state")).ToHaveAttributeAsync("data-dragging", "keyboard");
        // Back towards the start of the row, which is to the right in RTL; the focus stays.
        await page.Keyboard.PressAsync(dir == "rtl" ? "ArrowRight" : "ArrowLeft");
        await Assertions.Expect(page.Locator("#h-customer")).ToHaveAttributeAsync("data-drop-edge", "before");
        Assert.Equal("h-state", await Focused(page));
        await page.Keyboard.PressAsync("Space");

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "state", "cols", "cols", index: 0, fromIndex: 1, keyboard: true);
        Assert.Equal("h-state", await Focused(page));
        // The grid is still the grid: the arrows move between cells again.
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("TD", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
        await AssertNothingLeftBehind(page);
    }

    // ── Calendar slots ─────────────────────────────────────────────────────────

    private const string SlotLive = """
        <p class="visually-hidden" aria-live="assertive" data-drag-live
           data-drag-pickup="Picked up {item} on {zone}."
           data-drag-move="{zone}."
           data-drag-drop="{item} moved to {zone}."
           data-drag-cancel="{item} stays on {zone}."></p>
        """;

    /// <summary>Two weeks of a month: every day a slot, every week's events a zone that takes nothing back.</summary>
    private const string MonthPills = """
        <button class="cal-event" type="button" data-drag-item="e1" data-drag-label="Stand-up" style="grid-column: 1; grid-row: 1">Stand-up</button>
        <button class="cal-event" type="button" data-drag-item="e2" data-drag-label="Stocktake" style="grid-column: 3 / span 3; grid-row: 1">Stocktake</button>
        """;

    private const string WeekBlock =
        """<div class="cal-block" tabindex="0" data-drag-item="b1" data-drag-label="Stand-up" style="--cal-start: 1; --cal-span: 1">Stand-up</div>""";

    private static string CalMonth(string dir = "ltr")
    {
        static string Row(int week, string events) =>
            "<div class=\"cal-row\">"
            + string.Concat(Enumerable.Range(1, 7).Select(i =>
            {
                var n = week * 7 + i;
                return $"""<div class="cal-day" data-drag-zone="d{n}" data-drag-group="cal" data-drag-axis="slot" data-drag-label="Day {n}"><span class="cal-day-num">{n}</span></div>""";
            }))
            + $"""<div class="cal-row-events" data-drag-zone="week-{week}" data-drag-group="cal" data-drag-accept="">{events}</div></div>""";

        var first = Row(0, MonthPills);
        var second = Row(1, "");
        return SlotLive + $"""<div dir="{dir}" class="cal-month" style="--cal-lanes: 2; width: 700px">{first}{second}</div>""";
    }

    /// <summary>Two days of half-hour slots, the first hour of the first day closed.</summary>
    private static string CalWeek()
    {
        static string Col(string day, bool closedFirstHour, string blocks) =>
            $"""<div class="cal-day-col" data-drag-zone="{day}" data-drag-group="cal" data-drag-accept=""><div class="cal-slots">"""
            + string.Concat(Enumerable.Range(0, 8).Select(s =>
                $"""<div class="cal-slot" data-drag-zone="{day}-{s}" data-drag-group="cal" data-drag-axis="slot" data-drag-label="{day} slot {s}"{(closedFirstHour && s < 2 ? " aria-disabled=\"true\"" : "")}></div>"""))
            + "</div>"
            + (closedFirstHour ? """<div class="cal-closed" style="--cal-span: 1"></div>""" : "")
            + blocks + "</div>";

        var mon = Col("mon", true, WeekBlock);
        var tue = Col("tue", false, "");
        return SlotLive + $"""
            <div class="cal-week" style="--cal-days: 2; --cal-hours: 4; --cal-hour-height: 48px; width: 420px">
              <div class="cal-week-body"><div class="cal-hours"></div>{mon}{tue}</div>
            </div>
            """;
    }

    private static void AssertSlotDrop(Logged drop, string item, string from, string to, string fromSlot, bool keyboard)
    {
        Assert.Equal("sedna-drop", drop.Name);
        Assert.Equal(item, drop.Detail.GetProperty("item").GetString());
        Assert.Equal(from, drop.Detail.GetProperty("from").GetString());
        Assert.Equal(to, drop.Detail.GetProperty("to").GetString());
        Assert.Equal(fromSlot, drop.Detail.GetProperty("fromSlot").GetString());
        Assert.Equal(keyboard, drop.Detail.GetProperty("keyboard").GetBoolean());
    }

    [Fact]
    public async Task A_pill_dropped_on_another_days_pill_lands_on_that_day_and_names_the_day_it_left()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(CalMonth());

        // Stand-up, from day 1 onto the middle of Stocktake, which covers days 3 to 5.
        await MouseDrag(page, await Point(page, "[data-drag-item=e1]"), await Point(page, "[data-drag-item=e2]"), release: false);
        await Assertions.Expect(page.Locator("[data-drag-zone=d4]")).ToHaveAttributeAsync("data-drop-over", "");
        await Assertions.Expect(page.Locator("[data-drag-zone=d4]")).ToHaveAttributeAsync("data-drop-state", "valid");
        await Assertions.Expect(page.Locator("[data-drag-zone=week-0]")).ToHaveAttributeAsync("data-drop-state", "invalid");
        // Only the day under the pill is outlined; the other days and the lanes are left alone,
        // and the pills that stay keep their full colour.
        var looks = await page.EvaluateAsync<string[]>("""
            () => [getComputedStyle(document.querySelector('[data-drag-zone=d4]')).outlineStyle,
                   getComputedStyle(document.querySelector('[data-drag-zone=d2]')).outlineStyle,
                   getComputedStyle(document.querySelector('[data-drag-zone=week-0]')).outlineStyle,
                   getComputedStyle(document.querySelector('[data-drag-item=e2]')).opacity]
            """);
        Assert.Equal(["solid", "none", "none", "1"], looks);
        await page.Mouse.UpAsync();

        var log = await Log(page);
        Assert.Equal(["sedna-dragstart", "sedna-drop", "sedna-dragend"], log.Select(l => l.Name));
        AssertSlotDrop(log[1], "e1", "week-0", "d4", fromSlot: "d1", keyboard: false);
        Assert.Equal("d4", log[2].Detail.GetProperty("zone").GetString());
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_span_held_by_a_later_day_names_that_day_and_a_drop_back_on_it_sends_nothing()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(CalMonth());

        // Stocktake held by its last day, day 5, and put down on day 9 in the next week.
        await MouseDrag(page, await Point(page, "[data-drag-item=e2]", fx: 0.85f), await Point(page, "[data-drag-zone=d9]"));
        AssertSlotDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "e2", "week-0", "d9", fromSlot: "d5", keyboard: false);

        // Put down on the day it was held by: nothing moved.
        await Js.Run(page, "() => dragLog.length = 0");
        var held = await Point(page, "[data-drag-item=e2]", fx: 0.5f);
        await MouseDrag(page, held, (held.X + 6, held.Y));
        var log = await Log(page);
        Assert.Equal(["sedna-dragstart", "sedna-dragend"], log.Select(l => l.Name));
        Assert.False(log[1].Detail.GetProperty("dropped").GetBoolean());
        await AssertNothingLeftBehind(page);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task From_the_keyboard_a_pill_starts_on_its_day_and_the_arrows_move_it_by_days_and_weeks(string dir)
    {
        if (NoBrowser) return;
        var page = await OpenDrag(CalMonth(dir));
        var live = page.Locator("[data-drag-live]");

        await page.Locator("[data-drag-item=e2]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(live).ToHaveTextAsync("Picked up Stocktake on Day 3.");
        await Assertions.Expect(page.Locator("[data-drag-zone=d3]")).ToHaveAttributeAsync("data-drop-over", "");

        // Right on screen is the next day, or the one before in RTL; down is a week on.
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(live).ToHaveTextAsync(dir == "rtl" ? "Day 2." : "Day 4.");
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(live).ToHaveTextAsync(dir == "rtl" ? "Day 9." : "Day 11.");
        await page.Keyboard.PressAsync("ArrowDown");   // no third week: it stays
        await Assertions.Expect(page.Locator(dir == "rtl" ? "[data-drag-zone=d9]" : "[data-drag-zone=d11]")).ToHaveAttributeAsync("data-drop-over", "");

        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(live).ToHaveTextAsync(dir == "rtl" ? "Stocktake moved to Day 9." : "Stocktake moved to Day 11.");
        AssertSlotDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "e2", "week-0", dir == "rtl" ? "d9" : "d11", fromSlot: "d3", keyboard: true);
        await AssertNothingLeftBehind(page);

        // Picked up and put straight back down: no drop, and it says it stayed.
        await Js.Run(page, "() => dragLog.length = 0");
        await page.Locator("[data-drag-item=e1]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(live).ToHaveTextAsync("Stand-up stays on Day 1.");
        Assert.Equal(["sedna-dragstart", "sedna-dragend"], (await Log(page)).Select(l => l.Name));
    }

    [Fact]
    public async Task A_block_moves_to_another_time_and_day_and_a_closed_slot_refuses_it()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(CalWeek());

        // Held a quarter of the way down — the 1:00 to 1:30 slot — and put down at 2:30 on Tuesday.
        await MouseDrag(page, await Point(page, "[data-drag-item=b1]", fy: 0.25f), await Point(page, "[data-drag-zone=tue-5]"));
        AssertSlotDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "b1", "mon", "tue-5", fromSlot: "mon-2", keyboard: false);

        // Into the closed hour: marked as refusing, and letting go there moves nothing.
        await Js.Run(page, "() => dragLog.length = 0");
        await MouseDrag(page, await Point(page, "[data-drag-item=b1]"), await Point(page, "[data-drag-zone=mon-0]"), release: false);
        await Assertions.Expect(page.Locator("[data-drag-zone=mon-0]")).ToHaveAttributeAsync("data-drop-state", "invalid");
        await Assertions.Expect(page.Locator("[data-drag-zone=mon-0]")).ToHaveAttributeAsync("data-drop-over", "");
        await page.Mouse.UpAsync();
        Assert.Equal(["sedna-dragstart", "sedna-dragend"], (await Log(page)).Select(l => l.Name));
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task From_the_keyboard_a_block_moves_by_slots_down_and_days_across_and_never_into_closed_hours()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(CalWeek());
        var live = page.Locator("[data-drag-live]");

        await page.Locator("[data-drag-item=b1]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(live).ToHaveTextAsync("Picked up Stand-up on mon slot 2.");

        // Up from the first open slot: the closed hour above takes nothing, so it stays.
        await page.Keyboard.PressAsync("ArrowUp");
        await Assertions.Expect(page.Locator("[data-drag-zone=mon-2]")).ToHaveAttributeAsync("data-drop-over", "");
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(live).ToHaveTextAsync("mon slot 3.");
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(live).ToHaveTextAsync("tue slot 3.");
        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("ArrowUp");
        await Assertions.Expect(live).ToHaveTextAsync("tue slot 0.");
        await page.Keyboard.PressAsync("Enter");

        AssertSlotDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "b1", "mon", "tue-0", fromSlot: "mon-2", keyboard: true);
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_drop_that_is_not_on_a_slot_says_fromSlot_null()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Queue);
        await MouseDrag(page, await Point(page, "[data-drag-item=a]"), await Point(page, "[data-drag-item=c]", fy: 0.8f));
        var drop = (await Log(page)).Single(l => l.Name == "sedna-drop");
        Assert.Equal(JsonValueKind.Null, drop.Detail.GetProperty("fromSlot").ValueKind);
    }

    // ── From a zone that takes nothing back ────────────────────────────────────

    private const string AroundASource = Live + """
        <div style="display: grid; gap: 12px; width: 260px">
          <div class="drag-list" data-drag-zone="first" data-drag-group="g" data-drag-accept="row" aria-label="First"><span class="drag-empty">First</span></div>
          <div class="drag-list" data-drag-zone="second" data-drag-group="g" data-drag-accept="row" aria-label="Second"><span class="drag-empty">Second</span></div>
          <ul class="drag-list" data-drag-zone="rows" data-drag-group="g" data-drag-accept="" aria-label="Rows">
            <li class="drag-item" data-drag-item="r1" data-drag-type="row" tabindex="0">Row one</li>
            <li class="drag-item" data-drag-item="r2" data-drag-type="row" tabindex="0">Row two</li>
          </ul>
          <div class="drag-list" data-drag-zone="third" data-drag-group="g" data-drag-accept="row" aria-label="Third"><span class="drag-empty">Third</span></div>
        </div>
        """;

    [Fact]
    public async Task Page_Up_and_Page_Down_from_a_zone_that_takes_nothing_back_reach_the_nearest_zone_each_way()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(AroundASource);

        await page.Locator("[data-drag-item=r2]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("PageUp");
        await Assertions.Expect(page.Locator("[data-drag-zone=second]")).ToHaveAttributeAsync("data-drop-over", "");
        await page.Keyboard.PressAsync("PageUp");
        await Assertions.Expect(page.Locator("[data-drag-zone=first]")).ToHaveAttributeAsync("data-drop-over", "");
        await page.Keyboard.PressAsync("Escape");

        await page.Locator("[data-drag-item=r2]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("PageDown");
        await Assertions.Expect(page.Locator("[data-drag-zone=third]")).ToHaveAttributeAsync("data-drop-over", "");
        await page.Keyboard.PressAsync("Enter");

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "r2", "rows", "third", index: 0, fromIndex: 1, keyboard: true);
        await AssertNothingLeftBehind(page);
    }

    // ── Copies ─────────────────────────────────────────────────────────────────

    private const string Palette = Live + """
        <div style="display: flex; gap: 48px; align-items: flex-start">
          <ul class="drag-list" style="width: 220px" data-drag-zone="palette" data-drag-group="show" data-drag-effect="copy" aria-label="Sequences">
            <li class="drag-item" data-drag-item="q1" data-drag-type="sequence" tabindex="0">Spring promo</li>
            <li class="drag-item" data-drag-item="q2" data-drag-type="sequence" data-drag-label="Opening hours loop" tabindex="0">Opening hours</li>
            <li class="drag-item" data-drag-item="q3" data-drag-type="sequence" data-drag-effect="move" tabindex="0">Closing loop</li>
          </ul>
          <div style="display: grid; gap: 12px; width: 200px">
            <div class="drag-list" data-drag-zone="button-1" data-drag-group="show" data-drag-accept="sequence" aria-label="Button 1"
                 data-drag-move="{item} onto {zone}." data-drag-drop="{item} put on {zone}."><span class="drag-empty">Button 1</span></div>
            <div class="drag-list" data-drag-zone="button-2" data-drag-group="show" data-drag-accept="sequence" aria-label="Button 2"
                 data-drag-move="{item} onto {zone}." data-drag-drop="{item} put on {zone}."><span class="drag-empty">Button 2</span></div>
          </div>
        </div>
        <div style="height: 2000px"></div>
        """;

    private static async Task<int[]> Box(IPage page, string selector) =>
        await page.EvaluateAsync<int[]>(
            $"() => {{ const r = document.querySelector('{selector}').getBoundingClientRect(); return [r.left, r.top, r.width, r.height].map(Math.round); }}");

    [Fact]
    public async Task A_copy_leaves_the_original_in_place_carries_its_name_and_reports_the_effect()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Palette);
        var original = await Box(page, "[data-drag-item=q1]");
        var others = await RowTops(page, "[data-drag-item]:not([data-drag-item=q1])");

        await MouseDrag(page, await Point(page, "[data-drag-item=q1]"), await Point(page, "[data-drag-zone=button-2]"), release: false);

        var item = page.Locator("[data-drag-item=q1]");
        await Assertions.Expect(item).ToHaveAttributeAsync("data-drag-copy", "");
        // The zone it came from does not take it back: the original is still there.
        await Assertions.Expect(page.Locator("[data-drag-zone=palette]")).ToHaveAttributeAsync("data-drop-state", "invalid");
        await Assertions.Expect(page.Locator("[data-drag-zone=button-2]")).ToHaveAttributeAsync("data-drop-over", "");
        // Nothing has moved: not the original, not the rows around it.
        Assert.Equal(original, await Box(page, "[data-drag-item=q1]"));
        Assert.Equal(others, await RowTops(page, "[data-drag-item]:not([data-drag-item=q1])"));
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('[data-drag-item=q1]')).translate"));
        // What travels is the item's name, in a box carried by the pointer.
        Assert.Equal("\"Spring promo\"", await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('[data-drag-item=q1]'), '::before').content"));
        Assert.NotEqual("none", await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('[data-drag-item=q1]'), '::before').translate"));

        // Scrolled while it is carried, the original stays over its own place.
        await Js.Run(page, "() => window.scrollBy(0, 40)");
        await Js.Frames(page);
        var scrolled = await Box(page, "[data-drag-item=q1]");
        Assert.Equal(original[0], scrolled[0]);
        Assert.InRange(scrolled[1], original[1] - 41, original[1] - 39);
        await Js.Run(page, "() => window.scrollBy(0, -40)");
        await Js.Frames(page);

        await page.Mouse.UpAsync();

        var log = await Log(page);
        Assert.Equal(["sedna-dragstart", "sedna-drop", "sedna-dragend"], log.Select(l => l.Name));
        Assert.Equal("copy", log[0].Detail.GetProperty("effect").GetString());
        AssertDrop(log[1], "q1", "palette", "button-2", index: 0, fromIndex: 0, keyboard: false, effect: "copy");
        // The original never left, and dragend says where it still is.
        Assert.Equal("palette", log[2].Detail.GetProperty("zone").GetString());
        Assert.Equal(0, log[2].Detail.GetProperty("index").GetInt32());
        Assert.True(log[2].Detail.GetProperty("dropped").GetBoolean());
        Assert.Equal("copy", log[2].Detail.GetProperty("effect").GetString());
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_copy_from_the_keyboard_is_announced_in_the_target_zones_own_words()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Palette);
        var live = page.Locator("[data-drag-live]");

        await page.Locator("[data-drag-item=q2]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(page.Locator("[data-drag-item=q2]")).ToHaveAttributeAsync("data-drag-copy", "");
        await Assertions.Expect(live).ToHaveTextAsync("Picked up Opening hours loop.");

        await page.Keyboard.PressAsync("PageDown");
        await Assertions.Expect(live).ToHaveTextAsync(new Regex(@"^Opening hours loop onto Button 1\.\s?$"));
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(live).ToHaveTextAsync(new Regex(@"^Opening hours loop onto Button 2\.\s?$"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(live).ToHaveTextAsync("Opening hours loop put on Button 2.");

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "q2", "palette", "button-2", index: 0, fromIndex: 1, keyboard: true, effect: "copy");
        // The original kept the focus; nothing was moved away from it.
        await Assertions.Expect(page.Locator("[data-drag-item=q2]")).ToBeFocusedAsync();
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task An_item_that_says_move_in_a_copying_zone_moves()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Palette);

        await page.Locator("[data-drag-item=q3]").FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(page.Locator("[data-drag-zone=palette]")).ToHaveAttributeAsync("data-drop-state", "valid");
        Assert.Null(await page.Locator("[data-drag-item=q3]").GetAttributeAsync("data-drag-copy"));
        await page.Keyboard.PressAsync("Escape");

        var log = await Log(page);
        Assert.Equal("move", log[0].Detail.GetProperty("effect").GetString());
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_finger_carries_a_copy_and_leaves_the_original()
    {
        // Raw touch input needs CDP (see BrowserEngine).
        if (NoBrowser || !BrowserEngine.HasRawTouch) return;
        var page = await OpenDrag(Palette, hasTouch: true);
        var cdp = await page.Context.NewCDPSessionAsync(page);
        var original = await Box(page, "[data-drag-item=q1]");

        var from = await Point(page, "[data-drag-item=q1]");
        var to = await Point(page, "[data-drag-zone=button-1]");
        await Touch(cdp, "touchStart", from);
        await Assertions.Expect(page.Locator("[data-drag-item=q1]")).ToHaveAttributeAsync("data-drag-copy", "");
        await TouchMove(cdp, from, to, steps: 10);
        await Assertions.Expect(page.Locator("[data-drag-zone=button-1]")).ToHaveAttributeAsync("data-drop-over", "");
        Assert.Equal(original, await Box(page, "[data-drag-item=q1]"));
        await Touch(cdp, "touchEnd", null);

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "q1", "palette", "button-1", index: 0, fromIndex: 0, keyboard: false, effect: "copy");
        await AssertNothingLeftBehind(page);
    }
}
