using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Three pieces of geometry that only exist once a browser has laid them out.
/// </summary>
/// <remarks>
/// Each of these is a rule whose comment makes a claim a source scan cannot check: that
/// a marker replaces a pseudo-element, that stacked headers overlap by exactly one
/// pixel, and that a wide table scrolls inside its card instead of squashing. The last
/// one shipped wrong once — <c>overflow-wrap: anywhere</c> makes a cell's min-content
/// width one character, so the browser shrank every column to fit rather than
/// overflowing into the scroller, and a six-column table arrived as a stack of broken
/// words. Nothing about that is visible in the CSS.
/// </remarks>
public class ContainmentLayoutTests : ScriptTestBase
{
    [Fact]
    public async Task A_timeline_mark_replaces_the_dot()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <ul class="timeline" style="max-width:400px">
              <li data-kind="go" id="marked">
                <span class="timeline-mark"><i class="ri-check-line"></i></span>
                <span class="timeline-when">09:42</span>
                <span class="timeline-what">Decided</span>
              </li>
              <li id="plain">
                <span class="timeline-when">09:31</span>
                <span class="timeline-what">Opened</span>
              </li>
            </ul>
            """);

        var replaced = await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.getElementById('marked'), '::before').content");
        var kept = await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.getElementById('plain'), '::before').content");

        // :has() is what does this, and the browser floor is Chromium — but a rule that
        // silently stopped matching would leave a dot behind the tile rather than error.
        Assert.Equal("none", replaced);
        Assert.NotEqual("none", kept);

        // The tile has to straddle the connector, not sit beside it: the <li>'s own
        // inline-start border is the line, and the marker is centred on it.
        var offset = await page.EvaluateAsync<double>(
            """
            () => {
                const li = document.getElementById('marked');
                const mark = li.querySelector('.timeline-mark');
                const a = li.getBoundingClientRect();
                const b = mark.getBoundingClientRect();
                return (b.left + b.width / 2) - a.left;
            }
            """);
        Assert.True(Math.Abs(offset) <= 1,
            $"The marker's centre is {offset}px from the connector, not on it.");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Sticky_group_headers_stack_with_a_one_pixel_overlap()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <div id="scroller" style="height:180px; overflow-y:auto">
              <ul class="list">
                <li class="sticky-group-head" style="--group-head-index: 0">One</li>
                <li style="height:200px">a</li>
                <li class="sticky-group-head" style="--group-head-index: 1">Two</li>
                <li style="height:200px">b</li>
                <li class="sticky-group-head" style="--group-head-index: 2">Three</li>
                <li style="height:200px">c</li>
              </ul>
            </div>
            """);

        // Scrolled past all three, every header is pinned and none has replaced
        // another. The offsets are the library's arithmetic, not the app's.
        await page.EvaluateAsync("() => document.getElementById('scroller').scrollTop = 600");

        var tops = await page.EvaluateAsync<double[]>(
            """
            () => {
                const box = document.getElementById('scroller').getBoundingClientRect();
                return [...document.querySelectorAll('.sticky-group-head')]
                    .map(h => h.getBoundingClientRect().top - box.top);
            }
            """);
        var height = await page.EvaluateAsync<double>(
            "() => document.querySelector('.sticky-group-head').getBoundingClientRect().height");

        Assert.Equal(0, tops[0], 1);
        // One pixel short of a full height each time: without the overlap a sub-pixel
        // sliver of a scrolling row shows between two stacked headers.
        Assert.Equal(height - 1, tops[1], 1);
        Assert.Equal((height - 1) * 2, tops[2], 1);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_wide_table_in_foreign_prose_scrolls_instead_of_squashing()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <div class="card" id="card" style="width:320px">
              <div class="card-body">
                <div class="prose prose--foreign">
                  <p id="url">https://helpdesk.example.gov/ticket/4182/comments?thread=a7f3c1e9b2&amp;expand=attachments</p>
                  <table id="grid">
                    <tr><th>Host</th><th>Mailbox</th><th>Database</th><th>Quota</th><th>Used</th><th>Last logon</th></tr>
                    <tr><td>EXCH02</td><td>servicedesk</td><td>PRIMARYONE</td><td>50 GB</td><td>49.8 GB</td><td>2026-08-28</td></tr>
                  </table>
                </div>
              </div>
            </div>
            """);

        var measured = await page.EvaluateAsync<double[]>(
            """
            () => {
                const card = document.getElementById('card');
                const grid = document.getElementById('grid');
                return [card.scrollWidth, card.clientWidth,
                        grid.scrollWidth, grid.clientWidth,
                        document.documentElement.scrollWidth,
                        document.documentElement.clientWidth];
            }
            """);

        Assert.True(measured[2] > measured[3],
            "The table fits its container, so nothing here is being contained — the cells are "
            + "squashing to min-content instead of overflowing into the scroller.");
        Assert.True(measured[0] <= measured[1] + 1,
            $"The card scrolls to {measured[0]}px inside {measured[1]}px: the table broke out of it.");
        Assert.True(measured[4] <= measured[5] + 1,
            "The page itself scrolls sideways, which is the failure this class exists to prevent.");

        // And the long URL still wraps, which is the other half.
        var wrapped = await page.EvaluateAsync<double>(
            "() => document.getElementById('url').getBoundingClientRect().height");
        var line = await page.EvaluateAsync<double>(
            "() => parseFloat(getComputedStyle(document.getElementById('url')).lineHeight)");
        Assert.True(wrapped > line * 1.5,
            $"The URL is {wrapped}px tall on a {line}px line — it did not wrap.");

        Assert.Empty(errors);
    }
}
