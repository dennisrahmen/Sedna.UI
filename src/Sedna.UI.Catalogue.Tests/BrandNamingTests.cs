using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// No catalogue example, page or registry entry carries the pre-Sedna brand.
/// </summary>
/// <remarks>
/// An example using a class the stylesheet no longer defines renders UNSTYLED, with
/// no error anywhere. CoverageTests catches the same mistake from the other
/// direction — a sedna-* class no example mentions fails as undocumented — but only
/// while no other example happens to use it.
///
/// The two test projects — <c>Sedna.UI.Tests</c> and <c>Sedna.UI.Catalogue.Tests</c> —
/// are themselves outside both guards' scope, and that is deliberate: this file's own
/// doc comments and its <see cref="OldBrand"/> regex literal have to name the old
/// brand to describe and detect it, so scanning the test projects would make the
/// guard trip on its own documentation.
/// </remarks>
public class BrandNamingTests
{
    /// <summary>
    /// A second copy of the regex in <c>Sedna.UI.Tests.BrandNamingTests.OldBrand</c>.
    /// That field is internal to the library's test project, and this project has no
    /// reference to that project's assembly — only a single linked source file
    /// (<c>Assets.cs</c>) — so the type does not resolve here. Adding an
    /// InternalsVisibleTo or a project reference would couple the two suites, which is
    /// exactly the split CLAUDE.md requires staying honest (the library suite must
    /// pass with the catalogue project deleted). Two copies of one regex is the
    /// lesser evil.
    /// </summary>
    /// <remarks>
    /// <c>DR\\?\.Simple_UI</c> tolerates an optional literal backslash before the dot
    /// so it also catches the escaped spelling (<c>DR\.Simple_UI</c>) a grep,
    /// <c>perl -pi</c> or C# regex literal writes when it needs the dot to be
    /// literal — see the matching remark on the other copy of this regex for why.
    /// </remarks>
    private static readonly Regex OldBrand = new(
        @"\bdr-|DR\\?\.Simple_UI|DrSimpleUi|drSimpleUi|drui\.|@layer\s+dr\.|\bdr\.(tokens|base|frame|paint|utilities|overrides)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Files <see cref="CatalogueAssets.ContentFiles"/> doesn't walk, but which still
    /// ship user-visible or externally-served text.
    /// </summary>
    /// <remarks>
    /// <c>Mcp/*.cs</c> (<c>CatalogueTools.cs</c>, <c>McpInstructions.cs</c>,
    /// <c>Docs.cs</c> and friends) holds the six tool descriptions served by the
    /// public, unauthenticated MCP endpoint — the most externally visible text in
    /// the app. <c>Program.cs</c> is the host. <c>wwwroot/catalogue.js</c>'s absence
    /// from <c>ContentFiles()</c> was incidental, unlike <c>wwwroot/catalogue.css</c>,
    /// which that method excludes deliberately and says so in its own comment. Kept
    /// separate here rather than folded into <c>ContentFiles()</c> itself, because
    /// CoverageTests depends on that method's exact shape.
    /// </remarks>
    private static IEnumerable<string> ExtraContentFiles()
    {
        var mcpDir = Path.Combine(CatalogueAssets.AppDir, "Mcp");

        return Directory.EnumerateFiles(mcpDir, "*.cs", SearchOption.AllDirectories)
            .Append(Path.Combine(CatalogueAssets.AppDir, "Program.cs"))
            .Append(Path.Combine(CatalogueAssets.AppDir, "wwwroot", "catalogue.js"));
    }

    [Fact]
    public void No_catalogue_content_file_carries_the_old_brand()
    {
        var offenders = new List<string>();

        foreach (var file in CatalogueAssets.ContentFiles().Concat(ExtraContentFiles()))
        {
            var text = File.ReadAllText(file);

            var found = OldBrand
                .Matches(text)
                .Select(m => m.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (found.Count > 0)
                offenders.Add(
                    $"{Path.GetRelativePath(Assets.RepoRoot, file)}: {string.Join(", ", found)}");
        }

        Assert.True(offenders.Count == 0,
            "The old brand survives in: " + string.Join("; ", offenders));
    }

    /// <summary>
    /// The two copies of <c>OldBrand</c> — this one and
    /// <c>Sedna.UI.Tests.BrandNamingTests.OldBrand</c> — are meant to be
    /// byte-identical. Nothing enforces that automatically once they're written, so
    /// this reads the other file as plain text and compares its regex literal
    /// against this one. Reading a sibling source file as text needs no project
    /// reference, so it does not reopen the split the two-file design exists to keep:
    /// this lives here, in the catalogue project, which is the one that cannot see
    /// the library's internals.
    /// </summary>
    [Fact]
    public void The_two_copies_of_OldBrand_stay_in_sync()
    {
        var libraryFile = Path.Combine(
            Assets.RepoRoot, "src", "Sedna.UI.Tests", "Packaging", "BrandNamingTests.cs");
        var libraryText = File.ReadAllText(libraryFile);

        var match = Regex.Match(libraryText, @"OldBrand\s*=\s*new\(\s*@""(?<pattern>[^""]*)""");
        Assert.True(match.Success,
            $"Could not find the OldBrand regex literal in "
            + $"{Path.GetRelativePath(Assets.RepoRoot, libraryFile)} — did its declaration change shape?");

        Assert.Equal(match.Groups["pattern"].Value, OldBrand.ToString());
    }
}
