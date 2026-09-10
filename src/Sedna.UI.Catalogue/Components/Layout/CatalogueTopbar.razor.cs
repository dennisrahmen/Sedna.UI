using Microsoft.AspNetCore.Components;

namespace Sedna.UI.Catalogue.Components.Layout;

/// <summary>
/// The four appearance toggles, driven by <see cref="ISednaSettings"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every toggle reads <see cref="ISednaSettings.Current"/> rather than a field of its
/// own, so there is one answer to "what is applied" on the page. A component that
/// kept its own copy went stale the moment anything else changed a setting — the
/// theme control on <c>/branding</c> did exactly that, and the two contradicted each
/// other until a reload. A reader whose choice is <c>"system"</c> and who switches
/// their OS while the page is open is the same bug with no C# involved at all.
/// </para>
/// <para>
/// This is also the worked example of the interface: subscribe in
/// <c>OnInitialized</c>, <c>StartAsync</c> once the circuit is up because interop
/// cannot run during prerendering, marshal the handler with <c>InvokeAsync</c>, and
/// unsubscribe in <c>Dispose</c>.
/// </para>
/// </remarks>
public partial class CatalogueTopbar : ComponentBase, IDisposable
{
    [Parameter] public bool NavOpen { get; set; }
    [Parameter] public EventCallback<bool> NavOpenChanged { get; set; }

    [Parameter] public bool Collapsed { get; set; }
    [Parameter] public EventCallback<bool> CollapsedChanged { get; set; }

    [Inject] private ISednaSettings Settings { get; set; } = default!;

    // "system" resolves to one of the two in the document, but the stored choice is
    // still "system" — and this control has no third state, so it shows what the
    // reader is actually looking at.
    private bool Light => Settings.Current.Variant == "light";
    private bool ColourBlind => Settings.Current.ColourBlind;
    private bool Compact => Settings.Current.Compact;
    private bool Rtl => Settings.Current.Direction == "rtl";

    private static string Pressed(bool on) => on ? "true" : "false";

    protected override void OnInitialized() => Settings.Changed += Apply;

    /// <summary>
    /// Reads the stored settings once the circuit is up.
    /// </summary>
    /// <remarks>
    /// Not in <c>OnInitializedAsync</c>: an interop call cannot run during
    /// prerendering, and this is the worked example of that rule. Until it runs the
    /// buttons show the library's defaults, while <c>Sedna.UI.boot.js</c> has
    /// already applied the real settings to <c>&lt;html&gt;</c> — so the page is
    /// correct before the toggles are.
    /// </remarks>
    protected override Task OnAfterRenderAsync(bool firstRender) =>
        firstRender ? Settings.StartAsync() : Task.CompletedTask;

    // Raised from a JavaScript callback, so it has to be marshalled onto the
    // circuit's thread rather than calling StateHasChanged directly.
    private void Apply(SednaUiSettings applied) => InvokeAsync(StateHasChanged);

    private Task ToggleTheme() => Settings.SetVariantAsync(Light ? "dark" : "light");

    private Task ToggleColourBlind() => Settings.SetColourBlindAsync(!ColourBlind);

    private Task ToggleDensity() => Settings.SetCompactAsync(!Compact);

    // The whole point of shipping 70-rtl.css is that a mirrored document is one
    // attribute away, so the site that documents it has to be able to prove that on
    // any page rather than describe it.
    private Task ToggleDirection() => Settings.SetDirectionAsync(Rtl ? "ltr" : "rtl");

    public void Dispose() => Settings.Changed -= Apply;
}
