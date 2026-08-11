namespace Sedna.UI.Catalogue.Navigation;

/// <summary>
/// What Ctrl-K offers: one command per page.
/// </summary>
/// <remarks>
/// <para>
/// <c>sednaUi.palette</c> leaves the browser's own Ctrl-K binding alone until
/// something is registered, so a site that never calls <c>register</c> has a
/// command palette in the stylesheet and none in the browser. This site
/// demonstrates the palette, so it has to have one.
/// </para>
/// <para>
/// Derived from <see cref="CataloguePages"/> like the sidebar and the search index,
/// so a new page is in the palette without a second list to keep in step. Commands
/// registered from C# navigate — a <c>run</c> callback cannot cross the interop
/// boundary — and navigation is all a page command needs.
/// </para>
/// </remarks>
internal static class CatalogueCommands
{
    public static IReadOnlyList<PaletteCommand> All { get; } =
    [
        .. CataloguePages.All.Select(page => new PaletteCommand
        {
            Label = page.Label,
            Href = page.Route,
            Icon = page.Icon,
            Group = page.Group,
            Note = page.Route,
            Keywords = page.Keywords,
        }),
    ];
}
