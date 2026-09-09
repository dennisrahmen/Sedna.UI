using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.settings.onChange</c> and the Blazor bridge over it.
/// </summary>
/// <remarks>
/// The behaviour worth testing in a real browser is the one an app cannot poll for: a stored
/// preference of <c>"system"</c> following the OS while the page is open. Everything else here is
/// bookkeeping around that — that a listener hears an ordinary save, that unsubscribing works, and
/// that a listener throwing does not stop the theme being applied.
/// </remarks>
public class SettingsChangeTests : ScriptTestBase
{
    [Fact]
    public async Task A_listener_hears_a_saved_setting()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        var seen = await page.EvaluateAsync<string[]>("""
            () => {
                const seen = [];
                window.sednaUi.settings.onChange(s => seen.push(s.variant));
                window.sednaUi.settings.save('variant', 'light');
                window.sednaUi.settings.save('variant', 'dark');
                return seen;
            }
            """);

        Assert.Equal(["light", "dark"], seen);
    }

    [Fact]
    public async Task The_listener_sees_the_attributes_already_written()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        // apply() notifies last on purpose: a listener that reads the document — which is what a
        // page showing computed token values does — must not be handed the state it is replacing.
        var applied = await page.EvaluateAsync<string>("""
            () => {
                let attribute = 'never called';
                window.sednaUi.settings.onChange(() => {
                    attribute = document.documentElement.getAttribute('data-variant');
                });
                window.sednaUi.settings.save('variant', 'light');
                return attribute;
            }
            """);

        Assert.Equal("light", applied);
    }

    [Fact]
    public async Task Unsubscribing_stops_the_callbacks()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        var count = await page.EvaluateAsync<int>("""
            () => {
                let calls = 0;
                const off = window.sednaUi.settings.onChange(() => calls++);
                window.sednaUi.settings.save('variant', 'light');
                off();
                window.sednaUi.settings.save('variant', 'dark');
                return calls;
            }
            """);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task A_listener_that_throws_does_not_stop_the_theme_or_the_others()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        var result = await page.EvaluateAsync<string>("""
            () => {
                let reached = 'no';
                window.sednaUi.settings.onChange(() => { throw new Error('bad listener'); });
                window.sednaUi.settings.onChange(() => { reached = 'yes'; });
                window.sednaUi.settings.save('variant', 'light');
                return reached + ':' + document.documentElement.getAttribute('data-variant');
            }
            """);

        Assert.Equal("yes:light", result);
    }

    [Fact]
    public async Task A_stored_system_preference_notifies_when_the_OS_changes()
    {
        if (NoBrowser) return;

        // The whole reason ISednaSettings exists. Nothing in C# can observe this without the
        // callback: no request is made, no navigation happens, and the stored value never changes.
        var page = await Open("<div></div>",
            storage: new Dictionary<string, string> { ["sedna.variant"] = "system" });

        await page.EvaluateAsync("""
            () => {
                window.__variants = [];
                window.sednaUi.settings.onChange(s => window.__variants.push(
                    document.documentElement.getAttribute('data-variant')));
            }
            """);

        // Waited for rather than read straight after: the media-query change event is delivered
        // on its own task, so the emulation call returning is not the notification having run.
        await page.EmulateMediaAsync(new() { ColorScheme = Microsoft.Playwright.ColorScheme.Dark });
        await page.WaitForFunctionAsync("() => window.__variants.length >= 1");

        await page.EmulateMediaAsync(new() { ColorScheme = Microsoft.Playwright.ColorScheme.Light });
        await page.WaitForFunctionAsync("() => window.__variants.length >= 2");

        var variants = await page.EvaluateAsync<string[]>("() => window.__variants");

        Assert.Equal(["dark", "light"], variants);
    }

    [Fact]
    public async Task The_blazor_bridge_hands_out_an_id_and_takes_it_back()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        // The reference cannot be compared across interop calls, so the bridge is keyed on an id.
        // A stand-in object stands for the DotNetObjectReference: what matters here is that the
        // watcher is registered, invoked with the settings, and removable.
        var result = await page.EvaluateAsync<string>("""
            () => {
                const calls = [];
                const ref = { invokeMethodAsync: (name, s) => { calls.push(name + ':' + s.variant); } };

                const id = window.sednaUi.watchSettings(ref);
                window.sednaUi.settings.save('variant', 'light');

                const removed = window.sednaUi.unwatchSettings(id);
                window.sednaUi.settings.save('variant', 'dark');

                const again = window.sednaUi.unwatchSettings(id);
                return [id > 0, removed, again, calls.join('|')].join(' ');
            }
            """);

        // The id is real, the first unwatch removes it, the second reports nothing to remove, and
        // exactly one call was made — with the method name ISednaSettings declares.
        Assert.Equal("true true false SettingsChanged:light", result);
    }

    [Fact]
    public async Task A_disposed_reference_removes_its_own_watcher()
    {
        if (NoBrowser) return;
        var page = await Open("<div></div>");

        // A circuit ending is the normal case, and its reference throws on invoke. Left in place,
        // every navigation away would leave another dead listener behind for the life of the page.
        var calls = await page.EvaluateAsync<int>("""
            () => {
                let attempts = 0;
                const ref = { invokeMethodAsync: () => { attempts++; throw new Error('disposed'); } };

                window.sednaUi.watchSettings(ref);
                window.sednaUi.settings.save('variant', 'light');
                window.sednaUi.settings.save('variant', 'dark');
                return attempts;
            }
            """);

        Assert.Equal(1, calls);
    }
}
