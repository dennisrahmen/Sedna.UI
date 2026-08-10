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
