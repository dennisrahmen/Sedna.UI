using Sedna.UI.Catalogue.Tests.TestSupport;
using Microsoft.Playwright;
using Sedna.UI.Catalogue.Navigation;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Every page loads, and loads clean.
/// </summary>
/// <remarks>
/// A console error on a documentation site is worse than on an app: the reader
/// assumes the library is broken. Errors are collected before navigating, because a
/// listener attached afterwards misses everything the parse produced.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class PageLoadTests(CatalogueAppFixture app)
{
    [Fact]
    public async Task Every_page_loads_without_a_console_error()
    {
        if (app.NoBrowser) return;

        var problems = new List<string>();

        foreach (var route in RoutedPages.All)
        {
            var page = await app.Browser!.NewPageAsync();
            page.Console += (_, message) =>
            {
                // A photograph that did not arrive is not this page's fault. /media
                // loads from CatalogueAssets.PhotoHost, and a runner with no egress —
                // or a third party having a bad afternoon — logs one "failed to load
                // resource" per image. Failing the whole suite on somebody else's
                // uptime is the wrong trade for the one page that has an external
                // dependency at all, and the assets that actually matter are asserted
                // by /health.
                if (message.Type != "error") return;
                if (message.Text.Contains(CatalogueAssets.PhotoHost, StringComparison.OrdinalIgnoreCase)) return;

                problems.Add($"{route}: {message.Text}");
            };
            page.PageError += (_, error) => problems.Add($"{route}: {error}");

            var response = await page.GotoAsync(app.Url(route),
                new() { WaitUntil = WaitUntilState.NetworkIdle });

            if (response is null || !response.Ok)
                problems.Add($"{route}: returned {response?.Status.ToString() ?? "nothing"}");

            await page.CloseAsync();
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// A sub-route hit directly loads every asset, whatever the path depth.
    /// </summary>
    /// <remarks>
    /// The host page's asset paths are relative, so what they mean depends on the
    /// document's base URL. This is the case that has no base of its own: a trailing
    /// slash makes the browser resolve them one level deeper, and every one 404s
    /// unless <c>&lt;base href="/"&gt;</c> is there to say otherwise. The document
    /// still returns 200 — routing ignores the trailing slash — so the page renders
    /// unstyled and never becomes interactive, with nothing logged and no failing
    /// status to notice.
    ///
    /// It is asserted through a browser rather than over the markup because the
    /// markup is identical either way: the resolution is the browser's, and only a
    /// browser can be asked what it actually requested.
    /// </remarks>
    [Fact]
    public async Task A_sub_route_hit_directly_resolves_its_assets_against_the_root()
    {
        if (app.NoBrowser) return;

        var route = RoutedPages.All.First(r => r != "/");
        var page = await app.Browser!.NewPageAsync();
        var failures = new List<string>();

        page.Response += (_, response) =>
        {
            if (response.Status >= 400) failures.Add($"{response.Status} {response.Url}");
        };

        // Both forms of the same page. The bare one passed before the <base> existed,
        // because these routes are one segment deep and "/x" already resolves a
        // relative path against the root — so on its own it proves nothing, and it is
        // here to show that the fix did not move the problem.
        foreach (var url in new[] { app.Url(route.TrimStart('/')), app.Url(route.TrimStart('/') + "/") })
        {
            var response = await page.GotoAsync(url,
                new() { WaitUntil = WaitUntilState.NetworkIdle });

            Assert.NotNull(response);
            Assert.True(response!.Ok, $"{url} returned {response.Status}.");
        }

        await page.CloseAsync();

        Assert.True(failures.Count == 0,
            "A direct hit asked for assets that do not exist, so the host page has no "
            + "<base href=\"/\"> — or it is declared after them:"
            + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Every_page_renders_its_examples_and_its_navigation()
    {
        if (app.NoBrowser) return;

        var problems = new List<string>();

        foreach (var route in RoutedPages.All)
        {
            var page = await app.OpenAsync(route);

            // Scoped to .cat-drawer, which is the layout's own sidebar. A bare
            // ".sidebar .nav-link" also matches the demos on /frame and /layouts,
            // which render sidebars of their own — complete with an active link,
            // because that is what they are demonstrating.
            var sidebar = page.Locator(".cat-drawer .sidebar");

            // The sidebar is built from the registry, so a page that renders none of
            // it has lost its layout rather than its content. The rail carries every
            // group; the panel carries only the current one's pages.
            var areas = await sidebar.Locator(".nav-area").CountAsync();
            if (areas != CataloguePages.Groups.Count)
                problems.Add($"{route}: {areas} areas rendered, expected {CataloguePages.Groups.Count}");

            var activeAreas = await sidebar.Locator(".nav-area.active").CountAsync();
            if (activeAreas != 1) problems.Add($"{route}: {activeAreas} areas are active, expected 1");

            var active = await sidebar.Locator(".nav-link.active").CountAsync();
            if (active != 1) problems.Add($"{route}: {active} nav links are active, expected 1");

            // Server-rendered, so this holds before the circuit connects.
            var blocks = await page.Locator(".code-block pre code").AllTextContentsAsync();
            if (blocks.Any(string.IsNullOrWhiteSpace))
                problems.Add($"{route}: a code block rendered empty");

            await page.CloseAsync();
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Desktop, a phone, and the mirrored layout. A spill is a width problem, so the
    /// narrow viewport is where it shows, and a logical property written as a
    /// physical one only shows under <c>dir="rtl"</c>.
    /// </summary>
    public static TheoryData<int, string> Viewports() => new()
    {
        { 1280, "ltr" },
        { 375, "ltr" },
        { 1280, "rtl" },
    };

    [Theory]
    [MemberData(nameof(Viewports))]
    public async Task No_demo_spills_out_of_its_box(int width, string dir)
    {
        if (app.NoBrowser) return;

        // The defect this pins is the one a reader reports first: a panel that hangs
        // off the side of the bordered box it is in, so it has to be scrolled to
        // before it can be read. A source scan cannot see it; only a layout engine
        // can.
        //
        // OUT-OF-FLOW ELEMENTS ONLY, and that restriction is the whole design.
        // `.ex-demo` is itself a scroll container, so anything in the normal flow
        // that is wider than the box — a table, a wide toolbar — scrolls inside it,
        // which is correct and is what the library's own `.sedna-scroll` documents. A
        // dropdown cannot use that escape: scrolling the box to reach the panel
        // moves the trigger out from under it. So a floating panel must fit, and a
        // table need not.
        //
        // It also makes the measurement machine-independent. Whether a table is a
        // few pixels wider than a phone depends on the font stack, and this suite
        // runs on Windows and on a Linux runner; whether an absolutely positioned
        // panel is anchored to the wrong edge does not.
        //
        // Two pixels of tolerance, because a subpixel box measured against a
        // subpixel parent differs in the last decimal on any fractional layout.
        var problems = new List<string>();

        const string measure =
            """
            () => Array.from(document.querySelectorAll('.ex-demo')).flatMap(demo => {
                const box = demo.getBoundingClientRect();
                return Array.from(demo.querySelectorAll('*')).filter(el => {
                    const style = getComputedStyle(el);
                    if (style.display === 'none' || style.visibility === 'hidden') return false;

                    // The top layer is not in the box at all and is not meant to be.
                    if (el.matches(':popover-open') || el.matches('dialog[open]')) return false;
                    if (el.closest('[popover], dialog')) return false;

                    if (style.position !== 'absolute' && style.position !== 'sticky') return false;

                    // A sticky cell inside a scroller the EXAMPLE created — a wide
                    // table in a .sedna-scroll-x — takes the same escape the note
                    // above grants tables: scrolling the region to reach it is the
                    // intended interaction and moves nothing out from under
                    // anything. A pinned column exists precisely to sit outside the
                    // visible box while the rest scrolls past it.
                    //
                    // The scroller has to be one of the example's own, strictly
                    // between the element and .ex-demo. Exempting .ex-demo itself
                    // would exempt every dropdown too, which is the case this
                    // measurement is for.
                    for (let a = el.parentElement; a && a !== demo; a = a.parentElement) {
                        const ox = getComputedStyle(a).overflowX;
                        if (ox === 'auto' || ox === 'scroll') return false;
                    }

                    const r = el.getBoundingClientRect();
                    if (r.width === 0 && r.height === 0) return false;

                    return r.left < box.left - 2 || r.right > box.right + 2;
                }).map(el => (el.className || el.tagName) + '');
            })
            """;

        // And the user-visible consequence at the narrow width, which is the same
        // question asked of the document rather than of one box: nothing may push
        // the page sideways. This holds whatever the font metrics, because a demo
        // that scrolls internally does not widen its ancestors.
        const string documentScroll =
            """
            () => {
                const el = document.documentElement;
                return Math.max(0, el.scrollWidth - el.clientWidth);
            }
            """;

        foreach (var route in RoutedPages.All)
        {
            var page = await app.OpenAsync(route);
            await page.SetViewportSizeAsync(width, 900);
            await page.EvaluateAsync("d => document.documentElement.setAttribute('dir', d)", dir);

            var spills = new List<string>(await page.EvaluateAsync<string[]>(measure));

            // Then once per delegated menu, because a closed panel is `hidden` and
            // the static render therefore cannot show the commonest cause of a
            // spill: a 220px panel anchored to the trailing edge of a narrow
            // trigger. One at a time — an open panel covers the next trigger, and
            // `data-menu-toggle` is handled by the library's own script, which runs
            // without the circuit.
            var toggles = await page.Locator(".ex-demo [data-menu-toggle]").CountAsync();
            for (var i = 0; i < toggles; i++)
            {
                var toggle = page.Locator(".ex-demo [data-menu-toggle]").Nth(i);
                await toggle.ClickAsync();
                spills.AddRange(await page.EvaluateAsync<string[]>(measure));
                await page.Keyboard.PressAsync("Escape");
            }

            foreach (var spill in spills.Distinct(StringComparer.Ordinal))
                problems.Add($"{route} at {width}px {dir}: '{spill}' extends outside its .ex-demo box");

            var overflow = await page.EvaluateAsync<int>(documentScroll);
            if (overflow > 2)
                problems.Add($"{route} at {width}px {dir}: the page scrolls {overflow}px sideways");

            await page.CloseAsync();
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public async Task The_active_link_follows_navigation_without_a_reload()
    {
        if (app.NoBrowser) return;

        // The regression this pins: the sidebar survives navigation, so nothing
        // re-renders it unless it subscribes to LocationChanged ITSELF. Subscribing
        // in the layout instead compiles, runs, and does nothing — Blazor skips a
        // child component whose parameters are unchanged, so the links kept
        // reporting the address of the page the reader arrived on and only caught up
        // when some other interaction happened to change a parameter.
        var page = await app.OpenInteractiveAsync("/");
        var sidebar = page.Locator(".cat-drawer .sidebar");

        foreach (var route in new[] { "/badge", "/card", "/table", "/" })
        {
            // A page in another group is behind its area: the area link opens the
            // group's first page, and the panel then holds the link to click.
            var group = CataloguePages.All.Single(p => p.Route == route).Group;
            var areaHref = CataloguePages.InGroup(group).First().Route;
            if (await sidebar.Locator($".nav-link[href='{route}']").CountAsync() == 0)
            {
                await sidebar.Locator($".nav-area[href='{areaHref}']").ClickAsync();
                await sidebar.Locator($".nav-area.active[href='{areaHref}']")
                    .WaitForAsync(new() { Timeout = 10_000 });
            }

            await sidebar.Locator($".nav-link[href='{route}']").ClickAsync();

            // A Playwright timeout here names the selector, which is the whole
            // diagnosis: the link for the page we are on never became active.
            await sidebar.Locator($".nav-link.active[href='{route}']")
                .WaitForAsync(new() { Timeout = 10_000 });

            var active = await sidebar.Locator(".nav-link.active").AllAsync();
            var hrefs = await Task.WhenAll(active.Select(a => a.GetAttributeAsync("href")));

            Assert.Equal([route], hrefs.Select(h => h ?? "«no href»").ToList());
            Assert.Equal(route, await sidebar.Locator("[aria-current='page']").GetAttributeAsync("href"));
        }

        await page.CloseAsync();
    }
}
