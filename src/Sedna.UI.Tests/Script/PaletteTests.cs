using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// The command palette leaves Ctrl-K to the browser until an app registers commands.
/// </summary>
public class PaletteTests : ScriptTestBase
{
    [Fact]
    public async Task The_palette_shortcut_does_nothing_until_an_app_registers_commands()
    {
        if (NoBrowser) return;
        // With nothing registered, Ctrl-K must be left to the browser rather than
        // opening an empty panel.
        var page = await Open("<div></div>");

        await page.Keyboard.PressAsync("Control+k");
        Assert.Equal(0, await page.Locator("dialog.palette").CountAsync());

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
        var page = await Open("<div></div>");

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
        var page = await Open("<div></div>");

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

    // ── boot.js: the pre-paint theme ────────────────────────────────────────
}
