using System.Text.Json;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The generated class history actually attributes releases.
/// </summary>
/// <remarks>
/// A fully-nulled class-history.json is internally self-consistent, so
/// build/class-history.sh --check cannot detect one. The generator reads the
/// stylesheet out of each git tag, and it once did so at the working tree's path —
/// which stops resolving the moment the file is renamed, silently attributing
/// nothing. This asserts a class that demonstrably shipped still says so.
/// </remarks>
public class CatalogueDataTests
{
    private static JsonDocument History() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(CatalogueAssets.AppDir, "Data", "class-history.json")));

    [Fact]
    public void Classes_that_shipped_in_a_release_carry_that_release()
    {
        using var doc = History();
        var classes = doc.RootElement.GetProperty("classes");

        // .card is in the published 0.1.0 stylesheet and has never been renamed.
        Assert.Equal("0.1.0", classes.GetProperty("card").GetString());

        var attributed = classes.EnumerateObject().Count(p => p.Value.ValueKind != JsonValueKind.Null);
        Assert.True(attributed > 0,
            "Every class is null. build/class-history.sh read no tag — check build/css-path.sh.");
    }

    [Fact]
    public void Tokens_that_shipped_in_a_release_carry_that_release()
    {
        using var doc = History();
        Assert.Equal("0.1.0", doc.RootElement.GetProperty("tokens").GetProperty("--brand").GetString());
    }
}
