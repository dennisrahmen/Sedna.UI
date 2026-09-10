using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests.Css;

/// <summary>
/// Every shadow the dark default declares is remapped by the light variant.
/// </summary>
/// <remarks>
/// A shadow's own black has no palette step to trace to, so shadow tokens carry a
/// literal <c>rgba()</c> rather than a ramp step — which puts them outside every
/// other token guard in this suite. <c>TokenDeclarationTests</c> asks whether a
/// <c>var(--x)</c> is declared, and an unremapped shadow *is* declared: at
/// <c>:root</c>, in the dark value. So the failure mode is silent by construction,
/// and it is not theoretical. <c>--shadow-dropdown</c> and <c>--shadow-flyout</c>
/// were both missing from the light block, which left <c>.menu</c>, the select panel,
/// the search panel and the collapsed rail's flyout casting a near-black drop on a
/// white ground while their neighbours looked right.
/// </remarks>
public class ShadowTokenTests
{
    private const string LightSelector = ":root[data-variant=\"light\"]";

    private static readonly Regex ShadowName = new(
        @"(?<name>--shadow-[a-z0-9-]+)\s*:", RegexOptions.Compiled);

    [Fact]
    public void The_light_variant_remaps_every_shadow_the_dark_default_declares()
    {
        var dark = ShadowsIn(":root");
        var light = ShadowsIn(LightSelector);

        var missing = dark.Except(light, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0,
            "These shadow tokens are declared at :root but never remapped for the light variant, so "
            + "they keep their dark alpha on a light ground — which reads as a near-black smudge under "
            + "a panel and is the one token failure no other guard here can see. Add a light value in "
            + "02-theme-light.css: "
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [Fact]
    public void The_light_variant_remaps_no_shadow_the_dark_default_does_not_declare()
    {
        // The mirror of the test above, and not redundant with it: a light-only shadow
        // is a token an app can read in one variant and not the other, which fails as
        // an unresolved var() rather than as a wrong colour. Renaming a token in one
        // block and not the other produces one of each.
        var dark = ShadowsIn(":root");
        var light = ShadowsIn(LightSelector);

        var orphans = light.Except(dark, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.True(orphans.Count == 0,
            "These shadow tokens exist only in the light variant, so they resolve to nothing in the "
            + "dark default. Declare them at :root as well, or drop them: "
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, orphans)}");
    }

    [Fact]
    public void Every_shadow_a_rule_uses_is_a_shadow_token()
    {
        // box-shadow is where the no-hard-coded-colour rule is easiest to break by
        // accident, because a shadow reads as geometry and the colour is buried at the
        // end of a four-value shorthand. The focus ring is the deliberate exception:
        // every ring in this library is a box-shadow rather than an outline, and it
        // takes --ring / --ring-danger, which are colours and not shadow tokens.
        var css = Assets.StripComments(Assets.Css);

        var offenders = Regex.Matches(css, @"box-shadow\s*:\s*(?<value>[^;}]+)", RegexOptions.Compiled)
            .Select(m => Assets.Squash(m.Groups["value"].Value))
            .Where(v => !v.Equals("none", StringComparison.OrdinalIgnoreCase))
            .Where(v => Regex.IsMatch(v, @"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\("))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "A box-shadow must take its colour from a token, so the light variant can remap it. "
            + "These carry a literal: "
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    private static HashSet<string> ShadowsIn(string selector)
    {
        var css = Assets.StripComments(Assets.Css);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (found, body) in Assets.TokenBlocks(css))
        {
            if (Assets.Squash(found) != selector) continue;
            foreach (var match in ShadowName.Matches(body).Cast<Match>())
                names.Add(match.Groups["name"].Value);
        }

        Assert.True(names.Count > 0, $"No shadow tokens found in the {selector} block at all — "
            + "either the selector moved or this test is looking in the wrong place.");

        return names;
    }
}
