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

        // Every marker has to straddle the connector, not sit beside it: the <li>'s own
        // inline-start border IS the line, and both markers are centred on its centre.
        //
        // Measured to the sub-pixel and against the border's CENTRE rather than the
        // <li>'s left edge, because a pixel of slack is exactly what let this ship
        // wrong. The dot was 2px out and the tile 1px, and a `<= 1` assertion against
        // an edge half a pixel from the thing being aligned to reported neither. There
        // is no tolerance to spend here: both offsets are `calc()` over whole numbers,
        // so the correct answer is exact, and anything that is not is a rounding error
        // in the arithmetic rather than a browser's.
        var offsets = await page.EvaluateAsync<double[]>(
            """
            () => ['marked', 'plain'].map(id => {
                const li = document.getElementById(id);
                const line = parseFloat(getComputedStyle(li).borderInlineStartWidth);
                const box = li.getBoundingClientRect();
                // The connector is the <li>'s own inline-start border, so its centre is
                // half a border-width inside the border box.
                const lineCentre = box.left + line / 2;
                // A marker is placed against the PADDING box, which starts one border
                // width further in — the very offset the CSS has to subtract twice, and
                // the one this measurement would hide if it were left out.
                const padLeft = box.left + line;

                // The tile is an element with a box to measure. The dot is a
                // pseudo-element and has none, so it is read back off the two resolved
                // declarations that place it — `calc()` already evaluated.
                const mark = li.querySelector('.timeline-mark');
                if (mark) {
                    const b = mark.getBoundingClientRect();
                    return (b.left + b.width / 2) - lineCentre;
                }
                const dot = getComputedStyle(li, '::before');
                return padLeft + parseFloat(dot.left) + parseFloat(dot.width) / 2 - lineCentre;
            })
            """);

        Assert.True(Math.Abs(offsets[0]) < 0.01,
            $"The icon tile's centre is {offsets[0]}px off the connector, not on it.");
        Assert.True(Math.Abs(offsets[1]) < 0.01,
            $"The dot's centre is {offsets[1]}px off the connector, not on it.");

        // The other axis, and the reason --timeline-lead exists: the marker is centred
        // on the timestamp's line box, so the two move together when either changes.
        var vertical = await page.EvaluateAsync<double>(
            """
            () => {
                const li = document.getElementById('marked');
                const mark = li.querySelector('.timeline-mark').getBoundingClientRect();
                const when = li.querySelector('.timeline-when').getBoundingClientRect();
                return (mark.top + mark.height / 2) - (when.top + when.height / 2);
            }
            """);
        Assert.True(Math.Abs(vertical) < 0.01,
            $"The marker sits {vertical}px off the centre of its own timestamp.");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Every_timeline_marker_is_opaque_over_its_own_connector()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <ul class="timeline">
              <li id="dot"><span class="timeline-when">09:31</span></li>
              <li id="tile">
                <span class="timeline-mark"><i class="ri-check-line"></i></span>
                <span class="timeline-when">09:42</span>
              </li>
              <li id="bare">
                <span class="timeline-mark timeline-mark--bare"><span class="avatar">PN</span></span>
                <span class="timeline-when">09:52</span>
              </li>
              <li id="foot" class="timeline-more">37 earlier</li>
            </ul>
            """);

        // The connector runs BEHIND every marker rather than stopping at one, so a
        // marker that is not fully opaque is a window: the line goes straight through
        // it, and a translucent fill is darkened by it into a colour no token names.
        //
        // `.timeline-mark--bare` shipped exactly that. It dropped the tile's whole
        // chrome for an avatar, and `.avatar` is painted in `--brand-tint` — a
        // color-mix at 14% over transparent — so the line crossed the face.
        //
        // Resolved alpha is the check, because the offender is never the declaration
        // in front of you: it is a var() three hops away that happens to end in a
        // color-mix. Only the browser knows.
        var alphas = await page.EvaluateAsync<double[]>(
            """
            () => ['dot', 'tile', 'bare', 'foot'].map(id => {
                const li = document.getElementById(id);
                const mark = li.querySelector('.timeline-mark');
                const paint = mark
                    ? getComputedStyle(mark).backgroundColor
                    : getComputedStyle(li, '::before').backgroundColor;
                // Any notation the engine may resolve to — rgb(), rgba(), color().
                const parts = paint.match(/[\d.]+/g) ?? [];
                return paint.includes('/') || parts.length === 4
                    ? parseFloat(parts[parts.length - 1])
                    : 1;
            })
            """);

        string[] names = ["the plain dot", "the icon tile", "a bare avatar marker", "the .timeline-more foot"];
        for (var i = 0; i < alphas.Length; i++)
            Assert.True(alphas[i] >= 1,
                $"{names[i]} paints at alpha {alphas[i]}, so the connector shows through it.");

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
