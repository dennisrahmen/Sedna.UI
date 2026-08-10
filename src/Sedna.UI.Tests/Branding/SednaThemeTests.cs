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

        foreach (var (family, ramp) in SednaTheme.Sedna.Dark.Ramps())
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
    public void Sednas_dark_and_light_variants_are_the_same_palette()
    {
        // docs/BRANDING.md §4.1: tier 1 is "not remapped by variant" for Sedna. The built-in
        // theme reflects that by construction, not by two separately-typed copies that happen
        // to agree today.
        Assert.Same(SednaTheme.Sedna.Dark, SednaTheme.Sedna.Light);
    }

    [Fact]
    public void A_theme_requires_both_variants()
    {
        var palette = SednaTheme.Sedna.Dark;

        Assert.Throws<ArgumentNullException>(() => new SednaTheme("x", null!, palette));
        Assert.Throws<ArgumentNullException>(() => new SednaTheme("x", palette, null!));
    }

    [Fact]
    public void A_theme_requires_a_name()
    {
        var palette = SednaTheme.Sedna.Dark;

        Assert.Throws<ArgumentException>(() => new SednaTheme("", palette, palette));
        Assert.Throws<ArgumentNullException>(() => new SednaTheme(null!, palette, palette));
    }
}
