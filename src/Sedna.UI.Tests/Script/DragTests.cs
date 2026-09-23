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
        await page.EvaluateAsync("""
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
        await page.WaitForTimeoutAsync(50);
        if (release) await page.Mouse.UpAsync();
    }

    private static async Task AssertNothingLeftBehind(IPage page)
    {
        Assert.Equal(0, await page.EvaluateAsync<int>("() => nodesMoved"));
        Assert.Equal(0, await page.EvaluateAsync<int>(
            "() => document.querySelectorAll('[data-dragging], [data-drop-state], [data-drop-over], [data-drop-edge], [popover], [data-drag-slot], [data-drag-carried], [data-drag-count]').length"));
        Assert.Equal(0, await page.EvaluateAsync<int>(
            "() => [...document.querySelectorAll('[data-drag-item]')].filter(el => el.hasAttribute('style') && el.style.getPropertyValue('--drag-x')).length"));
    }

    private static Task<string> RowTops(IPage page, string selector = "[data-drag-item]:not([data-dragging])") =>
        page.EvaluateAsync<string>(
            $"() => [...document.querySelectorAll('{selector}')].map(el => {{ const r = el.getBoundingClientRect(); return el.getAttribute('data-drag-item') + '@' + Math.round(r.left) + ',' + Math.round(r.top); }}).filter(s => !s.startsWith('a@')).join(' ')");

    private static async Task<string> Order(IPage page, string zone) =>
        await page.EvaluateAsync<string>(
            $"() => [...document.querySelector('[data-drag-zone=\"{zone}\"]').children].map(el => el.getAttribute('data-drag-item')).filter(Boolean).join(',')");

    private static void AssertDrop(Logged drop, string item, string from, string to, int index, int fromIndex, bool keyboard)
    {
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
        await page.EvaluateAsync("() => { window.clicks = 0; document.getElementById('open-b').addEventListener('click', () => clicks++); }");

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
        await page.EvaluateAsync("() => document.addEventListener('sedna-dragstart', e => e.preventDefault())");

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
        await page.EvaluateAsync("() => dragLog.length = 0");
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
        await page.EvaluateAsync("() => dragLog.length = 0");
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

        await page.EvaluateAsync("() => dragLog.length = 0");
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
        await page.WaitForTimeoutAsync(900);

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
        if (NoBrowser) return;
        var page = await OpenDrag(Queue, hasTouch: true);
        var cdp = await page.Context.NewCDPSessionAsync(page);

        var from = await Point(page, "[data-drag-item=a]");
        var to = await Point(page, "[data-drag-item=c]", fy: 0.8f);

        await Touch(cdp, "touchStart", from);
        await page.WaitForTimeoutAsync(450);
        await Assertions.Expect(page.Locator("[data-drag-item=a]")).ToHaveAttributeAsync("data-dragging", "pointer");

        await TouchMove(cdp, from, to, steps: 10);
        await page.WaitForTimeoutAsync(50);
        await Assertions.Expect(page.Locator("[data-drag-item=d]")).ToHaveAttributeAsync("data-drop-edge", "before");
        await Touch(cdp, "touchEnd", null);

        AssertDrop((await Log(page)).Single(l => l.Name == "sedna-drop"), "a", "queue", "queue", index: 2, fromIndex: 0, keyboard: false);
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_finger_that_moves_before_the_hold_is_scrolling_and_lifts_nothing()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Queue, hasTouch: true);
        var cdp = await page.Context.NewCDPSessionAsync(page);

        var from = await Point(page, "[data-drag-item=a]");
        await Touch(cdp, "touchStart", from);
        await TouchMove(cdp, from, (from.X, from.Y + 60), steps: 6);
        await page.WaitForTimeoutAsync(450);
        await Touch(cdp, "touchEnd", null);

        Assert.Empty(await Log(page));
        await AssertNothingLeftBehind(page);
    }

    [Fact]
    public async Task A_finger_on_a_handle_lifts_at_once()
    {
        if (NoBrowser) return;
        var page = await OpenDrag(Nested, hasTouch: true);
        var cdp = await page.Context.NewCDPSessionAsync(page);

        var from = await Point(page, "#grip-s2");
        var to = await Point(page, "[data-drag-item=s1]", fy: 0.2f);
        await Touch(cdp, "touchStart", from);
        await TouchMove(cdp, from, to, steps: 10);
        await page.WaitForTimeoutAsync(50);
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
        await page.EvaluateAsync("""
            () => document.addEventListener('sedna-drop', e => {
                const zone = document.querySelector(`[data-drag-zone="${e.detail.to}"]`);
                const item = document.querySelector(`[data-drag-item="${e.detail.item}"]`);
                const rest = [...zone.children].filter(el => el !== item && el.hasAttribute('data-drag-item'));
                setTimeout(() => zone.insertBefore(item, rest[e.detail.index] || null), 30);
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
        await page.EvaluateAsync("() => { dragLog.length = 0; document.getElementById('pick-b1').checked = true; }");
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
        await page.EvaluateAsync("""
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
}
