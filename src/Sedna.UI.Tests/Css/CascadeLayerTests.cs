using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The whole stylesheet stays inside its declared layers, or the override model breaks.
/// </summary>
public class CascadeLayerTests
{
    [Fact]
    public void Every_rule_in_the_stylesheet_is_inside_a_cascade_layer()
    {
        // The override model rests on this one invariant. An unlayered rule beats every
        // layered rule whatever its specificity — so a rule that escaped its layer
        // would outrank the entire rest of the library AND be unreachable from a
        // consuming app's own stylesheet, which is unlayered too. The app would have
        // no way to override it short of !important, which this library does not use.
        //
        // The generator wraps each part by its NN- prefix, so the only way to get here
        // is a hand edit of the generated file — which the drift guard also catches —
        // or a bug in layer_for().
        var css = Assets.StripComments(Assets.Css);

        var problems = new List<string>();
        var depth = 0;
        var line = 1;
        var atRuleStart = -1;

        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];
            if (c == '\n') { line++; continue; }

            if (c == '{')
            {
                if (depth == 0)
                {
                    var selector = css[(atRuleStart + 1)..i].Trim();
                    if (!selector.StartsWith("@layer", StringComparison.Ordinal))
                        problems.Add($"line {line}: {Assets.Squash(selector)}");
                }

                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0) atRuleStart = i;
            }
            else if (depth == 0 && c == ';')
            {
                // The `@layer a, b, c;` ordering statement, and nothing else.
                atRuleStart = i;
            }
        }

        Assert.True(problems.Count == 0,
            "Every rule must sit inside a @layer block, or it outranks the whole library and no "
            + "consuming app can override it without !important. These are at the top level:"
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    [Fact]
    public void The_layer_order_is_declared_before_any_layer_is_used()
    {
        // Without the up-front `@layer a, b, c;` statement, layer order is the order in
        // which each layer first appears — which would make it depend on the numeric
        // prefix of whichever part happens to come first, and change silently when a
        // part is added. The statement pins it.
        var css = Assets.StripComments(Assets.Css);

        var statement = Regex.Match(css, @"@layer\s+(?<names>[a-z.\s,]+?)\s*;");
        Assert.True(statement.Success, "No `@layer a, b, c;` ordering statement in the stylesheet.");

        var declared = statement.Groups["names"].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        Assert.Equal(["sedna.tokens", "sedna.base", "sedna.frame", "sedna.paint", "sedna.utilities", "sedna.overrides"],
            declared);

        var firstBlock = css.IndexOf("@layer " + declared[0] + " {", StringComparison.Ordinal);
        Assert.True(firstBlock > statement.Index,
            "The ordering statement must come before the first @layer block.");

        // Every layer that is used must be declared, or it is appended after all the
        // declared ones and silently outranks them.
        var used = Regex.Matches(css, @"@layer\s+(sedna\.[a-z]+)\s*\{")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var undeclared = used.Except(declared, StringComparer.Ordinal).ToList();
        Assert.True(undeclared.Count == 0,
            "These layers are used but not in the ordering statement, so they sort after every "
            + "declared layer: " + string.Join(", ", undeclared));
    }
}
