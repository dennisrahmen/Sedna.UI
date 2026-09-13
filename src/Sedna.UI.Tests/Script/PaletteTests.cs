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

        await page.Keyboard.PressAsync("Control+k");
        Assert.Equal(0, await page.Locator("dialog.palette[open]").CountAsync());

        await page.EvaluateAsync(
            "() => sednaUi.palette.register([{ label: 'Open queue', run: () => {} }])");
        await page.Keyboard.PressAsync("Control+k");
        Assert.Equal(1, await page.Locator("dialog.palette[open]").CountAsync());

        await page.EvaluateAsync("() => sednaUi.palette.close()");
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
                setTimeout(() => resolve(seen), 500);
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
}
