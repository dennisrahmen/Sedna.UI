namespace Sedna.UI;

/// <summary>
/// Sedna's own slate ramp, as the literal values in <c>00-palette.css</c>.
/// </summary>
/// <remarks>
/// <see cref="SednaTheme.Sedna"/> builds its base from it and <see cref="SednaRamp.Surface"/>
/// measures its profile from it. <see cref="SednaRamp"/> must read it here, never through
/// <see cref="SednaTheme"/>: the built-in themes call <see cref="SednaRamp.Surface"/> from
/// <see cref="SednaTheme"/>'s static initialiser, so that read would be a cycle, and an app whose
/// first call is <c>Surface</c> would get a <see cref="TypeInitializationException"/>.
/// <c>SurfaceRampTests</c> calls it first in a fresh load of the assembly.
/// <c>SednaThemeTests.Sedna_matches_the_shipped_palette</c> holds these values against the
/// stylesheet through <see cref="SednaTheme.Sedna"/>.
/// </remarks>
internal static class SednaSlate
{
    public static IReadOnlyDictionary<int, string> Steps { get; } = new Dictionary<int, string>
    {
        [50] = "#f8fafc", [100] = "#eef4fb", [200] = "#dde7f3", [300] = "#c8d5e5",
        [400] = "#94a3b8", [500] = "#707e93", [600] = "#515e72", [700] = "#354255",
        [750] = "#2a3649", [800] = "#1e293b", [850] = "#162033", [900] = "#0f172a",
        [925] = "#0a1225", [950] = "#04071b",
    };
}
