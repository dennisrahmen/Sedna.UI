using Microsoft.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The agent pages through a real Blazor circuit: the orb starting in the markup the
/// interactive render left, and the Mochi playground writing the attributes it shows.
/// </summary>
/// <remarks>
/// The library's suite pins the orb and Mochi in a plain page. What only a running app shows
/// is that a prerendered page hands its orbs to the drawing once the circuit is up, and that a
/// re-render changes Mochi's attributes in place — the element stays, so its joints ease.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class AgentPagesTests(CatalogueAppFixture app)
{
    private async Task<IPage> Open(string route, List<string> errors)
    {
        var context = await app.Browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        await page.GotoAsync(app.Url(route), new() { WaitUntil = WaitUntilState.Load });
        await page.WaitForSelectorAsync("[data-interactive='true']", new() { Timeout = 15_000 });
        return page;
    }

    [Fact]
    public async Task The_orbs_draw_once_the_page_is_interactive()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/orb", errors);

        await page.Locator("[data-orb='searching']").First.ScrollIntoViewIfNeededAsync();
        await page.WaitForFunctionAsync("""
            () => {
                const c = document.querySelector("[data-orb='searching'] > canvas");
                if (!c || !c.width) return false;
                const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
                let n = 0;
                for (let i = 3; i < d.length; i += 4) if (d[i] > 8) n++;
                return n > 64;
            }
            """, null, new() { Timeout = 15_000 });

        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task The_playground_writes_the_attributes_it_shows()
    {
        if (app.NoBrowser) return;
        var errors = new List<string>();
        var page = await Open("/mochi", errors);
        var mochi = page.Locator("#mochi-playground");
        var section = page.Locator("#playground-section");
        var element = await mochi.ElementHandleAsync();

        async Task Choose(string group, string value) =>
            await section.GetByRole(AriaRole.Group, new() { Name = group })
                .GetByRole(AriaRole.Button, new() { Name = value, Exact = true }).ClickAsync();

        await Choose("Action", "wave");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("data-action", "wave");
        await Choose("Perch", "hang");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("data-perch", "hang");
        await Choose("Mood", "worried");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("data-mood", "worried");
        await Choose("Tone", "sky");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("data-tone", "sky");
        await Assertions.Expect(section.Locator("pre code"))
            .ToContainTextAsync("data-perch=\"hang\" data-action=\"wave\" data-mood=\"worried\" data-tone=\"sky\"");

        // The defaults are no attribute at all, as an app would write them.
        await Choose("Perch", "none");
        await Choose("Mood", "auto");
        await Choose("Tone", "pink");
        await Assertions.Expect(mochi).Not.ToHaveAttributeAsync("data-perch", new System.Text.RegularExpressions.Regex(".*"));
        await Assertions.Expect(mochi).Not.ToHaveAttributeAsync("data-mood", new System.Text.RegularExpressions.Regex(".*"));
        await Assertions.Expect(mochi).Not.ToHaveAttributeAsync("data-tone", new System.Text.RegularExpressions.Regex(".*"));

        // Pointing names the button it points at.
        await Choose("Perch", "point");
        await Assertions.Expect(mochi).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex("position-anchor: --mochi-playground"));

        // The same element throughout, so every change was a transition rather than a new drawing.
        Assert.True(await page.EvaluateAsync<bool>("el => el.isConnected && el === document.getElementById('mochi-playground')", element));
        Assert.Empty(errors);
        await page.Context.CloseAsync();
    }
}
