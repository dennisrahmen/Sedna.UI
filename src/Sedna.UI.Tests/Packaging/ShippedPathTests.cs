using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Asset paths a consuming app hard-codes. A rename here is a silent 404 there.
/// </summary>
public class ShippedPathTests
{
    [Theory]
    [InlineData("wwwroot/css/Sedna.UI.css")]
    [InlineData("wwwroot/js/Sedna.UI.js")]
    [InlineData("wwwroot/js/Sedna.UI.boot.js")]
    // build/verify-package.sh has always required this; nothing here pinned it.
    [InlineData("wwwroot/tokens/Sedna.UI.tokens.json")]
    [InlineData("wwwroot/lib/remixicon/remixicon.css")]
    [InlineData("wwwroot/lib/remixicon/remixicon.woff2")]
    [InlineData("wwwroot/lib/remixicon/LICENSE")]
    // No app writes this one out: Blazor finds a JavaScript initializer by exactly this
    // name, and a rename leaves @onsedna-drop receiving empty event arguments.
    [InlineData("wwwroot/Sedna.UI.lib.module.js")]
    // No app writes these out either: Sedna.UI.js imports a surface's module relative to itself,
    // and the module imports its engine relative to itself. A rename is a graph, or an editor,
    // that never starts.
    [InlineData("wwwroot/js/Sedna.UI.graph.js")]
    [InlineData("wwwroot/lib/cytoscape/cytoscape.js")]
    [InlineData("wwwroot/lib/cytoscape/VENDORED.txt")]
    [InlineData("wwwroot/js/Sedna.UI.editor.js")]
    [InlineData("wwwroot/lib/quill/quill.js")]
    [InlineData("wwwroot/lib/quill/VENDORED.txt")]
    [InlineData("wwwroot/js/Sedna.UI.orb.js")]
    public void Shipped_asset_exists_at_its_documented_path(string relativePath)
    {
        // Consuming apps write these paths out by hand as
        // _content/Sedna.UI/<path-under-wwwroot>. A rename here is a silent
        // 404 there, so the names are part of the public contract.
        var full = Path.Combine(Assets.ProjectDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), $"Missing shipped asset: {relativePath}");
    }

    [Fact]
    public void The_package_carries_no_catalogue()
    {
        // The catalogue is an application now, not a package asset. A
        // wwwroot/catalogue/ reappearing means the split has been undone by
        // accident — most plausibly by someone restoring a deleted file.
        // build/verify-package.sh asserts the same thing against a real .nupkg.
        var catalogue = Path.Combine(Assets.ProjectDir, "wwwroot", "catalogue");

        Assert.False(Directory.Exists(catalogue),
            "The catalogue is src/Sedna.UI.Catalogue, a hosted app. Nothing under "
            + "wwwroot/catalogue/ ships in the package.");
    }
}
