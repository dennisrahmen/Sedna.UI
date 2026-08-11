using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// The public surface itself. Removing or renaming a member is a Major change, and several apps call in.
/// </summary>
public class SurfaceTests : ScriptTestBase
{
    [Fact]
    public async Task The_public_surface_is_exactly_what_is_documented()
    {
        if (NoBrowser) return;
        // Removing or renaming any of these is a Major change, and four apps call in.
        // A new member is minor, so an addition must NOT fail here — only a
        // disappearance does. `_` is deliberately excluded: it is documented as
        // private and may change in a patch.
        var page = await Open("<div></div>");

        var missing = await page.EvaluateAsync<string[]>("""
            () => [
                'configure', 'settings', 'tips', 'toast', 'confirm', 'menu', 'tabs',
                'palette', 'search', 'md', 'copyText', 'openTab', 'viewportWidth',
                'getItem', 'setItem', 'requestNotify', 'notify', 'ping'
            ].filter(k => window.sednaUi[k] === undefined)
            """);

        Assert.True(missing.Length == 0,
            "These documented members of sednaUi are gone. Removing or renaming one is a Major "
            + $"change, and docs/architecture.md lists it: {string.Join(", ", missing)}");

        // The sub-objects are contracts too, not just present-ness.
        var shape = await page.EvaluateAsync<string[]>("""
            () => {
                const u = window.sednaUi, bad = [];
                const need = {
                    settings: ['load', 'save', 'apply'],
                    menu: ['closeAll'],
                    tabs: ['select'],
                    palette: ['register', 'open', 'close', 'rank'],
                    search: ['register', 'rank', 'close'],
                    md: ['init', 'apply', 'render'],
                };
                for (const k in need)
                    for (const fn of need[k])
                        if (typeof u[k]?.[fn] !== 'function') bad.push(k + '.' + fn);
                return bad;
            }
            """);

        Assert.True(shape.Length == 0, "Missing functions on sednaUi: " + string.Join(", ", shape));
    }

    // ── toast ───────────────────────────────────────────────────────────────
}
