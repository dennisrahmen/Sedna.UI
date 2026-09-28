using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The vendored graph engine: what is there, under which licence, where it came from, and that the
/// notice a reader audits agrees with it.
/// </summary>
/// <remarks>
/// <c>build/vendor-cytoscape.sh</c> writes <c>wwwroot/lib/cytoscape/</c> and its <c>VENDORED.txt</c>;
/// these hold the committed files to what that script promises, so a file dropped in by hand — a newer
/// plugin copied from a CDN, a GPL one — fails here rather than shipping.
/// </remarks>
public class GraphEngineTests
{
    private static string LibDir => Path.Combine(Assets.ProjectDir, "wwwroot", "lib", "cytoscape");

    private static string GraphJsPath => Path.Combine(Assets.ProjectDir, "wwwroot", "js", "Sedna.UI.graph.js");

    private sealed record Vendored(string Name, string Version, string Integrity, string[] Files);

    private static List<Vendored> Manifest() =>
        File.ReadAllLines(Path.Combine(LibDir, "VENDORED.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split(' ', 4))
            .Select(p => new Vendored(p[0], p[1], p[2],
                p[3].StartsWith('(') ? [] : p[3].Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .ToList();

    private static string LicenceFile(string package) =>
        Path.Combine(LibDir, "licenses", package.TrimStart('@').Replace('/', '-') + ".txt");

    [Fact]
    public void Every_vendored_package_ships_its_own_licence_and_it_is_permissive()
    {
        // MIT's one condition on redistribution is that the notice travels with the files. And a
        // copyleft licence would pass its terms to every app that installs the package — which is
        // why cytoscape-svg, GPL-3.0, is not here.
        var problems = new List<string>();
        foreach (var package in Manifest())
        {
            var path = LicenceFile(package.Name);
            if (!File.Exists(path))
            {
                problems.Add($"{package.Name}: no licence at {Path.GetFileName(path)}");
                continue;
            }

            var text = File.ReadAllText(path);
            if (!text.Contains("Permission is hereby granted, free of charge", StringComparison.Ordinal))
                problems.Add($"{package.Name}: its licence is not the MIT text");
            if (Regex.IsMatch(text, @"GNU (Lesser |Affero )?General Public License", RegexOptions.IgnoreCase))
                problems.Add($"{package.Name}: copyleft licence");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_manifest_lists_exactly_the_files_that_ship()
    {
        // A script file under lib/cytoscape/ that the manifest does not list came from somewhere
        // other than the vendoring script, with no pinned version and no checksum behind it.
        var listed = Manifest().SelectMany(p => p.Files).OrderBy(f => f, StringComparer.Ordinal).ToList();
        var onDisk = Directory.GetFiles(LibDir, "*.js").Select(Path.GetFileName).OfType<string>()
            .OrderBy(f => f, StringComparer.Ordinal).ToList();

        Assert.Equal(listed, onDisk);
    }

    [Fact]
    public void Every_vendored_file_names_its_source_and_imports_only_its_neighbours()
    {
        // The provenance header is what lets a reader of the package — or of a browser's devtools —
        // find out what a file is without this repository. And a bare specifier ("lodash.throttle")
        // would need an import map no consuming app has: every import is a file beside it.
        var problems = new List<string>();
        foreach (var package in Manifest())
        {
            foreach (var file in package.Files)
            {
                var text = File.ReadAllText(Path.Combine(LibDir, file));
                var head = text[..Math.Min(text.Length, 600)];
                if (!head.Contains($"{package.Name} {package.Version}", StringComparison.Ordinal)
                    || !head.Contains("build/vendor-cytoscape.sh", StringComparison.Ordinal))
                    problems.Add($"{file}: no provenance header naming {package.Name} {package.Version}");

                foreach (Match m in Regex.Matches(text, @"(?:\bfrom\s*|\bimport\s*\(?\s*)(['""])([^'""]+)\1"))
                {
                    var spec = m.Groups[2].Value;
                    if (!spec.StartsWith("./", StringComparison.Ordinal))
                        problems.Add($"{file}: imports '{spec}', which is not a file beside it");
                    else if (!File.Exists(Path.Combine(LibDir, spec[2..])))
                        problems.Add($"{file}: imports '{spec}', which does not ship");
                }

                if (text.Contains('\r', StringComparison.Ordinal))
                    problems.Add($"{file}: CRLF line endings — the vendoring script writes LF");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_notices_file_names_every_vendored_package_at_its_version()
    {
        // THIRD-PARTY-NOTICES.md is what somebody auditing the package reads, and VENDORED.txt is
        // what actually ships. A version bumped in one and not the other is a notice for a file
        // that is not in the package.
        var notices = File.ReadAllText(Path.Combine(Assets.RepoRoot, "THIRD-PARTY-NOTICES.md"));
        var missing = Manifest()
            .Where(p => !Regex.IsMatch(notices, $@"\|\s*{Regex.Escape(p.Name)}\s*\|\s*{Regex.Escape(p.Version)}\s*\|"))
            .Select(p => $"{p.Name} {p.Version}")
            .ToList();

        Assert.True(missing.Count == 0,
            "THIRD-PARTY-NOTICES.md does not list these at the vendored version: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_integrity_is_a_pinned_sha512()
    {
        foreach (var package in Manifest())
            Assert.Matches(@"^sha512-[A-Za-z0-9+/]{86}==$", package.Integrity);
    }

    [Fact]
    public void The_graph_module_imports_the_engine_beside_it_and_nothing_else()
    {
        // The module is loaded by relative URL from wherever an app serves the package, so every
        // import has to be relative too — and to a file that ships, or the graph never starts.
        var js = File.ReadAllText(GraphJsPath);
        var statics = Regex.Matches(js, @"^\s*import\s+[^'""]*['""]([^'""]+)['""]", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["../lib/cytoscape/cytoscape.js"], statics);

        // Plugins by name, resolved against the same folder when a graph first needs one.
        var plugins = Regex.Matches(js, @"^\s*(\w+):\s*'(cytoscape-[a-z-]+\.js)',", RegexOptions.Multiline)
            .Select(m => m.Groups[2].Value).ToList();
        Assert.NotEmpty(plugins);
        foreach (var plugin in plugins)
            Assert.True(File.Exists(Path.Combine(LibDir, plugin)), $"The module loads {plugin}, which does not ship");
    }

    [Fact]
    public void The_graph_module_names_nothing_real()
    {
        // The same shapes the other two scripts are held to: the module ships to every app that
        // installs the package, comments included.
        var found = RealWorldShapes.FoundIn(File.ReadAllText(GraphJsPath));
        Assert.True(found.Count == 0, $"Sedna.UI.graph.js names something real: {string.Join(", ", found)}");
    }

    // Colour is held for every surface at once: SurfaceRegistryTests.A_surface_script_names_no_colour.
}
