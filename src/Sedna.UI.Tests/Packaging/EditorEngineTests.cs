using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The editor module and the engine beside it: what it imports, and what it ships to every app.
/// </summary>
/// <remarks>
/// What is vendored, under which licence and from where, is <see cref="VendoredEngineTests"/>'s, for
/// every engine at once.
/// </remarks>
public class EditorEngineTests
{
    private static string EditorJsPath => Path.Combine(Assets.ProjectDir, "wwwroot", "js", "Sedna.UI.editor.js");

    [Fact]
    public void The_editor_module_imports_quill_beside_it_and_nothing_else()
    {
        // Loaded by relative URL from wherever an app serves the package, so its import is relative
        // too — and to the file that ships, or no editor ever starts.
        var js = File.ReadAllText(EditorJsPath);
        var imports = Regex.Matches(js, @"^\s*import\s+[^'""]*['""]([^'""]+)['""]", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["../lib/quill/quill.js"], imports);
        Assert.True(File.Exists(Path.Combine(Assets.WwwrootDir, "lib", "quill", "quill.js")));
        // No module loaded later, by name. Quill.import(…) is Quill's own registry, not a module.
        Assert.DoesNotMatch(@"(?<![.\w])import\s*\(", js);
    }

    [Fact]
    public void The_editor_module_names_nothing_real()
    {
        var found = RealWorldShapes.FoundIn(File.ReadAllText(EditorJsPath));
        Assert.True(found.Count == 0, $"Sedna.UI.editor.js names something real: {string.Join(", ", found)}");
    }
}
