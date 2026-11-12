using System.Net;
using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The contents beside every page list its sections, in order, and nothing else — a topic's
/// sections nested under it. A section is a heading, an h2 or a topic's h3, carrying its link
/// icon (<c>CatAnchor</c>), which a heading inside an example never does, so the served HTML says
/// exactly which sections a page has.
/// </summary>
[Trait(Scope.Name, Scope.Site)]
[Collection(CatalogueAppCollection.Name)]
public partial class PageContentsTests(CatalogueAppFixture app)
{
    [GeneratedRegex(@"<h(?<level>[23]) id=""(?<id>[^""]+)""[^>]*>(?:(?!</h\k<level>>).)*?class=""cat-anchor""", RegexOptions.Singleline)]
    private static partial Regex SectionHeading();

    [GeneratedRegex("""<div class="sedna-split-aside cat-toc">(?<toc>.*?)</nav>""", RegexOptions.Singleline)]
    private static partial Regex Contents();

    [GeneratedRegex(@"href=""#(?<id>[^""]+)""")]
    private static partial Regex Link();

    [Fact]
    public async Task Every_page_lists_its_sections_beside_it_in_order()
    {
        var problems = new List<string>();
        foreach (var page in CataloguePages.All)
        {
            var html = await Get(page.Route);
            var sections = SectionHeading().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups["id"].Value)).ToList();
            var toc = Contents().Match(html);

            if (sections.Count < 2)
            {
                if (toc.Success) problems.Add($"{page.Route} has {sections.Count} section and still shows contents.");
                continue;
            }

            if (!toc.Success)
            {
                problems.Add($"{page.Route} has {sections.Count} sections and no contents beside it.");
                continue;
            }

            var listed = Link().Matches(toc.Groups["toc"].Value).Select(m => WebUtility.HtmlDecode(m.Groups["id"].Value)).ToList();
            if (!listed.SequenceEqual(sections, StringComparer.Ordinal))
                problems.Add($"{page.Route}'s contents list [{string.Join(", ", listed)}] but its sections are [{string.Join(", ", sections)}].");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private async Task<string> Get(string route)
    {
        var response = await app.Client.GetAsync(new Uri(route, UriKind.Relative));
        Assert.True(response.IsSuccessStatusCode, $"{route} returned {(int)response.StatusCode}.");
        return await response.Content.ReadAsStringAsync();
    }
}
