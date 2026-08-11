using System.Text.Json;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The generated class history actually attributes releases.
/// </summary>
/// <remarks>
/// <para>
/// A fully-nulled <c>class-history.json</c> is internally self-consistent, so
/// <c>build/class-history.sh --check</c> cannot detect one. The generator reads the stylesheet out
/// of each git tag, and it once did so at the <em>working tree's</em> path — which stops resolving
/// the moment the file is renamed, silently attributing nothing at all. That shipped once and
/// nothing was red.
/// </para>
/// <para>
/// This asserts against the file's own <c>latestRelease</c> rather than a literal version number.
/// An earlier draft hard-coded <c>"0.1.0"</c>, which was true only while that tag existed — the
/// history was later squashed to a single commit and the tag removed, and a test pinned to a
/// version is a test that has to be edited every time the release line moves. What actually needs
/// guarding is the invariant: <b>if a release exists at all, something must be attributed to it.</b>
/// </para>
/// </remarks>
public class CatalogueDataTests
{
    private static JsonDocument History() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(CatalogueAssets.AppDir, "Data", "class-history.json")));

    [Theory]
    [InlineData("classes")]
    [InlineData("tokens")]
    public void If_a_release_exists_something_is_attributed_to_it(string section)
    {
        using var doc = History();

        var latest = doc.RootElement.TryGetProperty("latestRelease", out var l) ? l.GetString() : null;
        if (string.IsNullOrEmpty(latest))
        {
            // No tag in the checkout the file was generated from. The generator refuses to write
            // an all-null file in that state, so reaching here means something else produced it.
            Assert.Fail(
                "class-history.json names no latestRelease. build/class-history.sh exits 1 rather "
                + "than emit an unattributed file, so this file did not come from it — regenerate.");
        }

        var entries = doc.RootElement.GetProperty(section).EnumerateObject().ToList();
        Assert.NotEmpty(entries);

        var attributed = entries.Count(p => p.Value.ValueKind != JsonValueKind.Null);

        Assert.True(attributed > 0,
            $"Every {section} entry is null while latestRelease is \"{latest}\". That is the "
            + "silent failure this test exists for: the generator resolved no tag, most likely "
            + "because the stylesheet's path at that tag is missing from build/css-path.sh. An "
            + "all-null file passes --check, because it is self-consistent.");

        // Anything attributed must name a real release, not an arbitrary string.
        foreach (var entry in entries.Where(p => p.Value.ValueKind != JsonValueKind.Null))
        {
            var version = entry.Value.GetString();
            Assert.True(
                !string.IsNullOrWhiteSpace(version) && version!.Split('.').Length == 3,
                $"{section}/{entry.Name} is attributed to \"{version}\", which is not a version.");
        }
    }
}
