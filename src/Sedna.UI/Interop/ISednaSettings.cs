namespace Sedna.UI;

/// <summary>
/// The applied appearance settings, as state a component can bind to — which theme, which
/// variant, compact density, the colour-vision palette, language and direction — and an event
/// that fires when any of them changes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISednaUi.LoadSettingsAsync"/> answers "what are they now"; this answers "what are
/// they, and tell me when that changes". The difference matters for one case that has no other
/// answer: a reader whose stored preference is <c>"system"</c> and who switches their OS from
/// light to dark while the page is open. The library follows that without a reload, in
/// JavaScript, so an app polling <c>LoadSettingsAsync</c> after first render never hears about it
/// and every computed value it rendered is quietly stale.
/// </para>
/// <para>
/// <b>This service never writes the document.</b> The attributes on <c>&lt;html&gt;</c> are
/// written by <c>Sedna.UI.boot.js</c> before first paint and by <c>sednaUi.settings.apply()</c>
/// afterwards, and that stays the one writer; the setters here store a preference and let the
/// notification come back. Two writers would be two answers to "which variant is showing".
/// </para>
/// <para>
/// Scoped, like <see cref="ISednaUi"/>: one scope is one circuit is one browser.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// @inject ISednaSettings Settings
/// @implements IDisposable
///
/// @code {
///     protected override void OnInitialized() => Settings.Changed += Refresh;
///     protected override async Task OnAfterRenderAsync(bool firstRender)
///     {
///         if (firstRender) await Settings.StartAsync();
///     }
///     private void Refresh(SednaUiSettings s) => InvokeAsync(StateHasChanged);
///     public void Dispose() => Settings.Changed -= Refresh;
/// }
/// </code>
/// </example>
public interface ISednaSettings
{
    /// <summary>
    /// What is applied now — the library's defaults until <see cref="StartAsync"/> has run,
    /// because the server cannot know a browser's <c>localStorage</c> before the circuit is up.
    /// </summary>
    SednaUiSettings Current { get; }

    /// <summary>
    /// Whether <see cref="Current"/> has been read from the browser yet, as opposed to holding
    /// the defaults.
    /// </summary>
    /// <remarks>
    /// Worth checking before rendering a settings UI: a toggle drawn from the defaults during
    /// prerendering shows "dark" to a reader who chose light, then corrects itself, which reads
    /// as a flicker rather than as a load.
    /// </remarks>
    bool IsLive { get; }

    /// <summary>Raised whenever the applied settings change, with the new values.</summary>
    /// <remarks>
    /// Raised on the circuit's thread from a JavaScript callback, so a component handler must
    /// marshal with <c>InvokeAsync(StateHasChanged)</c> rather than calling
    /// <c>StateHasChanged</c> directly. Unsubscribe in <c>Dispose</c>; the event outlives a
    /// component within its circuit.
    /// </remarks>
    event Action<SednaUiSettings>? Changed;

    /// <summary>
    /// Reads the current settings from the browser and starts listening for changes. Call once,
    /// from <c>OnAfterRenderAsync(firstRender: true)</c>.
    /// </summary>
    /// <remarks>
    /// Interop cannot run during prerendering, which is why this is not done in the constructor.
    /// Calling it again is harmless and does not add a second listener.
    /// </remarks>
    Task StartAsync();

    /// <summary>Switches theme by name, e.g. <c>"sedna"</c>.</summary>
    /// <param name="theme">A registered theme's name. Unregistered names apply and simply find no palette.</param>
    Task SetThemeAsync(string theme);

    /// <summary>Switches variant.</summary>
    /// <param name="variant">
    /// <c>"dark"</c>, <c>"light"</c> or <c>"system"</c>. <c>"system"</c> is stored as itself, so
    /// the choice survives the reader moving between OS settings.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="variant"/> is none of the three.</exception>
    Task SetVariantAsync(string variant);

    /// <summary>Turns compact density on or off.</summary>
    Task SetCompactAsync(bool compact);

    /// <summary>Turns the colour-vision-deficiency palette on or off.</summary>
    Task SetColourBlindAsync(bool colourBlind);

    /// <summary>Sets the document language, e.g. <c>"de"</c>.</summary>
    /// <remarks>
    /// Also written as a cookie when <see cref="SednaUiOptions.LangCookie"/> is set, which is how
    /// a server-rendered app reads the choice on the next request.
    /// </remarks>
    Task SetLanguageAsync(string language);

    /// <summary>Sets the document direction.</summary>
    /// <param name="direction"><c>"ltr"</c> or <c>"rtl"</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="direction"/> is neither.</exception>
    Task SetDirectionAsync(string direction);
}
