using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A subtree that names its own variant redeclares the variant-sensitive tokens, through a
/// scope form added to each token block that remaps them. The scope form is part of the token
/// contract, so it is held the way the blocks are: tokens and nothing else, mirroring every
/// block, and exactly the tokens that need it.
/// </summary>
public class ScopedVariantTests
{
    private static readonly Regex Rule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex ScopeMember = new(
        @"^:root(?<attrs>(?:\[[^\]]*\])*) (?::where\(\[data-variant\]\)|\[data-variant=""light""\])$", RegexOptions.Compiled);
    private static readonly Regex RootMember = new(@"^:root(?<attrs>(?:\[[^\]]*\])*)$", RegexOptions.Compiled);

    private static string Css => Assets.StripComments(Assets.Css);

    private static IEnumerable<(string[] Members, string Body)> Rules(string css) =>
        Rule.Matches(css).Select(m => (
            m.Groups["selector"].Value.Split(',').Select(s => Assets.Squash(s).Trim()).ToArray(),
            m.Groups["body"].Value));

    [Fact]
    public void A_scope_form_belongs_to_a_token_block_and_ends_with_its_page_form()
    {
        var offenders = new List<string>();
        var found = 0;
        foreach (var (members, body) in Rules(Css))
        {
            var scopes = members.Where(m => ScopeMember.IsMatch(m)).ToList();
            if (scopes.Count == 0) continue;
            found++;

            // The page's own form comes last: every reader of the token blocks finds a block by
            // its `:root` selector, and a list that ends in a scope form would hide it from them.
            if (!RootMember.IsMatch(members[^1]))
                offenders.Add($"{string.Join(", ", members)} — does not end with the page's own form");

            var declarations = body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var d in declarations.Where(d => !d.StartsWith("--", StringComparison.Ordinal)))
                offenders.Add($"{string.Join(", ", members)} — declares `{d}`");

            // And it mirrors one page form, so the two have the same specificity and the cascade
            // between blocks is the same inside a subtree as on the page.
            foreach (var scope in scopes)
            {
                var attrs = ScopeMember.Match(scope).Groups["attrs"].Value;
                var wantsLight = scope.EndsWith("[data-variant=\"light\"]", StringComparison.Ordinal);
                var page = wantsLight
                    ? ":root[data-variant=\"light\"]" + attrs
                    : ":root" + attrs;
                if (!members.Contains(page))
                    offenders.Add($"{scope} has no page form {page} in the same rule");
            }
        }

        Assert.True(found >= 8, $"Expected the scope forms of the base, light, colour-blind and contrast blocks, found {found}.");
        Assert.True(offenders.Count == 0,
            "A subtree's scope form is tokens only, listed with the page form it mirrors:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Every_variant_block_has_a_scope_form()
    {
        var css = Css;
        var offenders = new List<string>();
        foreach (var (members, _) in Rules(css))
        {
            foreach (var member in members)
            {
                var root = RootMember.Match(member);
                if (!root.Success) continue;
                var attrs = root.Groups["attrs"].Value;
                if (!Regex.IsMatch(attrs, @"data-(variant|cvd|contrast)=")) continue;

                var withoutVariant = attrs.Replace("[data-variant=\"light\"]", "", StringComparison.Ordinal);
                var scope = attrs.Contains("[data-variant=\"light\"]", StringComparison.Ordinal)
                    ? ":root" + withoutVariant + " [data-variant=\"light\"]"
                    : ":root" + attrs + " :where([data-variant])";
                if (!members.Contains(scope))
                    offenders.Add($"{member} has no scope form {scope}");
            }
        }

        Assert.True(offenders.Count == 0,
            "A block that remaps tokens by variant, colour-blind palette or contrast has to be reachable by a subtree that names a variant:\n  "
            + string.Join("\n  ", offenders.Distinct()));
    }

    [Fact]
    public void The_scoped_token_block_holds_exactly_the_variant_sensitive_tokens()
    {
        // Sensitive: remapped by any variant, colour-blind, contrast or forced-colours block, or
        // reading a token that is. A derived token resolves where it is declared, so it has to be
        // declared in the subtree; and a token that is not sensitive stays out of it, because
        // redeclaring one would drop an app's own override and the page's density.
        var css = Css;
        var remapped = new HashSet<string>(StringComparer.Ordinal);
        var scopedBase = new Dictionary<string, string>(StringComparer.Ordinal);
        var plain = new Dictionary<string, string>(StringComparer.Ordinal);

        var outsideMedia = StripAtBlocks(css, "@media", out var media);
        foreach (var (members, body) in Rules(outsideMedia))
        {
            var decls = Declarations(body).ToList();
            var isScopedBase = members.Contains(":root :where([data-variant])");
            var isPlainRoot = members.Length == 1 && members[0] == ":root";
            var isAttributeForm = members.Any(m => RootMember.IsMatch(m) && m != ":root" && Regex.IsMatch(m, @"data-(variant|cvd|contrast)="));
            foreach (var (name, value) in decls)
            {
                if (isScopedBase) scopedBase[name] = value;
                else if (isPlainRoot) plain[name] = value;
                if (isAttributeForm) remapped.Add(name);
            }
        }
        foreach (var (condition, body) in media)
        {
            if (!condition.Contains("forced-colors", StringComparison.Ordinal) && !condition.Contains("prefers-contrast", StringComparison.Ordinal)) continue;
            foreach (var (members, ruleBody) in Rules(body))
                if (members.Contains(":root"))
                    foreach (var (name, _) in Declarations(ruleBody)) remapped.Add(name);
        }

        var sensitive = new HashSet<string>(remapped.Where(t => scopedBase.ContainsKey(t) || plain.ContainsKey(t)));
        bool changed;
        do
        {
            changed = false;
            foreach (var (name, value) in scopedBase.Concat(plain))
            {
                if (sensitive.Contains(name)) continue;
                if (Regex.Matches(value, @"var\((?<n>--[\w-]+)").Any(m => sensitive.Contains(m.Groups["n"].Value)))
                    changed |= sensitive.Add(name);
            }
        } while (changed);

        var stale = plain.Keys.Where(sensitive.Contains).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var extra = scopedBase.Keys.Where(n => !sensitive.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(stale.Count == 0,
            "These tokens differ by variant, colour-blind palette, contrast or forced colours, or read one that does, and are "
            + "declared in the plain :root block — a subtree that names a variant would keep the page's value. Move them into the "
            + "block whose selector lists `:root :where([data-variant])`:\n  " + string.Join(", ", stale));
        Assert.True(extra.Count == 0,
            "These tokens are in the scoped block and nothing about them differs by variant. Redeclared in a subtree they would drop "
            + "an app's own override of them. Move them into the plain :root block:\n  " + string.Join(", ", extra));
    }

    private static IEnumerable<(string Name, string Value)> Declarations(string body) =>
        Regex.Matches(body, @"(?<n>--[\w-]+)\s*:\s*(?<v>[^;]+);")
            .Select(m => (m.Groups["n"].Value, m.Groups["v"].Value.Trim()));

    /// <summary>The stylesheet without its at-rule blocks of one kind, and those blocks apart.</summary>
    private static string StripAtBlocks(string css, string at, out List<(string Condition, string Body)> blocks)
    {
        blocks = [];
        var kept = new System.Text.StringBuilder();
        var i = 0;
        while (i < css.Length)
        {
            var start = css.IndexOf(at, i, StringComparison.Ordinal);
            if (start < 0) { kept.Append(css, i, css.Length - i); break; }
            kept.Append(css, i, start - i);
            var open = css.IndexOf('{', start);
            var depth = 0;
            var j = open;
            for (; j < css.Length; j++)
            {
                if (css[j] == '{') depth++;
                else if (css[j] == '}' && --depth == 0) break;
            }
            blocks.Add((css[(start + at.Length)..open].Trim(), css[(open + 1)..j]));
            i = j + 1;
        }
        return kept.ToString();
    }
}
