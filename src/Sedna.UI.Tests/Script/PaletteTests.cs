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

    // ── boot.js: the pre-paint theme ────────────────────────────────────────
}
