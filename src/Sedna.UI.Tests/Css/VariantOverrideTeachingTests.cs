using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A subtree that names its own variant redeclares every variant-sensitive token, so an app's own
/// value for one has to name that subtree in its selector or the library's value wins inside it.
/// Everything the library teaches an app to copy — a documentation snippet, a catalogue example —
/// writes the scope form, so an app that copies it is right without knowing why.
/// </summary>
public class VariantOverrideTeachingTests
{
    private static readonly Regex Rule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex Declared = new(@"(?<n>--[\w-]+)\s*:", RegexOptions.Compiled);
    private static readonly Regex CssFence = new(@"```css\r?\n(?<code>.*?)```", RegexOptions.Compiled | RegexOptions.Singleline);

    private const string DarkScope = ":root :where([data-variant=\"dark\"])";
    private const string LightScope = ":root [data-variant=\"light\"]";

    /// <summary>The tokens the library's scoped block declares: what a subtree redeclares.</summary>
    private static HashSet<string> Sensitive()
    {
        var css = Assets.Squash(Assets.StripComments(Assets.Css));
        var start = css.IndexOf(":root :where([data-variant]), :root {", StringComparison.Ordinal);
        Assert.True(start >= 0, "The library's scoped token block was not found.");
        var body = css[css.IndexOf('{', start)..css.IndexOf('}', start)];
        return Declared.Matches(body).Select(m => m.Groups["n"].Value).ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<(string Where, string Css)> Taught()
    {
        var docs = Path.Combine(Assets.RepoRoot, "docs");
        foreach (var file in Directory.EnumerateFiles(docs, "*.md", SearchOption.AllDirectories)
                     .Concat([Path.Combine(Assets.RepoRoot, "README.md")]))
        {
            if (!File.Exists(file)) continue;
            foreach (Match fence in CssFence.Matches(File.ReadAllText(file)))
                yield return (Path.GetRelativePath(Assets.RepoRoot, file), fence.Groups["code"].Value);
        }

        var examples = Path.Combine(Assets.RepoRoot, "src", "Sedna.UI.Catalogue", "Examples");
        foreach (var file in Directory.EnumerateFiles(examples, "*.css", SearchOption.AllDirectories))
            yield return (Path.GetRelativePath(Assets.RepoRoot, file), File.ReadAllText(file));
    }

    [Fact]
    public void Everything_the_library_teaches_names_the_subtree_where_it_overrides_a_variant_sensitive_token()
    {
        var sensitive = Sensitive();
        var offenders = new List<string>();
        var checkedRules = 0;

        foreach (var (where, css) in Taught())
        {
            foreach (Match rule in Rule.Matches(Assets.StripComments(css)))
            {
                var members = rule.Groups["selector"].Value.Split(',').Select(m => Assets.Squash(m)).ToList();
                var names = Declared.Matches(rule.Groups["body"].Value).Select(m => m.Groups["n"].Value).ToList();

                if (members.Contains(":root[data-variant=\"light\"]"))
                {
                    checkedRules++;
                    if (!members.Contains(LightScope))
                        offenders.Add($"{where}: `:root[data-variant=\"light\"]` without `{LightScope}`");
                }
                else if (members.Contains(":root") && names.Any(sensitive.Contains))
                {
                    checkedRules++;
                    if (!members.Contains(DarkScope))
                        offenders.Add($"{where}: `:root` sets {string.Join(", ", names.Where(sensitive.Contains))} without `{DarkScope}`");
                }
            }
        }

        Assert.True(checkedRules >= 3, "The scan found nothing to check, so it proves nothing.");
        Assert.True(offenders.Count == 0,
            "A subtree that names its own variant redeclares these tokens. An override an app copies from here has to list it:\n  "
            + string.Join("\n  ", offenders));
    }
}
