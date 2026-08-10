using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <see cref="SednaTheme.Sedna"/> is retyped in C# rather than read from
/// <c>css-parts/00-palette.css</c> at run time — see its own doc comment for why — so this is
/// the test that keeps the two in agreement instead.
/// </summary>
public class SednaThemeTests
{
    private static readonly Regex TokenDeclaration =
        new(@"(?<name>--[a-z0-9-]+)\s*:\s*(?<value>#[0-9a-fA-F]{6})\s*;", RegexOptions.Compiled);

    /// <summary>Tokens the shipped palette declares that are not part of any <see cref="SednaPalette"/> ramp.</summary>
    private static readonly HashSet<string> NonRampTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "--white", "--black",
    };

    [Fact]
    public void Sedna_matches_the_shipped_palette_byte_for_byte()
    {
        var css = Assets.StripComments(
            File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", "00-palette.css")));

        var declared = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in TokenDeclaration.Matches(css))
            declared[m.Groups["name"].Value] = m.Groups["value"].Value;

        var generatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mismatches = new List<string>();

        foreach (var (family, ramp) in SednaTheme.Sedna.Palette.Ramps())
        {
            foreach (var step in ramp.Steps)
            {
                var name = $"--{family}-{step}";
                generatedNames.Add(name);

                if (!declared.TryGetValue(name, out var expected))
                {
                    mismatches.Add($"{name}: SednaTheme.Sedna defines it, 00-palette.css does not.");
                    continue;
                }

                if (!string.Equals(expected, ramp[step], StringComparison.OrdinalIgnoreCase))
                    mismatches.Add($"{name}: SednaTheme.Sedna has {ramp[step]}, 00-palette.css has {expected}.");
            }
        }

        foreach (var name in declared.Keys)
        {
            if (NonRampTokens.Contains(name)) continue;
            if (!generatedNames.Contains(name))
                mismatches.Add($"{name}: 00-palette.css defines it, SednaTheme.Sedna does not.");
        }

        Assert.True(mismatches.Count == 0,
            "SednaTheme.Sedna has drifted from css-parts/00-palette.css:\n  " + string.Join("\n  ", mismatches));
    }

    [Fact]
    public void A_theme_palette_covers_every_step_both_variants_reference()
    {
        // This is what "every theme has a light and a dark variant" actually means. Tier 1 is
        // invariant across variant, so a theme supplies ONE palette — but the shipped light and
        // dark blocks reach for different steps of it, and a theme that only chose
        // dark-suitable anchors would leave the light variant pointing at ramp steps it never
        // declared. Nothing would error; light mode would simply render with missing colours.
        //
        // So: collect every palette token the semantic tier references in either variant, and
        // assert the theme declares all of them.
        var referenced = ReferencedPaletteTokens();
        Assert.NotEmpty(referenced);

        var declared = SednaTheme.Sedna.Palette.Ramps()
            .SelectMany(r => r.Ramp.Steps.Select(step => $"--{r.Family}-{step}"))
            .ToHashSet(StringComparer.Ordinal);

        var missing = referenced.Except(declared, StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "The theme's palette is missing steps the shipped semantic tier references:\n  "
            + string.Join("\n  ", missing)
            + "\n\nA theme must supply every step BOTH variants reach for, or one variant "
            + "renders with colours that were never declared.");
    }

    /// <summary>
    /// Every <c>--family-step</c> token referenced from a semantic block — dark, light,
    /// colour-vision and contrast alike.
    /// </summary>
    private static ISet<string> ReferencedPaletteTokens()
    {
        var css = Assets.StripComments(Assets.Css);
        var palette = Assets.DeclaredCustomProperties(File.ReadAllText(
            Path.Combine(Assets.ProjectDir, "css-parts", "00-palette.css")));

        return Regex.Matches(css, @"var\(\s*(--[a-z]+-\d+)\s*\)")
            .Select(m => m.Groups[1].Value)
            .Where(palette.Contains)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void A_theme_requires_a_palette()
    {
        Assert.Throws<ArgumentNullException>(() => new SednaTheme("x", null!));
    }

    [Fact]
    public void A_theme_requires_a_name()
    {
        var palette = SednaTheme.Sedna.Palette;

        Assert.Throws<ArgumentException>(() => new SednaTheme("", palette));
        Assert.Throws<ArgumentNullException>(() => new SednaTheme(null!, palette));
    }
}
