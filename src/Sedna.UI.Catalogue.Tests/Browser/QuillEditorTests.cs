using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The library's Quill skin on the real editor: <c>Spillgebees.Blazor.RichTextEditor</c>, at the
/// version the catalogue pins, rendering into the chrome's classes on <c>/editor</c>.
/// </summary>
/// <remarks>
/// The skin follows markup Quill generates, which no library test can produce. So this is the
/// test that fails when a package or Quill upgrade moves a hook: an icon that no longer takes
/// the button's colour, a list marker that is back to the browser's own, or a tooltip that lost
/// the app's words.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class QuillEditorTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open()
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(app.Url("/editor"), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync(".editor .ql-toolbar button.ql-bold svg", new() { Timeout = 15_000 });
        return page;
    }

    [Fact]
    public async Task The_editor_renders_in_the_chrome_with_the_package_stylesheet_left_out()
    {
        if (app.NoBrowser) return;
        var page = await Open();

        var facts = await page.EvaluateAsync<string[]>("""
            () => {
                const editor = document.querySelector('.rich-text-editor-container');
                const bold = editor.querySelector('button.ql-bold');
                const stroke = editor.querySelector('button.ql-bold .ql-stroke');
                return [
                    String(editor.classList.contains('editor')),
                    String(editor.querySelector('.ql-toolbar').classList.contains('editor-toolbar')),
                    String([...document.styleSheets].some(s => (s.href || '').includes('Spillgebees'))),
                    getComputedStyle(stroke).stroke === getComputedStyle(bold).color ? 'stroke follows the button' : getComputedStyle(stroke).stroke,
                    getComputedStyle(editor.querySelector('.ql-container')).overflow,
                ];
            }
            """);

        Assert.Equal("true", facts[0]);
        Assert.Equal("true", facts[1]);
        Assert.Equal("false", facts[2]);
        Assert.Equal("stroke follows the button", facts[3]);
        Assert.Equal("visible", facts[4]);   // the tooltip is not clipped by a scrolling container
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Lists_are_numbered_and_checked_by_the_skin_not_the_browser()
    {
        if (app.NoBrowser) return;
        var page = await Open();

        var markers = await page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('.editor .ql-editor li')].map(li =>
                getComputedStyle(li).listStyleType + '|' + getComputedStyle(li.querySelector('.ql-ui'), '::before').content)
            """);

        Assert.All(markers, m => Assert.StartsWith("none|", m, StringComparison.Ordinal));
        Assert.Contains(markers, m => m.Contains("counter(list-0", StringComparison.Ordinal));
        Assert.Contains(markers, m => m.EndsWith("|\"✓\"", StringComparison.Ordinal));
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_link_tooltip_speaks_the_app_s_words()
    {
        if (app.NoBrowser) return;
        var page = await Open();

        await page.Locator(".editor .ql-editor p").ClickAsync();
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("Shift+ArrowRight");
        await page.Locator(".editor button.ql-link").ClickAsync();
        var tooltip = page.Locator(".editor .ql-tooltip.ql-editing");
        await tooltip.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var words = await page.EvaluateAsync<string[]>("""
            () => {
                const t = document.querySelector('.editor .ql-tooltip');
                return [getComputedStyle(t.querySelector('.ql-action'), '::after').content,
                        getComputedStyle(t.querySelector('.ql-remove'), '::before').content,
                        getComputedStyle(t).zIndex];
            }
            """);
        Assert.Equal("\"Save\"", words[0]);
        Assert.Equal("\"Remove\"", words[1]);
        Assert.Equal("550", words[2]);
        await page.Context.CloseAsync();
    }
}
