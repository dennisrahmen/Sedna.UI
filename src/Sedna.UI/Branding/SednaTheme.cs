namespace Sedna.UI;

/// <summary>
/// A named theme — a palette for each variant, reachable at <c>[data-theme="&lt;name&gt;"]</c>.
/// </summary>
/// <remarks>
/// <b>Both variants are mandatory.</b> There is no constructor and no property setter that
/// takes only one: a user who switches <c>data-variant</c> to <c>light</c> must never land on
/// a theme with nothing declared for it, so a one-variant theme cannot be constructed at all,
/// rather than merely being discouraged. For Sedna itself the two are the same palette —
/// <c>docs/BRANDING.md</c> §4.1 says tier 1 is "not remapped by variant, colour-vision or
/// contrast", so Sedna's light and dark variants share one set of ramps by design, not by
/// omission. A theme whose brand needs different anchors in each variant supplies two
/// different <see cref="SednaPalette"/> instances; <see cref="SednaUiBrand.ToCss"/> currently
/// emits only <see cref="Dark"/> (see its remarks) — <see cref="Light"/> exists so that
/// omission is impossible to express in the type today, and so a variant-aware emitter has
/// data to read once one exists.
/// </remarks>
public sealed class SednaTheme
{
    /// <summary>The theme name, matched against <c>data-theme</c>.</summary>
    public string Name { get; }

    /// <summary>The palette used for <c>data-variant="dark"</c>.</summary>
    public SednaPalette Dark { get; }

    /// <summary>The palette used for <c>data-variant="light"</c>.</summary>
    public SednaPalette Light { get; }

    /// <summary>Builds a theme. Both <paramref name="dark"/> and <paramref name="light"/> are required.</summary>
    /// <param name="name">The theme name, matched against <c>data-theme</c>.</param>
    /// <param name="dark">The palette used for <c>data-variant="dark"</c>.</param>
    /// <param name="light">The palette used for <c>data-variant="light"</c>.</param>
    public SednaTheme(string name, SednaPalette dark, SednaPalette light)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Dark = dark ?? throw new ArgumentNullException(nameof(dark));
        Light = light ?? throw new ArgumentNullException(nameof(light));
    }

    /// <summary>
    /// The built-in theme, built from the literal values in <c>00-palette.css</c>.
    /// </summary>
    /// <remarks>
    /// These values are retyped here rather than read from the CSS part at run time: the part
    /// file lives under <c>css-parts/</c>, which is not a shipped asset — a consuming app's
    /// restored package does not have it on disk, so a branding service that needs to work in
    /// that app cannot depend on reading it. C# is the single source instead, and
    /// <c>SednaThemeTests.Sedna_matches_the_shipped_palette</c> parses <c>00-palette.css</c> at
    /// test time and asserts every step here agrees with it byte-for-byte, so the two cannot
    /// silently drift apart.
    /// </remarks>
    public static SednaTheme Sedna { get; } = BuildSedna();

    private static SednaTheme BuildSedna()
    {
        var palette = new SednaPalette(
            slate: Ramp(
                (50, "#f8fafc"), (100, "#eef4fb"), (200, "#dde7f3"), (300, "#c8d5e5"),
                (400, "#94a3b8"), (500, "#707e93"), (600, "#515e72"), (700, "#354255"),
                (750, "#2a3649"), (800, "#1e293b"), (850, "#162033"), (900, "#0f172a"),
                (925, "#0a1225"), (950, "#04071b")),
            coral: Ramp(
                (50, "#fff4f1"), (100, "#ffe9e3"), (200, "#ffd1c5"), (300, "#ffb4a0"),
                (400, "#ff8f74"), (500, "#ff6b4a"), (600, "#d73f1a"), (700, "#be2e06"),
                (800, "#a22000"), (900, "#7b1403"), (950, "#580000")),
            orbit: Ramp(
                (50, "#eff8ff"), (100, "#e0f1ff"), (200, "#bde3ff"), (300, "#90d2ff"),
                (400, "#59c3ff"), (500, "#3aa8e2"), (600, "#158fc6"), (700, "#0077a5"),
                (800, "#005e82"), (900, "#004661"), (950, "#002c3f")),
            navy: Ramp(
                (50, "#f4f7fc"), (100, "#e9eff9"), (200, "#d4def0"), (300, "#bacae7"),
                (400, "#9db4dd"), (500, "#809dd2"), (600, "#6484c2"), (700, "#4b6cab"),
                (800, "#35548f"), (900, "#17346e"), (950, "#0e2450")),
            green: Ramp(
                (200, "#a2f4b6"), (300, "#75e594"), (400, "#44d272"), (500, "#22c55e"),
                (600, "#00a043"), (700, "#008433"), (800, "#006924"), (900, "#004e16")),
            amber: Ramp(
                (200, "#ffd5a2"), (300, "#feba61"), (400, "#f09f24"), (500, "#f59e0b"),
                (600, "#b87300"), (700, "#995e00"), (800, "#7a4900"), (900, "#5d3400")),
            crimson: Ramp(
                (200, "#ffceda"), (300, "#ffafc4"), (400, "#ff87ab"), (500, "#f44588"),
                (600, "#d92b73"), (700, "#c72268"), (800, "#a60653"), (900, "#80003d")),
            violet: Ramp(
                (200, "#ded7ff"), (300, "#cbbfff"), (400, "#b6a2ff"), (500, "#a283ff"),
                (600, "#8e61f7"), (700, "#754bd3"), (800, "#5d37ad"), (900, "#452685")),
            cyan: Ramp(
                (200, "#9cecfc"), (300, "#6adcf1"), (400, "#22d3ee"), (500, "#00b0c8"),
                (600, "#0096aa"), (700, "#007b8d"), (800, "#00616f"), (900, "#004853")),
            orange: Ramp(
                (200, "#ffd4ae"), (300, "#feb979"), (400, "#f59e4b"), (500, "#da852e"),
                (600, "#bf6e10"), (700, "#a15800"), (800, "#814400"), (900, "#623000")),
            teal: Ramp(
                (200, "#a5eee1"), (300, "#79dfcf"), (400, "#47cbb9"), (500, "#14b8a6"),
                (600, "#009a8b"), (700, "#007f72"), (800, "#00645a"), (900, "#004b42")),
            indigo: Ramp(
                (200, "#d4dbff"), (300, "#bbc5ff"), (400, "#9fabff"), (500, "#8590fd"),
                (600, "#6f79e0"), (700, "#5a61bf"), (800, "#464b9b"), (900, "#323777")));

        // Dark and light are the SAME palette object: tier 1 is invariant across variant for
        // Sedna, by design (docs/BRANDING.md §4.1). See the type-level remarks above.
        return new SednaTheme("sedna", palette, palette);
    }

    private static SednaRamp Ramp(params (int Step, string Hex)[] entries) =>
        new(entries.ToDictionary(e => e.Step, e => e.Hex));
}
