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

        // The confirmation is a 1400ms window, so it cannot be SAMPLED: on a machine
        // loaded enough — every full-suite run beside the browser tests — the whole
        // window opens and closes between the click and the assertion, and the test
        // failed for having looked too late rather than for anything being wrong.
        // Both directions of the same race: a fixed sleep afterwards was also too
        // short whenever the timer ran late.
        //
        // So the label is watched instead of read. The observer is installed before
        // the click and records every text the button ever had, which is what the
        // claim actually is — it said "Copied", and then it said "Copy" again — and
        // is true whenever those happened.
        await page.EvaluateAsync("""
            () => {
                const btn = document.getElementById('c');
                window.seen = [btn.innerText.trim()];
                new MutationObserver(() => {
                    const now = btn.innerText.trim();
                    if (now !== window.seen[window.seen.length - 1]) window.seen.push(now);
                }).observe(btn, { childList: true, subtree: true, characterData: true });
            }
            """);

        await page.Locator("#c").ClickAsync();
        await page.Locator("#c").ClickAsync();

        Assert.Equal("TCK0031209", await page.EvaluateAsync<string>(
            "() => navigator.clipboard.readText()"));

        await Assertions.Expect(page.Locator("#c")).ToHaveTextAsync(
            "Copy", new() { Timeout = 6000 });

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
