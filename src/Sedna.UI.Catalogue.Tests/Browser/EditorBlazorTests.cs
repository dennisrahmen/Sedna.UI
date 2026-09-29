using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The rich-text editor through a real Blazor circuit, on <c>/editor</c>: <c>@bind</c> on the
/// textarea in both directions, <c>@onsedna-editor-change</c> with its data, and
/// <c>ISednaEditors</c> reaching the document.
/// </summary>
/// <remarks>
/// The library's suite pins the editor in a plain page and the call shape of <c>ISednaEditors</c>.
/// What only a running app shows is the round trip: that the editor started in the markup the
/// interactive render left, that Blazor's binding hears the textarea's events, and that a value set in
/// C# reaches the document without the reader's typing coming back over it.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class EditorBlazorTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open(List<string> errors)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        await page.GotoAsync(app.Url("/editor"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        await page.Locator("#reply").ScrollIntoViewIfNeededAsync();
        await page.WaitForSelectorAsync("#reply[data-editor-state='ready']", new() { Timeout = 15_000 });
        return page;
    }

    private static ILocator Example(IPage page) =>
        page.Locator("#reply").Locator("xpath=ancestor::section[@data-example][1]").Locator(".ex-demo");

    [Fact]
    public async Task Typing_reaches_the_bound_field_and_the_change_event_with_its_length()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);
        var demo = Example(page);

        // Loaded from the bound field's first value, and counted by the ready event.
        await Assertions.Expect(demo.Locator(".ql-editor")).ToContainTextAsync("It leaves the warehouse on Tuesday.");
        await Assertions.Expect(demo.Locator(".toolbar-count")).ToHaveTextAsync("58 characters");

        await demo.Locator(".ql-editor").ClickAsync();
        await page.Keyboard.PressAsync("Control+End");
        await page.Keyboard.TypeAsync(" Thanks!");

        // The component renders what the binding received.
        await Assertions.Expect(demo.Locator(".code-block code")).ToContainTextAsync("on <strong>Tuesday</strong>. Thanks!</p>");
        await Assertions.Expect(demo.Locator(".toolbar-count")).ToHaveTextAsync("66 characters");
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_value_set_in_csharp_reaches_the_document_and_a_call_types_at_the_caret()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);
        var demo = Example(page);

        await demo.GetByRole(AriaRole.Button, new() { Name = "Use the template" }).ClickAsync();
        await Assertions.Expect(demo.Locator(".ql-editor")).ToContainTextAsync("Thanks for getting in touch.");
        await Assertions.Expect(demo.Locator(".ql-editor li")).ToHaveCountAsync(2);

        await demo.Locator(".ql-editor p").ClickAsync();
        await page.Keyboard.PressAsync("End");
        await demo.GetByRole(AriaRole.Button, new() { Name = "Sign it" }).ClickAsync();
        await Assertions.Expect(demo.Locator(".code-block code")).ToContainTextAsync("Thanks for getting in touch. — Alex Fischer, Northwind Retail</p>");

        await demo.GetByRole(AriaRole.Button, new() { Name = "Clear" }).ClickAsync();
        await Assertions.Expect(demo.Locator(".code-block code")).ToHaveTextAsync("(empty)");
        await Assertions.Expect(demo.GetByRole(AriaRole.Button, new() { Name = "Send" })).ToBeDisabledAsync();
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Every_editor_on_the_page_starts_once_in_the_markup_that_stays()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open(errors);

        foreach (var id in new[] { "note", "article", "comment", "signed-off", "reply" })
        {
            await page.Locator("#" + id).ScrollIntoViewIfNeededAsync();
            await page.WaitForSelectorAsync($"#{id}[data-editor-state='ready']", new() { Timeout = 15_000 });
            Assert.Equal(1, await page.Locator($"#{id} .ql-editor").CountAsync());
        }

        Assert.Equal("true", await page.Locator("#signed-off .ql-editor").GetAttributeAsync("aria-readonly"));
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }
}
