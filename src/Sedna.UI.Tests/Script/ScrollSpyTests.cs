using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-spy</c> on a <c>.page-toc</c> marks the link to the section in view
/// <c>aria-current="location"</c>: as the page scrolls, at both ends, inside a nested list,
/// and after a link is followed.
/// </summary>
/// <remarks>
/// The sections scroll in a <c>.sedna-scroll</c> box 400px tall, so the band the spy reads —
/// the top two fifths — is 160px. Each section is tall enough that only one heading is in it
/// at a time, except the last two, which are short: the end of the box can never bring them
/// to the band, which is what the end-of-scroll rule and a followed link are for.
/// </remarks>
public class ScrollSpyTests : ScriptTestBase
{
    private const string Fixture = """
        <div class="sedna-scroll" id="box" style="max-height:400px; width:600px">
          <nav class="page-toc" aria-label="On this page" data-spy id="toc">
            <ol class="page-toc-list">
              <li><a class="page-toc-link" href="#s-one" id="l-one">One</a>
                <ol class="page-toc-list">
                  <li><a class="page-toc-link" href="#s-one-a" id="l-one-a">One A</a></li>
                </ol>
              </li>
              <li><a class="page-toc-link" href="#s-two" id="l-two">Two</a></li>
              <li><a class="page-toc-link" href="#s-three" id="l-three">Three</a></li>
              <li><a class="page-toc-link" href="#s-four" id="l-four">Four</a></li>
            </ol>
          </nav>
          <h2 id="s-one">One</h2><div style="height:600px"></div>
          <h3 id="s-one-a">One A</h3><div style="height:600px"></div>
          <h2 id="s-two">Two</h2><div style="height:600px"></div>
          <h2 id="s-three">Three</h2><div style="height:60px"></div>
          <h2 id="s-four">Four</h2><div style="height:60px"></div>
        </div>
        """;

    private static Task<string?> Current(IPage page) =>
        page.EvaluateAsync<string?>("() => { const a = document.querySelector('#toc [aria-current]'); return a ? a.id + '=' + a.getAttribute('aria-current') : null; }");

    private static Task<int> CurrentCount(IPage page) =>
        page.EvaluateAsync<int>("() => document.querySelectorAll('#toc [aria-current]').length");

    private static async Task ScrollTo(IPage page, string targetId)
    {
        // The heading to the top of the box, as a reader's scroll would leave it.
        await Js.Run(page, "id => { const box = document.getElementById('box'); box.scrollTop = document.getElementById(id).offsetTop - box.offsetTop; }", targetId);
        await Js.ScrollAtRest(page, "#box");
    }

    private static Task WaitFor(IPage page, string linkId) =>
        page.WaitForFunctionAsync(
            "id => document.getElementById(id).getAttribute('aria-current') === 'location'", linkId);

    [Fact]
    public async Task At_the_top_the_first_section_is_current()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await WaitFor(page, "l-one");
        Assert.Equal("l-one=location", await Current(page));
        Assert.Equal(1, await CurrentCount(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Scrolling_marks_each_section_as_its_heading_reaches_the_top()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);
        await WaitFor(page, "l-one");

        await ScrollTo(page, "s-two");
        await WaitFor(page, "l-two");
        Assert.Equal(1, await CurrentCount(page));

        // Half way through a section, its heading has passed above the band and nothing new
        // has arrived: it is still the section being read.
        await Js.Run(page, "() => { document.getElementById('box').scrollTop += 300; }");
        await Js.ScrollAtRest(page, "#box");
        await Js.Frames(page);
        Assert.Equal("l-two=location", await Current(page));

        // And back up.
        await ScrollTo(page, "s-one");
        await WaitFor(page, "l-one");
        Assert.Equal(1, await CurrentCount(page));
    }

    [Fact]
    public async Task A_sub_section_is_marked_in_the_nested_list()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);
        await WaitFor(page, "l-one");

        await ScrollTo(page, "s-one-a");
        await WaitFor(page, "l-one-a");
        Assert.Equal(1, await CurrentCount(page));
    }

    [Fact]
    public async Task At_the_end_the_last_section_is_current_though_its_heading_never_reaches_the_top()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture);
        await WaitFor(page, "l-one");

        await Js.Run(page, "() => { const box = document.getElementById('box'); box.scrollTop = box.scrollHeight; }");
        await Js.ScrollAtRest(page, "#box");
        await WaitFor(page, "l-four");
        Assert.Equal(1, await CurrentCount(page));
    }

    [Fact]
    public async Task A_followed_link_is_current_until_the_reader_scrolls_again()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Fixture, BaseTag);
        await WaitFor(page, "l-one");

        // Three cannot reach the band — the box ends first — so only the click makes it current.
        await page.Locator("#l-three").ClickAsync();
        await WaitFor(page, "l-three");
        Assert.Equal("s-three", await page.EvaluateAsync<string>("() => document.activeElement.id"));
        Assert.Equal("/fixture.html", await page.EvaluateAsync<string>("() => location.pathname"));

        // The jump's own scroll has landed and changed nothing.
        await Js.ScrollAtRest(page, "#box");
        await Js.Frames(page);
        Assert.Equal("l-three=location", await Current(page));

        // The reader scrolls: the band decides again.
        await ScrollTo(page, "s-two");
        await WaitFor(page, "l-two");
    }

    [Fact]
    public async Task A_contents_list_rendered_later_is_picked_up_and_one_removed_is_let_go()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("<div id=\"host\"></div>");

        await Js.Run(page, "html => { document.getElementById('host').innerHTML = html; }", Fixture);
        await WaitFor(page, "l-one");

        // Taking data-spy off stops marking; the last mark is the app's to clear or keep.
        await Js.Run(page, "() => document.getElementById('toc').removeAttribute('data-spy')");
        await Js.Frames(page);
        await Js.Run(page, "() => { const box = document.getElementById('box'); box.scrollTop = box.scrollHeight; }");
        await Js.ScrollAtRest(page, "#box");
        await Js.Frames(page);
        Assert.NotEqual("l-four=location", await Current(page));
    }

    private const string BaseTag = """<base href="/">""";
}
