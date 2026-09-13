using Microsoft.AspNetCore.Components;

namespace Sedna.UI;

/// <summary>
/// Renders the app components <see cref="ISednaOverlays"/> is presenting.
/// </summary>
/// <remarks>
/// <para>
/// Place it once, in the layout, inside the interactive render tree:
/// <code>
/// &lt;div class="layout"&gt;
///     …
/// &lt;/div&gt;
/// &lt;SednaOverlayHost /&gt;
/// </code>
/// </para>
/// <para>
/// A presenter, in the root <c>CLAUDE.md</c>'s sense: it renders no element of its own, only
/// each app component in turn, and every <c>&lt;dialog&gt;</c> on screen is the one that
/// component wrote. Where it sits in the layout does not matter for stacking — an open modal
/// dialog is in the top layer, which ignores the DOM position entirely.
/// </para>
/// <para>
/// A second host in the same circuit renders nothing, so a layout nested in another cannot
/// show every overlay twice.
/// </para>
/// </remarks>
public partial class SednaOverlayHost
{
    private bool _attached;

    [Inject] private ISednaOverlays Overlays { get; set; } = default!;

    private SednaOverlays Service => (SednaOverlays)Overlays;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // A replaced ISednaOverlays — a test double — has no list to render.
        if (Overlays is not SednaOverlays service) return;

        _attached = service.Attach(this);
        if (_attached) service.Changed += OnChanged;
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (!_attached) return;
        foreach (var entry in Service.Open) SednaOverlays.MarkRendered(entry);
    }

    private Task OnChanged() => InvokeAsync(StateHasChanged);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_attached) return;
        Service.Changed -= OnChanged;
        Service.Detach(this);
        _attached = false;
    }
}
