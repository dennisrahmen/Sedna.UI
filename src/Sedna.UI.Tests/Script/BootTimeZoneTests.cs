using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-tz-cookie</c> writes the browser's IANA time zone before first paint, so a
/// server-rendered app can put UTC instants on the reader's own clock.
/// </summary>
/// <remarks>
/// It is the boot script's job rather than the main script's for one reason: in Blazor
/// Server with prerendering off, <c>App.razor</c> is the last component with an
/// <c>HttpContext</c> to read a cookie from, so a cookie that arrives after the circuit
/// connects arrives too late to render the first page with.
/// </remarks>
public class BootTimeZoneTests : ScriptTestBase
{
    private const string Tag =
        """<script src="/js/Sedna.UI.boot.js" data-tz-cookie="tz"></script>""";

    [Fact]
    public async Task No_attribute_writes_no_cookie()
    {
        if (NoBrowser) return;
        // Opt-in, and silent otherwise. A cookie nobody asked for is a cookie a
        // consent banner has to account for.
        const string plain = """<script src="/js/Sedna.UI.boot.js"></script>""";
        var page = await Open("<p>x</p>", head: plain, withMainScript: false);

        Assert.Equal(string.Empty, await page.EvaluateAsync<string>("() => document.cookie"));
    }

    [Fact]
    public async Task The_attribute_writes_the_browsers_own_zone()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>", head: Tag, withMainScript: false);

        // Compared against Intl in the page rather than against a literal: the
        // assertion is that the value is the BROWSER's zone, and hard-coding one
        // would only pin whatever the runner is set to.
        var expected = await page.EvaluateAsync<string>(
            "() => Intl.DateTimeFormat().resolvedOptions().timeZone");
        var cookie = await page.EvaluateAsync<string>("() => document.cookie");

        Assert.Equal($"tz={expected}", cookie);
    }

    [Fact]
    public async Task An_unchanged_value_is_not_rewritten()
    {
        if (NoBrowser) return;
        // Rewriting an identical value on every load costs a Set-Cookie-sized header
        // on every request and gives an app a value it cannot cache a formatter
        // against. The observable form of "not rewritten" is that a second load leaves
        // exactly one cookie with the same value — a duplicate write with a different
        // path would show as two.
        var page = await Open("<p>x</p>", head: Tag, withMainScript: false);
        await page.ReloadAsync();

        var expected = await page.EvaluateAsync<string>(
            "() => Intl.DateTimeFormat().resolvedOptions().timeZone");
        var cookies = await page.Context.CookiesAsync();

        Assert.Single(cookies);
        Assert.Equal("tz", cookies[0].Name);
        Assert.Equal(expected, cookies[0].Value);
        Assert.Equal("/", cookies[0].Path);
    }

    [Fact]
    public async Task A_stale_value_is_replaced()
    {
        if (NoBrowser) return;
        // The whole point of writing it on every load: a reader who travels, or whose
        // machine changes zone, must not keep rendering on the old clock. "Only when
        // it differs" has to mean differs, not "only once".
        var page = await Open("<p>x</p>", head: Tag, withMainScript: false);
        await page.EvaluateAsync("() => { document.cookie = 'tz=Etc/GMT-14;path=/'; }");
        await page.ReloadAsync();

        var expected = await page.EvaluateAsync<string>(
            "() => Intl.DateTimeFormat().resolvedOptions().timeZone");
        Assert.Equal($"tz={expected}", await page.EvaluateAsync<string>("() => document.cookie"));
    }

    [Fact]
    public async Task Blocked_storage_does_not_take_the_cookie_down_with_it()
    {
        if (NoBrowser) return;
        // The zone comes from Intl, not from localStorage, so the two live in separate
        // try blocks. With one block a browser refusing storage would silently lose the
        // cookie as well — and the theme fallback would hide it.
        var page = await Open("<p>x</p>", head: Tag, withMainScript: false);

        // An init script, not an evaluate: it has to be in place before the document's
        // own scripts run, which is the only moment boot.js touches storage. Init
        // scripts apply to every navigation after they are added, so the reload is what
        // makes it take effect.
        await page.AddInitScriptAsync("""
            Object.defineProperty(window, 'localStorage', {
                get() { throw new Error('blocked'); }
            });
            """);
        await page.EvaluateAsync("() => { document.cookie = 'tz=;path=/;max-age=0'; }");
        await page.ReloadAsync();

        // Storage throwing on the first read means NO attribute is stamped at all, and
        // the stylesheet's own dark `:root` block is what the page then falls back to.
        // That is the symptom which would otherwise hide this: with one try block,
        // storage throwing loses the cookie as well and the page still looks right.
        Assert.Null(
            await page.EvaluateAsync<string?>("() => document.documentElement.dataset.variant"));

        var expected = await page.EvaluateAsync<string>(
            "() => Intl.DateTimeFormat().resolvedOptions().timeZone");
        Assert.Equal($"tz={expected}", await page.EvaluateAsync<string>("() => document.cookie"));
    }
}
