using System.Collections.Concurrent;
using Sedna.UI.Catalogue.Guide;
using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Every page and anchor the design guide points at is there: a registered page, and with an
/// anchor, an id on the page as the running app serves it — an example's heading, a section's.
/// </summary>
/// <remarks>
/// Read off the served HTML rather than worked out from the page source, so an example's slug,
/// a heading written by hand and the guide page's own sections are found the same way, and a
/// renamed example fails here before a reader follows a dead link.
/// </remarks>
[Trait(Scope.Name, Scope.Site)]
[Collection(CatalogueAppCollection.Name)]
public class GuideReferenceTests(CatalogueAppFixture app)
{
    private static readonly ConcurrentDictionary<string, string> Served = new(StringComparer.Ordinal);

    [Fact]
    public async Task Every_page_and_anchor_the_guide_names_exists()
    {
        var guide = GuideFiles.Guide;
        var references = new List<(string Where, string Page)>();

        foreach (var entry in guide.Entries)
        {
            var at = $"{entry.Page} in components/{entry.Group.ToLowerInvariant()}.json";
            references.AddRange(entry.CombineWith.Select(c => ($"{at}, combineWith", c.Page)));
            references.AddRange(entry.NotFor.Select(n => ($"{at}, notFor \"{n.Text}\"", n.Instead)));
        }

        foreach (var goal in guide.Goals)
            references.AddRange(goal.Answer.Select(a => ($"goal \"{goal.Id}\" in goals/{goal.Group.ToLowerInvariant()}.json, answer", a.Page)));

        var site = CatalogueLinks.Site.TrimEnd('/');
        references.AddRange(GuideMarkdown.Links(GuideFiles.Principles)
            .Where(l => l.StartsWith(site + "/", StringComparison.Ordinal))
            .Select(l => ("principles.md", l[site.Length..])));

        Assert.NotEmpty(references);

        var problems = new List<string>();
        foreach (var (where, page) in references)
        {
            var (route, anchor) = DesignGuide.Split(page);
            if (CataloguePages.All.All(p => p.Route != route))
            {
                problems.Add($"{where}: {page} — {route} is no registered page.");
                continue;
            }

            if (anchor.Length > 0 && !(await Html(route)).Contains($"id=\"{anchor}\"", StringComparison.Ordinal))
                problems.Add($"{where}: {page} — {route} has no element with id \"{anchor}\". An example's id is "
                             + "its title as Slug.From makes it: lowercase words joined by hyphens.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private async Task<string> Html(string route)
    {
        if (Served.TryGetValue(route, out var cached)) return cached;

        var response = await app.Client.GetAsync(new Uri(route, UriKind.Relative));
        Assert.True(response.IsSuccessStatusCode, $"{route} returned {(int)response.StatusCode}.");
        return Served[route] = await response.Content.ReadAsStringAsync();
    }
}
