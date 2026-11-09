using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>.page-banner</c> sits directly under the topbar in every shell, spans the content column,
/// starts its words on the page's own column, scrolls away under a sticky header, and keeps
/// clear of the device's insets the way the topbar does.
/// </summary>
public class BannerLayoutTests : ScriptTestBase
{
    private const string Banner =
        """
        <div class="page-banner page-banner--info" id="banner" role="region" aria-label="Notice">
          <i class="ri-information-line" id="icon" aria-hidden="true"></i>
          <div class="page-banner-body" id="body"><span id="words">Exports pause on Saturday from 06:00 to 08:00 UTC.</span></div>
          <div class="page-banner-actions" id="actions"><a class="btn btn--sm" href="#">Details</a></div>
          <button class="page-banner-dismiss" id="dismiss" type="button" aria-label="Dismiss"><i class="ri-close-line"></i></button>
        </div>
        """;

    private const string Page = """<main class="page" id="page"><p id="first">Scheduled exports run at night.</p></main>""";

    /// <summary>Every shell a banner goes in, by name.</summary>
    public static TheoryData<string> Shells() => new() { "layout", "responsive", "flow", "bottombar", "bare" };

    private static string Shell(string shell) => shell switch
    {
        "bare" => $"""<div class="bare-layout" id="shell"><header class="topbar" id="topbar"></header>{Banner}{Page}</div>""",
        _ => $"""
            <div class="layout {Modifier(shell)}" id="shell">
              <aside class="sidebar"><div class="brand"><span class="brand-text">Orders console</span></div>
                <nav class="nav"><a class="nav-link" href="#"><i class="ri-home-line"></i><span>Overview</span></a></nav></aside>
              <div class="content" id="content">
                <header class="topbar" id="topbar"></header>
                {Banner}
                {Page}
              </div>
            </div>
            """,
    };

    private static string Modifier(string shell) => shell switch
    {
        "responsive" => "layout--responsive",
        "flow" => "layout--flow",
        "bottombar" => "layout--bottombar",
        _ => "",
    };

    private sealed class Rect
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private static Task<Rect> RectOf(IPage page, string id) =>
        page.EvaluateAsync<Rect>($$"""
            () => { const r = document.getElementById('{{id}}').getBoundingClientRect();
                    return { left: r.left, top: r.top, right: r.right, bottom: r.bottom, width: r.width, height: r.height }; }
            """);

    /// <summary>Where an element's first line starts, on the inline axis: a mixed-direction line is several runs.</summary>
    private static Task<double> TextStart(IPage page, string id, bool rtl) =>
        page.EvaluateAsync<double>($$"""
            () => { const r = document.createRange(); r.selectNodeContents(document.getElementById('{{id}}'));
                    const rects = [...r.getClientRects()].filter(x => x.width > 0);
                    const top = Math.min(...rects.map(x => x.top));
                    const line = rects.filter(x => Math.abs(x.top - top) < 2);
                    return {{(rtl ? "Math.max(...line.map(x => x.right))" : "Math.min(...line.map(x => x.left))")}}; }
            """);

    [Theory]
    [MemberData(nameof(Shells))]
    public async Task The_banner_sits_directly_under_the_topbar_across_the_content_column(string shell)
    {
        if (NoBrowser) return;
        foreach (var width in new[] { 1280, 400 })
        {
            var (page, errors) = await OpenStyled(Shell(shell));
            await page.SetViewportSizeAsync(width, 700);
            await Js.Settle(page);

            var topbar = await RectOf(page, "topbar");
            var banner = await RectOf(page, "banner");
            var main = await RectOf(page, "page");

            Assert.True(Math.Abs(banner.Top - topbar.Bottom) < 0.5, $"{shell} at {width}px: the banner starts at {banner.Top}, the topbar ends at {topbar.Bottom}.");
            Assert.True(Math.Abs(banner.Left - topbar.Left) < 0.5 && Math.Abs(banner.Right - topbar.Right) < 0.5,
                $"{shell} at {width}px: the banner spans {banner.Left}–{banner.Right}, the topbar {topbar.Left}–{topbar.Right}.");
            Assert.True(Math.Abs(main.Top - banner.Bottom) < 0.5, $"{shell} at {width}px: the page starts at {main.Top}, the banner ends at {banner.Bottom}.");
            Assert.True(banner.Height >= 30, $"{shell} at {width}px: the shell squeezed the banner to {banner.Height}px.");
            Assert.Empty(errors);
            await page.Context.CloseAsync();
        }
    }

    [Theory]
    [InlineData("ltr", 1280)]
    [InlineData("rtl", 1280)]
    [InlineData("ltr", 2400)]
    [InlineData("rtl", 400)]
    public async Task The_banners_words_start_on_the_pages_column(string dir, int width)
    {
        if (NoBrowser) return;
        // Without the icon, the banner's first word and the page's first word stand on one
        // line, at every width — including past --page-max, where the page centres its column.
        var markup = Shell("layout").Replace("""<i class="ri-information-line" id="icon" aria-hidden="true"></i>""", "", StringComparison.Ordinal);
        var (page, errors) = await OpenStyled($"""<div dir="{dir}">{markup}</div>""");
        await page.SetViewportSizeAsync(width, 700);
        await Js.Settle(page);

        var rtl = dir == "rtl";
        var words = await TextStart(page, "words", rtl);
        var first = await TextStart(page, "first", rtl);

        Assert.True(Math.Abs(words - first) < 1, $"{dir} at {width}px: the banner's words start at {words}, the page's at {first}.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Under_a_sticky_topbar_the_banner_scrolls_away_and_the_header_stays()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            $$"""
            <style>body { margin: 0; }</style>
            <header class="topbar topbar--sticky" id="topbar"></header>
            {{Banner}}
            <main id="page" style="height:3000px">Long page</main>
            """);
        await page.SetViewportSizeAsync(900, 600);
        await Js.Run(page, "window.scrollTo(0, 300)");
        await Js.ScrollAtRest(page);

        var topbar = await RectOf(page, "topbar");
        var banner = await RectOf(page, "banner");
        Assert.True(Math.Abs(topbar.Top) < 0.5, $"The sticky topbar moved to {topbar.Top}.");
        Assert.True(banner.Bottom <= topbar.Bottom, $"The banner ends at {banner.Bottom}, below the header's {topbar.Bottom}: it did not scroll away.");

        // While it passes under the header, the header is what is painted on top.
        await Js.Run(page, "window.scrollTo(0, 20)");
        await Js.ScrollAtRest(page);
        var onTop = await page.EvaluateAsync<bool>("""
            () => { const t = document.getElementById('topbar').getBoundingClientRect();
                    const hit = document.elementFromPoint(t.left + 200, t.bottom - 2);
                    return document.getElementById('topbar').contains(hit); }
            """);
        Assert.True(onTop, "The banner paints over the sticky header while it scrolls under it.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Under_layout_flow_on_a_phone_the_banner_scrolls_away_under_the_topbar()
    {
        if (NoBrowser) return;
        var markup = Shell("flow").Replace("""<p id="first">""", """<p id="first" style="height:3000px">""", StringComparison.Ordinal);
        var (page, errors) = await OpenStyled(markup);
        await page.SetViewportSizeAsync(400, 700);
        await Js.Run(page, "window.scrollTo(0, 400)");
        await Js.ScrollAtRest(page);

        var topbar = await RectOf(page, "topbar");
        var banner = await RectOf(page, "banner");
        Assert.True(Math.Abs(topbar.Top) < 0.5, $"The topbar moved to {topbar.Top}.");
        Assert.True(banner.Bottom <= topbar.Bottom, $"The banner ends at {banner.Bottom}, below the header's {topbar.Bottom}.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task On_a_notched_device_the_banner_keeps_clear_of_the_insets_as_the_topbar_does()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Shell("bare"),
            """<style>:root { --safe-block-start: 17px; --safe-block-end: 34px; --safe-inline-start: 11px; --safe-inline-end: 13px; } body { margin: 0; }</style>""");
        await page.SetViewportSizeAsync(800, 600);
        await Js.Settle(page);

        var banner = await RectOf(page, "banner");
        Assert.Equal(11.0, banner.Left, 0.5);
        Assert.Equal(787.0, banner.Right, 0.5);
        Assert.Equal(17 + await page.EvaluateAsync<double>("() => document.getElementById('topbar').offsetHeight"), banner.Top, 0.5);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task A_titled_banner_stands_its_icon_and_dismiss_on_the_first_line(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            $"""
            <div dir="{dir}" style="width:520px">
              <div class="page-banner page-banner--warn" id="banner">
                <i class="ri-tools-line" id="icon"></i>
                <div class="page-banner-body"><strong class="page-banner-title" id="title">Planned maintenance</strong>
                  <p>From 06:00 to 08:00 UTC orders can be viewed but not changed, and exports pause until the window ends.</p></div>
                <div class="page-banner-actions" id="actions"><a class="btn btn--sm" href="#">Add to calendar</a></div>
                <button class="page-banner-dismiss" id="dismiss" type="button" aria-label="Dismiss"><i class="ri-close-line"></i></button>
              </div>
            </div>
            """);
        await page.SetViewportSizeAsync(1000, 600);
        await Js.Settle(page);

        var line = await page.EvaluateAsync<double>("""
            () => { const r = document.createRange(); r.selectNodeContents(document.getElementById('title'));
                    const b = r.getClientRects()[0]; return b.top + b.height / 2; }
            """);
        foreach (var id in new[] { "icon", "dismiss", "actions" })
        {
            var r = await RectOf(page, id);
            var centre = r.Top + r.Height / 2;
            Assert.True(Math.Abs(centre - line) <= 1.5, $"{dir}: #{id} is centred at {centre}, the title's line at {line}.");
        }

        var icon = await RectOf(page, "icon");
        var dismiss = await RectOf(page, "dismiss");
        Assert.True(dir == "rtl" ? icon.Left > dismiss.Right : icon.Right < dismiss.Left, $"{dir}: the icon is not at the start and the dismiss at the end.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Each_tone_tints_the_banner_and_inks_its_icon_and_none_leaves_it_neutral()
    {
        if (NoBrowser) return;
        var tones = new[] { "brand", "go", "warn", "danger", "info", "secret" };
        var markup = string.Concat(tones.Select(t =>
            $"""<div class="page-banner page-banner--{t}" id="b-{t}"><i class="ri-information-line"></i><div class="page-banner-body"><span>x</span></div></div>"""))
            + """<div class="page-banner" id="b-none"><i class="ri-information-line"></i><div class="page-banner-body"><span>x</span></div></div>""";
        var (page, errors) = await OpenStyled(markup);

        foreach (var tone in tones)
        {
            var got = await page.EvaluateAsync<string[]>($$"""
                () => { const el = document.getElementById('b-{{tone}}');
                        const probe = document.createElement('span'); document.body.appendChild(probe);
                        probe.style.color = 'var(--{{TokenOf(tone, "bg")}})'; const wantBg = getComputedStyle(probe).color;
                        probe.style.color = 'var(--{{TokenOf(tone, "fg")}})'; const wantInk = getComputedStyle(probe).color;
                        probe.remove();
                        return [getComputedStyle(el).backgroundColor, getComputedStyle(el.querySelector('i')).color, wantBg, wantInk]; }
                """);
            Assert.True(got[0] == got[2], $"--{tone}: the banner is {got[0]}, its tint is {got[2]}.");
            Assert.True(got[1] == got[3], $"--{tone}: the icon is {got[1]}, the tone's ink is {got[3]}.");
        }

        var none = await page.EvaluateAsync<string[]>("""
            () => { const probe = document.createElement('span'); document.body.appendChild(probe);
                    probe.style.color = 'var(--surface-soft)'; const soft = getComputedStyle(probe).color; probe.remove();
                    return [getComputedStyle(document.getElementById('b-none')).backgroundColor, soft]; }
            """);
        Assert.Equal(none[1], none[0]);
        Assert.Empty(errors);
    }

    private static string TokenOf(string tone, string part) => (tone, part) switch
    {
        ("brand", "bg") => "brand-tint",
        ("brand", "fg") => "brand-text",
        _ => $"{tone}-{part}",
    };

    [Fact]
    public async Task Density_moves_the_banners_padding()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Banner);
        var pads = new Dictionary<string, double>();
        foreach (var density in new[] { "compact", "comfortable", "touch" })
        {
            await Js.Run(page, "d => d === 'comfortable' ? document.documentElement.removeAttribute('data-density') : document.documentElement.setAttribute('data-density', d)", density);
            pads[density] = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.getElementById('banner')).paddingTop)");
        }
        Assert.True(pads["compact"] < pads["comfortable"] && pads["comfortable"] < pads["touch"],
            $"Padding by density: compact {pads["compact"]}, comfortable {pads["comfortable"]}, touch {pads["touch"]}.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_hidden_banner_is_not_shown_and_a_printed_page_drops_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Banner.Replace("id=\"banner\"", "id=\"banner\" hidden", StringComparison.Ordinal) + Banner.Replace("id=\"banner\"", "id=\"shown\"", StringComparison.Ordinal));

        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('banner')).display"));
        Assert.Equal("grid", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('shown')).display"));

        await page.EmulateMediaAsync(new() { Media = Media.Print });
        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('shown')).display"));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task An_app_switcher_opened_from_the_end_of_the_topbar_stays_on_screen(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            $$"""
            <style>body { margin: 0; }</style>
            <header class="topbar" dir="{{dir}}">
              <div class="topbar-spacer"></div>
              <button class="topbar-btn" id="open" type="button" popovertarget="apps" aria-label="Apps"><i class="ri-apps-2-line"></i></button>
              <div class="popover" id="apps" popover>
                <nav class="app-switcher" aria-label="Apps">
                  <a class="app-switcher-item" href="#" aria-current="page"><span class="app-switcher-icon"><i class="ri-inbox-line"></i></span><span>Orders</span></a>
                  <a class="app-switcher-item" href="#"><span class="app-switcher-icon"><i class="ri-store-2-line"></i></span><span>Inventory</span></a>
                  <a class="app-switcher-item" href="#"><span class="app-switcher-icon"><i class="ri-truck-line"></i></span><span>Shipping</span></a>
                </nav>
              </div>
            </header>
            """);
        await page.SetViewportSizeAsync(1000, 600);
        await page.ClickAsync("#open");
        await Js.Settle(page);

        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('apps').matches(':popover-open')"));
        var panel = await RectOf(page, "apps");
        var button = await RectOf(page, "open");
        Assert.True(panel.Left >= 0 && panel.Right <= 1000, $"{dir}: the switcher spans {panel.Left}–{panel.Right} of a 1000px viewport.");
        Assert.True(panel.Top >= button.Bottom, $"{dir}: the switcher opens at {panel.Top}, above the button's foot at {button.Bottom}.");

        // Three tiles to a row.
        var tops = await page.EvaluateAsync<double[]>("() => [...document.querySelectorAll('.app-switcher-item')].map(a => a.getBoundingClientRect().top)");
        Assert.Single(tops.Select(t => Math.Round(t)).Distinct());
        Assert.Empty(errors);
    }
}
