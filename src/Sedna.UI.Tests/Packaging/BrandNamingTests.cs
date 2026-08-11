using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// No trace of the pre-Sedna brand survives in anything that ships.
/// </summary>
/// <remarks>
/// The rename moved a class name, a keyframes name, a DOM id, a cascade layer and a
/// storage prefix. Every one of those fails SILENTLY when only one side of a pair
/// moves: an unmatched keyframes name disables an animation, a stale utility class
/// renders unstyled, a stale storage prefix loses a stored setting. This is the cheap
/// check that stops one returning through a copy-pasted part.
///
/// Deliberately not repository-wide: docs/migrating-to-sedna-ui.md's body names the
/// old brand throughout, in order to describe it, and <c>docs/</c> is out of scope
/// for exactly that reason.
///
/// The two test projects — <c>Sedna.UI.Tests</c> and <c>Sedna.UI.Catalogue.Tests</c> —
/// are themselves outside both guards' scope, and that is deliberate too: this very
/// file's own doc comments and its <see cref="OldBrand"/> regex literal have to name
/// the old brand to describe and detect it, so scanning the test projects would make
/// the guard trip on its own documentation.
/// </remarks>
public class BrandNamingTests
{
    /// <summary>
    /// The old brand's markers. <c>dr-</c> carries a word boundary so
    /// <c>--dialog-backdrop</c> and friends are not false positives.
    /// </summary>
    /// <remarks>
    /// <c>DR\\?\.Simple_UI</c> tolerates an optional literal backslash before the dot,
    /// so it catches both <c>DR.Simple_UI</c> and the escaped spelling
    /// (<c>DR\.Simple_UI</c>) that a grep, <c>perl -pi</c> or C# regex literal writes
    /// when it needs the dot to be literal — exactly the shape of a real leftover
    /// this rebrand hit once already (an escaped verification pattern that survived a
    /// sweep). Without this, that spelling reappearing in a catalogue example or a
    /// shipped comment would pass silently.
    /// </remarks>
    internal static readonly Regex OldBrand = new(
        @"\bdr-|DR\\?\.Simple_UI|DrSimpleUi|drSimpleUi|drui\.|@layer\s+dr\.|\bdr\.(tokens|base|frame|paint|utilities|overrides)\b",
        RegexOptions.Compiled);

    public static IEnumerable<object[]> ShippedAssets() =>
    [
        [Assets.CssPath], [Assets.JsPath], [Assets.BootJsPath], [Assets.TokensPath]
    ];

    [Theory]
    [MemberData(nameof(ShippedAssets))]
    public void No_shipped_asset_carries_the_old_brand(string path) => AssertClean(path);

    /// <summary>
    /// Every <c>.cs</c> file in the library project, build output excluded.
    /// </summary>
    /// <remarks>
    /// <see cref="ShippedAssets"/> only covers the four generated static files, but
    /// this source compiles into the shipped <c>Sedna.UI.dll</c>, and
    /// <c>GenerateDocumentationFile</c> packs its XML doc comments into
    /// <c>Sedna.UI.xml</c> inside the .nupkg. A stale <c>&lt;see cref="DrSimpleUiOptions"/&gt;</c>
    /// would ship in both without either static-asset check ever seeing it.
    /// <c>bin/</c> and <c>obj/</c> are excluded: they hold MSBuild-generated
    /// intermediates (<c>*.AssemblyInfo.cs</c>, <c>*.GlobalUsings.g.cs</c>), not
    /// source, and scanning them would make this test's outcome depend on which
    /// configurations happen to have been built.
    /// </remarks>
    public static IEnumerable<object[]> LibrarySourceFiles() =>
        Directory.EnumerateFiles(Assets.ProjectDir, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(path => new object[] { path });

    [Theory]
    [MemberData(nameof(LibrarySourceFiles))]
    public void No_library_source_file_carries_the_old_brand(string path) => AssertClean(path);

    /// <summary>
    /// Every build script, minus one permanent exception.
    /// </summary>
    /// <remarks>
    /// This is where the motivating near-miss for the escaped-dot widening on
    /// <see cref="OldBrand"/> actually happened: an escaped
    /// <c>'DR\.Simple_UI\.Catalogue'</c> pattern in <c>build/verify-package.sh</c>
    /// once left a package-leak guard printing "ok" while missing what it was meant
    /// to catch. The widened regex only helps if something actually reads these
    /// files with it.
    /// </remarks>
    public static IEnumerable<object[]> BuildScripts() =>
        Directory.EnumerateFiles(Path.Combine(Assets.RepoRoot, "build"), "*.sh")
            .Where(path => Path.GetFileName(path) != PermanentlyExemptBuildScript)
            .Select(path => new object[] { path });

    /// <summary>
    /// <c>build/css-path.sh</c> hard-codes
    /// <c>src/DR.Simple_UI/wwwroot/css/DR.Simple_UI.css</c> as the path the
    /// stylesheet lived at before the rename (see that file's own header comment) —
    /// permanently, because an old git tag needs its old path to keep resolving.
    /// This exception never retires: unlike a scan that catches a leftover and gets
    /// cleaned up once the rename is complete, this file's job is to keep naming the
    /// old brand forever, for every future git tag older than the rename.
    /// </summary>
    private const string PermanentlyExemptBuildScript = "css-path.sh";

    [Theory]
    [MemberData(nameof(BuildScripts))]
    public void No_build_script_carries_the_old_brand(string path) => AssertClean(path);

    /// <summary>
    /// The fix for a match in a generated bundle lives upstream, in its part files —
    /// never in the bundle itself, which the CLAUDE.md Global Constraint forbids
    /// hand-editing.
    /// </summary>
    private static readonly Dictionary<string, string> BundleFixHints = new(StringComparer.Ordinal)
    {
        [Assets.CssPath] = "Fix the source in css-parts/, then rerun build/bundle-css.sh — never edit this generated file directly.",
        [Assets.JsPath] = "Fix the source in js-parts/, then rerun build/bundle-js.sh — never edit this generated file directly.",
    };

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    private static void AssertClean(string path)
    {
        var found = OldBrand.Matches(File.ReadAllText(path))
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var hint = BundleFixHints.TryGetValue(path, out var text) ? " " + text : "";

        Assert.True(found.Count == 0,
            $"{Path.GetFileName(path)} still carries: {string.Join(", ", found)}.{hint}");
    }
}
