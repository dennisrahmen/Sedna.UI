using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-fullscreen</c>: a button puts the element it names into full screen and takes it
/// out again, and its <c>aria-pressed</c> follows however the reader leaves.
/// </summary>
public class FullscreenTests : ScriptTestBase
{
    private const string Frame = """
        <section class="card" id="wall"><p>The run, on the wall.</p>
            <button class="btn" type="button" id="toggle" data-fullscreen aria-controls="wall" aria-pressed="false">Full screen</button>
        </section>
        <button class="btn" type="button" id="page" data-fullscreen aria-pressed="false">The whole page</button>
        """;

    private const string Element = "() => document.fullscreenElement ? (document.fullscreenElement.id || document.fullscreenElement.tagName) : null";

    [Fact]
    public async Task The_button_puts_the_element_it_names_into_full_screen_and_takes_it_out()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Frame);

        await page.Locator("#toggle").ClickAsync();
        await page.WaitForFunctionAsync("() => document.fullscreenElement && document.fullscreenElement.id === 'wall'");
        await page.WaitForFunctionAsync("() => document.getElementById('toggle').getAttribute('aria-pressed') === 'true'");
        Assert.Equal("false", await page.Locator("#page").GetAttributeAsync("aria-pressed"));

        await page.Locator("#toggle").ClickAsync();
        await page.WaitForFunctionAsync("() => !document.fullscreenElement");
        await page.WaitForFunctionAsync("() => document.getElementById('toggle').getAttribute('aria-pressed') === 'false'");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Leaving_another_way_still_unpresses_the_button()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Frame);

        await page.Locator("#toggle").ClickAsync();
        await page.WaitForFunctionAsync("() => !!document.fullscreenElement");

        // The browser's own way out, as Escape takes it.
        await page.EvaluateAsync("() => document.exitFullscreen()");
        await page.WaitForFunctionAsync("() => document.getElementById('toggle').getAttribute('aria-pressed') === 'false'");
    }

    [Fact]
    public async Task Without_aria_controls_the_button_acts_on_the_whole_page()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Frame);

        await page.Locator("#page").ClickAsync();
        await page.WaitForFunctionAsync("() => document.fullscreenElement === document.documentElement");
        Assert.Equal("HTML", await page.EvaluateAsync<string>(Element));
        await page.WaitForFunctionAsync("() => document.getElementById('page').getAttribute('aria-pressed') === 'true'");
        Assert.True(await page.EvaluateAsync<bool>("() => sednaUi.fullscreen.isOn()"));
    }
}
