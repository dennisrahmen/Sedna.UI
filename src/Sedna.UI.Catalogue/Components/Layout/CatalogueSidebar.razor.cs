using Sedna.UI.Catalogue.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Sedna.UI.Catalogue.Components.Layout;

public partial class CatalogueSidebar : ComponentBase, IDisposable
{
    [Parameter] public bool Collapsed { get; set; }

    /// <summary>
    /// The component that reads the address is the component that subscribes.
    /// </summary>
    /// <remarks>
    /// Putting this on the layout instead looks equivalent and is not: when the
    /// layout re-renders, Blazor only hands new parameters to a child component
    /// whose parameter frames actually differ, so a sidebar whose single parameter
    /// is unchanged is skipped entirely and keeps rendering the previous address.
    /// The symptom is an active link that updates on the next unrelated interaction
    /// rather than on navigation.
    /// </remarks>
    protected override void OnInitialized() => Nav.LocationChanged += OnLocationChanged;

    public void Dispose() => Nav.LocationChanged -= OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => StateHasChanged();

    // The root link needs NavLinkMatch.All or it is active everywhere — the same
    // trap the framework's NavLink has, and the reason this line is spelled out
    // rather than hidden behind a default.
    private static NavLinkMatch MatchFor(CataloguePage page) =>
        page.Route == "/" ? NavLinkMatch.All : NavLinkMatch.Prefix;
}
