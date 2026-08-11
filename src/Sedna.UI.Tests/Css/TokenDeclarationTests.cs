using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Token blocks declare values and nothing else, and every token used is declared.
/// </summary>
public class TokenDeclarationTests
{
    [Fact]
    public void Token_blocks_declare_only_custom_properties()
    {
        var css = Assets.StripComments(Assets.Css);

        var offenders = new List<string>();
        foreach (var (selector, blockBody) in Assets.TokenBlocks(css))
        {
            var declarations = blockBody
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(d => d.Length > 0);

            offenders.AddRange(
                declarations
                    .Where(d => !d.StartsWith("--", StringComparison.Ordinal))
                    .Select(d => $"{selector} {{ {d} }}"));
        }

        Assert.True(offenders.Count == 0,
            "A token block defines values, it does not style anything. Move these declarations to " +
            $"a real selector:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Fact]
    public void Every_referenced_token_is_declared()
    {
        var css = Assets.StripComments(Assets.Css);

        var declared = Assets.DeclaredCustomProperties(css);
        var referenced = Assets.ReferencedCustomProperties(css);
        var missing = referenced
            .Except(declared, StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "These tokens are used but never declared, so they silently resolve to nothing: " +
            string.Join(", ", missing));
    }

    [Fact]
    public void The_documented_override_tokens_exist()
    {
        // The tokens an app is documented to redefine. docs/getting-started.md's
        // rebrand recipe and the consuming-app CLAUDE.md template both name this
        // family; the README carries no token list. Renaming one silently breaks
        // every app's brand file, which makes it a MAJOR version change. This test
        // is the tripwire. The five --brand-*ring*/tint/glow names stay listed even
        // though they are now derived from --brand: an app may still pin them, so
        // removing the name would still be breaking.
        string[] required =
        [
            "--brand", "--brand-hover", "--brand-active", "--brand-soft", "--brand-text",
            "--brand-tint", "--brand-ring", "--brand-ring-soft", "--brand-ring-check",
            "--brand-glow", "--accent", "--sidebar-active"
        ];

        var declared = Assets.DeclaredCustomProperties(Assets.StripComments(Assets.Css));
        var missing = required.Where(t => !declared.Contains(t)).ToList();

        Assert.True(missing.Count == 0,
            "Renaming or removing a documented override token breaks every consuming app's brand " +
            $"file. Missing: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_root_rule_parses_as_a_token_block()
    {
        // The token-block parser requires a body with no braces. CSS nesting inside a
        // :root rule is valid CSS but stops matching, and the block then silently
        // leaves the token layer: its lines re-enter the colour scan and
        // Token_blocks_declare_only_custom_properties stops seeing it at all. Fail
        // loudly here instead, so adopting nesting means fixing the parser first.
        var css = Assets.StripComments(Assets.Css);

        var opened = Regex.Matches(css, @":root(?:\[[^\]]*\])*\s*\{", RegexOptions.Compiled).Count;
        var parsed = Assets.TokenBlocks(css).Count();

        Assert.True(opened == parsed,
            $"{opened} :root rules open but {parsed} parse as token blocks. A :root rule whose body " +
            "contains a nested rule or at-rule is no longer recognised as a token block and drops out " +
            "of every token guard silently. Make the parser brace-aware before nesting inside :root.");
    }
}
