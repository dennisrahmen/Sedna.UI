using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;
namespace Sedna.UI.Tests;
public class ScratchShotTests : ScriptTestBase
{
    [Fact]
    public async Task Shot()
    {
        if (NoBrowser) return;
        foreach (var v in new[] { "dark", "light" })
        {
            var ctx = await Browser!.NewContextAsync(new() { DeviceScaleFactor = 2, ViewportSize = new() { Width = 1280, Height = 900 } });
            var page = await ctx.NewPageAsync();
            await page.GotoAsync("http://localhost:65432/qr");
            await page.WaitForTimeoutAsync(2000);
            await page.EvaluateAsync("v => document.documentElement.dataset.variant = v", v);
            await page.EvaluateAsync("() => sednaUi.qr.init()");
            await page.WaitForTimeoutAsync(800);
            foreach (var t in new[] { "Tones", "Contrast that scans", "A logo in the middle", "Download as SVG or PNG", "Module and eye shapes" })
            {
                var demo = page.Locator(".cat-ex", new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = t }) }).First;
                await demo.Locator(".ex-demo").First.ScrollIntoViewIfNeededAsync();
                await page.WaitForTimeoutAsync(300);
                await demo.Locator(".ex-demo").First.ScreenshotAsync(new() { Path = "/private/tmp/claude-501/-Users-dennisrahmen-Source-Sedna-UI--claude-worktrees-catalogue-content-review-0e60f0/00a9caef-5d75-410b-8105-2dd19955d491/scratchpad/qr-" + v + "-" + t.Split(' ')[0] + ".png" });
            }
        }
    }
}
