using System.Collections.Concurrent;
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
/// <c>document.currentScript</c>. The same holds for the graph: <c>Sedna.UI.js</c> imports
/// <c>Sedna.UI.graph.js</c> relative to its own URL, and the module the engine relative to
/// its own, so every shipped file is served at its <c>wwwroot</c> path.
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
    /// <param name="timeZone">
    /// An IANA zone to emulate, for the two places the library reads one. Null leaves
    /// the runner's own, which is what a zone test must not assert against.
    /// </param>
    /// <param name="hasTouch">
    /// A touch screen, for a test that drives real touch input through CDP rather than
    /// synthesising pointer events: the browser's own decision between a scroll and a
    /// hold is what such a test is about.
    /// </param>
    /// <param name="reducedMotion">
    /// The reader's motion preference. <c>Reduce</c> makes the graph's layouts and view
    /// changes land at once, so a test reads the drawing it asked for rather than a frame
    /// of the way there.
    /// </param>
    /// <param name="forcedColors">Forced colours, as Windows high contrast sets them.</param>
    /// <param name="serve">
    /// Extra responses by path — the JSON a <c>data-graph-src</c> fetches, or an error.
    /// </param>
    /// <param name="beforeLoad">
    /// Runs on the page before it navigates: where a console listener goes when what is
    /// under test happens while the page loads.
    /// </param>
    protected async Task<IPage> Open(
        string body,
        string head = "",
        bool withMainScript = true,
        ColorScheme colorScheme = ColorScheme.Light,
        IDictionary<string, string>? storage = null,
        string? timeZone = null,
        bool hasTouch = false,
        ReducedMotion? reducedMotion = null,
        ForcedColors? forcedColors = null,
        IReadOnlyDictionary<string, Served>? serve = null,
        Action<IPage>? beforeLoad = null)
    {
        var context = await Browser!.NewContextAsync(new()
        {
            ColorScheme = colorScheme,
            TimezoneId = timeZone,
            HasTouch = hasTouch,
            ReducedMotion = reducedMotion,
            ForcedColors = forcedColors,
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
        beforeLoad?.Invoke(page);

        // A path the fixture serves itself, else the shipped file of that name under
        // wwwroot — the scripts, the stylesheet, the graph module, the engine and the
        // icon font, from the real files and as a web server would type them — else the
        // fixture page. Mapped rather than listed, so a module importing its neighbour by
        // relative path finds it exactly as it would in an app.
        await page.RouteAsync($"{Origin}/**", async route =>
        {
            Requests.Enqueue(route.Request);
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (serve is not null && serve.TryGetValue(path, out var served))
            {
                await route.FulfillAsync(new()
                {
                    Status = served.Status,
                    ContentType = served.ContentType,
                    Body = served.Body,
                });
                return;
            }

            if (Assets.WwwrootFile(path) is { } file)
            {
                await route.FulfillAsync(new()
                {
                    ContentType = Assets.ContentTypeOf(file),
                    BodyBytes = await File.ReadAllBytesAsync(file),
                });
                return;
            }

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
        });

        await page.GotoAsync($"{Origin}/fixture.html");
        return page;
    }

    /// <summary>A response the fixture serves at a path of its own.</summary>
    protected sealed record Served(string Body, string ContentType = "application/json; charset=utf-8", int Status = 200);

    /// <summary>Every request the fixture's origin answered, in order.</summary>
    protected ConcurrentQueue<IRequest> Requests { get; } = new();

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
    /// Loads a boot.js-only fixture and reports the variant (<c>dark</c> or
    /// <c>light</c>) it stamped on <c>&lt;html&gt;</c>.
    /// </summary>
    protected async Task<string> BootVariant(
        string bootTag, ColorScheme scheme, IDictionary<string, string>? storage = null)
    {
        // withMainScript: false — the main script would re-apply settings afterwards and
        // could mask a boot defect.
        var page = await Open("<p>x</p>", head: bootTag, withMainScript: false,
            colorScheme: scheme, storage: storage);
        return await page.EvaluateAsync<string>("() => document.documentElement.dataset.variant");
    }
}
