using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// axe-core over every page.
/// </summary>
/// <remarks>
/// <para>
/// It finds the WCAG failures a source scan cannot see at all: a missing accessible
/// name, a broken label association, a contrast pair the token audit did not think
/// to check. The catalogue is the only place the library's markup is all rendered
/// at once, so this is the only place they can be found.
/// </para>
/// <para>
/// Best-practice rules are excluded deliberately. They flag things that are matters
/// of taste rather than conformance — a page-level landmark on a documentation
/// page, or a heading order that follows the content rather than the outline.
/// </para>
/// <para>
/// Runs after the circuit connects, so the DOM axe sees is the one a user gets.
/// </para>
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class AccessibilityTests(CatalogueAppFixture app)
{
    private static List<string> Tags => ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

    [Fact]
    public async Task Every_page_passes_axe()
    {
        if (app.NoBrowser) return;

        var problems = new List<string>();

        foreach (var route in RoutedPages.All)
        {
            var page = await app.OpenInteractiveAsync(route);

            var result = await page.RunAxe(new AxeRunOptions
            {
                RunOnly = new RunOnlyOptions { Type = "tag", Values = Tags },
            });

            foreach (var violation in result.Violations)
            {
                problems.Add(
                    $"{route}: {violation.Id} ({violation.Impact}) — {violation.Help} "
                    + $"[{string.Join(", ", violation.Nodes.Take(3).Select(n => n.Target))}]");
            }

            await page.CloseAsync();
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
