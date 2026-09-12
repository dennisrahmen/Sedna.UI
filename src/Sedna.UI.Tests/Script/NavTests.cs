using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The nav filter and the area rail: both write attributes and leave the rest to CSS, so
/// every assertion here is about what a reader can see, not which attribute was set.
/// </summary>
public class NavTests : ScriptTestBase
{
    private const string FilterFixture = """
        <aside class="sidebar" style="height:600px">
            <nav class="nav">
                <div class="nav-filter">
                    <input class="nav-filter-input" type="search" aria-label="Filter pages" data-nav-filter id="filter">
                </div>
                <div class="nav-scroll">
                    <details class="nav-group" name="n" open>
                        <summary><span>Orders</span></summary>
                        <a class="nav-link" href="#queue" id="queue" data-keywords="pending"><span>Queue</span></a>
                        <a class="nav-link" href="#returns" id="returns" data-keywords="refund"><span>Returns</span></a>
                    </details>
                    <details class="nav-group" name="n" id="stock">
                        <summary><span>Stock</span></summary>
                        <a class="nav-link" href="#locations" id="locations" data-keywords="shelf bin"><span>Locations</span></a>
                    </details>
                    <div class="nav-section" id="admin">
                        <span class="nav-section-label">Administration</span>
                        <a class="nav-link" href="#status" id="status"><span>Status board</span></a>
                    </div>
                    <p class="nav-filter-empty" id="empty">No page matches.</p>
                </div>
                <div class="nav-tools">
                    <a class="nav-link nav-link-tool" href="#docs" id="docs"><span>Documentation</span></a>
                </div>
            </nav>
        </aside>
        """;

    private const string VisibilityScript = """
        () => Object.fromEntries(
            ['queue', 'returns', 'locations', 'status', 'stock', 'admin', 'empty', 'docs']
                .map(id => [id, document.getElementById(id).checkVisibility()]))
        """;

    [Fact]
    public async Task Filtering_shows_matches_by_label_or_keyword_even_inside_a_closed_group()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(FilterFixture);

        await page.Locator("#filter").FillAsync("shelf");
        var seen = await page.EvaluateAsync<JsonElement>(VisibilityScript);

        Assert.True(seen.GetProperty("locations").GetBoolean(), "a keyword match shows");
        Assert.True(seen.GetProperty("stock").GetBoolean(), "the closed group holding it shows");
        Assert.False(seen.GetProperty("queue").GetBoolean());
        Assert.False(seen.GetProperty("admin").GetBoolean(), "a section left empty goes, label and all");
        Assert.True(seen.GetProperty("docs").GetBoolean(), "the tools below the nav are never filtered");
        Assert.False(seen.GetProperty("empty").GetBoolean());
        // Shown through ::details-content, so the reader's own open state is untouched.
        Assert.Null(await page.Locator("#stock").GetAttributeAsync("open"));
    }

    [Fact]
    public async Task A_filter_is_containment_so_a_scattered_subsequence_does_not_match()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(FilterFixture);

        // t…a…b is a subsequence of "Status board" and a ranked search would keep it.
        await page.Locator("#filter").FillAsync("tab");
        var seen = await page.EvaluateAsync<JsonElement>(VisibilityScript);

        Assert.False(seen.GetProperty("status").GetBoolean());
        Assert.True(seen.GetProperty("empty").GetBoolean(), "the empty note shows when nothing is left");
    }

    [Fact]
    public async Task Escape_clears_and_restores_the_nav_and_Enter_follows_the_first_match()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(FilterFixture);

        await page.Locator("#filter").FillAsync("ret");
        await page.Keyboard.PressAsync("Escape");
        var seen = await page.EvaluateAsync<JsonElement>(VisibilityScript);

        Assert.Equal("", await page.Locator("#filter").InputValueAsync());
        Assert.True(seen.GetProperty("queue").GetBoolean());
        Assert.False(seen.GetProperty("locations").GetBoolean(), "a group closed before filtering is closed again");

        // A same-page fragment is handled by 29-fragment.js and leaves the address alone,
        // so what is asserted is the click Enter produced.
        await page.EvaluateAsync("() => document.addEventListener('click', e => window.clicked = e.target.closest('a')?.id)");
        await page.Locator("#filter").FillAsync("refund");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => window.clicked === 'returns'");
        // Following a link out of a filtered nav clears the filter.
        await page.WaitForFunctionAsync("() => document.getElementById('filter').value === ''");
    }

    [Fact]
    public async Task Filter_can_be_driven_programmatically_and_reports_the_match_count()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(FilterFixture);

        // "Locations" and "Status board"; neither Queue nor Returns has an "o" in its label or keywords.
        var hits = await page.EvaluateAsync<int>("() => sednaUi.nav.filter(document.getElementById('filter'), 'o')");
        var visible = await page.EvaluateAsync<int>(
            "() => [...document.querySelectorAll('.nav-scroll .nav-link')].filter(l => l.checkVisibility()).length");
        Assert.Equal(2, hits);
        Assert.Equal(hits, visible);

        Assert.Equal(4, await page.EvaluateAsync<int>("() => sednaUi.nav.filter(document.getElementById('filter'), '')"));
    }

    private const string AreasFixture = """
        <aside class="sidebar sidebar--areas" id="sidebar" style="height:400px">
            <div class="nav-areas">
                <nav class="nav-areas-list" aria-label="Areas">
                    <button class="nav-area" type="button" id="a1" aria-controls="p1" aria-expanded="true" data-nav-area><span>Orders</span></button>
                    <button class="nav-area" type="button" id="a2" aria-controls="p2" aria-expanded="false" data-nav-area><span>Stock</span></button>
                </nav>
            </div>
            <nav class="nav" id="p1"><div class="nav-area-title">Orders</div><div class="nav-scroll"><a class="nav-link" href="#q"><span>Queue</span></a></div></nav>
            <nav class="nav" id="p2" hidden><div class="nav-area-title">Stock</div><div class="nav-scroll"><a class="nav-link" href="#i"><span>Items</span></a></div></nav>
        </aside>
        """;

    [Fact]
    public async Task An_area_button_shows_its_panel_and_hides_the_others()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(AreasFixture);

        await page.Locator("#a2").ClickAsync();
        var state = await page.EvaluateAsync<JsonElement>("""
            () => ({
                expanded: ['a1', 'a2'].map(id => document.getElementById(id).getAttribute('aria-expanded')),
                visible: ['p1', 'p2'].map(id => document.getElementById(id).checkVisibility())
            })
            """);

        Assert.Equal(new[] { "false", "true" },
            state.GetProperty("expanded").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.Equal(new[] { false, true },
            state.GetProperty("visible").EnumerateArray().Select(e => e.GetBoolean()).ToArray());

        await page.EvaluateAsync("() => sednaUi.nav.showArea('p1')");
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('p1').checkVisibility()"));
    }

    [Fact]
    public async Task A_collapsed_area_rail_is_the_rail_alone()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(AreasFixture);

        // The sidebar animates its width; measuring mid-transition reads a width it is passing through.
        await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        var open = await page.EvaluateAsync<double>("() => document.getElementById('sidebar').getBoundingClientRect().width");
        await page.EvaluateAsync("() => document.getElementById('sidebar').classList.add('collapsed')");
        var state = await page.EvaluateAsync<JsonElement>("""
            () => ({
                width: document.getElementById('sidebar').getBoundingClientRect().width,
                rail: document.querySelector('.nav-areas').getBoundingClientRect().width,
                panel: document.getElementById('p1').checkVisibility()
            })
            """);

        Assert.True(open > state.GetProperty("width").GetDouble(), "collapsing narrows the sidebar");
        Assert.False(state.GetProperty("panel").GetBoolean());
        // The sidebar's own 1px edge is the only difference between the two.
        Assert.InRange(state.GetProperty("width").GetDouble() - state.GetProperty("rail").GetDouble(), 0, 1.5);
    }
}
