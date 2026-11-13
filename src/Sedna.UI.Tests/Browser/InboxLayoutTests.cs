using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// The notification inbox: a closed panel stays closed, the panel fits its screen, the count
/// sits on the bell's corner and the unread dot in the item's gutter in either direction, and
/// the row opens its notification while its own controls stay theirs.
/// </summary>
public class InboxLayoutTests : ScriptTestBase
{
    private static string Inbox(int items = 2) => $$"""
        <div>
          <header class="topbar">
            <div class="topbar-spacer"></div>
            <button class="topbar-btn" id="bell" type="button" popovertarget="inbox" aria-label="Notifications, 2 unread">
              <span class="count-host" id="glyph"><i class="ri-notification-3-line" aria-hidden="true"></i><span class="count-badge" id="count" aria-hidden="true">2</span></span>
            </button>
          </header>
          <div class="popover inbox" id="inbox" popover role="dialog" aria-label="Notifications">
            <div class="inbox-head" id="head">
              <strong class="inbox-title">Notifications</strong>
              <button class="btn btn--ghost btn--sm" id="all-read" type="button">Mark all as read</button>
            </div>
            <div class="tabs inbox-tabs" role="tablist" data-tabs>
              <button class="tab" role="tab" aria-selected="true" id="t-all" aria-controls="p-all" type="button">All</button>
              <button class="tab" role="tab" aria-selected="false" id="t-unread" aria-controls="p-unread" type="button" tabindex="-1">Unread</button>
            </div>
            <div class="tab-panel" role="tabpanel" id="p-all" aria-labelledby="t-all">
              <ul class="inbox-list" id="list">
                {{string.Concat(Enumerable.Range(1, items).Select(i => $"""
                  <li class="inbox-item{(i == 1 ? " inbox-item--unread" : "")}" id="item-{i}">
                    <span class="avatar" id="avatar-{i}">AF</span>
                    <div class="inbox-item-main">
                      <a class="inbox-item-title" id="title-{i}" href="#open-{i}">Alex Fischer assigned ORD-{4180 + i} to you</a>
                      <span class="inbox-item-text">Northwind Retail · 3 items, leaves the dock at 14:30, after the cold-chain check.</span>
                      <time class="inbox-item-time">5 minutes ago</time>
                      <div class="inbox-item-actions"><button class="btn btn--sm" id="accept-{i}" type="button">Accept</button></div>
                    </div>
                    <div class="menu-anchor inbox-item-menu">
                      <button class="btn btn--ghost btn--icon btn--sm" id="more-{i}" type="button" data-menu-toggle aria-expanded="false" aria-label="More"><i class="ri-more-2-fill"></i></button>
                      <div class="menu" hidden><button class="menu-item" type="button">Archive</button></div>
                    </div>
                  </li>
                  """))}}
              </ul>
            </div>
            <div class="tab-panel" role="tabpanel" id="p-unread" aria-labelledby="t-unread" hidden></div>
            <div class="inbox-foot"><a href="#prefs">Notification preferences</a></div>
          </div>
        </div>
        """;

    private async Task<IPage> OpenInbox(string dir = "ltr", int width = 1280, int items = 2)
    {
        var (page, _) = await OpenStyled(Inbox(items));
        await page.SetViewportSizeAsync(width, 800);
        await Js.Run(page, $"() => {{ document.documentElement.dir = '{dir}'; }}");
        return page;
    }

    [Fact]
    public async Task A_closed_panel_stays_closed_and_the_bell_opens_it()
    {
        if (NoBrowser) return;
        // `.inbox` lays out a panel, and a class that set `display` would outrank the UA's
        // rule for a closed popover and show it on every page load.
        var page = await OpenInbox();

        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('inbox')).display"));
        await page.Locator("#bell").ClickAsync();
        await Assertions.Expect(page.Locator("#inbox")).ToBeVisibleAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('inbox').matches(':popover-open')"));
    }

    [Theory]
    [InlineData("ltr", 1280)]
    [InlineData("rtl", 1280)]
    [InlineData("ltr", 375)]
    [InlineData("rtl", 375)]
    public async Task The_panel_opens_under_the_bell_and_stays_on_the_screen(string dir, int width)
    {
        if (NoBrowser) return;
        var page = await OpenInbox(dir, width);
        await page.Locator("#bell").ClickAsync();
        await Assertions.Expect(page.Locator("#inbox")).ToBeVisibleAsync();

        var panel = (await page.Locator("#inbox").BoundingBoxAsync())!;
        var bell = (await page.Locator("#bell").BoundingBoxAsync())!;
        Assert.True(panel.X >= 0 && panel.X + panel.Width <= width + 0.5,
            $"The panel runs off a {width}px screen: {panel.X:0} to {panel.X + panel.Width:0}.");
        Assert.True(panel.Y >= bell.Y + bell.Height - 0.5, "The panel does not open under the bell.");

        if (width > 560)
            Assert.True(Math.Abs(panel.Width - 380) < 1, $"On a wide screen the panel is {panel.Width}px, not its 380px.");
        else
            Assert.True(panel.Width > width - 48 && panel.Width < width, $"On a phone the panel should take the screen's width less a margin; it is {panel.Width}px of {width}.");

        // Rows meet the panel's edges; nothing in it is wider than it.
        var overflow = await page.EvaluateAsync<double>("() => { const p = document.getElementById('inbox'); return p.scrollWidth - p.clientWidth; }");
        Assert.True(overflow <= 0.5, $"Something in the panel runs {overflow}px past its edge.");
        var item = (await page.Locator("#item-1").BoundingBoxAsync())!;
        Assert.True(Math.Abs(item.Width - (panel.Width - 2)) < 1, "An item does not span the panel.");
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_count_sits_on_the_bells_end_corner_and_the_dot_in_the_start_gutter(string dir)
    {
        if (NoBrowser) return;
        var page = await OpenInbox(dir);
        await page.Locator("#bell").ClickAsync();
        await Assertions.Expect(page.Locator("#inbox")).ToBeVisibleAsync();

        var probe = await page.EvaluateAsync<JsonElement>("""
            () => {
                const r = id => document.getElementById(id).getBoundingClientRect();
                const glyph = r('glyph'), count = r('count'), item = r('item-1'), avatar = r('avatar-1'), title = r('title-1');
                const dot = getComputedStyle(document.getElementById('item-1'), '::before');
                const read = getComputedStyle(document.getElementById('item-2'), '::before');
                return {
                    countEnd: document.dir === 'rtl' ? glyph.left - count.left : count.right - glyph.right,
                    countAbove: glyph.top - count.top,
                    countOverIcon: document.dir === 'rtl' ? count.right > glyph.left : count.left < glyph.right,
                    dotWidth: parseFloat(dot.width),
                    dotStart: parseFloat(dot.insetInlineStart),
                    dotLeft: parseFloat(dot.left), dotRight: parseFloat(dot.right),
                    readDot: read.content,
                    avatarFromStart: document.dir === 'rtl' ? item.right - avatar.right : avatar.left - item.left,
                    titleTop: title.top - item.top,
                };
            }
            """);

        // The count hangs off the bell's inline-end top corner, over the icon's edge.
        Assert.True(probe.GetProperty("countEnd").GetDouble() > 0, "The count does not reach past the bell's end edge.");
        Assert.True(probe.GetProperty("countAbove").GetDouble() > 0, "The count does not sit above the bell's top.");
        Assert.True(probe.GetProperty("countOverIcon").GetBoolean(), "The count does not overlap the bell.");

        // The dot is in the gutter before the avatar, on the inline-start side, and only on an unread item.
        Assert.Equal(6, probe.GetProperty("dotWidth").GetDouble());
        Assert.Equal("none", probe.GetProperty("readDot").GetString());
        var gutter = probe.GetProperty("avatarFromStart").GetDouble();
        Assert.True(gutter > probe.GetProperty("dotStart").GetDouble() + 6, $"The dot does not fit the {gutter}px gutter before the avatar.");
        var physical = dir == "rtl" ? probe.GetProperty("dotRight").GetDouble() : probe.GetProperty("dotLeft").GetDouble();
        Assert.Equal(probe.GetProperty("dotStart").GetDouble(), physical);
    }

    [Fact]
    public async Task The_row_opens_its_notification_and_its_controls_stay_its_own()
    {
        if (NoBrowser) return;
        var page = await OpenInbox();
        await page.Locator("#bell").ClickAsync();
        await Assertions.Expect(page.Locator("#inbox")).ToBeVisibleAsync();

        var hits = await page.EvaluateAsync<string[]>("""
            () => {
                const at = (el, fx, fy) => { const r = el.getBoundingClientRect(); return document.elementFromPoint(r.left + r.width * fx, r.top + r.height * fy); };
                const id = el => el && (el.id || el.closest('[id]').id);
                const item = document.getElementById('item-1');
                return [
                    id(at(item, 0.6, 0.97)),                                  // the row's bottom edge: the title's stretch
                    id(at(document.getElementById('accept-1'), 0.5, 0.5)),    // the action
                    id(at(document.getElementById('more-1'), 0.5, 0.5)),      // the menu's trigger
                ];
            }
            """);
        Assert.Equal(["title-1", "accept-1", "more-1"], hits);

        // The menu opens from inside the panel and the panel stays open.
        await page.Locator("#more-1").ClickAsync();
        await Assertions.Expect(page.Locator("#item-1 .menu")).ToBeVisibleAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('inbox').matches(':popover-open')"));
    }

    [Fact]
    public async Task A_long_list_scrolls_inside_the_panel()
    {
        if (NoBrowser) return;
        var page = await OpenInbox(items: 12);
        await page.Locator("#bell").ClickAsync();
        await Assertions.Expect(page.Locator("#inbox")).ToBeVisibleAsync();

        Assert.True(await page.EvaluateAsync<bool>("() => { const l = document.getElementById('list'); return l.scrollHeight > l.clientHeight; }"),
            "Twelve notifications should scroll inside the list.");
        var panel = (await page.Locator("#inbox").BoundingBoxAsync())!;
        Assert.True(panel.Y + panel.Height <= 800, "The panel runs off the bottom of the screen.");
        // The head and the foot stay in the panel, around the scrolling list.
        var foot = (await page.Locator(".inbox-foot").BoundingBoxAsync())!;
        Assert.True(foot.Y + foot.Height <= panel.Y + panel.Height + 0.5, "The foot is pushed out of the panel.");
    }

    [Fact]
    public async Task An_empty_count_is_a_dot()
    {
        if (NoBrowser) return;
        var page = await OpenInbox();
        var size = await page.EvaluateAsync<double[]>("""
            () => {
                const c = document.getElementById('count');
                const n = c.getBoundingClientRect();
                c.textContent = '';
                const d = c.getBoundingClientRect();
                return [n.width, n.height, d.width, d.height];
            }
            """);
        Assert.True(size[0] >= 16 && Math.Abs(size[1] - 16) < 0.5, $"A count is {size[0]}×{size[1]}.");
        Assert.Equal(8, size[2]);
        Assert.Equal(8, size[3]);
    }
}
