using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.timeZone()</c> — the browser's IANA zone, for the moment a circuit
/// exists and the boot script's cookie does not answer.
/// </summary>
/// <remarks>
/// The cookie covers the first server render; this covers the first session, where
/// there is no cookie yet and a Blazor Server navigation is not an HTTP request that
/// could pick one up. Both read the same <c>Intl</c> value, and a test that the two
/// agree is the only thing keeping them from drifting apart.
/// </remarks>
public class TimeZoneTests : ScriptTestBase
{
    // A zone with a half-hour offset and a name nothing could produce by accident, so
    // a wrong answer cannot coincidentally look right.
    private const string Zone = "Australia/Adelaide";

    [Fact]
    public async Task It_reports_the_browsers_own_zone_as_an_IANA_id()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>", timeZone: Zone);

        var reported = await page.EvaluateAsync<string?>("() => sednaUi.timeZone()");

        Assert.Equal(Zone, reported);
        // An id, never an offset. "+09:30" is true until that zone's next transition,
        // and an app that cached a formatter against one would render the wrong hour
        // for months without anything failing.
        Assert.DoesNotContain(":", reported, StringComparison.Ordinal);
    }

    [Fact]
    public async Task It_agrees_with_the_zone_the_boot_script_writes_to_its_cookie()
    {
        if (NoBrowser) return;
        // Two readers of one value. They are in separate files — the boot script is
        // standalone on purpose — so nothing but this stops one of them changing.
        var page = await Open(
            "<p>x</p>",
            head: """<script src="/js/Sedna.UI.boot.js" data-tz-cookie="tz"></script>""",
            timeZone: Zone);

        var cookie = await page.EvaluateAsync<string>("() => document.cookie");
        var reported = await page.EvaluateAsync<string?>("() => sednaUi.timeZone()");

        Assert.Equal($"tz={Zone}", cookie);
        Assert.Equal(Zone, reported);
    }

    [Fact]
    public async Task A_browser_that_will_not_answer_gets_null_rather_than_a_guess()
    {
        if (NoBrowser) return;
        // Fail soft, and fail visibly: null is the signal for the app's own configured
        // fallback zone. A guessed zone — the server's, or an offset — renders plausible
        // times that are wrong, which is worse than rendering none.
        var page = await Open("<p>x</p>", timeZone: Zone);

        var reported = await page.EvaluateAsync<string?>(
            "() => { window.Intl = undefined; return sednaUi.timeZone(); }");

        Assert.Null(reported);
    }
}
