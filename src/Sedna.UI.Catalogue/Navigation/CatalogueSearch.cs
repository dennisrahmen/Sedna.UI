using Sedna.UI.Catalogue.Components.Docs;
using Sedna.UI.Catalogue.Mcp;

namespace Sedna.UI.Catalogue.Navigation;

/// <summary>
/// What the topbar search can find: every page, and every example on one.
/// </summary>
/// <remarks>
/// <para>
/// Built once at startup from the same two sources everything else on the site is
/// — <see cref="CataloguePages"/> and the embedded example resources — so a page
/// or an example cannot exist without being findable, and the search cannot offer
/// a result that is not there.
/// </para>
/// <para>
/// An example carries the classes it uses as keywords rather than its prose. That
/// is what makes <c>badge-go</c> land on the example that writes it, which is the
/// question this site is actually asked; full text is the MCP server's
/// <c>search</c> tool, which does not have to fit in a dropdown.
/// </para>
/// </remarks>
internal sealed class CatalogueSearch
{
    public CatalogueSearch(CatalogueIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);

        var labels = CataloguePages.All.ToDictionary(p => p.Route, p => p.Label, StringComparer.Ordinal);

        var pages = CataloguePages.All.Select(page => new SearchItem
        {
            Title = page.Label,
            Href = page.Route,
            Code = page.Route,
            Meta = page.Blurb,
            Keywords = $"{page.Keywords} {page.Group}",
        });

        var examples = index.Examples.Select(example => new SearchItem
        {
            Title = example.Title,
            // The anchor CatExample renders, so a result opens at the section
            // rather than at the top of a page holding a dozen of them.
            Href = $"{example.Route}#{Slug.From(example.Title)}",
            Meta = labels.GetValueOrDefault(example.Route, example.Page),
            Tag = "example",
            Keywords = string.Join(' ', example.Classes),
        });

        Items = [.. pages, .. examples];
    }

    /// <summary>The whole index, in the order the ranking uses to break ties.</summary>
    public IReadOnlyList<SearchItem> Items { get; }
}
