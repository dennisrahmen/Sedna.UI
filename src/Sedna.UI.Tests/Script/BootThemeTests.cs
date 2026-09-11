using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// boot.js resolves the theme name and the variant before first paint. A stored
/// variant choice always wins: choosing light on a dark machine must not be
/// reverted on the next load.
/// </summary>
public class BootThemeTests : ScriptTestBase
{
    [Fact]
    public async Task Boot_defaults_to_dark_and_ignores_the_machine_until_asked()
    {
        if (NoBrowser) return;
        // The default must not change for an already-released app, so a light machine
        // still gets dark without data-variant-default="system".
        Assert.Equal("dark", await BootVariant(BootTag, ColorScheme.Light));
        Assert.Equal("dark", await BootVariant(BootTag, ColorScheme.Dark));
    }

    [Fact]
    public async Task Boot_follows_the_machine_only_when_the_default_is_system()
    {
        if (NoBrowser) return;
        const string tag = """<script src="/js/Sedna.UI.boot.js" data-variant-default="system"></script>""";

        Assert.Equal("light", await BootVariant(tag, ColorScheme.Light));
        Assert.Equal("dark", await BootVariant(tag, ColorScheme.Dark));
    }

    [Fact]
    public async Task Boot_honours_an_explicit_light_default()
    {
        if (NoBrowser) return;
        const string tag = """<script src="/js/Sedna.UI.boot.js" data-variant-default="light"></script>""";

        Assert.Equal("light", await BootVariant(tag, ColorScheme.Dark));
    }

    [Theory]
    [InlineData("light", ColorScheme.Dark)]
    [InlineData("dark", ColorScheme.Light)]
    public async Task A_stored_choice_beats_both_the_default_and_the_machine(
        string stored, ColorScheme machine)
    {
        if (NoBrowser) return;
        // The case that matters: choosing light on a dark machine must not be silently
        // reverted on the next load. Checked in both directions, and against
        // data-variant-default="system" where the machine would otherwise decide.
        const string tag = """<script src="/js/Sedna.UI.boot.js" data-variant-default="system"></script>""";
        var storage = new Dictionary<string, string> { ["sedna.variant"] = stored };

        Assert.Equal(stored, await BootVariant(tag, machine, storage));
        Assert.Equal(stored, await BootVariant(BootTag, machine, storage));
    }

    [Fact]
    public async Task A_stored_system_preference_still_follows_the_machine()
    {
        if (NoBrowser) return;
        // "system" is a real, storable preference (see SednaUiSettings.Variant) —
        // not just the script tag's own fallback for someone who has chosen
        // nothing. A stored "system" must resolve against the machine every load,
        // even under the plain (dark-default) boot tag.
        var storage = new Dictionary<string, string> { ["sedna.variant"] = "system" };

        Assert.Equal("light", await BootVariant(BootTag, ColorScheme.Light, storage));
        Assert.Equal("dark", await BootVariant(BootTag, ColorScheme.Dark, storage));
    }

    [Fact]
    public async Task Boot_stamps_the_theme_name_and_defaults_to_sedna()
    {
        if (NoBrowser) return;
        // data-theme is now WHICH theme, orthogonal to data-variant. Nothing stored
        // falls back to the shipped default; a stored name is any registered theme.
        var page = await Open("<p>x</p>", head: BootTag, withMainScript: false);
        Assert.Equal("sedna",
            await page.EvaluateAsync<string>("() => document.documentElement.dataset.theme"));

        var forest = await Open("<p>x</p>", head: BootTag, withMainScript: false,
            storage: new Dictionary<string, string> { ["sedna.theme"] = "forest" });
        Assert.Equal("forest",
            await forest.EvaluateAsync<string>("() => document.documentElement.dataset.theme"));
    }

    [Fact]
    public async Task Boot_stamps_the_apps_own_default_theme_when_nothing_is_stored()
    {
        if (NoBrowser) return;
        // SednaUiBrand.ToCss emits SednaUiOptions.Default at bare :root and every other
        // registered theme at [data-theme="<name>"], so a first visit stamped with the
        // built-in name selects a palette the app did not choose — or none at all.
        const string tag =
            """<script src="/js/Sedna.UI.boot.js" data-theme-default="northwind"></script>""";

        var page = await Open("<p>x</p>", head: tag, withMainScript: false);
        Assert.Equal("northwind",
            await page.EvaluateAsync<string>("() => document.documentElement.dataset.theme"));

        // A stored name is still a choice, and still wins.
        var stored = await Open("<p>x</p>", head: tag, withMainScript: false,
            storage: new Dictionary<string, string> { ["sedna.theme"] = "forest" });
        Assert.Equal("forest",
            await stored.EvaluateAsync<string>("() => document.documentElement.dataset.theme"));
    }

    [Fact]
    public async Task The_main_script_falls_back_to_the_configured_default_theme()
    {
        if (NoBrowser) return;
        // The other half: configure() carries the same name, because the boot script's
        // attribute cannot reach settings.load() and an app reading it back would be
        // told a theme is active that is not the one on <html>.
        var page = await Open("<p>x</p>");

        var before = await page.EvaluateAsync<string>("() => sednaUi.settings.load().theme");
        Assert.Equal("sedna", before);

        await page.EvaluateAsync("() => sednaUi.configure({ themeDefault: 'northwind' })");

        Assert.Equal("northwind",
            await page.EvaluateAsync<string>("() => sednaUi.settings.load().theme"));
        // configure() applies, so the document says the same thing the load does.
        Assert.Equal("northwind",
            await page.EvaluateAsync<string>("() => document.documentElement.dataset.theme"));
    }

    [Fact]
    public async Task A_stored_theme_beats_the_configured_default_in_both_scripts()
    {
        if (NoBrowser) return;
        const string tag =
            """<script src="/js/Sedna.UI.boot.js" data-theme-default="northwind"></script>""";

        var page = await Open("<p>x</p>", head: tag,
            storage: new Dictionary<string, string> { ["sedna.theme"] = "forest" });

        await page.EvaluateAsync("() => sednaUi.configure({ themeDefault: 'northwind' })");

        Assert.Equal("forest",
            await page.EvaluateAsync<string>("() => document.documentElement.dataset.theme"));
        Assert.Equal("forest",
            await page.EvaluateAsync<string>("() => sednaUi.settings.load().theme"));
    }

    [Fact]
    public async Task Boot_stamps_the_other_stored_settings_and_the_language()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>", head: BootTag, withMainScript: false,
            storage: new Dictionary<string, string>
            {
                ["sedna.cvd"] = "1",
                ["sedna.density"] = "compact",
                ["sedna.lang"] = "de",
            });

        var root = await page.EvaluateAsync<JsonElement>("""
            () => ({ cvd: document.documentElement.dataset.cvd,
                     density: document.documentElement.dataset.density,
                     lang: document.documentElement.lang })
            """);

        Assert.Equal("1", root.GetProperty("cvd").GetString());
        Assert.Equal("compact", root.GetProperty("density").GetString());
        Assert.Equal("de", root.GetProperty("lang").GetString());
    }

    [Fact]
    public async Task The_boot_and_main_scripts_agree_on_where_the_variant_is_stored()
    {
        if (NoBrowser) return;
        // Both default to the "sedna." prefix. If they disagreed, the boot script would
        // read a key the settings code never writes, and the choice would appear to be
        // forgotten on every reload. ScriptContractTests pins the two literals; this
        // checks the two implementations actually meet.
        var page = await Open("<p>x</p>", head: BootTag);

        await page.EvaluateAsync("() => sednaUi.settings.save('variant', 'light')");
        var storedKey = await page.EvaluateAsync<string?>("() => localStorage.getItem('sedna.variant')");
        Assert.Equal("light", storedKey);

        await page.ReloadAsync();
        Assert.Equal("light",
            await page.EvaluateAsync<string>("() => document.documentElement.dataset.variant"));
    }

    [Fact]
    public async Task The_boot_and_main_scripts_agree_on_where_the_theme_name_is_stored()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>", head: BootTag);

        await page.EvaluateAsync("() => sednaUi.settings.save('theme', 'forest')");
        var storedKey = await page.EvaluateAsync<string?>("() => localStorage.getItem('sedna.theme')");
        Assert.Equal("forest", storedKey);

        await page.ReloadAsync();
        Assert.Equal("forest",
            await page.EvaluateAsync<string>("() => document.documentElement.dataset.theme"));
    }
}
