using System.Net;

using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The Start group opens with the pages every reader needs, then holds its pages under headed
/// sections; a page that moved keeps answering at its old address.
/// </summary>
[Trait(Scope.Name, Scope.Site)]
[Collection(CatalogueAppCollection.Name)]
public class StartSectionTests(CatalogueAppFixture app)
{
    private static readonly string[] OpeningPages = ["/", "/getting-started", "/design-guide"];

    [Fact]
    public void Start_opens_with_the_overview_getting_started_and_the_design_guide_under_no_heading()
    {
        var start = CataloguePages.InGroup(CataloguePages.Start).ToList();

        Assert.Equal(OpeningPages, start.Take(OpeningPages.Length).Select(p => p.Route));
        Assert.All(start.Take(OpeningPages.Length), p => Assert.Null(p.Section));
    }

    [Fact]
    public void Start_holds_every_other_page_under_a_heading_in_a_fixed_order()
    {
        var sections = CataloguePages.InGroup(CataloguePages.Start)
            .Where(p => p.Section is not null).Select(p => p.Section!).Distinct().ToList();

        Assert.Equal(
            [CataloguePages.Foundations, CataloguePages.Theming, CataloguePages.Brand, CataloguePages.Reference],
            sections);
        Assert.All(CataloguePages.InGroup(CataloguePages.Start).Skip(OpeningPages.Length), p => Assert.NotNull(p.Section));
    }

    [Fact]
    public void A_moved_address_is_not_also_a_page()
    {
        var registered = CataloguePages.All.Select(p => p.Route).ToHashSet(StringComparer.Ordinal);

        Assert.All(CataloguePages.Moved, m =>
        {
            Assert.DoesNotContain(m.Key, registered);
            Assert.Contains(m.Value.Split('#')[0], registered);
        });
    }

    [Fact]
    public async Task The_address_of_the_merged_C_sharp_page_redirects_to_it()
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var response = await client.GetAsync(app.Url("/interop"));

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/script", response.Headers.Location?.OriginalString);
    }
}
