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
    public static Dictionary<string, string> Resolved(string variant = Dark)
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

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in raw.Keys) resolved[name] = Resolve(name, raw, depth: 0);
        return resolved;
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
    private static string WithoutMediaBlocks(string css)
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
