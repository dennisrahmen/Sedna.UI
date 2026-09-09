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
        var tokens = Tokens.Resolved();
        var ratio = Tokens.Contrast(tokens, foreground, background);

        Assert.True(ratio >= floor,
            $"{foreground} on {background} is {ratio:0.00}:1, below the {floor:0.0}:1 floor. "
            + "Take the next darker ramp step rather than adjusting by eye — see "
            + "docs/BRANDING.md §7.1.");
    }

    // ── plumbing ─────────────────────────────────────────────────────────────

    private static IEnumerable<(string Name, string Value)> Declarations(string css) =>
        Declaration.Matches(css).Select(m => (m.Groups["name"].Value, m.Groups["value"].Value.Trim()));
}
