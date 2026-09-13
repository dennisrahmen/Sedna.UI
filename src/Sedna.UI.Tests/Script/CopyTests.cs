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
            """<button type="button" id="c" data-copy="orders-console-01"><span>Copy</span></button>""");

        // The confirmation is a 1400ms real-time window, so on a loaded machine two
        // ClickAsync round trips can straddle it: the first flash restores before the
        // second click lands and the button honestly reads Copy, Copied, Copy, Copied,
        // Copy. So the page clock is paused — InstallAsync alone leaves it running — and
        // the restore fires only when the test runs it, however slowly the clicks arrive.
        await page.Clock.InstallAsync(new() { TimeDate = new DateTime(2030, 1, 1) });
        await page.Clock.PauseAtAsync(new DateTime(2030, 1, 1, 0, 0, 1));

        // Every text the button ever had, and a count of label swaps: the second flash
        // writes the same "Copied" again, so only the count shows that it landed.
        await page.EvaluateAsync("""
            () => {
                const btn = document.getElementById('c');
                window.seen = [btn.innerText.trim()];
                window.flashes = 0;
                new MutationObserver(records => {
                    window.flashes += records.filter(r => r.target === btn && r.addedNodes.length).length;
                    const now = btn.innerText.trim();
                    if (now !== window.seen[window.seen.length - 1]) window.seen.push(now);
                }).observe(btn, { childList: true, subtree: true, characterData: true });
            }
            """);

        // The flash waits on the clipboard write, so each wait also orders the read
        // below after the write rather than racing it.
        await page.Locator("#c").ClickAsync();
        await page.WaitForFunctionAsync("() => window.flashes === 1");
        await page.Locator("#c").ClickAsync();
        await page.WaitForFunctionAsync("() => window.flashes === 2");

        Assert.Equal("orders-console-01", await page.EvaluateAsync<string>(
            "() => navigator.clipboard.readText()"));
        await Assertions.Expect(page.Locator("#c")).ToHaveTextAsync("Copied");

        await page.Clock.RunForAsync(1400);

        await Assertions.Expect(page.Locator("#c")).ToHaveTextAsync("Copy");
        var seen = await page.EvaluateAsync<string[]>("() => window.seen");
        Assert.Equal(["Copy", "Copied", "Copy"], seen);
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
