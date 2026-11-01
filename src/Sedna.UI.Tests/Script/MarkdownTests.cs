using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.md.render</c> — the escaping, asserted through the DOM rather than against
/// an expected HTML string.
/// </summary>
/// <remarks>
/// The renderer's own comment says its output is safe to inject, and an app takes it at
/// its word. Reading the parsed result back as attributes is the only way to test that
/// claim: a string comparison passes on markup the browser would parse as a handler.
/// </remarks>
public class MarkdownTests : ScriptTestBase
{
    private const string Fixture = """<div id="out"></div>""";

    [Fact]
    public async Task A_link_target_cannot_break_out_of_the_href()
    {
        if (NoBrowser) return;
        // The classic attribute injection: a double quote closes href, and everything
        // after it is parsed as further attributes on the same tag.
        var page = await Render(Fixture, """[x](https://evil"onmouseover=boom)""");

        Assert.Equal(
            new[] { "href", "target", "rel" },
            await page.EvaluateAsync<string[]>(
                "() => [...document.querySelector('#out a').attributes].map(a => a.name)"));

        Assert.Equal("""https://evil"onmouseover=boom""", await Href(page));
    }

    [Fact]
    public async Task A_query_string_ampersand_survives_exactly_once()
    {
        if (NoBrowser) return;
        // The line is escaped before the link pattern runs, so escaping the captured
        // target a second time produced &amp;amp; and a visibly wrong URL.
        var page = await Render(Fixture, "[x](https://example.test/?a=1&b=2)");

        Assert.Equal("https://example.test/?a=1&b=2", await Href(page));
    }

    [Fact]
    public async Task A_target_with_no_allowed_scheme_becomes_a_dead_link()
    {
        if (NoBrowser) return;
        var page = await Render(Fixture, "[x](javascript:boom)");

        Assert.Equal("#", await Href(page));
    }

    [Fact]
    public async Task Escaped_text_still_reads_as_the_characters_that_were_written()
    {
        if (NoBrowser) return;
        // esc() escapes the double quote for the attribute case; a text node has to keep
        // rendering it as a quote.
        var page = await Render(Fixture, """He said "no" & <left>.""");

        Assert.Equal("""He said "no" & <left>.""",
            await page.EvaluateAsync<string>("() => document.querySelector('#out p').textContent"));
    }

    [Fact]
    public async Task A_script_tag_in_the_source_is_inert()
    {
        if (NoBrowser) return;
        var page = await Render(Fixture, "<script>boom()</script>");

        Assert.Empty(await page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('#out script')].map(s => s.textContent)"));
    }

    [Fact]
    public async Task An_image_is_an_img_with_its_description_and_an_underscore_in_its_address_survives()
    {
        if (NoBrowser) return;
        // Emphasis runs after the image is set aside, so _week_12_ in a file name stays a file name.
        var page = await Render(Fixture, "Before ![Orders by _week_](/charts/orders_week_12.png?a=1&b=2) after");

        Assert.Equal(
            ["/charts/orders_week_12.png?a=1&b=2", "Orders by _week_", "lazy"],
            await page.EvaluateAsync<string[]>(
                "() => { const i = document.querySelector('#out p img'); return [i.getAttribute('src'), i.alt, i.loading]; }"));
        Assert.Equal(["src", "alt", "loading"],
            await page.EvaluateAsync<string[]>("() => [...document.querySelector('#out img').attributes].map(a => a.name)"));
    }

    [Theory]
    [InlineData("https://example.com/tour.mp4")]
    [InlineData("/media/tour.webm?v=2")]
    [InlineData("clips/tour.MOV")]
    public async Task The_address_of_a_video_file_is_a_video_with_controls_that_never_plays_by_itself(string url)
    {
        if (NoBrowser) return;
        var page = await Render(Fixture, $"![A tour of the queue]({url})");

        Assert.Equal(
            [url, "A tour of the queue", "true", "false"],
            await page.EvaluateAsync<string[]>("""
                () => { const v = document.querySelector('#out video');
                        return [v.getAttribute('src'), v.getAttribute('aria-label'), String(v.controls), String(v.autoplay)]; }
                """));
        Assert.Null(await page.EvaluateAsync<string?>("() => document.querySelector('#out img')?.outerHTML ?? null"));
    }

    [Theory]
    [InlineData("javascript:boom")]
    [InlineData("data:image/png;base64,AAAA")]
    public async Task An_image_from_an_address_with_another_scheme_is_its_description(string url)
    {
        if (NoBrowser) return;
        var page = await Render(Fixture, $"![the chart]({url})");

        Assert.Equal(0, await page.EvaluateAsync<int>("() => document.querySelectorAll('#out img, #out video').length"));
        Assert.Equal("the chart", await page.EvaluateAsync<string>("() => document.querySelector('#out p').textContent"));
    }

    [Fact]
    public async Task An_image_source_cannot_break_out_of_its_attribute()
    {
        if (NoBrowser) return;
        var page = await Render(Fixture, """![x"onerror="boom](https://example.com/a.png"onerror=boom)""");

        Assert.Equal(["src", "alt", "loading"],
            await page.EvaluateAsync<string[]>("() => [...document.querySelector('#out img').attributes].map(a => a.name)"));
    }

    [Fact]
    public async Task Image_syntax_inside_a_code_span_stays_text()
    {
        if (NoBrowser) return;
        var page = await Render(Fixture, "Write `![alt](/a.png)` for a picture.");

        Assert.Equal(0, await page.EvaluateAsync<int>("() => document.querySelectorAll('#out img').length"));
        Assert.Equal("![alt](/a.png)", await page.EvaluateAsync<string>("() => document.querySelector('#out code').textContent"));
    }

    /// <summary>Renders <paramref name="markdown"/> and injects it into the fixture.</summary>
    /// <remarks>
    /// The markdown is passed as an argument rather than interpolated into the script, so
    /// the payload reaches the renderer exactly as written here.
    /// </remarks>
    private async Task<IPage> Render(string body, string markdown)
    {
        var page = await Open(body);
        await page.EvaluateAsync(
            "md => { document.getElementById('out').innerHTML = sednaUi.md.render(md); }",
            markdown);
        return page;
    }

    private static Task<string> Href(IPage page) =>
        page.EvaluateAsync<string>("() => document.querySelector('#out a').getAttribute('href')");
}
