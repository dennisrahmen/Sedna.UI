using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>&lt;time data-relative&gt;</c> and <c>&lt;time data-countdown&gt;</c> — kept current by
/// <c>29-time.js</c>, in the app's words, from one timer on the page clock.
/// </summary>
/// <remarks>
/// Every test pauses the page clock and runs it, so a boundary is crossed to the millisecond
/// and nothing waits on the runner's own clock.
/// </remarks>
public class RelativeTimeTests : ScriptTestBase
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private static string At(TimeSpan offset) => (Now + offset).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

    private const string Ago =
        """data-relative data-seconds="{n} s ago" data-minutes="{n} min ago" data-hours="{n} h ago" data-days="{n} days ago" """;

    private const string Left = """data-countdown data-seconds="{n} s left" data-minutes="{n} min left" """;

    private static Task<string> Text(IPage page, string id) =>
        page.EvaluateAsync<string>("id => document.getElementById(id).textContent", id);

    // After a change to the page, once the observer has had its microtask.
    private static Task<string> TextAfter(IPage page, string change, string id) =>
        page.EvaluateAsync<string>($"async id => {{ {change}; await null; await null; return document.getElementById(id).textContent; }}", id);

    [Fact]
    public async Task Each_element_shows_the_largest_unit_it_has_a_format_for()
    {
        if (NoBrowser) return;
        var page = await Open($"""
            <time id="now" datetime="{At(TimeSpan.FromSeconds(-5))}" data-relative data-seconds="just now" data-minutes="{"{n}"} min ago">x</time>
            <time id="s" datetime="{At(TimeSpan.FromSeconds(-59))}" {Ago}>x</time>
            <time id="m" datetime="{At(TimeSpan.FromSeconds(-61))}" {Ago}>x</time>
            <time id="h" datetime="{At(TimeSpan.FromMinutes(-150))}" {Ago}>x</time>
            <time id="d" datetime="{At(TimeSpan.FromDays(-3))}" {Ago}>x</time>
            <time id="beyond" datetime="{At(TimeSpan.FromDays(-45))}" {Ago}>x</time>
            <time id="weeks" datetime="{At(TimeSpan.FromDays(-45))}" {Ago} data-weeks="{"{n}"} wk ago">x</time>
            <time id="below" datetime="{At(TimeSpan.FromSeconds(-30))}" data-relative data-minutes="{"{n}"} min ago">x</time>
            <time id="ahead" datetime="{At(TimeSpan.FromSeconds(20))}" {Ago}>x</time>
            <time id="bare" datetime="{At(TimeSpan.FromMinutes(-5))}" data-relative>8 Nov 2026, 11:55</time>
            <time id="broken" datetime="yesterday" {Ago}>sometime</time>
            """, clockPausedAt: Now);

        Assert.Equal("just now", await Text(page, "now"));
        Assert.Equal("59 s ago", await Text(page, "s"));
        Assert.Equal("1 min ago", await Text(page, "m"));
        Assert.Equal("2 h ago", await Text(page, "h"));
        Assert.Equal("3 days ago", await Text(page, "d"));
        // No format for weeks or months: still counted, in the largest unit the app gave.
        Assert.Equal("45 days ago", await Text(page, "beyond"));
        Assert.Equal("6 wk ago", await Text(page, "weeks"));
        // Below the smallest unit it has, that unit at zero.
        Assert.Equal("0 min ago", await Text(page, "below"));
        // A server clock ahead of the reader's is no time at all yet.
        Assert.Equal("0 s ago", await Text(page, "ahead"));
        // No words the app did not supply: no format, or no time, and the element is the app's.
        Assert.Equal("8 Nov 2026, 11:55", await Text(page, "bare"));
        Assert.Equal("sometime", await Text(page, "broken"));
    }

    [Fact]
    public async Task A_unit_turns_over_on_the_whole_unit_from_one_timer()
    {
        if (NoBrowser) return;
        var page = await Open($"""
            <time id="s" datetime="{At(TimeSpan.FromSeconds(-58))}" {Ago}>x</time>
            <time id="m" datetime="{At(TimeSpan.FromSeconds(-90))}" {Ago}>x</time>
            <time id="h" datetime="{At(TimeSpan.FromSeconds(-3599))}" {Ago}>x</time>
            """, clockPausedAt: Now);
        Assert.Equal("58 s ago", await Text(page, "s"));
        Assert.Equal("59 min ago", await Text(page, "h"));

        await page.Clock.RunForAsync(999);
        Assert.Equal("58 s ago", await Text(page, "s"));
        Assert.Equal("59 min ago", await Text(page, "h"));

        // One second on, the seconds count, and the hour arrives on the dot.
        await page.Clock.RunForAsync(1);
        Assert.Equal("59 s ago", await Text(page, "s"));
        Assert.Equal("1 h ago", await Text(page, "h"));

        await page.Clock.RunForAsync(999);
        Assert.Equal("59 s ago", await Text(page, "s"));
        await page.Clock.RunForAsync(1);
        Assert.Equal("1 min ago", await Text(page, "s"));

        // Ninety seconds became two minutes on the 120th, not a moment before.
        Assert.Equal("1 min ago", await Text(page, "m"));
        await page.Clock.RunForAsync(27_999);
        Assert.Equal("1 min ago", await Text(page, "m"));
        await page.Clock.RunForAsync(1);
        Assert.Equal("2 min ago", await Text(page, "m"));
    }

    [Fact]
    public async Task The_plural_form_follows_the_language_of_the_element()
    {
        if (NoBrowser) return;
        const string english = """data-relative data-minutes-one="{n} minute ago" data-minutes="{n} minutes ago" """;
        const string polish = """data-relative data-minutes-one="{n} minutę temu" data-minutes-few="{n} minuty temu" data-minutes-many="{n} minut temu" data-minutes="{n} minuty temu" """;
        var page = await Open($"""
            <time id="one" datetime="{At(TimeSpan.FromMinutes(-1))}" {english}>x</time>
            <time id="two" datetime="{At(TimeSpan.FromMinutes(-2))}" {english}>x</time>
            <p lang="pl">
                <time id="pl1" datetime="{At(TimeSpan.FromMinutes(-1))}" {polish}>x</time>
                <time id="pl3" datetime="{At(TimeSpan.FromMinutes(-3))}" {polish}>x</time>
                <time id="pl5" datetime="{At(TimeSpan.FromMinutes(-5))}" {polish}>x</time>
            </p>
            """, clockPausedAt: Now);

        Assert.Equal("1 minute ago", await Text(page, "one"));
        Assert.Equal("2 minutes ago", await Text(page, "two"));
        Assert.Equal("1 minutę temu", await Text(page, "pl1"));
        Assert.Equal("3 minuty temu", await Text(page, "pl3"));
        Assert.Equal("5 minut temu", await Text(page, "pl5"));

        // And it moves with the count.
        await page.Clock.RunForAsync(60_000);
        Assert.Equal("2 minutes ago", await Text(page, "one"));
    }

    [Fact]
    public async Task The_number_is_written_in_the_page_language()
    {
        if (NoBrowser) return;
        var page = await Open($"""
            <time id="d" datetime="{At(TimeSpan.FromDays(-1500))}" data-relative data-days="vor {"{n}"} Tagen">x</time>
            """, clockPausedAt: Now);
        Assert.Equal("vor 1,500 Tagen", await Text(page, "d"));

        // The language setting writes <html lang>; the number follows at once.
        Assert.Equal("vor 1.500 Tagen",
            await TextAfter(page, "document.documentElement.lang = 'de'", "d"));
    }

    [Fact]
    public async Task A_countdown_never_reads_zero_before_zero_and_reports_the_end_once()
    {
        if (NoBrowser) return;
        var until = At(TimeSpan.FromMilliseconds(120_500));
        var page = await Open($"""
            <script>window.ends = []; document.addEventListener('sedna-countdown-end', e => ends.push(e.detail.until));</script>
            <div id="host"><time id="c" datetime="{until}" {Left}>x</time></div>
            """, clockPausedAt: Now);
        Assert.Equal("2 min left", await Text(page, "c"));

        await page.Clock.RunForAsync(500);
        Assert.Equal("2 min left", await Text(page, "c"));
        await page.Clock.RunForAsync(1);
        Assert.Equal("1 min left", await Text(page, "c"));

        // Sixty seconds left is still a minute; a moment under it is seconds.
        await page.Clock.RunForAsync(59_999);
        Assert.Equal("1 min left", await Text(page, "c"));
        await page.Clock.RunForAsync(1);
        Assert.Equal("59 s left", await Text(page, "c"));

        // The last second reads one, not zero, until it is over.
        await page.Clock.RunForAsync(59_499);
        Assert.Equal("1 s left", await Text(page, "c"));
        await page.Clock.RunForAsync(499);
        Assert.Equal("1 s left", await Text(page, "c"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => ends.length"));

        await page.Clock.RunForAsync(1);
        Assert.Equal("0 s left", await Text(page, "c"));
        Assert.Equal(new[] { until }, await page.EvaluateAsync<string[]>("() => ends"));

        // Once, however long the page stays.
        await page.Clock.RunForAsync(600_000);
        Assert.Equal(1, await page.EvaluateAsync<int>("() => ends.length"));

        // Moved later, it counts down and ends again.
        var later = At(TimeSpan.FromMilliseconds(120_500 + 600_000 + 3_000));
        Assert.Equal("3 s left",
            await TextAfter(page, $"document.getElementById('c').setAttribute('datetime', '{later}')", "c"));
        await page.Clock.RunForAsync(3_000);
        Assert.Equal(new[] { until, later }, await page.EvaluateAsync<string[]>("() => ends"));
    }

    [Fact]
    public async Task A_countdown_already_over_when_found_reports_it()
    {
        if (NoBrowser) return;
        var until = At(TimeSpan.FromMinutes(-2));
        var page = await Open($"""
            <script>window.ends = []; document.addEventListener('sedna-countdown-end', e => ends.push(e.target.id));</script>
            <time id="c" datetime="{until}" {Left}>x</time>
            """, clockPausedAt: Now);

        Assert.Equal("0 s left", await Text(page, "c"));
        Assert.Equal(new[] { "c" }, await page.EvaluateAsync<string[]>("() => ends"));

        // A render that replaces the element hands the new one its own report — the one a
        // Blazor handler on the interactive markup is listening to.
        await Js.Run(page, $$"""
            async () => {
                document.getElementById('c').outerHTML = '<time id="c2" datetime="{{until}}" data-countdown data-seconds="{n} s left">x</time>';
                await null; await null;
            }
            """);
        Assert.Equal(new[] { "c", "c2" }, await page.EvaluateAsync<string[]>("() => ends"));
    }

    [Fact]
    public async Task Nothing_moves_while_the_page_is_hidden_and_everything_catches_up_when_shown()
    {
        if (NoBrowser) return;
        var page = await Open($"""
            <script>window.ends = []; document.addEventListener('sedna-countdown-end', e => ends.push(e.target.id));</script>
            <time id="r" datetime="{At(TimeSpan.FromSeconds(-50))}" {Ago}>x</time>
            <time id="c" datetime="{At(TimeSpan.FromMinutes(1))}" {Left}>x</time>
            """, clockPausedAt: Now);
        Assert.Equal("50 s ago", await Text(page, "r"));

        await Js.Run(page, """
            () => {
                Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' });
                document.dispatchEvent(new Event('visibilitychange'));
            }
            """);
        await page.Clock.RunForAsync(300_000);
        Assert.Equal("50 s ago", await Text(page, "r"));
        Assert.Equal("1 min left", await Text(page, "c"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => ends.length"));

        await Js.Run(page, """
            () => {
                Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'visible' });
                document.dispatchEvent(new Event('visibilitychange'));
            }
            """);
        Assert.Equal("5 min ago", await Text(page, "r"));
        Assert.Equal("0 s left", await Text(page, "c"));
        Assert.Equal(new[] { "c" }, await page.EvaluateAsync<string[]>("() => ends"));
    }

    [Fact]
    public async Task A_render_is_answered_at_once_and_its_text_node_is_kept()
    {
        if (NoBrowser) return;
        var page = await Open($"""
            <div id="list"><time id="a" datetime="{At(TimeSpan.FromMinutes(-5))}" {Ago}>8 Nov 2026, 11:55</time></div>
            """, clockPausedAt: Now);
        Assert.Equal("5 min ago", await Text(page, "a"));
        await Js.Run(page, "() => { window.node = document.getElementById('a').firstChild; }");

        // A render that writes the app's own text into the same node: answered, in that node.
        Assert.Equal("5 min ago", await TextAfter(page, "node.nodeValue = '8 Nov 2026, 11:55'", "a"));
        Assert.True(await page.EvaluateAsync<bool>(
            "() => document.getElementById('a').firstChild === node && document.getElementById('a').childNodes.length === 1"));

        // A render that moves the time.
        Assert.Equal("2 h ago", await TextAfter(page,
            $"document.getElementById('a').setAttribute('datetime', '{At(TimeSpan.FromHours(-2))}')", "a"));

        // A render that changes the words.
        Assert.Equal("vor 2 Std.", await TextAfter(page,
            "document.getElementById('a').setAttribute('data-hours', 'vor {n} Std.')", "a"));

        // A row a render adds, with the clock still stopped.
        Assert.Equal("3 days ago", await TextAfter(page, $$"""
            const row = document.createElement('p');
            row.innerHTML = '<span>Placed <time id="b" datetime="{{At(TimeSpan.FromDays(-3))}}" data-relative data-days="{n} days ago">5 Nov</time></span>';
            document.getElementById('list').append(row)
            """, "b"));
    }
}
