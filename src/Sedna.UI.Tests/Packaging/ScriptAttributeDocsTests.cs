using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every <c>data-*</c> attribute the scripts read from the app's markup is documented — in a
/// catalogue page or example, or in <c>docs/</c> — so a hook an app can use is never one it can
/// only find by reading the script.
/// </summary>
/// <remarks>
/// An attribute the script writes itself is its own state, not a hook, and is left out. The
/// served bundles are read, which have no comments, so an example in a comment is not counted
/// as a use.
/// </remarks>
public class ScriptAttributeDocsTests
{
    /// <summary>
    /// Attributes in the code that are neither the app's hooks nor written by name: the graph
    /// engine's own canvas layers, and marks the script writes through a constant.
    /// </summary>
    private static readonly HashSet<string> NotHooks = new(StringComparer.Ordinal)
    {
        "data-id",              // cytoscape's own canvas layers
        "data-sedna-closing",   // 42-modal-exit.js, through HELD
        "data-sedna-inert",     // 21-dependent.js, through MARK
    };

    [Fact]
    public void Every_attribute_the_scripts_read_from_the_app_is_documented()
    {
        var code = string.Concat(new[] { "Sedna.UI.js", "Sedna.UI.graph.js", "Sedna.UI.editor.js" }
            .Select(f => File.ReadAllText(Path.Combine(Assets.WwwrootDir, "js", f))));

        // A name followed by a hyphen is a prefix the script builds names from, 'data-app-' + name.
        var used = Regex.Matches(code, @"data-[a-z]+(?:-[a-z]+)*(?![a-z-])").Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        var written = Regex.Matches(code, @"(?:setAttribute|removeAttribute|toggleAttribute)\(\s*['""](?<a>data-[a-z-]+)['""]")
            .Select(m => m.Groups["a"].Value).ToHashSet(StringComparer.Ordinal);
        var hooks = used.Except(written).Except(NotHooks).ToList();
        Assert.NotEmpty(hooks);

        var docs = string.Concat(
            new[] { Path.Combine(Assets.RepoRoot, "docs") }
                .SelectMany(d => Directory.EnumerateFiles(d, "*.md", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(Path.Combine(Assets.RepoRoot, "src", "Sedna.UI.Catalogue", "Examples"), "*", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(Path.Combine(Assets.RepoRoot, "src", "Sedna.UI.Catalogue", "Components", "Pages"), "*.razor"))
                .Select(File.ReadAllText));

        var missing = hooks.Where(a => !Regex.IsMatch(docs, Regex.Escape(a) + @"(?![a-z-])")).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0,
            "The scripts read these attributes from the app's markup and no catalogue page, example or doc names them: "
            + string.Join(", ", missing));
    }
}

/// <summary>
/// Every member the script puts on <c>sednaUi</c> has a row in the JavaScript table of
/// <c>docs/architecture.md</c>, which is where an app finds what it may call.
/// </summary>
public class ScriptMemberDocsTests
{
    [Fact]
    public void Every_public_script_member_has_a_row_in_the_architecture_table()
    {
        var parts = Directory.GetFiles(Path.Combine(Assets.ProjectDir, "js-parts"), "*.js");
        var members = parts
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"^\s*ui\.(?<m>[a-zA-Z]+)\s*=", RegexOptions.Multiline))
            .Select(m => m.Groups["m"].Value)
            .Where(m => m != "_")
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(members);

        var doc = File.ReadAllText(Path.Combine(Assets.RepoRoot, "docs", "architecture.md"));
        var missing = members.Where(m => !Regex.IsMatch(doc, $@"^\|[^|\n]*`{m}[`(.]", RegexOptions.Multiline)).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0,
            "These sednaUi members have no row in docs/architecture.md's JavaScript table: " + string.Join(", ", missing));
    }
}
