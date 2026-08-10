using System.Globalization;

namespace Sedna.UI;

/// <summary>
/// sRGB ⇄ OKLCH, the colour space <c>docs/BRANDING.md</c> §2.1 says every ramp was generated
/// in. Implemented directly rather than taken from a package — see the root
/// <c>CLAUDE.md</c>: the library takes no third-party dependency, and this is about thirty
/// lines either way.
/// </summary>
/// <remarks>
/// The forward and inverse matrices are Björn Ottosson's published OKLab constants
/// (<see href="https://bottosson.github.io/posts/oklab/"/>). OKLCH is OKLab in polar form:
/// same <c>L</c>, with <c>a</c>/<c>b</c> read as chroma and hue.
/// </remarks>
internal static class Oklch
{
    /// <summary>Converts a <c>#rrggbb</c> sRGB hex colour to OKLCH (L 0–1, C, H degrees).</summary>
    public static (double L, double C, double H) FromHex(string hex)
    {
        var (r, g, b) = ParseHex(hex);

        var rl = ToLinear(r);
        var gl = ToLinear(g);
        var bl = ToLinear(b);

        // linear sRGB -> LMS
        var l = 0.4122214708 * rl + 0.5363325363 * gl + 0.0514459929 * bl;
        var m = 0.2119034982 * rl + 0.6806995451 * gl + 0.1073969566 * bl;
        var s = 0.0883024619 * rl + 0.2817188376 * gl + 0.6299787005 * bl;

        var l_ = Math.Cbrt(l);
        var m_ = Math.Cbrt(m);
        var s_ = Math.Cbrt(s);

        // LMS' -> OKLab
        var lab_L = 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_;
        var lab_a = 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_;
        var lab_b = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_;

        var c = Math.Sqrt(lab_a * lab_a + lab_b * lab_b);
        var h = Math.Atan2(lab_b, lab_a) * (180 / Math.PI);
        if (h < 0) h += 360;

        return (lab_L, c, h);
    }

    /// <summary>Converts OKLCH back to a <c>#rrggbb</c> sRGB hex colour, clamped to gamut.</summary>
    public static string ToHex(double l, double c, double h)
    {
        var hRad = h * (Math.PI / 180);
        var lab_a = c * Math.Cos(hRad);
        var lab_b = c * Math.Sin(hRad);

        // OKLab -> LMS'
        var l_ = l + 0.3963377774 * lab_a + 0.2158037573 * lab_b;
        var m_ = l - 0.1055613458 * lab_a - 0.0638541728 * lab_b;
        var s_ = l - 0.0894841775 * lab_a - 1.2914855480 * lab_b;

        var lCube = l_ * l_ * l_;
        var mCube = m_ * m_ * m_;
        var sCube = s_ * s_ * s_;

        // LMS -> linear sRGB
        var rl = +4.0767416621 * lCube - 3.3077115913 * mCube + 0.2309699292 * sCube;
        var gl = -1.2684380046 * lCube + 2.6097574011 * mCube - 0.3413193965 * sCube;
        var bl = -0.0041960863 * lCube - 0.7034186147 * mCube + 1.7076147010 * sCube;

        // Out-of-gamut values are clamped rather than gamut-mapped: a generated ramp step
        // that overshoots sRGB (most reachable near L=1, where the gamut narrows sharply)
        // ends up at the nearest in-gamut colour instead of failing to render.
        return FormatHex(ToSrgb(rl), ToSrgb(gl), ToSrgb(bl));
    }

    private static (double R, double G, double B) ParseHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        var span = hex.AsSpan().TrimStart('#');
        if (span.Length != 6)
            throw new ArgumentException($"\"{hex}\" is not a #rrggbb colour.", nameof(hex));

        return (
            int.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0,
            int.Parse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0,
            int.Parse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0);
    }

    private static string FormatHex(double r, double g, double b) =>
        $"#{ToByte(r):x2}{ToByte(g):x2}{ToByte(b):x2}";

    private static int ToByte(double channel01) =>
        (int)Math.Round(Math.Clamp(channel01, 0, 1) * 255, MidpointRounding.AwayFromZero);

    private static double ToLinear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static double ToSrgb(double linear)
    {
        var c = Math.Clamp(linear, 0, 1);
        return c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
    }
}
