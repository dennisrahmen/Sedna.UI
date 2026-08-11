namespace Sedna.UI.Catalogue.Navigation;

/// <summary>
/// One page of the catalogue, as the sidebar, the landing-page tiles, the search
/// index and the MCP server all see it.
/// </summary>
internal sealed record CataloguePage(
    string Route,
    string Group,
    string Label,
    string Icon,
    string Blurb,
    string Keywords)
{
    /// <summary>
    /// The static filename this page was before the catalogue became an
    /// application. Published links point at <c>/catalogue/&lt;file&gt;</c>, so
    /// <c>Program.cs</c> redirects from it — derived here rather than listed, so a
    /// new page cannot forget its redirect.
    /// </summary>
    public string LegacyFile =>
        Route == "/" ? "index.html" : Route.TrimStart('/') + ".html";
}
