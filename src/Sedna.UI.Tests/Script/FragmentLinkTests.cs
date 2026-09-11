using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// A bare fragment link moves the reader to its target on the same page, under the
/// <c>&lt;base href="/"&gt;</c> every consuming app declares.
/// </summary>
/// <remarks>
/// The fixture is served at <c>/fixture.html</c> with a base of <c>/</c>, so a bare
/// <c>#main</c> resolves to <c>/#main</c> — another document — exactly as it does on
/// every route of an app but its root. A marker on <c>window</c> tells a jump from a
/// reload: a reload clears it.
/// </remarks>
public class FragmentLinkTests : ScriptTestBase
{
    private const string BaseTag = """<base href="/">""";

    private const string Fixture = """
        <a class="skip-link" href="#main" id="skip">Skip to content</a>
        <a href="#nowhere" id="dead">A fragment with no target</a>
        <div style="height:3000px"></div>
        <main id="main" tabindex="-1">
            <a href="#email" id="to-email">a work e-mail address</a>
            <a href="#section" id="to-section">the section</a>
            <input id="email" type="email">
            <h2 id="section">A heading</h2>
        </main>
        """;

    // Records whether anything before the window cancelled the click, then cancels it
    // so a test of "left alone" does not navigate away from the page it is reading.
    private const string Recorder = """
        () => {
            window.__stay = true;
            window.__prevented = null;
            window.addEventListener('click', e => { window.__prevented = e.defaultPrevented; e.preventDefault(); });
        }
        """;

    [Fact]
    public async Task A_skip_link_moves_focus_to_main_without_leaving_the_page()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: BaseTag);
        await page.EvaluateAsync("() => { window.__stay = true; }");

        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("skip", await ActiveId(page));

        await page.Keyboard.PressAsync("Enter");
        await page.WaitForLoadStateAsync();

        Assert.Equal("/fixture.html", await page.EvaluateAsync<string>("() => location.pathname"));
        Assert.True(await page.EvaluateAsync<bool>("() => window.__stay === true"),
            "The skip link reloaded the page: a bare #main resolved against <base href=\"/\">.");
        Assert.Equal("main", await ActiveId(page));
    }

    [Fact]
    public async Task A_link_to_a_field_focuses_the_field()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: BaseTag);
        await page.EvaluateAsync("() => { window.__stay = true; }");

        await page.Locator("#to-email").ClickAsync();
        await page.WaitForLoadStateAsync();

        Assert.True(await page.EvaluateAsync<bool>("() => window.__stay === true"));
        Assert.Equal("email", await ActiveId(page));
    }

    [Fact]
    public async Task A_target_that_cannot_take_focus_takes_it_until_it_loses_it()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: BaseTag);

        await page.Locator("#to-section").ClickAsync();
        Assert.Equal("section", await ActiveId(page));
        Assert.Equal("-1", await page.Locator("#section").GetAttributeAsync("tabindex"));

        await page.Locator("#skip").FocusAsync();
        Assert.Null(await page.Locator("#section").GetAttributeAsync("tabindex"));
    }

    [Fact]
    public async Task A_click_something_else_cancelled_is_left_to_it()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: BaseTag);
        // A tab or a menu item that is also a fragment link, and handles its own click.
        await page.EvaluateAsync(
            "() => document.getElementById('to-email').addEventListener('click', e => e.preventDefault())");

        await page.Locator("#to-email").ClickAsync();

        Assert.NotEqual("email", await ActiveId(page));
    }

    [Fact]
    public async Task A_modified_click_and_a_fragment_with_no_target_are_left_to_the_browser()
    {
        if (NoBrowser) return;
        var page = await Open(Fixture, head: BaseTag);
        await page.EvaluateAsync(Recorder);

        // Ctrl-click opens a new tab; cancelling it would take that away.
        await page.Locator("#to-email").ClickAsync(new() { Modifiers = [KeyboardModifier.Control] });
        Assert.False(await page.EvaluateAsync<bool>("() => window.__prevented"),
            "A Ctrl-click on a fragment link was cancelled.");

        await page.Locator("#dead").ClickAsync();
        Assert.False(await page.EvaluateAsync<bool>("() => window.__prevented"),
            "A fragment naming no element was cancelled, so it did nothing at all.");
    }

    private static Task<string> ActiveId(IPage page) =>
        page.EvaluateAsync<string>("() => document.activeElement.id");
}
