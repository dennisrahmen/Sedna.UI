using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Saving and leaving through a real Blazor circuit: the save bar following the component's own
/// state on <c>/form-saving</c>, <c>NavigationLock</c> asking with the app's dialog before a link
/// inside the app, and the session timeout warning on <c>/common-pages</c>.
/// </summary>
/// <remarks>
/// The library's suite pins the stylesheet (<c>SaveBarTests</c>) and the browser's own prompt
/// (<c>CloseGuardTests</c>). What only a running app shows is that <c>data-dirty</c> rendered from
/// a bool comes and goes with the comparison, and that the awaited dialogs settle.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class FormSavingTests(CatalogueAppFixture app)
{
    private const string Form = "form[data-close-guard]";

    private async Task<IPage> OpenAt(string path)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(app.Url(path), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    // An internal link is a navigation inside the document, which fires no load event, so
    // the wait is on the address itself rather than on WaitForURL's default load state.
    private static Task AtPath(IPage page, string path) =>
        page.WaitForFunctionAsync("p => location.pathname === p", path);

    private static Task<bool> Dirty(IPage page) =>
        page.Locator(Form).EvaluateAsync<bool>("f => f.hasAttribute('data-dirty')");

    private static Task<string> SaveBarDisplay(IPage page) =>
        page.Locator($"{Form} .form-actions--save-bar").EvaluateAsync<string>("e => getComputedStyle(e).display");

    [Fact]
    public async Task The_save_bar_comes_and_goes_with_the_components_own_state()
    {
        if (app.NoBrowser) return;
        var page = await OpenAt("/form-saving");

        Assert.False(await Dirty(page));
        Assert.Equal("none", await SaveBarDisplay(page));

        var name = page.Locator("#uc-name");
        await name.FillAsync("North depot, gate B");
        await page.WaitForFunctionAsync($"() => document.querySelector('{Form}').hasAttribute('data-dirty')");
        Assert.Equal("flex", await SaveBarDisplay(page));

        // Typed back to what was saved is not a change.
        await name.FillAsync("North depot");
        await page.WaitForFunctionAsync($"() => !document.querySelector('{Form}').hasAttribute('data-dirty')");

        await name.FillAsync("North depot, gate B");
        await page.WaitForFunctionAsync($"() => document.querySelector('{Form}').hasAttribute('data-dirty')");
        await page.Locator(Form).GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await page.WaitForFunctionAsync($"() => !document.querySelector('{Form}').hasAttribute('data-dirty')");
        Assert.Equal("none", await SaveBarDisplay(page));

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_link_inside_the_app_asks_with_the_apps_dialog_while_the_form_is_dirty()
    {
        if (app.NoBrowser) return;
        var page = await OpenAt("/form-saving");
        var leave = page.Locator("#leave-unsaved");
        var link = page.Locator("a[href='/form#action-placement']").First;

        await page.Locator("#uc-dock").FillAsync("Dock 4");
        await page.WaitForFunctionAsync($"() => document.querySelector('{Form}').hasAttribute('data-dirty')");

        await link.ClickAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('leave-unsaved').open");
        Assert.Equal("/form-saving", new Uri(page.Url).AbsolutePath);

        await leave.GetByRole(AriaRole.Button, new() { Name = "Stay on this page" }).ClickAsync();
        await page.WaitForFunctionAsync("() => !document.getElementById('leave-unsaved').open");
        Assert.Equal("/form-saving", new Uri(page.Url).AbsolutePath);
        Assert.Equal("Dock 4", await page.Locator("#uc-dock").InputValueAsync());

        await link.ClickAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('leave-unsaved').open");
        await leave.GetByRole(AriaRole.Button, new() { Name = "Leave and discard" }).ClickAsync();
        await AtPath(page, "/form");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task A_clean_form_lets_a_link_through_without_asking()
    {
        if (app.NoBrowser) return;
        var page = await OpenAt("/form-saving");

        await page.Locator("a[href='/form#action-placement']").First.ClickAsync();
        await AtPath(page, "/form");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_session_warning_opens_as_an_alertdialog_counting_down_and_staying_keeps_the_session()
    {
        if (app.NoBrowser) return;
        var page = await OpenAt("/common-pages");
        var warning = page.Locator("#session-timeout");
        var badge = page.Locator(".cat-ex:has(#session-timeout) .badge").First;

        await page.GetByRole(AriaRole.Button, new() { Name = "Start a short session" }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('session-timeout').open");

        Assert.Equal("alertdialog", await warning.GetAttributeAsync("role"));
        var time = warning.Locator("time[data-countdown]");
        Assert.False(string.IsNullOrEmpty(await time.GetAttributeAsync("datetime")));
        await Assertions.Expect(time).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^(2 minutes|1 minute)$"));

        await warning.GetByRole(AriaRole.Button, new() { Name = "Stay signed in" }).ClickAsync();
        await page.WaitForFunctionAsync("() => !document.getElementById('session-timeout').open");
        await Assertions.Expect(badge).ToHaveTextAsync("signed in");

        // The extended session warns again, and signing out ends it.
        await page.WaitForFunctionAsync("() => document.getElementById('session-timeout').open");
        await warning.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await Assertions.Expect(badge).ToHaveTextAsync("signed out");
        Assert.Null(await time.GetAttributeAsync("datetime"));

        await page.Context.CloseAsync();
    }
}
