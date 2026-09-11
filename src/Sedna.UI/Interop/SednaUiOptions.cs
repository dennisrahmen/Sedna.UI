namespace Sedna.UI;

/// <summary>
/// The options <c>sednaUi.configure()</c> accepts, in C# form.
/// </summary>
/// <remarks>
/// Set these through <c>AddSednaUi</c>. They are pushed to the browser by
/// <see cref="ISednaUi.ConfigureAsync"/>, which an app calls once from
/// <c>OnAfterRenderAsync(firstRender)</c> — an interop call cannot run during
/// prerendering.
/// </remarks>
public sealed class SednaUiOptions
{
    /// <summary>
    /// The <c>localStorage</c> key prefix. Defaults to <c>sedna.</c>.
    /// </summary>
    /// <remarks>
    /// <c>localStorage</c> is origin-scoped, so apps on separate domains cannot
    /// collide and this needs no changing. Override it only when two apps share one
    /// origin under different paths — and then set the same value in
    /// <c>data-prefix</c> on the boot script, or the theme is not found on reload.
    /// </remarks>
    public string StoragePrefix { get; set; } = "sedna.";

    /// <summary>
    /// Icon for desktop notifications, as a URL. Null uses the browser's default.
    /// </summary>
    public string? NotifyIcon { get; set; }

    /// <summary>
    /// Also mirror the language into a <c>&lt;prefix&gt;lang</c> cookie, so a
    /// server-rendered app can prerender in the chosen language. Cookies are not
    /// origin-scoped the way <c>localStorage</c> is, so the prefix matters here
    /// even when it does not elsewhere.
    /// </summary>
    public bool LangCookie { get; set; }

    /// <summary>
    /// The registered themes. <see cref="SednaBrandStyle"/> and <see cref="SednaUiBrand.ToCss"/>
    /// emit palette CSS for every one of these, reachable at <c>[data-theme="&lt;name&gt;"]</c>.
    /// </summary>
    /// <remarks>
    /// Defaults to just <see cref="SednaTheme.Sedna"/>. A theme name appearing more than once is
    /// not rejected here; <see cref="SednaUiBrand.ToCss"/> matches <see cref="Default"/> against
    /// the first one found.
    /// </remarks>
    public IReadOnlyList<SednaTheme> Themes { get; set; } = [SednaTheme.Sedna];

    /// <summary>
    /// Which registered theme's palette is emitted at bare <c>:root</c>, so it applies with no
    /// <c>data-theme</c> attribute present. Must name a theme in <see cref="Themes"/>.
    /// </summary>
    /// <remarks>
    /// This is also the name the browser stamps on <c>&lt;html data-theme&gt;</c> when nothing is
    /// stored, so it has to reach the two scripts as well:
    /// <see cref="ISednaUi.ConfigureAsync"/> pushes it to <c>Sedna.UI.js</c>, and the boot
    /// script takes it from <c>data-theme-default</c> on its own tag — render that from here
    /// rather than retyping it. Left at <c>sedna</c> while this is an app's own theme, the
    /// first visit — the one with nothing stored — is stamped with a name that selects a
    /// different registered palette, or one no theme answers to.
    /// </remarks>
    public string Default { get; set; } = "sedna";
}
