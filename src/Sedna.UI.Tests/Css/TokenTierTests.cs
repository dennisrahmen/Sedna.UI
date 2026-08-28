using System.Globalization;
using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The token layer has two tiers, and neither may leak into the other.
/// </summary>
/// <remarks>
/// <para>
/// Tier 1 is the palette — ramps, declared once, holding literals. Tier 2 is the
/// semantic roles the stylesheet actually uses, each pointing at a palette step.
/// The split is what makes a rebrand a matter of redefining a handful of anchors
/// instead of re-deriving every value by hand: <c>docs/BRANDING.md</c> was 30 000
/// characters of per-token tables precisely because this tier did not exist.
/// </para>
/// <para>
/// Two rules keep it honest, and both fail silently without a test. A raw hex in
/// tier 2 is a value no theme can reach — it looks fine until someone switches
/// theme and one surface does not move. A <c>var()</c> in tier 1 inverts the tiers,
/// so "what colour is this ramp step" stops having an answer.
/// </para>
/// </remarks>
public class TokenTierTests
{
    private const string PalettePart = "00-palette.css";

    /// <summary>Every token name declared by the palette part.</summary>
    private static ISet<string> PaletteNames { get; } = Assets.DeclaredCustomProperties(
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", PalettePart)));

    private static readonly Regex HexLiteral = new(@"#[0-9a-fA-F]{3,8}\b", RegexOptions.Compiled);
    private static readonly Regex Declaration = new(
        @"(?<name>(?<![\w-])--[a-z0-9-]+)\s*:\s*(?<value>[^;]+);", RegexOptions.Compiled);

    [Fact]
    public void A_palette_token_holds_a_literal_and_never_a_reference()
    {
        var offenders = new List<string>();

        foreach (var (name, value) in Declarations(Assets.StripComments(Assets.Css)))
        {
            if (!PaletteNames.Contains(name)) continue;

            if (value.Contains("var(", StringComparison.Ordinal))
                offenders.Add($"{name}: {value} — a ramp step may not reference another token");
        }

        Assert.True(offenders.Count == 0,
            "Tier 1 must be literals only, or the tiers invert:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void A_semantic_token_resolves_through_the_palette_and_never_hard_codes_a_colour()
    {
        var offenders = new List<string>();

        foreach (var (name, value) in Declarations(Assets.StripComments(Assets.Css)))
        {
            if (PaletteNames.Contains(name)) continue;
            if (!HexLiteral.IsMatch(value)) continue;

            offenders.Add($"{name}: {value}");
        }

        Assert.True(offenders.Count == 0,
            "Tier 2 must point at the palette. A hex here is a value no theme can reach:\n  "
            + string.Join("\n  ", offenders)
            + "\n\nIf the colour genuinely is not on a ramp, add it to " + PalettePart
            + " — --white and --black are there for exactly that reason.");
    }

    [Fact]
    public void The_palette_is_declared_once_and_never_remapped_by_a_variant()
    {
        // A ramp step is a colour, not a decision. If a theme, the colour-blind
        // palette or a contrast preference could redefine one, then "coral 600"
        // would mean different things in different states and every contrast
        // measurement in docs/BRANDING.md §2 would be conditional.
        var offenders = new List<string>();

        foreach (var (selector, body) in Assets.TokenBlocks(Assets.StripComments(Assets.Css)))
        {
            if (Assets.Squash(selector) == ":root") continue;

            foreach (var (name, _) in Declarations(body))
                if (PaletteNames.Contains(name))
                    offenders.Add($"{Assets.Squash(selector)} remaps {name}");
        }

        Assert.True(offenders.Count == 0,
            "A palette token may only be declared at bare :root:\n  " + string.Join("\n  ", offenders));
    }

    [Theory]
    // The six pairs docs/BRANDING.md §7.1 names as sitting closest to the WCAG AA
    // floor. They are listed here rather than left implicit because the manual says
    // it out loud: "Do not lighten any of them without re-measuring." This is the
    // re-measurement, and it runs on every build.
    [InlineData("--on-solid", "--brand", 4.5)]
    [InlineData("--on-solid", "--danger-solid", 4.5)]
    [InlineData("--on-solid", "--go-solid", 4.5)]
    [InlineData("--on-solid", "--warn-solid", 4.5)]
    [InlineData("--fg", "--bg", 4.5)]
    [InlineData("--muted", "--bg", 4.5)]
    public void A_near_floor_pair_still_clears_AA(string foreground, string background, double floor)
    {
        var tokens = ResolvedRootTokens();

        var fg = Rgb(tokens, foreground);
        var bg = Rgb(tokens, background);
        var ratio = Contrast(fg, bg);

        Assert.True(ratio >= floor,
            $"{foreground} on {background} is {ratio:0.00}:1, below the {floor:0.0}:1 floor. "
            + "Take the next darker ramp step rather than adjusting by eye — see "
            + "docs/BRANDING.md §7.1.");
    }

    // ── plumbing ─────────────────────────────────────────────────────────────

    private static IEnumerable<(string Name, string Value)> Declarations(string css) =>
        Declaration.Matches(css).Select(m => (m.Groups["name"].Value, m.Groups["value"].Value.Trim()));

    /// <summary>
    /// Every token declared at bare <c>:root</c>, with <c>var()</c> chains followed
    /// to the literal they end at. That is the default (dark) state; the variants are
    /// remaps of it.
    /// </summary>
    private static Dictionary<string, string> ResolvedRootTokens()
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);

        // Media blocks are excised first. `@media (forced-colors: active)` and
        // `prefers-contrast` both carry their own `:root` remap, and forced colours
        // hands the palette to the OS — `--fg` becomes `CanvasText`, which has no
        // measurable value here. Those are conditional states; the default is what
        // the bare `:root` outside any media query says.
        foreach (var (selector, body) in Assets.TokenBlocks(WithoutMediaBlocks(Assets.StripComments(Assets.Css))))
        {
            if (Assets.Squash(selector) != ":root") continue;
            foreach (var (name, value) in Declarations(body)) raw[name] = value;
        }

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in raw.Keys) resolved[name] = Resolve(name, raw, depth: 0);
        return resolved;
    }

    /// <summary>
    /// The stylesheet with every <c>@media</c> block removed, matched by counting
    /// braces rather than by regex so a nested rule cannot truncate one.
    /// </summary>
    private static string WithoutMediaBlocks(string css)
    {
        var result = new System.Text.StringBuilder(css.Length);
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

    private static string Resolve(string name, Dictionary<string, string> raw, int depth)
    {
        if (depth > 8 || !raw.TryGetValue(name, out var value)) return string.Empty;

        var reference = Regex.Match(value, @"^var\(\s*(--[a-z0-9-]+)\s*\)$");
        return reference.Success ? Resolve(reference.Groups[1].Value, raw, depth + 1) : value;
    }

    private static (double R, double G, double B) Rgb(Dictionary<string, string> tokens, string name)
    {
        Assert.True(tokens.TryGetValue(name, out var value),
            $"{name} is not declared at :root.");

        var hex = HexLiteral.Match(value);
        Assert.True(hex.Success,
            $"{name} resolves to \"{value}\", which is not a plain colour — this test can only "
            + "measure a token that ends at a hex literal.");

        var text = hex.Value[1..];
        if (text.Length == 3)
            text = string.Concat(text.Select(c => new string(c, 2)));

        return (Channel(text[..2]), Channel(text[2..4]), Channel(text[4..6]));

        static double Channel(string pair) =>
            int.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
    }

    /// <summary>WCAG 2.1 relative-luminance contrast ratio.</summary>
    private static double Contrast((double R, double G, double B) a, (double R, double G, double B) b)
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
}
