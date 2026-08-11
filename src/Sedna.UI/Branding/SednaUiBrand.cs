using System.Text;

namespace Sedna.UI;

/// <summary>
/// Turns registered themes into the palette CSS <see cref="SednaBrandStyle"/> writes
/// into <c>&lt;head&gt;</c>.
/// </summary>
public static class SednaUiBrand
{
    /// <summary>
    /// The palette CSS for every theme in <paramref name="options"/> — <c>:root { … }</c> for
    /// <see cref="SednaUiOptions.Default"/>, then <c>[data-theme="&lt;name&gt;"] { … }</c> for
    /// each other registered theme.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Palette tokens only.</b> The semantic tier (<c>--brand</c>, <c>--bg</c>, and the rest
    /// of the ~160 roles) already ships in the stylesheet and already points at the palette, so
    /// a theme that changes only its anchors needs no semantic block at all — this method never
    /// emits one. The two absolutes, <c>--white</c> and <c>--black</c>, are emitted once, in the
    /// <c>:root</c> block, because they do not vary by theme.
    /// </para>
    /// <para>
    /// <b>One block per theme, not per theme×variant.</b> A theme has one
    /// <see cref="SednaTheme.Palette"/> and it serves both variants, because tier 1 is never
    /// remapped by variant — <c>docs/BRANDING.md</c> §4.1, enforced by
    /// <c>TokenTierTests.The_palette_is_declared_once_and_never_remapped_by_a_variant</c>. The
    /// dark/light difference is a tier-2 concern and already ships in the stylesheet: the light
    /// block simply reaches for different steps of the same ramps.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// No themes are registered, or <see cref="SednaUiOptions.Default"/> does not name one of them.
    /// </exception>
    public static string ToCss(SednaUiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var themes = options.Themes;
        if (themes.Count == 0)
            throw new InvalidOperationException(
                "SednaUiOptions.Themes has no themes registered. At minimum, register SednaTheme.Sedna.");

        var defaultTheme = themes.FirstOrDefault(t => t.Name == options.Default)
            ?? throw new InvalidOperationException(
                $"SednaUiOptions.Default is \"{options.Default}\", which does not match any " +
                $"registered theme name ({string.Join(", ", themes.Select(t => t.Name))}).");

        var css = new StringBuilder();

        css.Append(":root{");
        AppendAbsolutes(css);
        AppendPalette(css, defaultTheme.Palette);
        css.Append('}');

        foreach (var theme in themes)
        {
            if (theme.Name == options.Default) continue;

            css.Append("[data-theme=\"").Append(theme.Name).Append("\"]{");
            AppendPalette(css, theme.Palette);
            css.Append('}');
        }

        return css.ToString();
    }

    private static void AppendAbsolutes(StringBuilder css)
    {
        css.Append("--white:#ffffff;--black:#000000;");
    }

    private static void AppendPalette(StringBuilder css, SednaPalette palette)
    {
        foreach (var (family, ramp) in palette.Ramps())
            foreach (var step in ramp.Steps)
                css.Append("--").Append(family).Append('-').Append(step).Append(':').Append(ramp[step]).Append(';');
    }
}
