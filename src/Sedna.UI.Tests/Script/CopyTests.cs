using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>
/// Declarative copy, including the stash that stops a second click restoring "Copied" as the original label.
/// </summary>
public class CopyTests : ScriptTestBase
{
    [Fact]
    public async Task Copy_puts_the_text_on_the_clipboard_and_restores_the_real_label()
    {
        if (NoBrowser) return;
        // Two rapid clicks must not restore "Copied" as if it were the original, which
        // is why the original is stashed on the element rather than in a closure.
        var page = await Open(
            """<button type="button" id="c" data-copy="TCK0031209"><span>Copy</span></button>""");

        await page.Locator("#c").ClickAsync();
        await page.Locator("#c").ClickAsync();

        Assert.Equal("TCK0031209", await page.EvaluateAsync<string>(
            "() => navigator.clipboard.readText()"));
        Assert.Contains("Copied", await page.Locator("#c").InnerTextAsync(), StringComparison.Ordinal);

        // Polled, not slept. The restore is a 1400ms timer in the script, and a fixed
        // wait a little longer than it fails whenever the machine is loaded enough to
        // run the timer late — which is every full-suite run alongside the browser
        // tests, and passes in isolation, so it read as a real regression each time.
        await Assertions.Expect(page.Locator("#c")).ToHaveTextAsync(
            "Copy", new() { Timeout = 6000 });
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

    // ── command palette ─────────────────────────────────────────────────────
}
