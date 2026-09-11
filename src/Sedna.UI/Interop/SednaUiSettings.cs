using System.Text.Json.Serialization;

namespace Sedna.UI;

/// <summary>
/// The accessibility and appearance settings the browser is currently applying.
/// </summary>
/// <remarks>
/// Read with <see cref="ISednaUi.LoadSettingsAsync"/>. These live in
/// <c>localStorage</c> and are applied to <c>&lt;html&gt;</c> before first paint by
/// <c>Sedna.UI.boot.js</c>, so the server has no way to know them until the
/// circuit is connected.
/// </remarks>
public sealed record SednaUiSettings
{
    /// <summary>
    /// The two-letter language code, falling back to the browser's own when
    /// nothing is stored.
    /// </summary>
    [JsonPropertyName("lang")]
    public string Language { get; init; } = "en";

    /// <summary>
    /// Which theme is active, by name — falling back to
    /// <see cref="SednaUiOptions.Default"/> with nothing stored. Never absent.
    /// </summary>
    /// <remarks>
    /// This used to be <c>"dark"</c> or <c>"light"</c>; that concept moved to
    /// <see cref="Variant"/>. <c>data-theme</c> and <see cref="Theme"/> are now
    /// orthogonal to it — which design, not which variant of it.
    /// </remarks>
    [JsonPropertyName("theme")]
    public string Theme { get; init; } = "sedna";

    /// <summary>
    /// <c>"dark"</c>, <c>"light"</c>, or <c>"system"</c>. Never absent: apps brand
    /// the light palette with <c>:root[data-variant="light"]</c>, so that selector
    /// has to match whenever the light palette is in use.
    /// </summary>
    /// <remarks>
    /// <c>"system"</c> is a real, storable preference, not collapsed to
    /// <c>dark</c>/<c>light</c> on read — it is what lets a settings UI show
    /// "follow system" as selected. The attribute actually applied to
    /// <c>&lt;html data-variant&gt;</c> is always the resolved <c>dark</c> or
    /// <c>light</c>; <c>"system"</c> only ever appears here, in the stored
    /// preference.
    /// </remarks>
    [JsonPropertyName("variant")]
    public string Variant { get; init; } = "dark";

    /// <summary>
    /// Whether the colour-blind palette is on, which moves the <c>go</c> family to
    /// blue so go and danger separate.
    /// </summary>
    [JsonPropertyName("cvd")]
    public bool ColourBlind { get; init; }

    /// <summary>Whether compact density is on, which tightens table and cell padding.</summary>
    [JsonPropertyName("compact")]
    public bool Compact { get; init; }

    /// <summary>
    /// <c>"ltr"</c> or <c>"rtl"</c> — the document's writing direction.
    /// </summary>
    /// <remarks>
    /// Unlike the theme, this is only written to <c>&lt;html dir&gt;</c> once a
    /// choice has been stored: it is an attribute the host page declares for itself,
    /// so an app whose document is RTL by default keeps saying so in its own markup.
    /// With nothing stored, this reports what the document already says.
    /// </remarks>
    [JsonPropertyName("dir")]
    public string Direction { get; init; } = "ltr";
}
