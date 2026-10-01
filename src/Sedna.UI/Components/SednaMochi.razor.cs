namespace Sedna.UI;

/// <summary>
/// Renders Mochi, the agent's character — one rigged <c>&lt;symbol&gt;</c> — as a hidden
/// <c>&lt;svg&gt;</c> in the page, so a <c>.mochi</c> can reference it from anywhere in the document.
/// </summary>
/// <remarks>
/// <para>
/// Place it once, at the top of <c>&lt;body&gt;</c>, in an app that shows Mochi:
/// <code>
/// &lt;body&gt;
///     &lt;SednaStateArt /&gt;
///     &lt;SednaMochi /&gt;
///     …
/// &lt;span class="mochi" data-action="wave"&gt;&lt;svg viewBox="0 0 160 160" aria-hidden="true"&gt;&lt;use href="#sedna-mochi" /&gt;&lt;/svg&gt;&lt;/span&gt;
/// </code>
/// </para>
/// <para>
/// In the page for the reason <see cref="SednaStateArt"/> is: a same-document <c>&lt;use&gt;</c>
/// inherits the custom properties the rig is driven by in every engine, and an external one does
/// not in WebKit. The drawing is fixed and the library's — one character, not a set to pick
/// from — and everything about how it moves and what it shows is in <c>68-mochi.css</c>.
/// </para>
/// <para>
/// An infrastructure component, like <see cref="SednaStateArt"/>, in the sense of
/// <b>Markup belongs to the app</b> in the root <c>CLAUDE.md</c>: it emits definitions nothing
/// renders directly. The markup a page reader copies is the <c>&lt;span class="mochi"&gt;</c>.
/// </para>
/// </remarks>
public partial class SednaMochi
{
    /// <summary>The sprite, read from the assembly once and rendered verbatim.</summary>
    private static readonly Lazy<string> SpriteMarkup = new(() =>
    {
        var assembly = typeof(SednaMochi).Assembly;
        using var stream = assembly.GetManifestResourceStream("Sedna.UI.mochi.svg")
                           ?? throw new InvalidOperationException(
                               "Sedna.UI.mochi.svg is not embedded in the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>The sprite's markup, for the component and for tests.</summary>
    public static string Sprite => SpriteMarkup.Value;
}
