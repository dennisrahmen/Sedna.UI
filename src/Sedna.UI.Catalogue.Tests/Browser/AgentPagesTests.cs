using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The agent pages through a real Blazor circuit: the orb starting in the markup the
/// interactive render left, and the Mochi playground writing the attributes it shows.
/// </summary>
/// <remarks>
/// The library's suite pins the orb and Mochi in a plain page. What only a running app shows
/// is that a prerendered page hands its orbs to the drawing once the circuit is up, and that a
/// re-render changes Mochi's attributes in place — the element stays, so its joints ease.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class AgentPagesTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open(string route, List<string> errors)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        await page.GotoAsync(app.Url(route), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    [Fact]
    public async Task The_orbs_draw_once_the_page_is_interactive()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/orb", errors);

        await page.Locator("[data-orb='searching']").First.ScrollIntoViewIfNeededAsync();
        await page.WaitForFunctionAsync("""
            () => {
                const c = document.querySelector("[data-orb='searching'] > canvas");
                if (!c || !c.width) return false;
                const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
                let n = 0;
                for (let i = 3; i < d.length; i += 4) if (d[i] > 8) n++;
                return n > 64;
            }
            """, null, new() { Timeout = 15_000 });

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_playground_writes_the_attributes_it_shows()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/mochi", errors);
        var mochi = page.Locator("#mochi-playground");
        var section = page.Locator("#playground-section");
        var element = await mochi.ElementHandleAsync();

        async Task Choose(string group, string value) =>
            await section.GetByRole(AriaRole.Group, new() { Name = group })
                .GetByRole(AriaRole.Button, new() { Name = value, Exact = true }).ClickAsync();

        await Choose("Action", "wave");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("data-action", "wave");
        await Choose("Perch", "hang");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("data-perch", "hang");
        await Choose("Mood", "worried");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("data-mood", "worried");
        await Choose("Tone", "sky");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("data-tone", "sky");
        await Assertions.Expect(section.Locator("pre code"))
            .ToContainTextAsync("data-perch=\"hang\" data-action=\"wave\" data-mood=\"worried\" data-tone=\"sky\"");

        // The defaults are no attribute at all, as an app would write them.
        await Choose("Perch", "none");
        await Choose("Mood", "auto");
        await Choose("Tone", "pink");
        await Assertions.Expect(mochi).Not.ToHaveAttributeAsync("data-perch", new System.Text.RegularExpressions.Regex(".*"));
        await Assertions.Expect(mochi).Not.ToHaveAttributeAsync("data-mood", new System.Text.RegularExpressions.Regex(".*"));
        await Assertions.Expect(mochi).Not.ToHaveAttributeAsync("data-tone", new System.Text.RegularExpressions.Regex(".*"));

        // Pointing names the button it points at.
        await Choose("Perch", "point");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex("position-anchor: --mochi-playground"));

        // The same element throughout, so every change was a transition rather than a new drawing.
        Assert.True(await page.EvaluateAsync<bool>("el => el.isConnected && el === document.getElementById('mochi-playground')", element));
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_failed_tool_call_is_open_and_a_question_waits_for_its_answer()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/agent-steps", errors);

        // Open from the markup alone, as the example writes it, through the interactive render.
        await Assertions.Expect(page.Locator(".agent-tool[data-state='failed']").First).ToHaveAttributeAsync("open", "");
        await Assertions.Expect(page.Locator(".agent-tool[data-state='failed'] .agent-tool-error").First).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".agent-tool[data-state='done'] .agent-tool-body").First).ToBeHiddenAsync();
        await page.Locator(".agent-tool[data-state='done'] > summary").First.ClickAsync();
        await Assertions.Expect(page.Locator(".agent-tool[data-state='done'] .agent-tool-body").First).ToBeVisibleAsync();

        await Assertions.Expect(page.Locator(".agent-approval--question [type='submit']").First).ToBeDisabledAsync();
        await Assertions.Expect(page.Locator(".reasoning[data-state='running'] .reasoning-label").First)
            .ToHaveCSSAsync("animation-name", "text-shimmer");

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Resting_on_the_context_ring_opens_what_the_context_holds()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/agent-chat", errors);
        var card = page.Locator("#context-window");

        await Assertions.Expect(card).ToBeHiddenAsync();
        var ring = page.Locator("[data-popover='context-window']");
        await ring.ScrollIntoViewIfNeededAsync();
        await ring.HoverAsync();
        await Assertions.Expect(card).ToBeVisibleAsync();
        await Assertions.Expect(card.Locator(".context-usage-foot")).ToContainTextAsync("Total cost");

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    // ── The prompt's model choice, toolbar, docked composer and context ring ──────────────

    [Theory]
    [InlineData("/agent-chat")]
    [InlineData("/assistant-panels")]
    public async Task No_id_on_the_page_is_written_twice(string route)
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(route, errors);

        // A heading's anchor is the example's title as a slug, so an example's own id that
        // equals one is the page's second: `popovertarget` then finds the heading, and the
        // button does nothing.
        var duplicates = await page.EvaluateAsync<string[]>("""
            () => {
                const seen = new Map();
                for (const el of document.querySelectorAll('[id]')) seen.set(el.id, (seen.get(el.id) ?? 0) + 1);
                return [...seen].filter(([, n]) => n > 1).map(([id]) => id);
            }
            """);

        Assert.Empty(duplicates);
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Theory]
    [InlineData("[data-example]:has(#model-choice) button[popovertarget='model-choice']", "#model-choice")]
    [InlineData("[data-example]:has(#prompt-field) button[popovertarget='pf-models']", "#pf-models")]
    [InlineData("[data-example]:has(#prompt-toolbar) button[popovertarget='pt-models']", "#pt-models")]
    public async Task The_model_button_opens_the_list_of_models_under_itself(string trigger, string panel)
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/agent-chat", errors);
        var button = page.Locator(trigger);
        var list = page.Locator(panel);

        await button.ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(list).ToBeHiddenAsync();
        await button.ClickAsync();
        await Assertions.Expect(list).ToBeVisibleAsync();
        await Assertions.Expect(list.Locator("[role=option][aria-selected=true]")).ToContainTextAsync("Balanced");

        // Under the button, starting at its edge: anchored the way every other panel is.
        await Js.Settle(page);
        var gap = await page.EvaluateAsync<double[]>("""
            ([t, p]) => {
                const a = document.querySelector(t).getBoundingClientRect(), b = document.querySelector(p).getBoundingClientRect();
                return [b.top - a.bottom, Math.abs(b.left - a.left)];
            }
            """, new[] { trigger, panel });
        Assert.InRange(gap[0], 0, 16);
        Assert.InRange(gap[1], 0, 2);

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(list).ToBeHiddenAsync();
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_labelled_button_beside_send_keeps_the_control_radius_and_only_icons_are_round()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/agent-chat", errors);

        var radii = await page.EvaluateAsync<string[][]>("""
            () => [...document.querySelectorAll('.chat-composer-send > .btn')].map(b => [
                b.classList.contains('btn--icon') ? 'icon' : 'labelled',
                getComputedStyle(b).borderTopLeftRadius,
                String(b.getBoundingClientRect().width)])
            """);

        Assert.Contains(radii, r => r[0] == "labelled");
        foreach (var r in radii)
        {
            // An engine may report the radius as the 50% that was written or as the pixels it comes to.
            var radius = r[1].EndsWith('%') ? double.Parse(r[1].TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) * double.Parse(r[2], System.Globalization.CultureInfo.InvariantCulture) / 100
                : double.Parse(r[1].Replace("px", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture);
            if (r[0] == "icon") Assert.Equal(double.Parse(r[2], System.Globalization.CultureInfo.InvariantCulture) / 2, radius, 1);
            else Assert.True(radius <= 12, $"a labelled button is {r[1]} round");
        }
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_prompt_toolbar_is_one_row_inside_the_frame_with_the_attachments_above_the_text()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/agent-chat", errors);
        var form = page.Locator("[data-example]:has(#prompt-toolbar) form.chat-composer--prompt");
        await form.ScrollIntoViewIfNeededAsync();

        var m = await page.EvaluateAsync<System.Text.Json.JsonElement>("""
            () => {
                const f = document.querySelector('[data-example]:has(#prompt-toolbar) form.chat-composer--prompt');
                const r = e => e.getBoundingClientRect();
                const frame = r(f);
                const tools = [...f.querySelectorAll('.chat-composer-tools button')]
                    .filter(b => b.offsetParent && !b.closest('.menu') && !b.closest('[data-when]'));
                const send = f.querySelector('.chat-composer-send > .btn--solid');
                const row = [...tools, send].map(b => ({ top: r(b).top, bottom: r(b).bottom, left: r(b).left, right: r(b).right }));
                const files = r(f.querySelector('.chat-composer-files'));
                const field = r(f.querySelector('textarea'));
                return { frame: { top: frame.top, bottom: frame.bottom, left: frame.left, right: frame.right }, row, filesBottom: files.bottom, fieldTop: field.top };
            }
            """);

        var frame = m.GetProperty("frame");
        double F(string n) => frame.GetProperty(n).GetDouble();
        var centres = new List<double>();
        foreach (var b in m.GetProperty("row").EnumerateArray())
        {
            Assert.InRange(b.GetProperty("top").GetDouble(), F("top"), F("bottom"));
            Assert.InRange(b.GetProperty("bottom").GetDouble(), F("top"), F("bottom"));
            Assert.InRange(b.GetProperty("left").GetDouble(), F("left"), F("right"));
            Assert.InRange(b.GetProperty("right").GetDouble(), F("left"), F("right"));
            centres.Add((b.GetProperty("top").GetDouble() + b.GetProperty("bottom").GetDouble()) / 2);
        }
        Assert.True(centres.Max() - centres.Min() <= 1, $"the toolbar's controls stand on {centres.Max() - centres.Min()}px of different lines");
        var heights = await page.EvaluateAsync<double[]>("""
            () => [...document.querySelectorAll('[data-example]:has(#prompt-toolbar) .chat-composer-tools .btn')]
                .filter(b => b.offsetParent && !b.closest('.menu')).map(b => b.getBoundingClientRect().height)
            """);
        Assert.True(heights.Length >= 5 && heights.Max() - heights.Min() <= 0.5, "the toolbar's buttons are not all one height");

        // The attachments are in the frame, above the field, with nothing between them but the frame's gap.
        Assert.True(m.GetProperty("filesBottom").GetDouble() <= m.GetProperty("fieldTop").GetDouble());
        Assert.True(m.GetProperty("fieldTop").GetDouble() - m.GetProperty("filesBottom").GetDouble() <= 16);

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_composer_in_a_docked_panel_is_set_in_from_the_panels_edges()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/assistant-panels", errors);
        await page.Locator("[data-example]:has(#assistant-beside-the-page) .topbar-btn").ClickAsync();
        var dock = page.Locator("#dock-assistant");
        await Assertions.Expect(dock).ToBeVisibleAsync();
        await Js.Settle(page);

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const d = document.getElementById('dock-assistant').getBoundingClientRect();
                const c = document.querySelector('#dock-assistant .chat-composer--prompt').getBoundingClientRect();
                return [c.left - d.left, d.right - c.right, d.bottom - c.bottom];
            }
            """);

        // Its rounded corners do not meet the panel's straight lines.
        foreach (var inset in m) Assert.True(inset >= 6, $"the composer is {inset}px from the panel's edge");

        // And the panel's own head, which is part of its frame, has no corner of its own.
        var head = await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#dock-assistant .drawer-header')).borderRadius");
        Assert.Equal("0px", head);
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_context_ring_has_the_same_room_either_side()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/agent-chat", errors);
        var button = page.Locator("[data-popover='context-window']");
        await button.ScrollIntoViewIfNeededAsync();

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const b = document.querySelector("[data-popover='context-window']");
                const ring = b.querySelector('.context-ring').getBoundingClientRect(), fig = b.querySelector('.text-nums').getBoundingClientRect(), box = b.getBoundingClientRect();
                return [ring.left - box.left, fig.left - ring.right];
            }
            """);

        // The figure is no closer to the ring than the button's own edge is.
        Assert.InRange(m[1], m[0] - 3, m[0] + 3);
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_version_pagers_arrows_stand_off_the_label_they_sit_beside()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/assistant-panels", errors);
        var pager = page.Locator("[data-example]:has(#file-beside-the-conversation) .chat-versions");
        await pager.ScrollIntoViewIfNeededAsync();

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const p = document.querySelector('[data-example]:has(#file-beside-the-conversation) .chat-versions');
                const [prev, next] = p.querySelectorAll('.btn'), label = p.querySelector(':scope > span').getBoundingClientRect();
                const a = prev.getBoundingClientRect(), b = next.getBoundingClientRect();
                return [label.left - a.right, b.left - label.right, a.width, a.height, Number.parseFloat(getComputedStyle(prev).borderTopLeftRadius)];
            }
            """);

        // The hover fill and the focus ring are the button's box, so the room is between boxes.
        Assert.True(m[0] >= 3 && m[1] >= 3, $"the arrows are {m[0]}px and {m[1]}px from the label");
        Assert.InRange(m[2], 24, 24.5);          // still a target of 24px
        Assert.Equal(m[2], m[3], 0.5);
        Assert.True(m[4] <= 8);                   // a corner of its own, not a pill around the label
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_hint_in_the_prompts_toolbar_starts_where_the_text_does()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/agent-chat", errors);
        await page.Locator("#ghost-hint").ScrollIntoViewIfNeededAsync();

        var m = await page.EvaluateAsync<double[]>("""
            () => {
                const f = document.querySelector('[data-example]:has(#suggested-completion) form');
                const t = f.querySelector('textarea'), cs = getComputedStyle(t);
                const hint = document.getElementById('ghost-hint');
                const range = document.createRange(); range.selectNodeContents(hint.querySelector('kbd'));
                return [t.getBoundingClientRect().left + Number.parseFloat(cs.paddingLeft) + Number.parseFloat(cs.borderLeftWidth), hint.getBoundingClientRect().left + Number.parseFloat(getComputedStyle(hint).paddingLeft)];
            }
            """);

        Assert.Equal(m[0], m[1], 1.5);
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }
}
