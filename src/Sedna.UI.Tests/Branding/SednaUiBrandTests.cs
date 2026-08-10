using System.Text.RegularExpressions;

namespace Sedna.UI.Tests;

/// <summary>
/// What <see cref="SednaUiBrand.ToCss"/> emits: palette tokens only, one block for the default
/// theme at bare <c>:root</c>, one <c>[data-theme="…"]</c> block per additional theme, and
/// nothing else — no selectors, no semantic roles.
/// </summary>
public class SednaUiBrandTests
{
    private static readonly Regex Rule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

    [Fact]
    public void The_default_theme_is_emitted_at_bare_root()
    {
        var options = new SednaUiOptions();
        var css = SednaUiBrand.ToCss(options);

        Assert.StartsWith(":root{", css, StringComparison.Ordinal);
        Assert.Contains("--coral-500:#ff6b4a;", css, StringComparison.Ordinal);
        Assert.Contains("--white:#ffffff;", css, StringComparison.Ordinal);
        Assert.Contains("--black:#000000;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void An_additional_theme_is_emitted_at_its_own_attribute_selector()
    {
        var forest = new SednaTheme("forest", SednaTheme.Sedna.Dark, SednaTheme.Sedna.Light);
        var options = new SednaUiOptions { Themes = [SednaTheme.Sedna, forest], Default = "sedna" };

        var css = SednaUiBrand.ToCss(options);

        Assert.Contains("[data-theme=\"forest\"]{", css, StringComparison.Ordinal);
        // The default theme's own name gets no attribute block — it is already the bare :root.
        Assert.DoesNotContain("[data-theme=\"sedna\"]", css, StringComparison.Ordinal);
    }

    [Fact]
    public void No_semantic_token_is_emitted()
    {
        var css = SednaUiBrand.ToCss(new SednaUiOptions());

        // The semantic tier ships in the stylesheet and already points at the palette; a
        // theme that changes only anchors needs none of it repeated here.
        Assert.DoesNotContain("--brand:", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--bg:", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--accent:", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_rule_declares_only_custom_properties()
    {
        // The new guard docs/superpowers/specs/2026-08-10-sedna-theming-design.md §7 calls for:
        // the emitted CSS parses into rules that are nothing but `--x: value;` declarations —
        // no nested selector, no non-custom property, no stray text.
        var css = SednaUiBrand.ToCss(new SednaUiOptions
        {
            Themes = [SednaTheme.Sedna, new SednaTheme("forest", SednaTheme.Sedna.Dark, SednaTheme.Sedna.Light)],
            Default = "sedna",
        });

        var rules = Rule.Matches(css);
        Assert.True(rules.Count == 2, $"Expected exactly 2 rules (:root and one extra theme), found {rules.Count}.");

        foreach (Match rule in rules)
        {
            var body = rule.Groups["body"].Value;
            var declarations = body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var declaration in declarations)
            {
                Assert.True(Regex.IsMatch(declaration + ";", @"^--[a-z0-9-]+\s*:\s*[^;]+;$"),
                    $"\"{declaration}\" in {rule.Groups["selector"].Value} is not a bare custom-property declaration.");
            }
        }
    }

    [Fact]
    public void Rejects_a_default_that_names_no_registered_theme()
    {
        var options = new SednaUiOptions { Default = "does-not-exist" };
        Assert.Throws<InvalidOperationException>(() => SednaUiBrand.ToCss(options));
    }

    [Fact]
    public void Rejects_an_empty_theme_list()
    {
        var options = new SednaUiOptions { Themes = [] };
        Assert.Throws<InvalidOperationException>(() => SednaUiBrand.ToCss(options));
    }

    [Fact]
    public void Rejects_a_null_options() =>
        Assert.Throws<ArgumentNullException>(() => SednaUiBrand.ToCss(null!));
}
