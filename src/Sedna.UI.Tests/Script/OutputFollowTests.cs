using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Follow-tail on a <c>data-follow</c> output pane: where the reader has scrolled is the
/// script's to report, whether the pane follows at all is the app's.
/// </summary>
public class OutputFollowTests : ScriptTestBase
{
    private static string Pane(int lines, string extra = "") => $"""
        <div class="output-panel">
            <ul class="output output--columns" id="p" data-follow style="max-height:120px">
                {string.Concat(Enumerable.Range(1, lines).Select(i =>
                    $"<li class=\"output-line\"><span class=\"output-msg\">line {i}</span></li>"))}
                {extra}
            </ul>
            <button class="btn btn-sm output-jump" id="jump" type="button" data-output-follow>Jump to latest</button>
        </div>
        """;

    private const string Append = """
        () => {
            const li = document.createElement('li');
            li.className = 'output-line';
            li.textContent = 'appended';
            document.getElementById('p').appendChild(li);
        }
        """;

    private const string AtBottom = """
        () => { const p = document.getElementById('p'); return p.scrollHeight - p.clientHeight - p.scrollTop <= 4; }
        """;

    private const string Paused = "() => document.getElementById('p').hasAttribute('data-follow-paused')";

    private const string RecordEvents = """
        () => {
            window.follows = [];
            document.addEventListener('sedna-follow', e => window.follows.push(e.detail.following));
        }
        """;

    private static async Task ScrollToTop(IPage page)
    {
        await page.EvaluateAsync("() => { document.getElementById('p').scrollTop = 0; }");
        await page.WaitForFunctionAsync(Paused);
    }

    private static Task<string> JumpDisplay(IPage page) =>
        page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('jump')).display");

    [Fact]
    public async Task A_pane_opens_on_its_newest_line_and_follows_what_is_appended()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Pane(30));
        await page.WaitForFunctionAsync(AtBottom);

        await page.EvaluateAsync(Append);
        await page.WaitForFunctionAsync(AtBottom);
        Assert.False(await page.EvaluateAsync<bool>(Paused));
        Assert.Equal("none", await JumpDisplay(page));
    }

    [Fact]
    public async Task Scrolling_up_pauses_marks_the_pane_and_shows_the_jump_button()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Pane(30));
        await page.WaitForFunctionAsync(AtBottom);
        await page.EvaluateAsync(RecordEvents);

        await ScrollToTop(page);
        await page.WaitForFunctionAsync("() => window.follows.length === 1");
        Assert.Equal(new[] { false }, await page.EvaluateAsync<bool[]>("() => window.follows"));
        Assert.NotEqual("none", await JumpDisplay(page));

        await page.EvaluateAsync(Append);
        await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => r()))");
        Assert.Equal(0, await page.EvaluateAsync<double>("() => document.getElementById('p').scrollTop"));
    }

    [Fact]
    public async Task The_jump_button_follows_again()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Pane(30));
        await page.WaitForFunctionAsync(AtBottom);
        await page.EvaluateAsync(RecordEvents);
        await ScrollToTop(page);

        await page.ClickAsync("#jump");
        await page.WaitForFunctionAsync(AtBottom);
        Assert.False(await page.EvaluateAsync<bool>(Paused));
        await page.WaitForFunctionAsync("() => window.follows.length === 2");
        Assert.Equal(new[] { false, true }, await page.EvaluateAsync<bool[]>("() => window.follows"));

        await page.EvaluateAsync(Append);
        await page.WaitForFunctionAsync(AtBottom);
    }

    [Fact]
    public async Task Removing_data_follow_stops_following_and_adding_it_jumps_to_the_newest_line()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Pane(30));
        await page.WaitForFunctionAsync(AtBottom);
        await ScrollToTop(page);

        await page.EvaluateAsync("() => document.getElementById('p').removeAttribute('data-follow')");
        await page.WaitForFunctionAsync($"() => !({Paused})()");

        await page.EvaluateAsync("() => { const p = document.getElementById('p'); p.scrollTop = p.scrollHeight; }");
        await page.WaitForFunctionAsync(AtBottom);
        await page.EvaluateAsync(Append);
        await page.EvaluateAsync(Append);
        await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => r()))");
        Assert.False(await page.EvaluateAsync<bool>(AtBottom));

        await page.EvaluateAsync("() => document.getElementById('p').setAttribute('data-follow', '')");
        await page.WaitForFunctionAsync(AtBottom);
    }

    [Fact]
    public async Task Opening_a_row_that_grows_past_the_bottom_lets_go_of_the_tail()
    {
        if (NoBrowser) return;
        const string row = """
            <li class="output-line"><details class="output-details" id="d">
                <summary><span class="output-msg">failed</span></summary>
                <pre class="output-trace">one
            two
            three
            four
            five</pre>
            </details></li>
            """;
        var (page, _) = await OpenStyled(Pane(30, row));
        await page.WaitForFunctionAsync(AtBottom);

        await page.EvaluateAsync("() => { document.getElementById('d').open = true; }");
        await page.WaitForFunctionAsync(Paused);
        var top = await page.EvaluateAsync<double>("() => document.getElementById('p').scrollTop");

        await page.EvaluateAsync(Append);
        await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => r()))");
        Assert.Equal(top, await page.EvaluateAsync<double>("() => document.getElementById('p').scrollTop"));
    }

    [Fact]
    public async Task An_open_menu_in_the_pane_holds_it_still()
    {
        if (NoBrowser) return;
        const string row = """
            <li class="output-line"><span class="output-msg">has a menu</span>
                <div class="output-actions"><div class="menu-anchor">
                    <button class="btn btn-sm btn-ghost btn-icon" id="more" type="button" data-menu-toggle aria-expanded="false" aria-label="More">…</button>
                    <div class="menu" hidden><button class="menu-item" type="button">Copy</button></div>
                </div></div>
            </li>
            """;
        var (page, _) = await OpenStyled(Pane(30, row));
        await page.WaitForFunctionAsync(AtBottom);

        await page.EvaluateAsync("() => document.getElementById('more').setAttribute('aria-expanded', 'true')");
        var top = await page.EvaluateAsync<double>("() => document.getElementById('p').scrollTop");
        await page.EvaluateAsync(Append);
        await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => r()))");
        Assert.Equal(top, await page.EvaluateAsync<double>("() => document.getElementById('p').scrollTop"));

        await page.EvaluateAsync("() => document.getElementById('more').setAttribute('aria-expanded', 'false')");
        await page.EvaluateAsync(Append);
        await page.WaitForFunctionAsync(AtBottom);
    }

    [Fact]
    public async Task Row_actions_show_on_hover_and_while_their_menu_is_open()
    {
        if (NoBrowser) return;
        const string row = """
            <li class="output-line" id="row"><span class="output-msg">has actions</span>
                <div class="output-actions" id="actions"><div class="menu-anchor">
                    <button class="btn btn-sm btn-ghost btn-icon" id="more" type="button" data-menu-toggle aria-expanded="false" aria-label="More">…</button>
                    <div class="menu" hidden><button class="menu-item" type="button">Copy</button></div>
                </div></div>
            </li>
            """;
        var (page, _) = await OpenStyled(
            $"""<p id="away">away</p><ul class="output output--columns" id="p" style="margin-top:120px">{row}</ul>""");
        const string opacity = "() => getComputedStyle(document.getElementById('actions')).opacity";

        await page.HoverAsync("#away");
        Assert.Equal("0", await page.EvaluateAsync<string>(opacity));

        await page.HoverAsync("#row");
        Assert.Equal("1", await page.EvaluateAsync<string>(opacity));

        await page.ClickAsync("#more");
        await page.HoverAsync("#away");
        Assert.Equal("1", await page.EvaluateAsync<string>(opacity));
    }
}
