using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Delegated tabs exist mostly for the keyboard: arrows that skip a disabled tab, Home/End, and a roving tabindex.
/// </summary>
public class TabsTests : ScriptTestBase
{
    private const string TabsFixture = """
        <div class="tabs" data-tabs>
            <button class="tab" role="tab" aria-controls="p1" aria-selected="true" id="t1">One</button>
            <button class="tab" role="tab" aria-controls="p2" aria-selected="false" id="t2" disabled>Two</button>
            <button class="tab" role="tab" aria-controls="p3" aria-selected="false" id="t3">Three</button>
        </div>
        <div class="tab-panel" role="tabpanel" id="p1">first</div>
        <div class="tab-panel" role="tabpanel" id="p2" hidden>second</div>
        <div class="tab-panel" role="tabpanel" id="p3" hidden>third</div>
        """;

    [Fact]
    public async Task Selecting_a_tab_shows_its_panel_and_leaves_only_it_in_the_tab_order()
    {
        if (NoBrowser) return;
        var page = await Open(TabsFixture);

        await page.Locator("#t3").ClickAsync();

        var state = await page.EvaluateAsync<JsonElement>("""
            () => ({
                selected: [...document.querySelectorAll('[role=tab]')]
                    .map(t => t.getAttribute('aria-selected')),
                tabIndexes: [...document.querySelectorAll('[role=tab]')].map(t => t.tabIndex),
                visible: [...document.querySelectorAll('[role=tabpanel]')].map(p => !p.hidden)
            })
            """);

        Assert.Equal(new[] { "false", "false", "true" },
            state.GetProperty("selected").EnumerateArray().Select(e => e.GetString()).ToArray());
        // Roving tabindex: Tab steps past the whole tablist, not through every tab.
        Assert.Equal(new[] { -1, -1, 0 },
            state.GetProperty("tabIndexes").EnumerateArray().Select(e => e.GetInt32()).ToArray());
        Assert.Equal(new[] { false, false, true },
            state.GetProperty("visible").EnumerateArray().Select(e => e.GetBoolean()).ToArray());
    }

    [Fact]
    public async Task Arrow_keys_skip_a_disabled_tab_and_wrap_and_Home_End_jump()
    {
        if (NoBrowser) return;
        var page = await Open(TabsFixture);

        await page.Locator("#t1").FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        // t2 is disabled, so right from t1 lands on t3, not on t2.
        Assert.Equal("t3", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        await page.Keyboard.PressAsync("ArrowRight");
        Assert.Equal("t1", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        await page.Keyboard.PressAsync("End");
        Assert.Equal("t3", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        await page.Keyboard.PressAsync("Home");
        Assert.Equal("t1", await page.EvaluateAsync<string>("() => document.activeElement.id"));

        // A disabled tab is never selectable, by key or by click.
        await page.Locator("#t2").ClickAsync(new() { Force = true });
        Assert.Equal("false", await page.Locator("#t2").GetAttributeAsync("aria-selected"));
    }

    [Fact]
    public async Task Tabs_can_be_selected_programmatically_by_panel_id()
    {
        if (NoBrowser) return;
        var page = await Open(TabsFixture);

        await page.EvaluateAsync("() => sednaUi.tabs.select('p3')");

        Assert.Equal("true", await page.Locator("#t3").GetAttributeAsync("aria-selected"));
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('p3').hidden"));
    }

    // ── declarative copy ────────────────────────────────────────────────────
}
