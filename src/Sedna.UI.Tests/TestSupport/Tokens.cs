using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// The shipped stylesheet's tokens, resolved to the literals they end at, and the
/// contrast between two of them.
/// </summary>
/// <remarks>
/// One implementation of "what colour is this token, in this variant". It lives here
/// rather than inside a test class because two suites need it — the near-floor pairs
/// in <c>TokenTierTests</c> and the surface ladder in <c>SurfaceTests</c> — and a
/// second copy would be a second answer to the same question.
/// </remarks>
internal static class Tokens
{
    /// <summary>The dark default: what bare <c>:root</c> says.</summary>
    public const string Dark = "dark";

    /// <summary>The light variant: bare <c>:root</c> with the light block over it.</summary>
    public const string Light = "light";

    private static readonly Regex Declaration = new(
        @"(?<name>--[a-z0-9-]+)\s*:\s*(?<value>[^;}]+)", RegexOptions.Compiled);

    private static readonly Regex HexLiteral = new(
        @"#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\b", RegexOptions.Compiled);

    /// <summary>
    /// Every token declared for one variant, with <c>var()</c> chains followed to the
    /// literal they end at.
    /// </summary>
    /// <remarks>
    /// Media blocks are excised first. <c>forced-colors</c> hands the palette to the
    /// OS — <c>--fg</c> becomes <c>CanvasText</c>, which has no measurable value — and
    /// <c>prefers-contrast</c> is a preference, not a variant. Both are conditional
    /// states on top of what this returns.
    /// </remarks>
    public static Dictionary<string, string> Resolved(string variant = Dark) =>
        Resolved(variant, palette: null);

    /// <summary>
    /// The same, with tier 1 replaced by <paramref name="palette"/> — what a registered
    /// theme's <c>[data-theme="…"]</c> block does to the shipped <c>:root</c> palette.
    /// </summary>
    /// <remarks>
    /// The semantic tier is the stylesheet's either way: a theme supplies ramps, never roles
    /// (<c>SednaUiBrand.ToCss</c> emits palette tokens only), so measuring a theme means
    /// resolving the shipped roles through its ramps.
    /// </remarks>
    public static Dictionary<string, string> Resolved(string variant, SednaPalette? palette)
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);
        var css = WithoutMediaBlocks(Assets.StripComments(Assets.Css));

        // Bare :root first, then the variant's own block over it — which is the order
        // the cascade applies them in, and the reason a variant only has to remap what
        // it changes.
        foreach (var selector in Selectors(variant))
            foreach (var (found, body) in Assets.TokenBlocks(css))
            {
                if (Assets.Squash(found) != selector) continue;
                foreach (var match in Declaration.Matches(body).Cast<Match>())
                    raw[match.Groups["name"].Value] = match.Groups["value"].Value.Trim();
            }

        if (palette is not null)
            foreach (var (family, ramp) in palette.Ramps())
                foreach (var step in ramp.Steps)
                    raw[$"--{family}-{step}"] = ramp[step];

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in raw.Keys) resolved[name] = Resolve(name, raw, depth: 0);
        return resolved;
    }

    /// <summary>
    /// The colour actually painted when <paramref name="overlayToken"/> — a translucent tint —
    /// sits on <paramref name="canvasToken"/>, as an RGB triple.
    /// </summary>
    /// <remarks>
    /// A tint has no contrast of its own: <c>--brand-tint</c> is a <c>color-mix</c> with
    /// <c>transparent</c>, so what a reader sees depends on the surface under it. Measuring the
    /// token alone answers a question nobody is asking. Handles the two forms the sheet uses —
    /// <c>color-mix(in srgb, var(--x) N%, transparent)</c> and <c>rgba(r, g, b, a)</c>.
    /// </remarks>
    public static (double R, double G, double B) Over(
        Dictionary<string, string> tokens, string overlayToken, string canvasToken)
    {
        var canvas = Rgb(tokens, canvasToken);

        Assert.True(tokens.TryGetValue(overlayToken, out var value), $"{overlayToken} is not declared.");

        var mix = Regex.Match(value!,
            @"color-mix\(\s*in\s+srgb\s*,\s*var\(\s*(?<name>--[a-z0-9-]+)\s*\)\s*(?<pct>[\d.]+)%\s*,\s*transparent\s*\)");
        if (mix.Success)
            return Blend(Rgb(tokens, mix.Groups["name"].Value), canvas,
                double.Parse(mix.Groups["pct"].Value, CultureInfo.InvariantCulture) / 100);

        var rgba = Regex.Match(value!,
            @"rgba?\(\s*(?<r>\d+)\s*,\s*(?<g>\d+)\s*,\s*(?<b>\d+)\s*(?:,\s*(?<a>[\d.]+)\s*)?\)");
        if (rgba.Success)
        {
            var colour = (Channel("r"), Channel("g"), Channel("b"));
            var alpha = rgba.Groups["a"].Success
                ? double.Parse(rgba.Groups["a"].Value, CultureInfo.InvariantCulture)
                : 1;
            return Blend(colour, canvas, alpha);

            double Channel(string group) =>
                int.Parse(rgba.Groups[group].Value, CultureInfo.InvariantCulture) / 255.0;
        }

        // Opaque: whatever is under it makes no difference.
        return Rgb(tokens, overlayToken);

        static (double R, double G, double B) Blend(
            (double R, double G, double B) over, (double R, double G, double B) under, double alpha) =>
            (over.R * alpha + under.R * (1 - alpha),
             over.G * alpha + under.G * (1 - alpha),
             over.B * alpha + under.B * (1 - alpha));
    }

    private static IEnumerable<string> Selectors(string variant) =>
        variant == Light
            ? [":root", ":root[data-variant=\"light\"]"]
            : [":root"];

    /// <summary>One token as an RGB triple, asserting it ends at a hex literal.</summary>
    public static (double R, double G, double B) Rgb(Dictionary<string, string> tokens, string name)
    {
        Assert.True(tokens.TryGetValue(name, out var value), $"{name} is not declared at :root.");

        var hex = HexLiteral.Match(value!);
        Assert.True(hex.Success,
            $"{name} resolves to \"{value}\", which is not a plain colour — this can only "
            + "measure a token that ends at a hex literal.");

        var text = hex.Value[1..];
        if (text.Length == 3)
            text = string.Concat(text.Select(c => new string(c, 2)));

        return (Channel(text[..2]), Channel(text[2..4]), Channel(text[4..6]));

        static double Channel(string pair) =>
            int.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
    }

    /// <summary>WCAG 2.1 relative-luminance contrast ratio.</summary>
    public static double Contrast((double R, double G, double B) a, (double R, double G, double B) b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);

        static double Luminance((double R, double G, double B) c) =>
            0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);

        static double Linear(double channel) =>
            channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    /// <summary>The contrast between two tokens in one variant.</summary>
    public static double Contrast(Dictionary<string, string> tokens, string foreground, string background) =>
        Contrast(Rgb(tokens, foreground), Rgb(tokens, background));

    private static string Resolve(string name, Dictionary<string, string> raw, int depth)
    {
        if (depth > 8 || !raw.TryGetValue(name, out var value)) return string.Empty;

        var reference = Regex.Match(value, @"^var\(\s*(--[a-z0-9-]+)\s*\)$");
        return reference.Success ? Resolve(reference.Groups[1].Value, raw, depth + 1) : value;
    }

    /// <summary>
    /// The stylesheet with every <c>@media</c> block removed, matched by counting
    /// braces rather than by regex so a nested rule cannot truncate one.
    /// </summary>
    public static string WithoutMediaBlocks(string css)
    {
        var result = new StringBuilder(css.Length);
        var i = 0;

        while (i < css.Length)
        {
            var at = css.IndexOf("@media", i, StringComparison.Ordinal);
            if (at < 0) { result.Append(css, i, css.Length - i); break; }

            result.Append(css, i, at - i);

            var open = css.IndexOf('{', at);
            if (open < 0) break;

            var depth = 1;
            var j = open + 1;
            while (j < css.Length && depth > 0)
            {
                if (css[j] == '{') depth++;
                else if (css[j] == '}') depth--;
                j++;
            }

            i = j;
        }

        return result.ToString();
    }
}
