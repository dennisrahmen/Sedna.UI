using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// What a search engine and a link preview read: every registered page is described,
/// canonical and previewed in its prerendered HTML, listed in the sitemap, and open to
/// crawlers — and an address that is no page is marked noindex.
/// </summary>
/// <remarks>
/// Against the served HTML, because a crawler and a preview fetcher read exactly that
/// and never wait for the circuit.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public partial class SiteMetaTests(CatalogueAppFixture app)
{
    public static TheoryData<string> Routes() => RoutedPages.AsTheoryData();

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Every_page_carries_its_description_canonical_and_preview(string route)
    {
        var page = Assert.Single(CataloguePages.All, p => p.Route == route);
        var html = await app.Client.GetStringAsync(new Uri(route, UriKind.Relative));
        var canonical = SiteMeta.Canonical(route);

        Assert.Equal([canonical], Links(html, "canonical"));
        Assert.Equal([SiteMeta.Description(page)], Metas(html, "name", "description"));
        Assert.Equal([canonical], Metas(html, "property", "og:url"));
        Assert.Equal([SiteMeta.Image], Metas(html, "property", "og:image"));
        Assert.Equal(["summary_large_image"], Metas(html, "name", "twitter:card"));
        Assert.Empty(Metas(html, "name", "robots"));

        // One block, and valid JSON: a parse error drops the page's rich result silently.
        var ld = Assert.Single(JsonLd().Matches(html)).Groups["json"].Value;
        using var doc = JsonDocument.Parse(ld);
        Assert.Equal("https://schema.org", doc.RootElement.GetProperty("@context").GetString());
    }

    [Fact]
    public void Every_description_fits_a_search_result()
    {
        // Past about 160 characters a result truncates it mid-sentence.
        var tooLong = CataloguePages.All
            .Select(p => (p.Route, Length: SiteMeta.Description(p).Length))
            .Where(p => p.Length > 160)
            .ToList();

        Assert.True(tooLong.Count == 0,
            "Shorten the blurb, which is the page's search description: " +
            string.Join(", ", tooLong.Select(p => $"{p.Route} ({p.Length})")));
    }

    [Fact]
    public async Task An_address_that_is_no_page_is_not_indexed()
    {
        var response = await app.Client.GetAsync(new Uri("/no-such-page", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(["noindex"], Metas(html, "name", "robots"));
        Assert.Empty(Links(html, "canonical"));
    }

    [Fact]
    public async Task The_sitemap_lists_every_page_once_at_its_canonical_address()
    {
        var response = await app.Client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative));
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = XDocument.Parse(await response.Content.ReadAsStringAsync())
            .Descendants(ns + "loc").Select(l => l.Value).ToList();

        Assert.Equal(CataloguePages.All.Select(p => SiteMeta.Canonical(p.Route)), locs);
        Assert.Equal(locs.Count, locs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Robots_names_the_sitemap_and_hides_only_program_endpoints()
    {
        var robots = await app.Client.GetStringAsync(new Uri("/robots.txt", UriKind.Relative));

        Assert.Contains($"Sitemap: {CatalogueLinks.Site}sitemap.xml", robots, StringComparison.Ordinal);

        // A Disallow is a prefix. Anchoring /mcp is what keeps /mcp-server indexable, and
        // no registered page may start with any other rule.
        var rules = robots.Split('\n')
            .Where(l => l.StartsWith("Disallow:", StringComparison.Ordinal))
            .Select(l => l["Disallow:".Length..].Trim())
            .ToList();
        Assert.NotEmpty(rules);

        var hidden = CataloguePages.All.Where(p => rules.Any(r => r.EndsWith('$')
            ? p.Route == r.TrimEnd('$')
            : p.Route.StartsWith(r, StringComparison.Ordinal))).Select(p => p.Route).ToList();
        Assert.Empty(hidden);
    }

    [Theory]
    [InlineData(SiteMeta.ImagePath)]
    [InlineData("brand/sedna-ui-tile-256.png")]
    [InlineData("brand/sedna-ui-tile-512.png")]
    [InlineData("site.webmanifest")]
    public async Task The_preview_image_icons_and_manifest_are_served(string path)
    {
        var response = await app.Client.GetAsync(new Uri("/" + path, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static List<string> Metas(string html, string attribute, string name) =>
        MetaTag().Matches(html)
            .Where(m => m.Groups["attr"].Value == attribute && m.Groups["name"].Value == name)
            .Select(m => WebUtility.HtmlDecode(m.Groups["content"].Value))
            .ToList();

    private static List<string> Links(string html, string rel) =>
        LinkTag().Matches(html)
            .Where(m => m.Groups["rel"].Value == rel)
            .Select(m => WebUtility.HtmlDecode(m.Groups["href"].Value))
            .ToList();

    [GeneratedRegex(""""<meta (?<attr>name|property)="(?<name>[^"]+)" content="(?<content>[^"]*)"""")]
    private static partial Regex MetaTag();

    [GeneratedRegex(""""<link rel="(?<rel>[^"]+)" href="(?<href>[^"]*)"""")]
    private static partial Regex LinkTag();

    [GeneratedRegex("""<script type="application/ld\+json">(?<json>.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex JsonLd();
}
