using System.Reflection;

namespace Sedna.UI;

/// <summary>
/// Renders the state illustrations — thirteen <c>&lt;symbol&gt;</c> drawings, one per
/// state — as a hidden <c>&lt;svg&gt;</c> in the page, so a <c>.state-art</c> can reference
/// one by id from anywhere in the document.
/// </summary>
/// <remarks>
/// <para>
/// Place it once, at the top of <c>&lt;body&gt;</c>:
/// <code>
/// &lt;body&gt;
///     &lt;SednaStateArt /&gt;
///     …
/// &lt;svg class="state-art" aria-hidden="true"&gt;&lt;use href="#no-access" /&gt;&lt;/svg&gt;
/// </code>
/// </para>
/// <para>
/// It is in the page rather than a file a <c>&lt;use&gt;</c> points at because an
/// external reference is rendered as a document of its own in WebKit: nothing the host
/// page sets reaches it — not <c>color</c>, not a custom property — so every line came
/// out black. A same-document reference inherits both in every engine, and it also
/// arrives in the first server-rendered paint, which matters most on the pages that
/// show when something is broken.
/// </para>
/// <para>
/// This is the second of the two components the root <c>CLAUDE.md</c> allows, for the
/// same reason as <see cref="SednaBrandStyle"/>: it emits infrastructure — a block of
/// definitions nothing renders directly — not markup a page reader needs to see or copy.
/// The markup they copy is the <c>&lt;svg class="state-art"&gt;</c> on their own page.
/// </para>
/// </remarks>
public partial class SednaStateArt
{
    /// <summary>The sprite, read from the assembly once and rendered verbatim.</summary>
    private static readonly Lazy<string> SpriteMarkup = new(() =>
    {
        var assembly = typeof(SednaStateArt).Assembly;
        using var stream = assembly.GetManifestResourceStream("Sedna.UI.states.svg")
                           ?? throw new InvalidOperationException(
                               "Sedna.UI.states.svg is not embedded in the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>The sprite's markup, for the component and for tests.</summary>
    public static string Sprite => SpriteMarkup.Value;
}
