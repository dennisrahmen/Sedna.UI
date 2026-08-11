using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Documentation a consuming app copies verbatim, and the README that doubles as the nuget.org page.
/// </summary>
public class DocumentationTests
{
    [Fact]
    public void The_documented_host_page_loads_the_assets_in_the_right_order()
    {
        // Every consuming app copies this block out of getting-started.md, so an error
        // here is an error in each of them. The order is not cosmetic: boot.js applies
        // the stored theme before first paint, and brand.css has to win over the
        // library's tokens. This used to be pinned against the project template's
        // App.razor; with the template gone, the documentation IS the host page.
        var doc = File.ReadAllText(Path.Combine(Assets.RepoRoot, "docs", "getting-started.md"));

        var boot = doc.IndexOf("js/Sedna.UI.boot.js", StringComparison.Ordinal);
        var sheet = doc.IndexOf("css/Sedna.UI.css", StringComparison.Ordinal);
        var brand = doc.IndexOf("css/brand.css", StringComparison.Ordinal);
        var headEnd = doc.IndexOf("</head>", StringComparison.Ordinal);
        var main = doc.IndexOf("js/Sedna.UI.js\"", StringComparison.Ordinal);

        Assert.True(boot > 0, "getting-started.md no longer shows boot.js being loaded.");
        Assert.True(headEnd > 0, "getting-started.md no longer shows a host page <head>.");
        Assert.True(boot < headEnd, "boot.js must be in <head>, or the theme flashes.");
        Assert.True(sheet > boot, "The stylesheet must be documented after boot.js.");
        Assert.True(brand > sheet, "brand.css must be documented after the library stylesheet.");
        Assert.True(main > headEnd, "The main script belongs at the end of <body>.");
    }

    [Fact]
    public void The_readme_hero_uses_an_absolute_image_url()
    {
        // README.md is also the nuget.org readme. Relative image paths do not
        // resolve there, and nuget.org only renders images from allow-listed
        // hosts — raw.githubusercontent.com being the usual one. A relative path
        // renders fine on GitHub and silently breaks on the package page.
        var readme = File.ReadAllText(Path.Combine(Assets.RepoRoot, "README.md"));
        var hero = readme.Split('\n').First(l => l.TrimStart().StartsWith("![", StringComparison.Ordinal));

        Assert.Contains("https://raw.githubusercontent.com/", hero, StringComparison.Ordinal);
    }
}
