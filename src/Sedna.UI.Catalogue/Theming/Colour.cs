using System.Globalization;
using System.Text.RegularExpressions;

namespace Sedna.UI.Catalogue.Theming;

/// <summary>A colour with its alpha, each channel from 0 to 1, in sRGB.</summary>
internal readonly record struct Rgba(double R, double G, double B, double A = 1)
{
    public bool Opaque => A >= 0.999;

    /// <summary>The colour as <c>#rrggbb</c>, alpha dropped: what a colour input can show.</summary>
    public string Hex => "#" + Byte(R) + Byte(G) + Byte(B);

    /// <summary>What is painted when this colour sits on <paramref name="under"/>.</summary>
    public Rgba Over(Rgba under)
    {
        if (Opaque) return this with { A = 1 };
        var below = under.Opaque ? under : under with { A = 1 };
        return new Rgba(
            R * A + below.R * (1 - A),
            G * A + below.G * (1 - A),
            B * A + below.B * (1 - A));
    }

    public static Rgba? FromHex(string hex)
    {
        var text = hex.Trim().TrimStart('#');
        if (text.Length is 3 or 4) text = string.Concat(text.Select(c => new string(c, 2)));
        if (text.Length is not (6 or 8) || !text.All(Uri.IsHexDigit)) return null;
        return new Rgba(Channel(0), Channel(2), Channel(4), text.Length == 8 ? Channel(6) : 1);

        double Channel(int at) =>
            int.Parse(text.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
    }

    private static string Byte(double channel) =>
        ((int)Math.Round(Math.Clamp(channel, 0, 1) * 255)).ToString("x2", CultureInfo.InvariantCulture);
}

/// <summary>WCAG contrast and OKLCH hue of a colour.</summary>
internal static class Colour
{
    public static double Contrast(string a, string b) =>
        Contrast(Rgba.FromHex(a) ?? throw new ArgumentException($"\"{a}\" is not a hex colour.", nameof(a)),
            Rgba.FromHex(b) ?? throw new ArgumentException($"\"{b}\" is not a hex colour.", nameof(b)));

    /// <summary>The WCAG 2.1 contrast ratio of two opaque colours, from 1 to 21.</summary>
    public static double Contrast(Rgba a, Rgba b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static double Luminance(string hex) => Luminance(Rgba.FromHex(hex)!.Value);

    public static double Luminance(Rgba colour)
    {
        var (r, g, b) = Linear(colour);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>The OKLCH hue in degrees, or null for a colour too grey to have one.</summary>
    public static double? Hue(string hex) => Hue(Rgba.FromHex(hex)!.Value);

    public static double? Hue(Rgba colour)
    {
        var (r, g, b) = Linear(colour);
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

    private static (double R, double G, double B) Linear(Rgba colour)
    {
        static double Channel(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return (Channel(colour.R), Channel(colour.G), Channel(colour.B));
    }
}

/// <summary>
/// Reads a token's CSS value as a colour, following <c>var()</c> through the other tokens —
/// the forms the stylesheet's own tokens are written in: a hex, <c>rgb()</c>,
/// <c>transparent</c>, <c>var()</c> and <c>color-mix(in srgb, …)</c>.
/// </summary>
/// <remarks>
/// Anything else — a keyword the browser decides, <c>currentColor</c>, another colour space —
/// reads as no colour, and a check on it says it could not be measured rather than guess.
/// </remarks>
internal static class CssColour
{
    private static readonly Regex Percent = new(@"^(?<colour>.*?)\s+(?<pct>[\d.]+)%$|^(?<pct2>[\d.]+)%\s+(?<colour2>.*)$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Every token in <paramref name="raw"/> that reads as a colour, resolved.</summary>
    public static Dictionary<string, Rgba> ResolveAll(IReadOnlyDictionary<string, string> raw)
    {
        var resolved = new Dictionary<string, Rgba>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in raw.Keys) Token(name);
        return resolved;

        Rgba? Token(string name)
        {
            if (resolved.TryGetValue(name, out var done)) return done;
            if (failed.Contains(name) || !raw.TryGetValue(name, out var value) || !visiting.Add(name)) return null;
            var colour = Parse(value, Token);
            visiting.Remove(name);
            if (colour is { } c) resolved[name] = c;
            else failed.Add(name);
            return colour;
        }
    }

    /// <summary>One CSS value as a colour, with <paramref name="token"/> answering <c>var()</c>.</summary>
    public static Rgba? Parse(string value, Func<string, Rgba?> token)
    {
        var text = value.Trim();
        if (text.Length == 0) return null;
        if (text[0] == '#') return Rgba.FromHex(text);

        switch (text.ToLowerInvariant())
        {
            case "transparent": return new Rgba(0, 0, 0, 0);
            case "white": return new Rgba(1, 1, 1);
            case "black": return new Rgba(0, 0, 0);
        }

        var open = text.IndexOf('(', StringComparison.Ordinal);
        if (open <= 0 || text[^1] != ')') return null;
        var function = text[..open].Trim().ToLowerInvariant();
        var args = Split(text[(open + 1)..^1]);

        return function switch
        {
            "var" => Var(args),
            "rgb" or "rgba" => Rgb(text[(open + 1)..^1]),
            "color-mix" => Mix(args),
            _ => null,
        };

        Rgba? Var(List<string> parts)
        {
            if (parts.Count == 0) return null;
            return token(parts[0].Trim()) ?? (parts.Count > 1 ? Parse(string.Join(",", parts.Skip(1)), token) : null);
        }

        Rgba? Mix(List<string> parts)
        {
            if (parts.Count != 3 || !parts[0].Trim().Equals("in srgb", StringComparison.OrdinalIgnoreCase)) return null;
            var (a, pa) = Component(parts[1]);
            var (b, pb) = Component(parts[2]);
            if (a is not { } first || b is not { } second) return null;

            // The CSS Color 5 rules: a missing percentage is what the other leaves, and the
            // two are normalised, with what falls short of 100% taken off the alpha.
            double p1, p2;
            if (pa is null && pb is null) (p1, p2) = (0.5, 0.5);
            else if (pa is null) (p2, p1) = (pb!.Value, 1 - pb.Value);
            else if (pb is null) (p1, p2) = (pa.Value, 1 - pa.Value);
            else (p1, p2) = (pa.Value, pb.Value);
            var sum = p1 + p2;
            if (sum <= 0) return null;
            var scale = Math.Min(sum, 1);
            (p1, p2) = (p1 / sum, p2 / sum);

            var alpha = first.A * p1 + second.A * p2;
            if (alpha <= 0) return new Rgba(0, 0, 0, 0);
            return new Rgba(
                (first.R * first.A * p1 + second.R * second.A * p2) / alpha,
                (first.G * first.A * p1 + second.G * second.A * p2) / alpha,
                (first.B * first.A * p1 + second.B * second.A * p2) / alpha,
                alpha * scale);
        }

        (Rgba? Colour, double? Share) Component(string part)
        {
            var match = Percent.Match(part.Trim());
            if (!match.Success) return (Parse(part, token), null);
            var colour = match.Groups["colour"].Success ? match.Groups["colour"].Value : match.Groups["colour2"].Value;
            var pct = match.Groups["pct"].Success ? match.Groups["pct"].Value : match.Groups["pct2"].Value;
            return (Parse(colour, token), double.Parse(pct, CultureInfo.InvariantCulture) / 100);
        }
    }

    private static Rgba? Rgb(string inner)
    {
        var parts = inner.Replace("/", " ", StringComparison.Ordinal)
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not (3 or 4)) return null;

        var channels = new double[4];
        channels[3] = 1;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var isPercent = part.EndsWith('%');
            if (!double.TryParse(isPercent ? part[..^1] : part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return null;
            channels[i] = isPercent ? number / 100 : i == 3 ? number : number / 255;
        }

        return new Rgba(channels[0], channels[1], channels[2], Math.Clamp(channels[3], 0, 1));
    }

    /// <summary>The arguments of a function, split at the commas that are not inside a nested one.</summary>
    private static List<string> Split(string inner)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            switch (inner[i])
            {
                case '(': depth++; break;
                case ')': depth--; break;
                case ',' when depth == 0:
                    parts.Add(inner[start..i]);
                    start = i + 1;
                    break;
            }
        }

        parts.Add(inner[start..]);
        return parts;
    }
}
