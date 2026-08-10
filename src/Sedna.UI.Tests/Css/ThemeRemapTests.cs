using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A theme only remaps tokens, which is what keeps CSS load order irrelevant.
/// </summary>
public class ThemeRemapTests
{
    [Fact]
    public void Theme_blocks_only_remap_tokens_and_never_override_selectors()
    {
        var css = Assets.StripComments(Assets.Css);

        // A rule scoped to an appearance attribute that also targets a descendant
        // means a value escaped the token layer. Density is exempt: it changes
        // geometry (table padding), which is not a colour and not a token.
        //
        // The attribute list is an allowlist rather than `data-[a-z-]+`, so that
        // ordinary attribute selectors (`[data-tip]`) are not swept up. ADD ANY NEW
        // APPEARANCE ATTRIBUTE HERE — a theme this test does not know about is a
        // theme that may quietly override selectors. `:root` is optional because
        // `[data-theme="light"] .btn { }` is the same mistake written shorter.
        var offenders = Regex.Matches(
                css,
                @"(?::root)?(?:\[[^\]]*\])*\[data-(?:theme|cvd|contrast)=[^\]]*\](?:\[[^\]]*\])*\s+[^{,]+\{",
                RegexOptions.Compiled)
            .Select(m => m.Value.Trim())
            .ToList();

        Assert.True(offenders.Count == 0,
            "The light and colour-blind themes must be pure token remapping — that is what keeps " +
            "load order from being load-bearing. Express these as tokens instead: " +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Fact]
    public void Appearance_media_queries_only_remap_tokens()
    {
        // The light and colour-blind themes are pure token remaps, and that is what
        // makes CSS load order irrelevant. A media query that varies *appearance* is
        // the same kind of block and carries the same obligation — with the sharper
        // edge that @media adds source order without adding specificity, which is
        // exactly how the five light-theme cascade bugs found migrating AI_Console
        // outranked the semantic rules above them.
        //
        // Layout media queries (min-width / max-width / orientation / print) are NOT
        // covered: a responsive frame has to move real selectors, and a print sheet
        // has to hide chrome. Only appearance is constrained.
        string[] appearance = ["prefers-color-scheme", "prefers-contrast", "forced-colors"];

        var css = Assets.StripComments(Assets.Css);

        var offenders = new List<string>();
        foreach (var (condition, body) in Assets.MediaBlocks(css))
        {
            if (!appearance.Any(f => condition.Contains(f, StringComparison.OrdinalIgnoreCase)))
                continue;

            foreach (var (selector, ruleBody) in Assets.TopLevelRules(body))
            {
                // A token block is `:root` plus attribute filters and NOTHING else.
                // StartsWith(":root") would wave through `:root .btn` and
                // `:root[data-theme="light"] .table`, which are the very overrides
                // this guard exists to catch.
                if (Regex.IsMatch(selector, @"^:root(?:\[[^\]]*\])*$")) continue;

                // forced-colors is the one appearance query with legitimate non-token
                // rules, and there are exactly two kinds.
                //
                // `forced-color-adjust` opts an element out of the forced palette,
                // for the few things whose actual colour IS the content — a status
                // dot is not a coloured label, it is the label.
                //
                // `outline` restates a focus ring. Every ring in this library is a
                // box-shadow, and forced colours does not paint box-shadow at all —
                // so without these rules the library becomes unusable by keyboard for
                // exactly the people most likely to be in that mode. An outline is
                // what the mode does paint, and it cannot be expressed as a token.
                string[] permitted = ["forced-color-adjust", "outline"];

                var declarations = ruleBody
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(d => d.Length > 0)
                    .ToList();

                if (declarations.Count > 0 && declarations.TrueForAll(
                        d => permitted.Any(p => d.StartsWith(p, StringComparison.Ordinal))))
                    continue;

                offenders.Add($"@media {condition} → {selector}");
            }
        }

        Assert.True(offenders.Count == 0,
            "An appearance media query must remap tokens on :root, not restyle selectors — otherwise " +
            "load order becomes load-bearing again. Express the difference as token values. The only " +
            "permitted exception is a rule whose declarations are all forced-color-adjust:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }
}
