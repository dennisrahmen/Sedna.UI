using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// An overlay that asks before it closes, end to end through a real Blazor circuit: the
/// <c>/modal</c> example "Asking before it closes".
/// </summary>
/// <remarks>
/// The library's own suite pins what the script does with <c>data-close-guard</c>. What only
/// a running app can show is that Blazor's <c>@oncancel:preventDefault</c> refuses the close
/// in the browser before the circuit hears of it, and that the handler behind
/// <c>@oncancel</c> then runs — so Escape, pressed as often as a reader likes, never loses
/// what they wrote.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class CloseGuardTests(CatalogueAppFixture app)
{
    private static ILocator Reply(IPage page) =>
        page.Locator("dialog.modal[data-close-guard]");

    private static ILocator Discard(IPage page) =>
        page.Locator("dialog.modal-sm:has(h3:text-is('Discard this reply?'))");

    private static ILocator Outcome(IPage page) =>
        page.Locator(".cat-ex:has(button:text-is('Reply on ORD-4209')) .badge").First;

    private async Task<IPage> OpenReply()
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(app.Url("/modal"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });

        await page.GetByRole(AriaRole.Button, new() { Name = "Reply on ORD-4209" }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('dialog.modal[data-close-guard]')?.open === true");
        return page;
    }

    private static Task<bool> IsOpen(ILocator dialog) =>
        dialog.EvaluateAsync<bool>("d => d.open");

    [Fact]
    public async Task Escape_on_an_unsent_reply_asks_every_time_and_never_closes_it()
    {
        if (app.NoBrowser) return;
        var page = await OpenReply();

        await Reply(page).Locator("textarea").FillAsync("The replacement ships on Monday.");

        for (var i = 0; i < 4; i++)
        {
            await page.Keyboard.PressAsync("Escape");
            await page.WaitForFunctionAsync("() => [...document.querySelectorAll('dialog.modal-sm')].some(d => d.open)");

            // Escape on the question is "keep writing".
            await page.Keyboard.PressAsync("Escape");
            await page.WaitForFunctionAsync("() => ![...document.querySelectorAll('dialog.modal-sm')].some(d => d.open)");

            Assert.True(await IsOpen(Reply(page)));
        }

        Assert.Equal("The replacement ships on Monday.", await Reply(page).Locator("textarea").InputValueAsync());

        await page.Keyboard.PressAsync("Escape");
        await Discard(page).GetByRole(AriaRole.Button, new() { Name = "Discard" }).ClickAsync();

        await page.WaitForFunctionAsync("() => !document.querySelector('dialog.modal[data-close-guard]')");
        Assert.Equal("not sent", await Outcome(page).TextContentAsync());

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_close_button_asks_the_same_question()
    {
        if (app.NoBrowser) return;
        var page = await OpenReply();

        await Reply(page).Locator("textarea").FillAsync("The replacement ships on Monday.");
        await Reply(page).GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await page.WaitForFunctionAsync("() => [...document.querySelectorAll('dialog.modal-sm')].some(d => d.open)");

        await Discard(page).GetByRole(AriaRole.Button, new() { Name = "Keep writing" }).ClickAsync();
        await page.WaitForFunctionAsync("() => ![...document.querySelectorAll('dialog.modal-sm')].some(d => d.open)");
        Assert.True(await IsOpen(Reply(page)));

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Escape_on_an_empty_reply_closes_it_without_asking()
    {
        if (app.NoBrowser) return;
        var page = await OpenReply();

        await page.Keyboard.PressAsync("Escape");

        await page.WaitForFunctionAsync("() => !document.querySelector('dialog.modal[data-close-guard]')");
        Assert.Equal("not sent", await Outcome(page).TextContentAsync());

        await page.Context.CloseAsync();
    }
}
