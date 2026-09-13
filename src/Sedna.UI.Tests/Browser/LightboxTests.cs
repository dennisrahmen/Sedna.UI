using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>The lightbox: modal, the whole viewport, and the picture inside it.</summary>
public class LightboxTests : ScriptTestBase
{
    // A 4000×1000 data-URI SVG stands in for a photograph wider than any viewport.
    private const string Body = """
        <button id="open" type="button" onclick="document.getElementById('lb').showModal()">Open</button>
        <dialog class="lightbox" id="lb" aria-labelledby="t">
            <div class="lightbox-head">
                <strong class="lightbox-title" id="t">wide.svg</strong>
                <div class="lightbox-actions"><form method="dialog"><button class="btn btn-ghost btn-icon" id="close" aria-label="Close">x</button></form></div>
            </div>
            <div class="lightbox-stage">
                <img id="img" src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='4000' height='1000'%3E%3Crect width='4000' height='1000' fill='%23888'/%3E%3C/svg%3E" alt="" />
            </div>
            <p class="lightbox-caption">A caption</p>
        </dialog>
        """;

    [Fact]
    public async Task The_picture_fits_the_viewport_and_the_dialog_is_modal()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Body);
        await page.SetViewportSizeAsync(900, 600);
        await page.Locator("#open").ClickAsync();

        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('lb').matches(':modal')"));
        var box = await page.Locator("#img").BoundingBoxAsync();
        Assert.NotNull(box);
        Assert.True(box!.Width <= 900 && box.Height <= 600, $"The picture is {box.Width}×{box.Height} in a 900×600 viewport.");
        Assert.True(box.Width > 600, "The picture was not scaled up to the stage.");

        await page.Locator("#close").ClickAsync();
        Assert.False(await page.EvaluateAsync<bool>("() => document.getElementById('lb').open"));
    }
}
