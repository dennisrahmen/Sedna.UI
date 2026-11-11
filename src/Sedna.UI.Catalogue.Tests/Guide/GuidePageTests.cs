using System.Net;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The guide on the site: every entry at the foot of its page, every goal and section on the
/// design guide page — rendered by the server, so they are there with scripting off.
/// </summary>
[Trait(Scope.Name, Scope.Site)]
[Collection(CatalogueAppCollection.Name)]
public class GuidePageTests(CatalogueAppFixture app)
{
    [Fact]
    public async Task Every_entry_is_shown_on_its_own_page()
    {
        var problems = new List<string>();
        foreach (var entry in GuideFiles.Guide.Entries)
        {
            var html = WebUtility.HtmlDecode(await Get(entry.Page));
            if (!html.Contains("class=\"cat-guide\"", StringComparison.Ordinal))
                problems.Add($"{entry.Page} renders no guide panel.");
            else if (!html.Contains(entry.UseFor, StringComparison.Ordinal))
                problems.Add($"{entry.Page}'s guide panel does not say what the entry's useFor says.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public async Task A_page_with_no_entry_shows_no_panel()
    {
        Assert.DoesNotContain("cat-guide\"", await Get("/getting-started"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_design_guide_page_holds_every_goal_and_section()
    {
        var html = await Get("/design-guide");
        var guide = GuideFiles.Guide;

        foreach (var id in guide.Goals.Select(g => g.Id).Concat(guide.Sections.Select(s => s.Id)).Append("goals"))
            Assert.True(html.Contains($"id=\"{id}\"", StringComparison.Ordinal), $"/design-guide has no element with id \"{id}\".");
    }

    private async Task<string> Get(string route)
    {
        var response = await app.Client.GetAsync(new Uri(route, UriKind.Relative));
        Assert.True(response.IsSuccessStatusCode, $"{route} returned {(int)response.StatusCode}.");
        return await response.Content.ReadAsStringAsync();
    }
}
