using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sedna.UI.Catalogue.Theming;

/// <summary>One contrast or hue check on a generated theme.</summary>
/// <param name="Pair">What is measured, in the reader's words.</param>
/// <param name="Measured">The ratio, or the hue distance in degrees.</param>
/// <param name="Floor">What it has to reach.</param>
/// <param name="Unit"><c>:1</c> for a contrast ratio, <c>°</c> for a hue distance.</param>
/// <param name="Fix">What to change when it fails.</param>
internal sealed record ThemeCheck(string Pair, double Measured, double Floor, string Unit, string Fix)
{
    public bool Passes => Measured >= Floor;

    public string MeasuredText => Unit == "°"
        ? Measured.ToString("0", CultureInfo.InvariantCulture) + "°"
        : Measured.ToString("0.00", CultureInfo.InvariantCulture) + ":1";

    public string FloorText => Unit == "°"
        ? Floor.ToString("0", CultureInfo.InvariantCulture) + "°"
        : Floor.ToString("0.0", CultureInfo.InvariantCulture) + ":1";
}

/// <summary>What the theme builder generates for one choice of colours.</summary>
/// <param name="Brand">The brand colour, as <c>#rrggbb</c>.</param>
/// <param name="Accent">The accent colour, or null when the accent is left as it is.</param>
/// <param name="Css">The <c>brand.css</c> an app pastes: palette tokens on <c>:root</c>.</param>
/// <param name="Tokens">Every token the CSS sets, with its value, in the order it sets them.</param>
/// <param name="Checks">The library's contrast and hue rules, measured on the result.</param>
/// <param name="Problem">Why a colour was not used, or null.</param>
internal sealed record ThemeResult(
    string Brand,
    string? Accent,
    string Css,
    IReadOnlyList<KeyValuePair<string, string>> Tokens,
    IReadOnlyList<ThemeCheck> Checks,
    string? Problem);

/// <summary>
/// Builds a <c>brand.css</c> from a brand colour and, optionally, an accent: the
/// <c>coral</c> ramp — which <c>--brand</c> and its family point at — and the <c>orbit</c>
/// ramp — <c>--accent</c> and <c>--info-*</c> — generated the way the built-in themes generate
/// theirs (<c>docs/BRANDING.md</c> §4).
/// </summary>
/// <remarks>
/// Palette tokens only. The semantic tier already points at the palette in both variants, so
/// a redefined ramp re-themes every brand role, tint, ring and glow, in dark and light alike.
/// Sedna Red and Orbit Blue give Sedna's own hand-tuned ramps rather than a generated copy.
/// </remarks>
internal static class BrandCssBuilder
{
    public const string SednaRed = "#ff6b4a";
    public const string OrbitBlue = "#59c3ff";

    private static readonly Regex Hex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    /// <summary>The white-text floor the brand step is solved for, as the built-in themes do.</summary>
    private const double BrandFloor = 4.6;

    private static SednaPalette Sedna => SednaTheme.Sedna.Palette;

    public static ThemeResult Build(string? brand, string? accent)
    {
        string? problem = null;
        var brandHex = Normalise(brand);
        if (brandHex is null)
        {
            problem = $"\"{brand}\" is not a colour of the form #rrggbb, so Sedna Red is used. Pick the colour again.";
            brandHex = SednaRed;
        }

        var accentHex = accent is null ? null : Normalise(accent);
        if (accent is not null && accentHex is null)
        {
            problem ??= $"\"{accent}\" is not a colour of the form #rrggbb, so the accent is left as it is. Pick it again.";
        }

        SednaRamp coral;
        try
        {
            coral = brandHex == SednaRed
                ? Sedna.Coral
                : SednaRamp.FromAnchor(brandHex, 500,
                    contrastSolvedStep: new ContrastSolvedStep(600, "#ffffff", BrandFloor));
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            problem ??= $"No ramp at {brandHex}'s hue carries white text at {BrandFloor}:1, so Sedna Red is used. Pick a more saturated colour.";
            brandHex = SednaRed;
            coral = Sedna.Coral;
        }

        var orbit = accentHex is null ? Sedna.Orbit
            : accentHex == OrbitBlue ? Sedna.Orbit
            : SednaRamp.FromAnchor(accentHex, 400);

        var tokens = new List<KeyValuePair<string, string>>();
        foreach (var step in coral.Steps)
            tokens.Add(new("--coral-" + step.ToString(CultureInfo.InvariantCulture), coral[step]));
        if (accentHex is not null)
            foreach (var step in orbit.Steps)
                tokens.Add(new("--orbit-" + step.ToString(CultureInfo.InvariantCulture), orbit[step]));

        return new ThemeResult(brandHex, accentHex, Css(brandHex, accentHex, tokens), tokens,
            Checks(coral, orbit), problem);
    }

    private static string? Normalise(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is not null && Hex.IsMatch(trimmed) ? trimmed.ToLowerInvariant() : null;
    }

    private static string Css(string brand, string? accent, List<KeyValuePair<string, string>> tokens)
    {
        var css = new StringBuilder();
        css.Append("/* brand.css: the Sedna.UI theme builder, brand ").Append(brand);
        if (accent is not null) css.Append(", accent ").Append(accent);
        css.Append(".\n   Load it after Sedna.UI.css. */\n:root {\n");
        foreach (var (name, value) in tokens)
            css.Append("    ").Append(name).Append(": ").Append(value).Append(";\n");
        css.Append("}\n");
        return css.ToString();
    }

    /// <summary>The rules in <c>docs/BRANDING.md</c> §7.1 and §8 a new brand or accent can break.</summary>
    private static List<ThemeCheck> Checks(SednaRamp coral, SednaRamp orbit)
    {
        var darkCanvas = Sedna.Slate[900];
        var lightCanvas = Sedna.Slate[50];
        var darkCard = Sedna.Slate[800];
        var checks = new List<ThemeCheck>
        {
            new("White text on a filled brand button", Colour.Contrast("#ffffff", coral[600]), 4.5, ":1",
                "Choose a darker or more saturated brand colour."),
            new("Brand text on the dark canvas", Colour.Contrast(coral[400], darkCanvas), 4.5, ":1",
                "Choose a lighter brand colour."),
            new("Brand text on the light canvas", Colour.Contrast(coral[800], lightCanvas), 4.5, ":1",
                "Choose a darker brand colour."),
            new("A filled brand button's edge on a dark card", Colour.Contrast(coral[500], darkCard), 3.0, ":1",
                "Choose a lighter brand colour."),
            new("Links on the dark canvas", Colour.Contrast(orbit[400], darkCanvas), 4.5, ":1",
                "Choose a lighter accent."),
            new("Links on the light canvas", Colour.Contrast(orbit[700], lightCanvas), 4.5, ":1",
                "Choose a darker accent."),
        };

        // §3.3: a brand within ~15° of a semantic family reads as that family.
        var brandHue = Colour.Hue(coral[600]);
        if (brandHue is { } hue)
        {
            foreach (var (family, hex) in new[]
                     {
                         ("danger", Sedna.Crimson[700]), ("go", Sedna.Green[700]),
                         ("warn", Sedna.Amber[700]), ("info and links", orbit[700]),
                     })
            {
                if (Colour.Hue(hex) is not { } other) continue;
                checks.Add(new($"Brand hue apart from {family}", Colour.HueDistance(hue, other), 15, "°",
                    $"Choose a brand further from {family}'s hue, or move the {family} family as docs/BRANDING.md §3.3 says."));
            }
        }

        return checks;
    }
}

/// <summary>WCAG contrast and OKLCH hue of a <c>#rrggbb</c> colour.</summary>
internal static class Colour
{
    public static double Contrast(string a, string b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static double Luminance(string hex)
    {
        var (r, g, b) = Linear(hex);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>The OKLCH hue in degrees, or null for a colour too grey to have one.</summary>
    public static double? Hue(string hex)
    {
        var (r, g, b) = Linear(hex);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        var labA = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
        var labB = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;
        if (Math.Sqrt(labA * labA + labB * labB) < 0.03) return null;
        var degrees = Math.Atan2(labB, labA) * (180 / Math.PI);
        return degrees < 0 ? degrees + 360 : degrees;
    }

    public static double HueDistance(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    private static (double R, double G, double B) Linear(string hex)
    {
        static double Channel(string hex, int at)
        {
            var c = int.Parse(hex.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (Channel(hex, 1), Channel(hex, 3), Channel(hex, 5));
    }
}
