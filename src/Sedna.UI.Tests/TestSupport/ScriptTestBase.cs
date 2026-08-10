using System.Text.Json;
using Microsoft.Playwright;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// Serves a fixture page carrying the shipped scripts, for testing the behaviour behind
/// <c>window.sednaUi</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>sednaUi</c> is a contract several apps call into: adding a member is minor,
/// removing or renaming one is major. A source scan cannot test any of it — the
/// interesting parts are what the platform does, such as whether a promise settles when
/// a <c>&lt;dialog&gt;</c> closes, or whether a hidden element leaves the tab order.
/// </para>
/// <para>
/// Pages are served over a fake HTTPS origin through request interception rather than
/// <c>file://</c>. Two reasons: <c>localStorage</c> needs a real origin, which
/// <c>boot.js</c> depends on entirely; and the scripts have to be genuine
/// <c>&lt;script src&gt;</c> tags, because <c>boot.js</c> reads its options off
/// <c>document.currentScript</c>.
/// </para>
/// </remarks>
public abstract class ScriptTestBase : BrowserTestBase
{
    private const string Origin = "https://sedna-ui.test";

    /// <summary>
    /// Opens a fixture page with <paramref name="body"/> inside <c>&lt;body&gt;</c> and
    /// the shipped script loaded at the end of it.
    /// </summary>
    /// <param name="body">Markup for the fixture.</param>
    /// <param name="head">
    /// Extra markup for <c>&lt;head&gt;</c> — where a <c>boot.js</c> tag goes, so it runs
    /// before first paint with its real attributes.
    /// </param>
    /// <param name="withMainScript">
    /// False for a boot.js-only fixture, so the main script cannot overwrite what boot
    /// stamped and hide a boot defect.
    /// </param>
    /// <param name="colorScheme">The OS preference to emulate.</param>
    /// <param name="storage">localStorage seeded before any script runs.</param>
    protected async Task<IPage> Open(
        string body,
        string head = "",
        bool withMainScript = true,
        ColorScheme colorScheme = ColorScheme.Light,
        IDictionary<string, string>? storage = null)
    {
        var context = await Browser!.NewContextAsync(new()
        {
            ColorScheme = colorScheme,
            // The copy tests need the clipboard without a permission prompt.
            Permissions = new[] { "clipboard-read", "clipboard-write" },
        });

        // Seeding storage has to happen before the document's own scripts run, which is
        // exactly what an init script is for — boot.js reads it during parse.
        if (storage is { Count: > 0 })
        {
            var json = JsonSerializer.Serialize(storage);
            await context.AddInitScriptAsync(
                $"try {{ const s = {json}; for (const k in s) localStorage.setItem(k, s[k]); }} catch (e) {{}}");
        }

        var page = await context.NewPageAsync();

        await page.RouteAsync($"{Origin}/**", async route =>
        {
            var url = new Uri(route.Request.Url).AbsolutePath;
            switch (url)
            {
                case "/js/Sedna.UI.js":
                    await route.FulfillAsync(new()
                    {
                        ContentType = "text/javascript",
                        Body = await File.ReadAllTextAsync(Assets.JsPath),
                    });
                    return;
                case "/js/Sedna.UI.boot.js":
                    await route.FulfillAsync(new()
                    {
                        ContentType = "text/javascript",
                        Body = await File.ReadAllTextAsync(Assets.BootJsPath),
                    });
                    return;
                case "/css/Sedna.UI.css":
                    await route.FulfillAsync(new()
                    {
                        ContentType = "text/css",
                        Body = await File.ReadAllTextAsync(Assets.CssPath),
                    });
                    return;
                case "/lib/remixicon/remixicon.css":
                    await route.FulfillAsync(new()
                    {
                        ContentType = "text/css",
                        Body = await File.ReadAllTextAsync(Assets.IconCssPath),
                    });
                    return;
                default:
                    await route.FulfillAsync(new()
                    {
                        ContentType = "text/html",
                        Body = $"""
                            <!DOCTYPE html>
                            <html lang="en">
                            <head><meta charset="utf-8"><title>fixture</title>{head}</head>
                            <body>
                            {body}
                            {(withMainScript ? "<script src=\"/js/Sedna.UI.js\"></script>" : "")}
                            </body>
                            </html>
                            """,
                    });
                    return;
            }
        });

        await page.GotoAsync($"{Origin}/fixture.html");
        return page;
    }

    /// <summary>The <c>boot.js</c> tag with no options, i.e. every default.</summary>
    protected const string BootTag = """<script src="/js/Sedna.UI.boot.js"></script>""";

    /// <summary>
    /// The shipped stylesheet and the icon font, for a fixture that tests CSS rather
    /// than script.
    /// </summary>
    /// <remarks>
    /// The CSS guards used to load a catalogue page and use it as "a page with the
    /// stylesheet on it". They build their own markup anyway, so the page was
    /// incidental — and taking it away removes the catalogue from the library's CSS
    /// guarantees entirely, which is the point of the split.
    /// </remarks>
    protected const string StylesheetTag =
        """
        <link rel="stylesheet" href="/lib/remixicon/remixicon.css">
        <link rel="stylesheet" href="/css/Sedna.UI.css">
        """;

    /// <summary>
    /// Opens a fixture carrying the shipped stylesheet, and collects console errors.
    /// </summary>
    protected async Task<(IPage Page, List<string> Errors)> OpenStyled(
        string body, string extraHead = "")
    {
        var errors = new List<string>();
        var page = await Open(body, head: StylesheetTag + extraHead);
        page.Console += (_, message) =>
        {
            if (message.Type == "error") errors.Add(message.Text);
        };
        page.PageError += (_, error) => errors.Add(error);

        return (page, errors);
    }

    /// <summary>
    /// Loads a boot.js-only fixture and reports the theme it stamped on
    /// <c>&lt;html&gt;</c>.
    /// </summary>
    protected async Task<string> BootTheme(
        string bootTag, ColorScheme scheme, IDictionary<string, string>? storage = null)
    {
        // withMainScript: false — the main script would re-apply settings afterwards and
        // could mask a boot defect.
        var page = await Open("<p>x</p>", head: bootTag, withMainScript: false,
            colorScheme: scheme, storage: storage);
        return await page.EvaluateAsync<string>("() => document.documentElement.dataset.theme");
    }
}
