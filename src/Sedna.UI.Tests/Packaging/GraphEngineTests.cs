using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The graph module and the engine beside it: what it imports, and the plugins it loads by name.
/// </summary>
/// <remarks>
/// What is vendored, under which licence and from where, is <see cref="VendoredEngineTests"/>'s, for
/// every engine at once.
/// </remarks>
public class GraphEngineTests
{
    private static string LibDir => Path.Combine(Assets.ProjectDir, "wwwroot", "lib", "cytoscape");

    private static string GraphJsPath => Path.Combine(Assets.ProjectDir, "wwwroot", "js", "Sedna.UI.graph.js");

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
