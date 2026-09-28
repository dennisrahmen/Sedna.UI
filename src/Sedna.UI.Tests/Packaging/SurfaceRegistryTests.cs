using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Tier 3 is the one place the library draws, and <c>docs/surfaces.md</c> is its list. These hold the
/// list to the files and the files to the list: a surface cannot exist without a row, a row cannot
/// exist without what it names, and every row meets the contract a test can check.
/// </summary>
/// <remarks>
/// The catalogue half — each surface's pages and their tier 3 badge — is <c>SurfacePageTests</c>
/// in the catalogue's tests, which read the same table.
/// </remarks>
public class SurfaceRegistryTests
{
    private static readonly string[] Deliveries = ["drawn", "shipped", "styled"];

    /// <summary>
    /// Elements a script outside tier 3 may create, because a reader never sees one: an anchor
    /// clicked and removed at once to navigate, a textarea the clipboard copies from.
    /// </summary>
    private static readonly string[] InvisibleHelpers = ["a", "textarea"];

    private static string Lib(string relative) =>
        Path.Combine(Assets.ProjectDir, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Css(string part) => Path.Combine(Assets.ProjectDir, "css-parts", part);

    /// <summary>Every script file a row owns: a js-part itself, or each file of a parts directory.</summary>
    private static IEnumerable<string> ScriptFiles(Surfaces.Surface s) =>
        s.Scripts.SelectMany(p => p.EndsWith('/')
            ? Directory.GetFiles(Lib(p), "*.js")
            : [Lib(p)]);

    /// <summary>
    /// Code with its comments blanked, line numbers kept — prose about <c>innerHTML</c> is not a
    /// use of it. A <c>//</c> counts as a comment only at the start of a line or after whitespace, so
    /// the one inside <c>'https://…'</c> is left alone.
    /// </summary>
    private static string[] CodeLines(string path)
    {
        var text = Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/",
            m => new string('\n', m.Value.Count(c => c == '\n')), RegexOptions.Singleline);
        return text.Split('\n').Select(l => Regex.Replace(l, @"(^|\s)//.*$", "$1")).ToArray();
    }

    [Fact]
    public void The_registry_names_each_surface_once_with_a_known_delivery()
    {
        Assert.NotEmpty(Surfaces.All);
        Assert.Equal(Surfaces.All.Count, Surfaces.All.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var s in Surfaces.All)
            Assert.True(Deliveries.Contains(s.Delivery),
                $"{s.Name}: delivery \"{s.Delivery}\" is not one of {string.Join(", ", Deliveries)}");
    }

    [Fact]
    public void Every_file_a_row_names_exists()
    {
        var problems = new List<string>();
        foreach (var s in Surfaces.All)
        {
            if (s.Stylesheet is null || !File.Exists(Css(s.Stylesheet)))
                problems.Add($"{s.Name}: stylesheet part {s.Stylesheet ?? "(none)"} is not in css-parts/");

            foreach (var script in s.Scripts)
                if (script.EndsWith('/') ? !Directory.Exists(Lib(script)) : !File.Exists(Lib(script)))
                    problems.Add($"{s.Name}: script {script} is not in src/Sedna.UI/");

            if (s.Engine is not null && !Directory.Exists(Lib(s.Engine)))
                problems.Add($"{s.Name}: engine {s.Engine} is not in src/Sedna.UI/");

            // The reference, and the heading its anchor points at.
            var target = s.Reference.Split('#', 2);
            var doc = target[0].Length == 0 ? Surfaces.DocPath : Path.Combine(Assets.RepoRoot, "docs", target[0]);
            if (!File.Exists(doc))
            {
                problems.Add($"{s.Name}: reference {s.Reference} does not exist");
                continue;
            }

            if (target.Length == 2)
            {
                var anchors = File.ReadAllLines(doc)
                    .Where(l => l.StartsWith('#'))
                    .Select(l => Regex.Replace(l.TrimStart('#').Trim().ToLowerInvariant(), @"[^\p{L}\p{N} -]", string.Empty).Replace(' ', '-'));
                if (!anchors.Contains(target[1]))
                    problems.Add($"{s.Name}: reference {s.Reference} names no heading in {Path.GetFileName(doc)}");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_delivery_decides_what_a_row_carries()
    {
        // drawn: the library's script and nothing behind it. shipped: an engine as well. styled: the
        // app brings the engine, so the library carries a stylesheet and none of the engine's code.
        var problems = new List<string>();
        foreach (var s in Surfaces.All)
        {
            switch (s.Delivery)
            {
                case "drawn" when s.Scripts.Count == 0 || s.Engine is not null:
                    problems.Add($"{s.Name}: a drawn surface has a script and no engine");
                    break;
                case "shipped" when s.Scripts.Count == 0 || s.Engine is null:
                    problems.Add($"{s.Name}: a shipped surface has a script and an engine");
                    break;
                case "styled" when s.Scripts.Count != 0 || s.Engine is not null:
                    problems.Add($"{s.Name}: a styled surface ships a stylesheet and no code");
                    break;
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Only_a_surface_script_draws()
    {
        // Rule 1 of the whole library: markup belongs to the app. A script part that builds an
        // element a reader sees, or writes markup as a string, is drawing — and drawing is tier 3.
        var owned = Surfaces.All.SelectMany(ScriptFiles).Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        foreach (var file in Directory.GetFiles(Lib("js-parts/"), "*.js").Where(f => !owned.Contains(Path.GetFullPath(f))))
        {
            var lines = CodeLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match m in Regex.Matches(lines[i], @"createElement(?:NS)?\(\s*(?:[^,()]+,\s*)?['""]([a-z0-9-]+)['""]"))
                    if (!InvisibleHelpers.Contains(m.Groups[1].Value))
                        problems.Add($"{Path.GetFileName(file)}:{i + 1}: creates <{m.Groups[1].Value}>");
                if (Regex.IsMatch(lines[i], @"\.(innerHTML|outerHTML)\s*[+]?=|insertAdjacentHTML|document\.write"))
                    problems.Add($"{Path.GetFileName(file)}:{i + 1}: writes markup");
            }
        }

        Assert.True(problems.Count == 0,
            "A script part outside tier 3 draws. Move the markup into the app, or propose a surface in docs/surfaces.md:"
            + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void A_surface_script_writes_no_markup_as_a_string()
    {
        // Contract rule 2: the frame around a surface is the app's markup, filled with textContent.
        // The one string of markup a surface writes is a document rendered from the app's own text,
        // escaped first — the Markdown preview's `innerHTML = render(…)`.
        var problems = new List<string>();
        foreach (var s in Surfaces.All)
            foreach (var file in ScriptFiles(s))
            {
                var lines = CodeLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (Regex.IsMatch(line, @"outerHTML|insertAdjacentHTML|document\.write|createContextualFragment|DOMParser"))
                        problems.Add($"{s.Name}: {Path.GetFileName(file)}:{i + 1}: {line.Trim()}");
                    else if (Regex.IsMatch(line, @"\.innerHTML\s*[+]?=") && !Regex.IsMatch(line, @"\.innerHTML\s*=\s*[\w.]*\brender\("))
                        problems.Add($"{s.Name}: {Path.GetFileName(file)}:{i + 1}: {line.Trim()}");
                }
            }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void A_surface_script_names_no_colour()
    {
        // Contract rule 3: every colour is a token the browser resolves, so the variant, the
        // colour-vision and contrast settings, forced colours and an app's brand all reach the
        // surface. A literal in the script would reach it unchanged by any of them.
        var literal = new Regex(
            @"['""`]#[0-9a-fA-F]{3,8}['""`]|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(\s*[\d.]|\bcolor\(\s*(?:srgb|display-p3)",
            RegexOptions.IgnoreCase);
        var problems = new List<string>();
        foreach (var s in Surfaces.All)
            foreach (var file in ScriptFiles(s))
            {
                var lines = CodeLines(file);
                for (var i = 0; i < lines.Length; i++)
                    if (literal.IsMatch(lines[i]))
                        problems.Add($"{s.Name}: {Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }

        Assert.True(problems.Count == 0, "Read a token instead:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void A_shipped_engine_is_vendored_noticed_and_permissive()
    {
        // Contract rule 7, for every engine rather than the one there is today: a pinned manifest, a
        // vendoring script that writes it, a licence beside each package — none of them copyleft —
        // and the notice an auditor reads naming each at the version that ships.
        var notices = File.ReadAllText(Path.Combine(Assets.RepoRoot, "THIRD-PARTY-NOTICES.md"));
        var vendorScripts = Directory.GetFiles(Path.Combine(Assets.RepoRoot, "build"), "vendor-*.sh").Select(File.ReadAllText).ToList();
        var problems = new List<string>();
        foreach (var s in Surfaces.All.Where(s => s.Engine is not null))
        {
            var dir = Lib(s.Engine!);
            var manifest = Path.Combine(dir, "VENDORED.txt");
            if (!File.Exists(manifest))
            {
                problems.Add($"{s.Name}: {s.Engine} has no VENDORED.txt");
                continue;
            }

            var engineDir = "src/Sedna.UI/" + s.Engine!.TrimEnd('/');
            if (!vendorScripts.Any(v => v.Contains(engineDir, StringComparison.Ordinal)))
                problems.Add($"{s.Name}: no build/vendor-*.sh writes {engineDir}");

            foreach (var entry in File.ReadAllLines(manifest).Where(l => l.Length > 0 && !l.StartsWith('#')).Select(l => l.Split(' ')))
            {
                var (name, version) = (entry[0], entry[1]);
                var licence = Path.Combine(dir, "licenses", name.TrimStart('@').Replace('/', '-') + ".txt");
                if (!File.Exists(licence))
                    problems.Add($"{s.Name}: {name} has no licence under licenses/");
                else if (Regex.IsMatch(File.ReadAllText(licence), @"GNU (Lesser |Affero )?General Public License|Mozilla Public License|Eclipse Public License", RegexOptions.IgnoreCase))
                    problems.Add($"{s.Name}: {name} is under a copyleft licence");
                if (!Regex.IsMatch(notices, $@"\|\s*{Regex.Escape(name)}\s*\|\s*{Regex.Escape(version)}\s*\|"))
                    problems.Add($"{s.Name}: THIRD-PARTY-NOTICES.md does not list {name} {version}");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void A_shipped_engine_is_loaded_on_demand_by_the_library_alone()
    {
        // Contract rule 4: nothing is added to the host page, and a page without the surface never
        // downloads its engine. The main script imports the surface's module by relative URL when
        // one is shown; only that module imports the engine.
        var mainJs = File.ReadAllText(Assets.JsPath);
        var hostPage = File.ReadAllText(Path.Combine(Assets.RepoRoot, "docs", "getting-started.md"));
        var problems = new List<string>();
        foreach (var s in Surfaces.All.Where(s => s.Engine is not null))
        {
            var engine = s.Engine!.TrimEnd('/');
            var libPath = engine["wwwroot/".Length..];

            if (mainJs.Contains(libPath, StringComparison.Ordinal))
                problems.Add($"{s.Name}: Sedna.UI.js names {libPath} — the engine must load through the surface's module");
            if (hostPage.Contains(libPath, StringComparison.Ordinal))
                problems.Add($"{s.Name}: getting-started.md puts {libPath} on the host page");

            // The module: `graph-parts/` is generated into wwwroot/js/Sedna.UI.graph.js.
            var parts = s.Scripts.SingleOrDefault(p => p.EndsWith("-parts/", StringComparison.Ordinal));
            if (parts is null)
            {
                problems.Add($"{s.Name}: a shipped surface is an ES module generated from a <name>-parts/ directory");
                continue;
            }

            var module = "Sedna.UI." + parts[..^"-parts/".Length] + ".js";
            var modulePath = Path.Combine(Assets.WwwrootDir, "js", module);
            if (!File.Exists(modulePath))
            {
                problems.Add($"{s.Name}: {module} is not generated");
                continue;
            }

            if (!Regex.IsMatch(mainJs, $@"new URL\(\s*'{Regex.Escape(module)}'") || !mainJs.Contains("import(", StringComparison.Ordinal))
                problems.Add($"{s.Name}: Sedna.UI.js does not import {module} by relative URL");
            if (hostPage.Contains(module, StringComparison.Ordinal))
                problems.Add($"{s.Name}: getting-started.md puts {module} on the host page");
            if (!File.ReadAllText(modulePath).Contains("../" + libPath, StringComparison.Ordinal))
                problems.Add($"{s.Name}: {module} does not import from ../{libPath}");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
