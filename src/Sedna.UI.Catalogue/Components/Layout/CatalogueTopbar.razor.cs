using Sedna.UI.Catalogue.Navigation;
using Microsoft.AspNetCore.Components;

namespace Sedna.UI.Catalogue.Components.Layout;

public partial class CatalogueTopbar : ComponentBase
{
    private bool _light;
    private bool _colourBlind;
    private bool _compact;
    private bool _rtl;

    [Parameter] public bool NavOpen { get; set; }
    [Parameter] public EventCallback<bool> NavOpenChanged { get; set; }

    [Parameter] public bool Collapsed { get; set; }
    [Parameter] public EventCallback<bool> CollapsedChanged { get; set; }

    [Inject] private ISednaUi Ui { get; set; } = default!;
    [Inject] private ThemeState Theme { get; set; } = default!;

    private static string Pressed(bool on) => on ? "true" : "false";

    /// <summary>
    /// Reads the stored settings once the circuit is up.
    /// </summary>
    /// <remarks>
    /// Not in <c>OnInitializedAsync</c>: an interop call cannot run during
    /// prerendering, and this is the worked example of that rule. Until it runs
    /// the buttons show the defaults, while <c>Sedna.UI.boot.js</c> has
    /// already applied the real settings to <c>&lt;html&gt;</c> — so the page is
    /// correct before the toggles are.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        var settings = await Ui.LoadSettingsAsync();
        _light = settings.Theme == "light";
        _colourBlind = settings.ColourBlind;
        _compact = settings.Compact;
        _rtl = settings.Direction == "rtl";
        StateHasChanged();
    }

    private Task ToggleTheme()
    {
        _light = !_light;
        return Save("theme", _light ? "light" : "dark");
    }

    private Task ToggleColourBlind()
    {
        _colourBlind = !_colourBlind;
        return Save("cvd", _colourBlind ? "1" : "0");
    }

    private Task ToggleDensity()
    {
        _compact = !_compact;
        return Save("density", _compact ? "compact" : "comfortable");
    }

    // The whole point of shipping 70-rtl.css is that a mirrored document is one
    // attribute away, so the site that documents it has to be able to prove that on
    // any page rather than describe it.
    private Task ToggleDirection()
    {
        _rtl = !_rtl;
        return Save("dir", _rtl ? "rtl" : "ltr");
    }

    private async Task Save(string key, string value)
    {
        // settings.save stores the choice under the sedna. prefix and stamps <html>,
        // which is what remaps the tokens. Nothing here touches data-* directly.
        await Ui.SaveSettingAsync(key, value);

        // The Tokens page shows computed values, which have just changed.
        Theme.NotifyChanged();
    }
}
