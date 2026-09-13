using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Declarative copy: the outcome is an attribute on the app's button, and the app's own
/// words for it are shown by the stylesheet.
/// </summary>
public class CopyTests : ScriptTestBase
{
    private const string Button = """
        <button type="button" id="c" data-copy="orders-console-01">
            <span data-copied-hide>Kopieren</span>
            <span data-copied-show="ok">Kopiert</span>
            <span data-copied-show="failed">Fehlgeschlagen</span>
        </button>
        """;

    [Fact]
    public async Task Copy_marks_the_button_and_never_rewrites_it()
    {
        if (NoBrowser) return;
        // The script used to swap the button's content for an English "Copied": drawing
        // markup inside an element the app owns, which a framework can revert mid-flash.
        var page = await Open(Button, head: StylesheetTag);

        // The confirmation is a 1400ms real-time window, so on a loaded machine two
        // ClickAsync round trips can straddle it. The page clock is paused, so the restore
        // fires only when the test runs it, however slowly the clicks arrive.
        await page.Clock.InstallAsync(new() { TimeDate = new DateTime(2030, 1, 1) });
        await page.Clock.PauseAtAsync(new DateTime(2030, 1, 1, 0, 0, 1));

        await page.EvaluateAsync("""
            () => {
                window.childChanges = 0;
                new MutationObserver(r => window.childChanges += r.length)
                    .observe(document.getElementById('c'), { childList: true, subtree: true, characterData: true });
            }
            """);

        await page.Locator("#c").ClickAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('c').dataset.copied === 'ok'");
        // A second click mid-flash restarts the window rather than ending it early.
        await page.Locator("#c").ClickAsync();

        Assert.Equal("orders-console-01", await page.EvaluateAsync<string>(
            "() => navigator.clipboard.readText()"));
        // innerText, which is what is rendered: textContent would include the hidden words.
        Assert.Equal("Kopiert", await page.EvaluateAsync<string>("() => document.getElementById('c').innerText.trim()"));

        await page.Clock.RunForAsync(1400);

        Assert.Equal("Kopieren", await page.EvaluateAsync<string>("() => document.getElementById('c').innerText.trim()"));
        Assert.Null(await page.GetAttributeAsync("#c", "data-copied"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.childChanges"));
    }

    [Fact]
    public async Task Copy_target_reads_the_code_block_it_belongs_to()
    {
        if (NoBrowser) return;
        var page = await Open("""
            <div class="code-block">
                <div class="code-block-head">
                    <button type="button" class="code-block-copy" id="c" data-copy-target>Copy</button>
                </div>
                <pre>dotnet add package Sedna.UI</pre>
            </div>
            """);

        await page.Locator("#c").ClickAsync();

        Assert.Equal("dotnet add package Sedna.UI",
            (await page.EvaluateAsync<string>("() => navigator.clipboard.readText()")).Trim());
    }
}
