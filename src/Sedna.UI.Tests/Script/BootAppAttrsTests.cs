using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-app-attrs</c> mirrors the app's own stored preferences onto <c>&lt;html&gt;</c>
/// before first paint, so a page no circuit reaches still paints the reader's choice.
/// </summary>
public class BootAppAttrsTests : ScriptTestBase
{
    private const string Tag =
        """<script src="/js/Sedna.UI.boot.js" data-app-attrs="mochi"></script>""";

    // An inline script at the top of <body> runs before first paint, after boot.js —
    // what it sees is what the first frame is styled with.
    private const string Probe =
        """<script>window.seen = document.documentElement.getAttribute('data-app-mochi');</script><p>x</p>""";

    private static Task<string?> Attr(Microsoft.Playwright.IPage page, string name)
        => page.EvaluateAsync<string?>($"() => document.documentElement.getAttribute('{name}')");

    [Fact]
    public async Task A_stored_value_is_on_html_before_the_body_is_parsed()
    {
        if (NoBrowser) return;
        var page = await Open(Probe, head: Tag, withMainScript: false,
            storage: new Dictionary<string, string> { ["sedna.mochi"] = "off" });

        Assert.Equal("off", await page.EvaluateAsync<string?>("() => window.seen"));
        Assert.Equal("off", await Attr(page, "data-app-mochi"));
    }

    [Fact]
    public async Task Nothing_stored_writes_no_attribute()
    {
        if (NoBrowser) return;
        var page = await Open(Probe, head: Tag, withMainScript: false);

        Assert.Null(await page.EvaluateAsync<string?>("() => window.seen"));
        Assert.Null(await Attr(page, "data-app-mochi"));
    }

    [Fact]
    public async Task No_attribute_on_the_tag_mirrors_nothing()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>", head: BootTag, withMainScript: false,
            storage: new Dictionary<string, string> { ["sedna.mochi"] = "off" });

        Assert.Null(await Attr(page, "data-app-mochi"));
    }

    [Fact]
    public async Task Every_listed_name_is_mirrored_and_an_unsafe_one_is_ignored()
    {
        if (NoBrowser) return;
        // A name goes into a storage key and an attribute name, so anything outside the
        // token pattern is skipped — and skipping it must not stop the names after it.
        const string tag =
            """<script src="/js/Sedna.UI.boot.js" data-app-attrs="Mochi x=y 9lives mochi  compact-rows"></script>""";
        var page = await Open("<p>x</p>", head: tag, withMainScript: false,
            storage: new Dictionary<string, string>
            {
                ["sedna.mochi"] = "off",
                ["sedna.compact-rows"] = "1",
                ["sedna.Mochi"] = "on",
                ["sedna.9lives"] = "on",
                ["sedna.x=y"] = "on",
            });

        Assert.Equal("off", await Attr(page, "data-app-mochi"));
        Assert.Equal("1", await Attr(page, "data-app-compact-rows"));
        var names = await page.EvaluateAsync<string[]>(
            "() => [...document.documentElement.attributes].map(a => a.name).filter(n => n.startsWith('data-app-'))");
        Assert.Equal(new[] { "data-app-mochi", "data-app-compact-rows" }, names);
    }

    [Fact]
    public async Task A_value_is_only_ever_an_attribute_value()
    {
        if (NoBrowser) return;
        const string hostile = "\"><img src=x onerror=\"window.ran=1\">";
        var page = await Open("<p>x</p>", head: Tag, withMainScript: false,
            storage: new Dictionary<string, string> { ["sedna.mochi"] = hostile });

        Assert.Equal(hostile, await Attr(page, "data-app-mochi"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => document.querySelectorAll('img').length"));
        Assert.Null(await page.EvaluateAsync<int?>("() => window.ran ?? null"));
    }

    [Fact]
    public async Task A_custom_prefix_is_honoured()
    {
        if (NoBrowser) return;
        const string tag =
            """<script src="/js/Sedna.UI.boot.js" data-prefix="app-a." data-app-attrs="mochi"></script>""";
        var page = await Open("<p>x</p>", head: tag, withMainScript: false,
            storage: new Dictionary<string, string>
            {
                ["sedna.mochi"] = "on",
                ["app-a.mochi"] = "off",
            });

        Assert.Equal("off", await Attr(page, "data-app-mochi"));
    }

    [Fact]
    public async Task Blocked_storage_does_not_break_the_boot()
    {
        if (NoBrowser) return;
        // The tz cookie runs after the app attributes, so it arriving proves the boot
        // script got past them instead of throwing out of the whole IIFE.
        const string tag =
            """<script src="/js/Sedna.UI.boot.js" data-app-attrs="mochi" data-tz-cookie="tz"></script>""";
        var errors = new List<string>();
        var page = await Open("<p>x</p>", head: tag, withMainScript: false);
        page.PageError += (_, e) => errors.Add(e);

        await page.AddInitScriptAsync("""
            Object.defineProperty(window, 'localStorage', {
                get() { throw new Error('blocked'); }
            });
            """);
        await page.ReloadAsync();

        Assert.Empty(errors);
        Assert.Null(await Attr(page, "data-app-mochi"));
        Assert.StartsWith("tz=", await page.EvaluateAsync<string>("() => document.cookie"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_through_the_settings_api_keeps_the_attribute_in_step()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>", head: Tag);
        Assert.Null(await Attr(page, "data-app-mochi"));

        await page.EvaluateAsync("() => sednaUi.settings.save('mochi', 'off')");
        Assert.Equal("off", await page.EvaluateAsync<string?>("() => localStorage.getItem('sedna.mochi')"));
        Assert.Equal("off", await Attr(page, "data-app-mochi"));

        await page.EvaluateAsync("() => sednaUi.settings.save('mochi', 'on')");
        Assert.Equal("on", await Attr(page, "data-app-mochi"));

        // Storage is the source: a key removed is an attribute removed on the next apply.
        await page.EvaluateAsync("() => { localStorage.removeItem('sedna.mochi'); sednaUi.settings.apply(); }");
        Assert.Null(await Attr(page, "data-app-mochi"));
    }

    [Fact]
    public async Task The_main_script_leaves_unlisted_keys_alone()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>", head: BootTag);

        await page.EvaluateAsync("() => sednaUi.settings.save('mochi', 'off')");
        Assert.Null(await Attr(page, "data-app-mochi"));
    }
}
