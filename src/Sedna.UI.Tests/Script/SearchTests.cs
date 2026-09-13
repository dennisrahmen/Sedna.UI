using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// The header search: ranking, the panel, and the keyboard contract behind the
/// combobox role it claims.
/// </summary>
/// <remarks>
/// Driven in a real browser rather than asserted from the source, because the
/// interesting parts are what the platform does — whether the panel escapes the
/// topbar's stacking context, whether Enter follows the highlighted result, and
/// whether clearing the box tells a framework binding about it.
/// </remarks>
public class SearchTests : ScriptTestBase
{
    private const string Box =
        """
        <header class="topbar">
          <div class="search">
            <i class="ri-search-line search-icon"></i>
            <input class="search-input" type="search" data-search placeholder="Search…">
            <button class="search-clear" type="button" aria-label="Clear"><i class="ri-close-line"></i></button>
          </div>
        </header>
        <div class="search-panel sedna-scroll" data-search-panel hidden>
          <div data-search-list aria-label="Ergebnisse"></div>
          <template data-search-item>
            <a class="search-item">
              <span class="search-item-title" data-title></span>
              <span class="search-item-meta">
                <span class="text-mono" data-code></span><span data-meta></span>
                <span class="search-tag" data-tag></span>
              </span>
            </a>
          </template>
          <template data-search-empty><div class="search-status">Kein Treffer für „<span data-query></span>“</div></template>
          <template data-search-more><div class="search-status"><span data-count></span> weitere Treffer</div></template>
        </div>
        """;

    private const string Register =
        """
        () => sednaUi.search.register([
            { title: 'Approval queue', href: '/queue', code: '/queue', meta: 'Waiting on a human' },
            { title: 'Decided topics', href: '/decided', code: '/decided' },
            { title: 'Quality report', href: '/quality', keywords: 'metrics kpi' }
        ])
        """;

    [Fact]
    public async Task An_unregistered_box_stays_a_plain_input()
    {
        if (NoBrowser) return;
        // An app rendering its own results uses these classes and does not register
        // an index. Answering "nothing matches" over its results would be a lie.
        var page = await Open(Box);

        await page.FillAsync(".search-input", "queue");

        Assert.Equal(0, await page.Locator(".search-panel:not([hidden])").CountAsync());
    }

    [Fact]
    public async Task Typing_opens_a_panel_of_ranked_results()
    {
        if (NoBrowser) return;
        var page = await Open(Box);
        await page.EvaluateAsync(Register);

        await page.FillAsync(".search-input", "qu");

        // "Quality" is a prefix match and outranks the word-start hit inside
        // "Approval queue"; "Decided topics" does not match at all.
        var titles = await page.Locator(".search-item-title").AllInnerTextsAsync();
        Assert.Equal(["Quality report", "Approval queue"], titles);

        // The first result is where the keyboard starts, and the input has to say so
        // for a screen reader to follow the highlight without focus moving.
        Assert.Equal(await page.GetAttributeAsync(".search-item >> nth=0", "id"),
            await page.GetAttributeAsync(".search-input", "aria-activedescendant"));
        Assert.Equal("combobox", await page.GetAttributeAsync(".search-input", "role"));
    }

    [Fact]
    public async Task A_keyword_hit_ranks_below_the_same_hit_in_a_title()
    {
        if (NoBrowser) return;
        var page = await Open(Box);

        var ranked = await page.EvaluateAsync<string[]>("""
            () => {
                sednaUi.search.register([
                    { title: 'Autonomy settings', keywords: 'queue' },
                    { title: 'Approval queue' }
                ]);
                return sednaUi.search.rank('queue').map(i => i.title);
            }
            """);

        Assert.Equal(["Approval queue", "Autonomy settings"], ranked);
    }

    [Fact]
    public async Task The_apps_panel_outside_the_topbar_is_fixed_under_the_box()
    {
        if (NoBrowser) return;
        // .topbar is z-index 60 and creates a stacking context: a panel nested inside
        // it could never rise above a modal backdrop, whatever its own z-index. So the
        // app puts its panel outside, and the script places it under the box it measured.
        var page = await Open(Box, head: StylesheetTag);
        await page.EvaluateAsync(Register);
        await page.FillAsync(".search-input", "qu");

        var placed = await page.EvaluateAsync<string>("""
            () => {
                const panel = document.querySelector('.search-panel');
                const box = document.querySelector('.search').getBoundingClientRect();
                const r = panel.getBoundingClientRect();
                return [panel.parentElement === document.body, Math.round(r.left) === Math.round(box.left),
                        Math.round(r.width) === Math.round(box.width), r.top >= box.bottom].join(':');
            }
            """);

        Assert.Equal("true:true:true:true", placed);
        Assert.Equal("550", await page.EvalOnSelectorAsync<string>(
            ".search-panel", "el => getComputedStyle(el).zIndex"));
    }

    [Fact]
    public async Task A_result_is_the_apps_template_and_an_empty_slot_is_removed()
    {
        if (NoBrowser) return;
        var page = await Open(Box);
        await page.EvaluateAsync("""
            () => sednaUi.search.register([
                { title: 'Quality report', tag: 'KPI', tone: 'warn' },
                { title: 'Quality gate', href: '/gate' }
            ])
            """);

        await page.FillAsync(".search-input", "quality");

        var shape = await page.EvaluateAsync<string[]>("""
            () => {
                const rows = document.querySelectorAll('.search-item');
                return [
                    String(rows[0].hasAttribute('href')),                    // no href, no attribute
                    rows[0].querySelector('.search-tag').className,
                    String(!!rows[0].querySelector('[data-code]')),          // empty slot removed
                    String(!!rows[1].querySelector('.search-item-meta')),    // nothing left to show
                    rows[1].getAttribute('href'),
                ];
            }
            """);

        Assert.Equal(["false", "search-tag search-tag--warn", "false", "false", "/gate"], shape);
    }

    [Fact]
    public async Task Nothing_found_uses_the_apps_own_words()
    {
        if (NoBrowser) return;
        var page = await Open(Box);
        await page.EvaluateAsync(Register);

        await page.FillAsync(".search-input", "xyzzy");

        Assert.Equal("Kein Treffer für „xyzzy“", await page.InnerTextAsync(".search-status"));
    }

    [Fact]
    public async Task Without_panel_markup_a_registered_box_stays_a_plain_input()
    {
        if (NoBrowser) return;
        // The script draws no panel of its own any more; with none in the page it
        // warns once and leaves the box alone.
        var page = await Open("""<div class="search"><input class="search-input" type="search" data-search></div>""");
        await page.EvaluateAsync(Register);

        await page.FillAsync(".search-input", "qu");

        Assert.Equal(0, await page.Locator(".search-panel").CountAsync());
        Assert.Null(await page.GetAttributeAsync(".search-input", "role"));
    }

    [Fact]
    public async Task Arrows_move_the_highlight_and_Enter_follows_it()
    {
        if (NoBrowser) return;
        var page = await Open(Box);
        await page.EvaluateAsync(Register);

        await page.ClickAsync(".search-input");
        await page.Locator(".search-input").PressSequentiallyAsync("qu");
        await page.Locator(".search-input").PressAsync("ArrowDown");

        Assert.Equal("Approval queue",
            await page.InnerTextAsync(".search-item--sel .search-item-title"));

        // Focus never leaves the input — that is the whole point of
        // aria-activedescendant, and typing has to keep working.
        Assert.True(await page.EvaluateAsync<bool>(
            "() => document.activeElement.classList.contains('search-input')"));

        await page.Locator(".search-input").PressAsync("Enter");

        // Polled in the page, not waited for as a navigation event. What is under test is
        // that Enter changed the address; every lifecycle-based wait here — Load,
        // NetworkIdle, even WaitForURL with Commit — hangs intermittently on the
        // fixture's own route handler, which made this the one test in the suite that
        // failed at random. A predicate re-evaluated in whichever document is current
        // cannot race it.
        await page.WaitForFunctionAsync(
            "() => location.pathname.endsWith('/queue')", null, new() { Timeout = 10_000 });
    }

    [Fact]
    public async Task Choosing_a_result_empties_the_box()
    {
        if (NoBrowser) return;
        // Otherwise the query survives a router navigation but not a full page load,
        // so the box sometimes holds the last search and sometimes does not.
        var page = await Open(Box);
        await page.EvaluateAsync(
            "() => sednaUi.search.register([{ title: 'Approval queue', run: () => {} }])");

        await page.FillAsync(".search-input", "queue");
        await page.ClickAsync(".search-item");

        Assert.Equal("", await page.InputValueAsync(".search-input"));
        Assert.Equal(0, await page.Locator(".search-panel:not([hidden])").CountAsync());
    }

    [Fact]
    public async Task Escape_closes_the_panel_and_then_clears_the_box()
    {
        if (NoBrowser) return;
        // The same key always undoes the last thing that happened.
        var page = await Open(Box);
        await page.EvaluateAsync(Register);

        await page.ClickAsync(".search-input");
        await page.Locator(".search-input").PressSequentiallyAsync("qu");
        Assert.Equal(1, await page.Locator(".search-panel:not([hidden])").CountAsync());

        await page.Locator(".search-input").PressAsync("Escape");
        Assert.Equal(0, await page.Locator(".search-panel:not([hidden])").CountAsync());
        // The query survives the first Escape. type="search" has a native Escape
        // that empties the box, and the handler has to suppress it or closing the
        // panel and losing what was typed are the same keystroke.
        Assert.Equal("qu", await page.InputValueAsync(".search-input"));

        await page.Locator(".search-input").PressAsync("Escape");
        Assert.Equal("", await page.InputValueAsync(".search-input"));
    }

    [Fact]
    public async Task The_clear_button_raises_an_input_event_so_a_binding_sees_it()
    {
        if (NoBrowser) return;
        // Assigning .value alone is invisible to Blazor's @bind and to every other
        // listener. The event is the part that matters.
        var page = await Open(Box);
        await page.EvaluateAsync(Register);
        await page.EvaluateAsync(
            "() => { window.seen = 0; document.querySelector('.search-input')"
            + ".addEventListener('input', () => window.seen++); }");

        await page.FillAsync(".search-input", "qu");
        await page.ClickAsync(".search-clear");

        Assert.Equal("", await page.InputValueAsync(".search-input"));
        Assert.Equal(2, await page.EvaluateAsync<int>("() => window.seen"));
        Assert.Equal(0, await page.Locator(".search-panel:not([hidden])").CountAsync());
    }

    [Fact]
    public async Task The_clear_button_is_hidden_until_there_is_something_to_clear()
    {
        if (NoBrowser) return;
        // CSS, not script: the button is app markup, so a rendered-and-hidden button
        // is the one form that also works with scripting blocked.
        var page = await Open(Box, head: StylesheetTag);

        Assert.False(await page.Locator(".search-clear").IsVisibleAsync());
        await page.FillAsync(".search-input", "q");
        Assert.True(await page.Locator(".search-clear").IsVisibleAsync());
    }

    [Fact]
    public async Task Two_words_are_an_AND_over_the_terms()
    {
        if (NoBrowser) return;
        // Scored as the mean of the terms, so a two-word query and a five-word one
        // are comparable — and an item matching only one of the words is not a
        // result at all.
        var page = await Open(Box);

        var ranked = await page.EvaluateAsync<string[]>("""
            () => {
                sednaUi.search.register([
                    { title: 'Semantic', keywords: 'badge badge-go badge-warn' },
                    { title: 'Head and body', keywords: 'card card-head badge badge-warn' },
                    { title: 'Badge go', keywords: 'badge' }
                ]);
                return sednaUi.search.rank('badge go').map(i => i.title);
            }
            """);

        // The literal phrase in a title outranks the two words found separately,
        // and the item carrying only "badge" is gone.
        Assert.Equal(["Badge go", "Semantic"], ranked);
    }

    [Fact]
    public async Task A_long_field_is_matched_by_substring_not_by_subsequence()
    {
        if (NoBrowser) return;
        // The subsequence matcher over a kilobyte of keywords matches nearly
        // everything, at scores that mean nothing — which is how an unrelated card
        // example came to outrank the page the reader asked for.
        var page = await Open(Box);

        var ranked = await page.EvaluateAsync<string[]>("""
            () => {
                sednaUi.search.register([
                    { title: 'Head and body', keywords: 'card card-head badge alert table-row grid' },
                    { title: 'Semantic', keywords: 'badge badge-go' }
                ]);
                return sednaUi.search.rank('badgo').map(i => i.title);
            }
            """);

        // "badgo" is a subsequence of the first item's keywords and a substring of
        // neither. Only a real substring counts outside the title.
        Assert.Empty(ranked);
    }

    [Fact]
    public async Task A_cut_result_list_says_how_many_it_cut()
    {
        if (NoBrowser) return;
        // A silently truncated list reads as "that is everything".
        var page = await Open(Box);
        await page.EvaluateAsync("""
            () => sednaUi.search.register(
                Array.from({ length: 12 }, (_, i) => ({ title: 'Queue ' + i, href: '#' })))
            """);

        await page.FillAsync(".search-input", "queue");

        Assert.Equal(8, await page.Locator(".search-item").CountAsync());
        Assert.Equal("4 weitere Treffer", await page.InnerTextAsync(".search-status"));
    }
}
