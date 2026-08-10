using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// boot.js resolves the theme before first paint. A stored choice always wins: choosing light on a dark machine must not be reverted on the next load.
/// </summary>
public class BootThemeTests : ScriptTestBase
{
    [Fact]
    public async Task Boot_defaults_to_dark_and_ignores_the_machine_until_asked()
    {
        if (NoBrowser) return;
        // The default must not change for an already-released app, so a light machine
        // still gets dark without data-theme-default="system".
        Assert.Equal("dark", await BootTheme(BootTag, ColorScheme.Light));
        Assert.Equal("dark", await BootTheme(BootTag, ColorScheme.Dark));
    }

    [Fact]
    public async Task Boot_follows_the_machine_only_when_the_default_is_system()
    {
        if (NoBrowser) return;
        const string tag = """<script src="/js/Sedna.UI.boot.js" data-theme-default="system"></script>""";

        Assert.Equal("light", await BootTheme(tag, ColorScheme.Light));
        Assert.Equal("dark", await BootTheme(tag, ColorScheme.Dark));
    }

    [Fact]
    public async Task Boot_honours_an_explicit_light_default()
    {
        if (NoBrowser) return;
        const string tag = """<script src="/js/Sedna.UI.boot.js" data-theme-default="light"></script>""";

        Assert.Equal("light", await BootTheme(tag, ColorScheme.Dark));
    }

    [Theory]
    [InlineData("light", ColorScheme.Dark)]
    [InlineData("dark", ColorScheme.Light)]
    public async Task A_stored_choice_beats_both_the_default_and_the_machine(
        string stored, ColorScheme machine)
    {
        if (NoBrowser) return;
        if (NoBrowser) return;
        // The case that matters: choosing light on a dark machine must not be silently
        // reverted on the next load. Checked in both directions, and against
        // data-theme-default="system" where the machine would otherwise decide.
        const string tag = """<script src="/js/Sedna.UI.boot.js" data-theme-default="system"></script>""";
        var storage = new Dictionary<string, string> { ["sedna.theme"] = stored };

        Assert.Equal(stored, await BootTheme(tag, machine, storage));
        Assert.Equal(stored, await BootTheme(BootTag, machine, storage));
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
    public async Task The_boot_and_main_scripts_agree_on_where_a_setting_is_stored()
    {
        if (NoBrowser) return;
        // Both default to the "sedna." prefix. If they disagreed, the theme boot would
        // read a key the settings code never writes, and the choice would appear to be
        // forgotten on every reload. ShippedAssetsTests pins the two literals; this
        // checks the two implementations actually meet.
        var page = await Open("<p>x</p>", head: BootTag);

        await page.EvaluateAsync("() => sednaUi.settings.save('theme', 'light')");
        var storedKey = await page.EvaluateAsync<string?>("() => localStorage.getItem('sedna.theme')");
        Assert.Equal("light", storedKey);

        await page.ReloadAsync();
        Assert.Equal("light",
            await page.EvaluateAsync<string>("() => document.documentElement.dataset.theme"));
    }
}
