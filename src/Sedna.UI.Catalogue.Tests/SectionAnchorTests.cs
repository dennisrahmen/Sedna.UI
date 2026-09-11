using System.Net;
using System.Text.RegularExpressions;

using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Every section's link icon points at a heading on the page it sits on.
/// </summary>
/// <remarks>
/// The host page declares <c>&lt;base href="/"&gt;</c>, and a bare <c>href="#x"</c>
/// resolves against it: every link icon pointed at <c>/#x</c>, and following one
/// opened the landing page. The markup reads as correct, so each href is resolved the
/// way a browser resolves it — against the document's own base.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class SectionAnchorTests(CatalogueAppFixture app)
{
    private static readonly Regex Base =
        new("""<base href="(?<href>[^"]*)""", RegexOptions.Compiled);

    private static readonly Regex Anchor =
        new("""<a class="cat-anchor" href="(?<href>[^"]*)""", RegexOptions.Compiled);

    public static TheoryData<string> Routes() => RoutedPages.AsTheoryData();

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Every_section_link_resolves_to_a_heading_on_its_own_page(string route)
    {
        var html = await Get(route);
        var document = new Uri(app.BaseAddress, route);
        var baseUri = new Uri(document, Base.Match(html).Groups["href"].Value);

        foreach (var anchor in Anchor.Matches(html).Cast<Match>())
        {
            var href = WebUtility.HtmlDecode(anchor.Groups["href"].Value);
            var target = new Uri(baseUri, href);

            Assert.True(target.AbsolutePath == document.AbsolutePath,
                $"{route}: the section link \"{href}\" resolves to {target.AbsolutePath}, not to this page.");

            var id = target.Fragment.TrimStart('#');
            Assert.True(id.Length > 0 && html.Contains($"id=\"{id}\"", StringComparison.Ordinal),
                $"{route}: the section link \"{href}\" names no id on the page.");
        }
    }

    [Fact]
    public async Task The_catalogue_renders_section_links_at_all()
    {
        // The vacuity guard: the theory above passes on a page with no links, and a
        // regex that silently stops matching would look identical to a clean run.
        // Counted off the landing page, because a bare "#x" resolves correctly there
        // and it is the one page where the defect could not show.
        var found = 0;
        foreach (var route in RoutedPages.All.Where(r => r != "/"))
        {
            found += Anchor.Matches(await Get(route)).Count;
        }

        Assert.True(found >= 5, $"Only {found} section links rendered across the catalogue's sub-pages.");
    }

    private async Task<string> Get(string route)
    {
        var response = await app.Client.GetAsync(new Uri(route, UriKind.Relative));
        Assert.True(response.IsSuccessStatusCode, $"{route} returned {(int)response.StatusCode}.");
        return await response.Content.ReadAsStringAsync();
    }
}
