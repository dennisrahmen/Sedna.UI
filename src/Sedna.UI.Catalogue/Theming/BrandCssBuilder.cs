using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sedna.UI.Catalogue.Theming;

/// <summary>The ramps the quick start generates from up to three colours.</summary>
/// <param name="Brand">The brand colour, as <c>#rrggbb</c>.</param>
/// <param name="Accent">The accent colour, or null when the accent is left as it is.</param>
/// <param name="Canvas">The dark canvas colour, or null when the base is left as it is.</param>
/// <param name="Tokens">Every palette step the ramps hold, in ramp order.</param>
/// <param name="Problem">Why a colour was not used, or null.</param>
internal sealed record QuickStart(
    string Brand,
    string? Accent,
    string? Canvas,
    IReadOnlyList<KeyValuePair<string, string>> Tokens,
    string? Problem);

/// <summary>What the theme builder generates.</summary>
/// <param name="Css">The <c>brand.css</c> an app pastes.</param>
/// <param name="Root">What it sets at bare <c>:root</c>, in declaration order.</param>
/// <param name="Light">What it sets for the light variant.</param>
/// <param name="Dark">Every token's value in the dark variant, the library's with the CSS over it.</param>
/// <param name="LightValues">The same for the light variant.</param>
/// <param name="DarkColours">Those values that read as a colour, resolved.</param>
/// <param name="LightColours">The same for the light variant.</param>
/// <param name="Checks">The contrast pairs, measured in both variants.</param>
/// <param name="HueChecks">The brand's hue against each semantic family's.</param>
internal sealed record ThemeResult(
    string Css,
    IReadOnlyList<KeyValuePair<string, string>> Root,
    IReadOnlyList<KeyValuePair<string, string>> Light,
    IReadOnlyDictionary<string, string> Dark,
    IReadOnlyDictionary<string, string> LightValues,
    IReadOnlyDictionary<string, Rgba> DarkColours,
    IReadOnlyDictionary<string, Rgba> LightColours,
    IReadOnlyList<ContrastCheck> Checks,
    IReadOnlyList<HueCheck> HueChecks)
{
    /// <summary>How many tokens the CSS changes, counting a token set in both blocks once.</summary>
    public int Changed => Root.Select(t => t.Key).Union(Light.Select(t => t.Key)).Count();
}

/// <summary>
/// Builds a <c>brand.css</c>: a quick start that generates the brand, accent and base ramps
/// the way the built-in themes generate theirs (<c>docs/BRANDING.md</c> §4), and any other
/// token an app may redefine, per variant — written out only where it differs from the library.
/// </summary>
/// <remarks>
/// <para>
/// The quick start is palette tokens only. The semantic tier already points at the palette in
/// both variants, so a redefined ramp re-themes every role, tint, ring and glow, in dark and
/// light alike. Sedna Red, Orbit Blue and Deep Space give Sedna's own hand-tuned ramps rather
/// than a generated copy, and so change nothing.
/// </para>
/// <para>
/// A token set at bare <c>:root</c> in an app's unlayered stylesheet also beats the library's
/// light value for it (<c>docs/architecture.md</c>), so where the library gives a token a light
/// value of its own and only the dark one is changed, the light block restates the library's.
/// </para>
/// </remarks>
internal static class BrandCssBuilder
{
    public const string SednaRed = "#ff6b4a";
    public const string OrbitBlue = "#59c3ff";
    public const string DeepSpace = "#0f172a";

    private static readonly Regex Hex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>The white-text floor the brand step is solved for, as the built-in themes do.</summary>
    private const double BrandFloor = 4.6;

    private static SednaPalette Sedna => SednaTheme.Sedna.Palette;

    /// <summary>The ramps for a brand colour and, optionally, an accent and a canvas.</summary>
    public static QuickStart Ramps(string? brand, string? accent = null, string? canvas = null)
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
            problem ??= $"\"{accent}\" is not a colour of the form #rrggbb, so the accent is left as it is. Pick it again.";

        var canvasHex = canvas is null ? null : Normalise(canvas);
        if (canvas is not null && canvasHex is null)
            problem ??= $"\"{canvas}\" is not a colour of the form #rrggbb, so the base is left as it is. Pick it again.";

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

        var orbit = accentHex is null || accentHex == OrbitBlue ? Sedna.Orbit : SednaRamp.FromAnchor(accentHex, 400);

        var slate = Sedna.Slate;
        if (canvasHex is not null && canvasHex != DeepSpace)
        {
            try
            {
                slate = SednaRamp.Surface(canvasHex);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                problem ??= $"No base ramp fits {canvasHex}, so the base is left as it is. {e.Message}";
                canvasHex = null;
            }
        }

        var tokens = new List<KeyValuePair<string, string>>();
        Add("coral", coral);
        if (accentHex is not null) Add("orbit", orbit);
        if (canvasHex is not null) Add("slate", slate);

        return new QuickStart(brandHex, accentHex, canvasHex, tokens, problem);

        void Add(string family, SednaRamp ramp)
        {
            foreach (var step in ramp.Steps)
                tokens.Add(new($"--{family}-{step.ToString(CultureInfo.InvariantCulture)}", ramp[step]));
        }
    }

    /// <summary>
    /// The <c>brand.css</c> for a quick start and the reader's own values: <paramref name="dark"/>
    /// for bare <c>:root</c>, <paramref name="light"/> for the light variant.
    /// </summary>
    public static ThemeResult Build(
        ThemeTokens library,
        QuickStart start,
        IReadOnlyDictionary<string, string> dark,
        IReadOnlyDictionary<string, string> light)
    {
        var generated = start.Tokens.ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal);

        var root = new List<KeyValuePair<string, string>>();
        foreach (var name in library.Order)
        {
            var value = dark.GetValueOrDefault(name) ?? generated.GetValueOrDefault(name);
            if (value is not null && !Same(value, library.DarkDefaults[name]))
                root.Add(new(name, value.Trim()));
        }

        var rootValues = root.ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal);
        var lightBlock = new List<KeyValuePair<string, string>>();
        var restated = new List<string>();
        foreach (var name in library.Order)
        {
            var own = library.LightDefaults.GetValueOrDefault(name);
            // What light shows without a value of its own here: the library's light value, or,
            // where it has none, whatever bare :root now says.
            var baseline = own ?? rootValues.GetValueOrDefault(name) ?? library.DarkDefaults[name];

            if (light.GetValueOrDefault(name) is { } chosen && !Same(chosen, baseline))
            {
                lightBlock.Add(new(name, chosen.Trim()));
            }
            else if (own is not null && rootValues.ContainsKey(name))
            {
                lightBlock.Add(new(name, own));
                restated.Add(name);
            }
        }

        var darkValues = new Dictionary<string, string>(library.DarkDefaults, StringComparer.Ordinal);
        foreach (var (name, value) in root) darkValues[name] = value;
        var lightValues = ThemeTokens.LightEffective(library.DarkDefaults, library.LightDefaults);
        foreach (var (name, value) in root) lightValues[name] = value;
        foreach (var (name, value) in lightBlock) lightValues[name] = value;

        var darkColours = CssColour.ResolveAll(darkValues);
        var lightColours = CssColour.ResolveAll(lightValues);

        return new ThemeResult(
            Css(start, root, lightBlock, restated, library),
            root, lightBlock, darkValues, lightValues, darkColours, lightColours,
            ThemeChecks.Contrast(darkColours, lightColours),
            ThemeChecks.Hue(darkColours));
    }

    /// <summary>Why a value cannot go into the stylesheet, or null when it can.</summary>
    public static string? Refuse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Empty. Reset the token instead to keep the library's value.";
        if (value.IndexOfAny([';', '{', '}', '<', '>']) >= 0)
            return "A value cannot contain ; { } < or >: it would end the declaration it is written in.";
        if (value.Contains("!important", StringComparison.OrdinalIgnoreCase))
            return "No !important: the app's stylesheet already wins over the library, and important flips the cascade layers.";
        return null;
    }

    /// <summary>Whether two CSS values are the same text, ignoring case and spacing.</summary>
    public static bool Same(string a, string b) =>
        string.Equals(Whitespace.Replace(a.Trim(), " "), Whitespace.Replace(b.Trim(), " "), StringComparison.OrdinalIgnoreCase);

    public static string? Normalise(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is not null && Hex.IsMatch(trimmed) ? trimmed.ToLowerInvariant() : null;
    }

    private static string Css(
        QuickStart start,
        List<KeyValuePair<string, string>> root,
        List<KeyValuePair<string, string>> light,
        List<string> restated,
        ThemeTokens library)
    {
        var css = new StringBuilder();
        css.Append("/* brand.css: the Sedna.UI theme builder");
        var from = new List<string>();
        if (start.Brand != SednaRed) from.Add("brand " + start.Brand);
        if (start.Accent is not null && start.Accent != OrbitBlue) from.Add("accent " + start.Accent);
        if (start.Canvas is not null && start.Canvas != DeepSpace) from.Add("canvas " + start.Canvas);
        if (from.Count > 0) css.Append(", ").Append(string.Join(", ", from));
        css.Append(".\n   Load it after Sedna.UI.css. Only what differs from the library is here. */\n");

        if (root.Count == 0 && light.Count == 0)
        {
            css.Append("\n/* Nothing is changed yet: every token has the library's value. */\n");
            return css.ToString();
        }

        if (root.Count > 0)
        {
            // The palette first and the roles after it, a blank line apart: the ramps are what a
            // reader regenerates, the roles what they chose one at a time.
            var palette = root.Where(t => library.ByName.GetValueOrDefault(t.Key)?.Tier == TokenTier.Palette).ToList();
            var roles = root.Except(palette).ToList();
            css.Append("\n:root {\n");
            Declare(palette);
            if (palette.Count > 0 && roles.Count > 0) css.Append('\n');
            Declare(roles);
            css.Append("}\n");
        }

        if (light.Count > 0)
        {
            css.Append("\n/* The light variant. A token set on :root above also wins in light");
            css.Append(restated.Count > 0 ? ",\n   so the library's own light value is restated where it has one. */\n" : ". */\n");
            css.Append(ThemeTokens.LightSelector).Append(" {\n");
            Declare(light);
            css.Append("}\n");
        }

        return css.ToString();

        void Declare(IEnumerable<KeyValuePair<string, string>> tokens)
        {
            foreach (var (name, value) in tokens)
                css.Append("    ").Append(name).Append(": ").Append(value).Append(";\n");
        }
    }
}
