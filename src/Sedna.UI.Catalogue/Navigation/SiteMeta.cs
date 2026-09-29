using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Sedna.UI.Catalogue.Navigation;

/// <summary>
/// What a search engine, a link preview and a crawler read about each page: the
/// description, the canonical address, the preview image, the structured data, and
/// the sitemap that lists them all.
/// </summary>
/// <remarks>
/// Derived from <see cref="CataloguePages"/> and nothing else, so a new page is
/// indexed, described and previewed the moment it is registered. The description is
/// the page's <see cref="CataloguePage.Blurb"/> — write a blurb as the sentence a
/// search result shows.
/// </remarks>
internal static class SiteMeta
{
    public const string Name = "Sedna.UI";

    /// <summary>The landing page's title, which is also the site's in a search result.</summary>
    public const string HomeTitle = "Sedna.UI — semantic CSS for Blazor, built to be written by an agent";

    /// <summary>The one line the repository, the package and a link preview say the library is.</summary>
    public const string Tagline = "Semantic CSS for Blazor, built to be written by an agent.";

    /// <summary>Every other page's title follows its own heading with this.</summary>
    public const string TitleSuffix = " — Sedna.UI Blazor catalogue";

    public const string HomeDescription =
        "Component libraries get you to 90%. Sedna.UI starts you at 100%: " +
        "your own HTML, semantic classes, and an MCP server that hands your agent the exact markup.";

    /// <summary>The link preview. <c>BrandAssetTests</c> holds the copy under <c>wwwroot/brand/</c> to <c>assets/brand/</c>.</summary>
    public const string ImagePath = "brand/sedna-ui-social-preview.png";
    public const int ImageWidth = 1280;
    public const int ImageHeight = 640;
    public const string ImageAlt = "The Sedna.UI mark and wordmark on a deep-space field";

    public static string Image => CatalogueLinks.Site + ImagePath;

    /// <summary>The absolute, canonical address of a route on the production host.</summary>
    public static string Canonical(string route) => CatalogueLinks.Site + route.TrimStart('/');

    /// <summary>The registered page at <paramref name="path"/>, or null for an address that is none.</summary>
    public static CataloguePage? PageAt(string path)
    {
        var route = "/" + path.Split('?', '#')[0].Trim('/');
        return CataloguePages.All.FirstOrDefault(p => string.Equals(p.Route, route, StringComparison.OrdinalIgnoreCase));
    }

    public static string Description(CataloguePage page) =>
        page.Route == "/"
            ? HomeDescription
            : $"{page.Blurb} Sedna.UI — semantic CSS for Blazor.";

    public static string Title(CataloguePage page) =>
        page.Route == "/" ? HomeTitle : page.Label + TitleSuffix;

    /// <summary>
    /// The page's JSON-LD. The landing page names the site and the package, which is
    /// what a search engine reads the site's name from; every other page is an article
    /// about one family, inside that site, with a breadcrumb back to it.
    /// </summary>
    public static string StructuredData(CataloguePage page)
    {
        var site = new Dictionary<string, object>
        {
            ["@type"] = "WebSite",
            ["@id"] = CatalogueLinks.Site + "#website",
            ["name"] = Name,
            ["alternateName"] = new[] { "Sedna UI", "Sedna.UI catalogue" },
            ["url"] = CatalogueLinks.Site,
            ["inLanguage"] = "en",
        };

        var package = new Dictionary<string, object>
        {
            ["@type"] = "SoftwareSourceCode",
            ["@id"] = CatalogueLinks.Site + "#package",
            ["name"] = Name,
            ["description"] = Tagline,
            ["url"] = CatalogueLinks.Site,
            ["codeRepository"] = CatalogueLinks.Repo,
            ["programmingLanguage"] = new[] { "C#", "CSS", "JavaScript" },
            ["runtimePlatform"] = ".NET",
            ["license"] = CatalogueLinks.Licence,
            ["image"] = Image,
            ["sameAs"] = new[] { CatalogueLinks.Repo, CatalogueLinks.NuGet },
        };

        object graph;
        if (page.Route == "/")
        {
            graph = new object[] { site, package };
        }
        else
        {
            var url = Canonical(page.Route);
            graph = new object[]
            {
                new Dictionary<string, object>
                {
                    ["@type"] = "TechArticle",
                    ["@id"] = url + "#article",
                    ["headline"] = page.Label,
                    ["description"] = Description(page),
                    ["url"] = url,
                    ["mainEntityOfPage"] = url,
                    ["image"] = Image,
                    ["inLanguage"] = "en",
                    ["isPartOf"] = new Dictionary<string, object> { ["@id"] = site["@id"] },
                    ["about"] = new Dictionary<string, object> { ["@id"] = package["@id"] },
                    ["keywords"] = page.Keywords,
                },
                new Dictionary<string, object>
                {
                    ["@type"] = "BreadcrumbList",
                    // Two steps, not three: a group has no page of its own, and a
                    // step with no address is one a search engine rejects.
                    ["itemListElement"] = new[]
                    {
                        Crumb(1, Name, CatalogueLinks.Site),
                        Crumb(2, page.Label, url),
                    },
                },
                site,
                package,
            };
        }

        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph,
        });

        static Dictionary<string, object> Crumb(int position, string name, string item) => new()
        {
            ["@type"] = "ListItem",
            ["position"] = position,
            ["name"] = name,
            ["item"] = item,
        };
    }

    /// <summary>Every registered page, at its canonical address.</summary>
    public static string SitemapXml()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset",
                CataloguePages.All.Select(p => new XElement(ns + "url",
                    new XElement(ns + "loc", Canonical(p.Route))))));

        using var writer = new Utf8StringWriter();
        doc.Save(writer);
        return writer.ToString();
    }

    /// <summary>
    /// Everything a person reads is open to a crawler. The MCP transport, the
    /// healthcheck and Blazor's circuit are endpoints for programs, not pages.
    /// <c>_framework</c> and <c>_content</c> stay open: a crawler that renders the page
    /// needs its script and stylesheet. <c>/mcp$</c> is anchored because a rule is a
    /// prefix, and a bare <c>/mcp</c> would also hide the <c>/mcp-server</c> page.
    /// </summary>
    public static string RobotsTxt() =>
        $"""
        User-agent: *
        Disallow: /mcp$
        Disallow: /health
        Disallow: /_blazor
        Allow: /

        Sitemap: {CatalogueLinks.Site}sitemap.xml

        """;

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
