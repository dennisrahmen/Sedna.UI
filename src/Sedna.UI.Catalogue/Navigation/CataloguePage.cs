namespace Sedna.UI.Catalogue.Navigation;

/// <summary>
/// One page of the catalogue, as the sidebar, the landing-page tiles, the search
/// index and the MCP server all see it.
/// </summary>
/// <param name="Section">
/// A labelled section inside the group's panel, which splits a long group into the
/// jobs its pages do. The sidebar and the landing page show a group's main list first
/// and its sections after it, in first-seen order. Null for the main list.
/// </param>
internal sealed record CataloguePage(
    string Route,
    string Group,
    string Label,
    string Icon,
    string Blurb,
    string Keywords,
    string? Section = null)
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
