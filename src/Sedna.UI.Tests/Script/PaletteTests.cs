using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// The command palette: the app's own markup filled from its templates, and Ctrl-K left to
/// the browser until an app registers commands.
/// </summary>
public class PaletteTests : ScriptTestBase
{
    /// <summary>The palette an app writes: the dialog, its words, and a template per row shape.</summary>
    private const string Palette = """
        <dialog class="palette" data-palette aria-label="Befehle">
            <input class="palette-input" type="text" placeholder="Befehl suchen…" aria-label="Befehl suchen">
            <ul class="palette-list" aria-label="Befehle"></ul>
            <div class="palette-footer"><span>Enter zum Ausführen</span></div>
            <template data-palette-item>
                <li role="presentation">
                    <div class="palette-item" role="option">
                        <i data-icon aria-hidden="true"></i><span data-label></span>
                        <span class="palette-item-note" data-note></span>
                    </div>
                </li>
            </template>
            <template data-palette-group>
                <li role="presentation"><div class="palette-group" data-group></div></li>
            </template>
            <template data-palette-empty>
                <li role="presentation"><div class="palette-empty">Kein Treffer für „<span data-query></span>“</div></li>
            </template>
        </dialog>
        """;

    [Fact]
    public async Task The_rows_are_the_apps_templates_filled_with_text_and_nothing_else()
    {
        if (NoBrowser) return;
        // The script draws nothing. Every element and word on screen comes from the app's
        // markup, which is what lets the palette speak the app's language.
        var page = await Open(Palette);

        var result = await page.EvaluateAsync<string[]>("""
            () => {
                sednaUi.palette.register([
                    { label: 'Warteschlange öffnen', icon: 'ri-inbox-line', group: 'Navigation', note: 'G W', run: () => {} },
                    { label: '<b>Bericht</b>', group: 'Navigation', run: () => {} }
                ]);
                sednaUi.palette.open();
                const list = document.querySelector('.palette-list');
                const rows = list.querySelectorAll('[role="option"]');
                return [
                    list.querySelector('.palette-group').textContent,
                    rows[0].querySelector('[data-label]').textContent,
                    rows[0].querySelector('[data-icon]').className,
                    String(!!rows[1].querySelector('[data-note]')),        // empty slot removed
                    String(!!rows[1].querySelector('[data-icon]')),        // no icon, no element
                    String(!!rows[1].querySelector('b')),                  // text, never markup
                    document.querySelector('.palette-input').getAttribute('role'),
                    document.querySelector('.palette-input').getAttribute('aria-activedescendant') === rows[0].id ? 'first' : 'other',
                ];
            }
            """);

        Assert.Equal(["Navigation", "Warteschlange öffnen", "ri-inbox-line", "false", "false", "false", "combobox", "first"], result);
    }

    [Fact]
    public async Task Nothing_found_uses_the_apps_own_words()
    {
        if (NoBrowser) return;
        var page = await Open(Palette);

        var text = await page.EvaluateAsync<string>("""
            () => {
                sednaUi.palette.register([{ label: 'Warteschlange', run: () => {} }]);
                sednaUi.palette.open();
                const input = document.querySelector('.palette-input');
                input.value = 'xyzzy';
                input.dispatchEvent(new Event('input'));
                return document.querySelector('.palette-empty').textContent;
            }
            """);

        Assert.Equal("Kein Treffer für „xyzzy“", text);
    }

    [Fact]
    public async Task Without_palette_markup_the_shortcut_stays_the_browsers()
    {
        if (NoBrowser) return;
        // Commands registered, no <dialog data-palette>: swallowing Ctrl-K to log a
        // warning would take the browser's own binding for nothing.
        var page = await Open("<div></div>");

        var outcome = await page.EvaluateAsync<string>("""
            () => {
                sednaUi.palette.register([{ label: 'Open queue', run: () => {} }]);
                const e = new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true, cancelable: true });
                document.dispatchEvent(e);
                return e.defaultPrevented + ':' + sednaUi.palette.open() + ':' + document.querySelectorAll('dialog').length;
            }
            """);

        Assert.Equal("false:false:0", outcome);
    }

    [Fact]
    public async Task The_palette_shortcut_does_nothing_until_an_app_registers_commands()
    {
        if (NoBrowser) return;
        // With nothing registered, Ctrl-K must be left to the browser rather than
        // opening an empty panel.
        var page = await Open(Palette);

        await page.Keyboard.PressAsync(Keys.Primary("k"));
        Assert.Equal(0, await page.Locator("dialog.palette[open]").CountAsync());

        await Js.Run(page, 
            "() => sednaUi.palette.register([{ label: 'Open queue', run: () => {} }])");
        await page.Keyboard.PressAsync(Keys.Primary("k"));
        Assert.Equal(1, await page.Locator("dialog.palette[open]").CountAsync());

        await Js.Run(page, "() => sednaUi.palette.close()");
        Assert.Equal(0, await page.Locator("dialog.palette[open]").CountAsync());
    }

    [Fact]
    public async Task The_palette_ranks_a_subsequence_match()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        var ranked = await page.EvaluateAsync<string[]>("""
            () => {
                sednaUi.palette.register([
                    { label: 'Approve transfer', run: () => {} },
                    { label: 'Open queue', run: () => {} },
                    { label: 'Open quality report', run: () => {} }
                ]);
                return sednaUi.palette.rank('opq').map(c => c.label);
            }
            """);

        Assert.NotEmpty(ranked);
        Assert.DoesNotContain("Approve transfer", ranked);
        Assert.Contains("Open queue", ranked);
    }

    [Fact]
    public async Task An_href_command_navigates_by_clicking_a_real_anchor()
    {
        if (NoBrowser) return;
        // The whole point of this: window.location.assign is always a FULL PAGE LOAD.
        // In a Blazor Server app that tears down the SignalR circuit and builds a new
        // one, so every scoped service is re-created — a demo mode, a guided tour, an
        // unsaved form all end silently, because a palette command is the last thing
        // anyone suspects. A router intercepts a click on a same-origin <a> and routes
        // it client-side; nothing can intercept an assignment.
        var page = await Open(Palette);

        var navigated = await page.EvaluateAsync<string[]>("""
            () => new Promise(resolve => {
                const seen = [];
                document.addEventListener('click', e => {
                    const a = e.target.closest && e.target.closest('a');
                    if (!a) return;
                    // Let nothing actually navigate: the test is that a click happened
                    // at all, which is the part a router needs.
                    e.preventDefault();
                    seen.push(a.tagName, a.getAttribute('href'));
                    resolve(seen);
                }, true);

                sednaUi.palette.register([{ label: 'Open the queue', href: '/queue' }]);
                sednaUi.palette.open();
                document.querySelector('.palette-item').click();
                // Every handler the click set off has run two frames later.
                requestAnimationFrame(() => requestAnimationFrame(() => resolve(seen)));
            })
            """);

        Assert.Equal(["A", "/queue"], navigated);
    }

    [Fact]
    public async Task A_command_with_a_callback_does_not_navigate()
    {
        if (NoBrowser) return;
        // `run` first, and `href` then only exists for a middle-click. A command
        // doing both would otherwise run and leave the page in the same gesture.
        var page = await Open(Palette);

        var clicks = await page.EvaluateAsync<int>("""
            () => {
                let anchors = 0;
                document.addEventListener('click', e => {
                    if (e.target.closest && e.target.closest('a')) { e.preventDefault(); anchors++; }
                }, true);

                let ran = 0;
                sednaUi.palette.register(
                    [{ label: 'Dispatch', href: '/queue', run: () => ran++ }]);
                sednaUi.palette.open();
                document.querySelector('.palette-item').click();
                return anchors;
            }
            """);

        Assert.Equal(0, clicks);
    }

    /// <summary>The palette with every optional part: the path, and the separator, loading and crumb templates.</summary>
    private const string Full = """
        <dialog class="palette" data-palette aria-label="Befehle">
            <div class="palette-path" data-palette-path hidden>
                <button class="btn btn--sm btn--icon" type="button" data-palette-back aria-label="Zurück">←</button>
                <span class="palette-trail" data-palette-trail></span>
            </div>
            <input class="palette-input" type="text" aria-label="Befehl suchen">
            <ul class="palette-list" aria-label="Befehle"></ul>
            <template data-palette-item>
                <li role="presentation"><div class="palette-item" role="option"><span data-label></span></div></li>
            </template>
            <template data-palette-group>
                <li role="presentation"><div class="palette-group" data-group></div></li>
            </template>
            <template data-palette-separator>
                <li role="presentation" class="palette-separator"></li>
            </template>
            <template data-palette-empty>
                <li role="presentation"><div class="palette-empty">Kein Treffer für „<span data-query></span>“</div></li>
            </template>
            <template data-palette-loading>
                <li role="presentation"><div class="palette-loading">Lädt…</div></li>
            </template>
            <template data-palette-crumb><span class="palette-crumb" data-label></span></template>
        </dialog>
        """;

    /// <summary>A root with one page, "theme".</summary>
    private const string WithPages = """
        () => {
            sednaUi.palette.register([
                { label: 'Design ändern…', opens: 'theme' },
                { label: 'Warteschlange öffnen', run: () => {} },
                { label: 'Bericht drucken', run: () => {} },
                { page: 'theme', label: 'Dunkel', run: () => {} },
                { page: 'theme', label: 'Hell', run: () => {} }
            ]);
            sednaUi.palette.open();
        }
        """;

    /// <summary>What the reader sees: the open state, the option labels, the selected label, the path and its crumbs.</summary>
    private const string Read = """
        () => {
            const d = document.querySelector('dialog[data-palette]');
            const options = [...d.querySelectorAll('[role="option"]')];
            const selected = options.find(o => o.getAttribute('aria-selected') === 'true');
            return [
                String(d.open),
                options.map(o => o.textContent.trim()).join('|'),
                selected ? selected.textContent.trim() : '',
                d.querySelector('[data-palette-path]').hidden ? 'root' : 'page',
                [...d.querySelectorAll('.palette-crumb')].map(c => c.textContent).join('>'),
                d.querySelector('.palette-input').value
            ];
        }
        """;

    [Fact]
    public async Task A_command_that_opens_a_page_lists_that_pages_commands_under_a_trail()
    {
        if (NoBrowser) return;
        var page = await Open(Full);
        await Js.Run(page, WithPages);

        Assert.Equal(["true", "Design ändern…|Warteschlange öffnen|Bericht drucken", "Design ändern…", "root", "", ""],
            await page.EvaluateAsync<string[]>(Read));

        await page.Keyboard.PressAsync("Enter");

        // Still open: the command opened a page instead of running.
        Assert.Equal(["true", "Dunkel|Hell", "Dunkel", "page", "Design ändern…", ""],
            await page.EvaluateAsync<string[]>(Read));
        Assert.Equal(["Hell"], await page.EvaluateAsync<string[]>(
            "() => sednaUi.palette.rank('hell', 'theme').map(c => c.label)"));
        Assert.Empty(await page.EvaluateAsync<string[]>("() => sednaUi.palette.rank('hell').map(c => c.label)"));
    }

    [Fact]
    public async Task Backspace_in_an_empty_search_goes_back_to_the_query_and_row_the_reader_left()
    {
        if (NoBrowser) return;
        var page = await Open(Full);
        await Js.Run(page, WithPages);

        // "dr" ranks "Bericht drucken" first and the opener second.
        await page.Keyboard.TypeAsync("dr");
        await page.Keyboard.PressAsync("ArrowDown");
        var selected = (await page.EvaluateAsync<string[]>(Read))[2];
        Assert.Equal("Design ändern…", selected);
        await page.Keyboard.PressAsync("Enter");

        // With text in the input, Backspace edits it and the page stays.
        await page.Keyboard.TypeAsync("h");
        await page.Keyboard.PressAsync("Backspace");
        Assert.Equal("page", (await page.EvaluateAsync<string[]>(Read))[3]);

        await page.Keyboard.PressAsync("Backspace");
        var back = await page.EvaluateAsync<string[]>(Read);
        Assert.Equal("root", back[3]);
        Assert.Equal("dr", back[5]);
        Assert.Equal("Bericht drucken|Design ändern…", back[1]);
        Assert.Equal(selected, back[2]);
    }

    [Fact]
    public async Task Escape_goes_back_one_page_and_closes_only_on_the_root()
    {
        if (NoBrowser) return;
        var page = await Open(Full);
        await Js.Run(page, WithPages);
        await page.Keyboard.PressAsync("Enter");

        await page.Keyboard.PressAsync("Escape");
        var state = await page.EvaluateAsync<string[]>(Read);
        Assert.Equal("true", state[0]);
        Assert.Equal("root", state[3]);

        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("false", (await page.EvaluateAsync<string[]>(Read))[0]);
    }

    [Fact]
    public async Task The_apps_back_button_goes_back_one_page()
    {
        if (NoBrowser) return;
        var page = await Open(Full);
        await Js.Run(page, WithPages);
        await page.Keyboard.PressAsync("Enter");

        await page.Locator("[data-palette-back]").ClickAsync();

        var state = await page.EvaluateAsync<string[]>(Read);
        Assert.Equal(["true", "root"], [state[0], state[3]]);
        Assert.True(await page.EvaluateAsync<bool>(
            "() => document.activeElement === document.querySelector('.palette-input')"));
        Assert.False(await page.EvaluateAsync<bool>("() => sednaUi.palette.back()"));
    }

    [Fact]
    public async Task The_arrow_keys_wrap_around_the_list()
    {
        if (NoBrowser) return;
        var page = await Open(Full);
        await Js.Run(page, WithPages);

        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("Bericht drucken", (await page.EvaluateAsync<string[]>(Read))[2]);

        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("Design ändern…", (await page.EvaluateAsync<string[]>(Read))[2]);
    }

    [Fact]
    public async Task While_the_app_is_loading_the_list_ends_with_its_loading_row_and_holds_back_nothing_found()
    {
        if (NoBrowser) return;
        var page = await Open(Full);

        var states = await page.EvaluateAsync<string[]>("""
            async () => {
                const d = document.querySelector('dialog[data-palette]');
                const input = d.querySelector('.palette-input');
                const read = () => [...d.querySelector('.palette-list').children]
                    .map(li => li.querySelector('.palette-loading') ? 'loading'
                             : li.querySelector('.palette-empty') ? 'empty'
                             : li.textContent.trim()).join('|');

                sednaUi.palette.register([{ label: 'Warteschlange', run: () => {} }]);
                sednaUi.palette.open();
                const out = [];

                d.setAttribute('aria-busy', 'true');
                await null;                                // the observer's microtask
                out.push(read());

                input.value = 'xyz';
                input.dispatchEvent(new Event('input'));
                out.push(read());

                // The page's commands arrive, then the app clears the signal.
                sednaUi.palette.register([{ label: 'Warteschlange', run: () => {} }, { label: 'xyz-Bericht', run: () => {} }]);
                out.push(read());
                d.removeAttribute('aria-busy');
                await null;
                out.push(read());

                input.value = 'qqq';
                input.dispatchEvent(new Event('input'));
                out.push(read());
                return out;
            }
            """);

        Assert.Equal(["Warteschlange|loading", "loading", "xyz-Bericht|loading", "xyz-Bericht", "empty"], states);
    }

    [Fact]
    public async Task Groups_and_separators_show_only_while_nothing_is_typed()
    {
        if (NoBrowser) return;
        var page = await Open(Full);

        var counts = await page.EvaluateAsync<int[]>("""
            () => {
                sednaUi.palette.register([
                    { label: 'Warteschlange', group: 'Navigation', run: () => {} },
                    { label: 'Berichte', group: 'Navigation', run: () => {} },
                    { label: 'Abmelden', group: 'Konto', run: () => {} },
                    { label: 'Profil', group: 'Konto', run: () => {} }
                ]);
                sednaUi.palette.open();
                const d = document.querySelector('dialog[data-palette]');
                const count = () => [d.querySelectorAll('.palette-group').length, d.querySelectorAll('.palette-separator').length];
                const before = count();
                const input = d.querySelector('.palette-input');
                input.value = 'e';
                input.dispatchEvent(new Event('input'));
                return before.concat(count());
            }
            """);

        // A separator between the two runs, never above the first.
        Assert.Equal([2, 1, 0, 0], counts);
    }

    [Fact]
    public async Task A_pinned_command_follows_the_matches_whatever_is_typed()
    {
        if (NoBrowser) return;
        var page = await Open(Full);

        var lists = await page.EvaluateAsync<string[]>("""
            () => {
                sednaUi.palette.register([
                    { label: 'In der Hilfe suchen', pinned: true, run: () => {} },
                    { label: 'Warteschlange', run: () => {} },
                    { label: 'Berichte', run: () => {} }
                ]);
                sednaUi.palette.open();
                const d = document.querySelector('dialog[data-palette]');
                const input = d.querySelector('.palette-input');
                const read = () => [...d.querySelector('.palette-list').children]
                    .map(li => li.querySelector('.palette-empty') ? 'empty' : li.textContent.trim()).join('|');
                const out = [read()];
                input.value = 'ber';
                input.dispatchEvent(new Event('input'));
                out.push(read());
                input.value = 'zzz';
                input.dispatchEvent(new Event('input'));
                out.push(read());
                return out;
            }
            """);

        Assert.Equal([
            "In der Hilfe suchen|Warteschlange|Berichte",
            "Berichte|In der Hilfe suchen",
            "empty|In der Hilfe suchen",
        ], lists);
    }
}
