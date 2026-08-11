using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Single source of CSS, and the host page every consuming app copies.
/// </summary>
/// <remarks>
/// Replaces the old <c>StylesheetSourceTests</c>, which asserted these over 29
/// static pages. There is one host page now, so "every page" collapses — but the
/// last test here is stronger than anything the static catalogue could assert: it
/// compares the bytes the browser actually receives against the file in the repo,
/// rather than checking that a relative path resolves on disk.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class HostPageTests(CatalogueAppFixture app)
{
    private const string ShippedCss = "_content/Sedna.UI/css/Sedna.UI.css";
    private const string ShippedJs = "_content/Sedna.UI/js/Sedna.UI.js";
    private const string BootJs = "_content/Sedna.UI/js/Sedna.UI.boot.js";
    private const string IconCss = "_content/Sedna.UI/lib/remixicon/remixicon.css";

    private static string AppRazor =>
        File.ReadAllText(Path.Combine(CatalogueAssets.AppDir, "Components", "App.razor"));

    [Fact]
    public void The_host_page_links_the_shipped_stylesheet() =>
        Assert.Contains(ShippedCss, AppRazor, StringComparison.Ordinal);

    [Fact]
    public void The_host_page_links_no_copy_of_the_design_system()
    {
        var hrefs = Regex.Matches(AppRazor, @"<link[^>]*rel=""stylesheet""[^>]*href=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Equal([IconCss, ShippedCss, "catalogue.css"], hrefs);
    }

    [Fact]
    public void The_host_page_loads_the_assets_in_the_documented_order()
    {
        // The block in docs/getting-started.md, in its order. The app's own
        // catalogue.css stands in for a consuming app's brand.css, so only the
        // library's five lines are compared — but they are compared against the
        // text the documentation actually contains, so the two cannot drift.
        var documented = File.ReadAllText(
            Path.Combine(Assets.RepoRoot, "docs", "getting-started.md"));

        AssertOrder(documented, BootJs, IconCss, ShippedCss);
        AssertOrder(AppRazor, BootJs, IconCss, ShippedCss);

        // The script comes after the document, in both.
        Assert.True(documented.IndexOf(ShippedJs, StringComparison.Ordinal)
                    > documented.IndexOf(ShippedCss, StringComparison.Ordinal));
        Assert.True(AppRazor.IndexOf(ShippedJs, StringComparison.Ordinal)
                    > AppRazor.IndexOf(ShippedCss, StringComparison.Ordinal));
    }

    [Fact]
    public void Blazor_loads_after_the_library_script()
    {
        // Otherwise an interactive component can run before window.sednaUi
        // exists, which fails intermittently and only under load.
        Assert.True(
            AppRazor.IndexOf("_framework/blazor.web.js", StringComparison.Ordinal)
            > AppRazor.IndexOf(ShippedJs, StringComparison.Ordinal),
            "blazor.web.js must come after Sedna.UI.js.");
    }

    [Fact]
    public void The_host_page_supplies_the_reconnect_banner_the_catalogue_documents()
    {
        // Blazor injects its own reconnect UI — unstyled, with inline styles — when the
        // host page has no #components-reconnect-modal. The library ships styles for
        // all three rows and this site did not use them, so the one place a reader
        // would ever see a connection failure looked nothing like the documentation of
        // it. Comparing against the snippet keeps the page and the app the same markup.
        var documented = File.ReadAllText(Path.Combine(
            CatalogueAssets.ExamplesDir, "Frame", "ReconnectHostPage.html"));

        // Comments are stripped from both sides: the snippet is HTML and carries
        // <!-- … -->, the host page is Razor and carries @* … *@, and the same note
        // cannot be written both ways at once. Everything else is compared verbatim.
        Assert.Contains(WithoutComments(documented), WithoutComments(AppRazor),
            StringComparison.Ordinal);
    }

    private static string WithoutComments(string source) =>
        Assets.Squash(Regex.Replace(source, @"<!--.*?-->|@\*.*?\*@", " ",
            RegexOptions.Singleline));

    [Fact]
    public async Task The_stylesheet_the_running_app_serves_is_the_file_that_ships()
    {
        var served = await app.Client.GetStringAsync(new Uri(ShippedCss, UriKind.Relative));

        // Not "a relative path resolves on disk" — the bytes the browser receives.
        // This is what catches a static-web-asset misconfiguration.
        Assert.Equal(Normalise(File.ReadAllText(Assets.CssPath)), Normalise(served));
    }

    [Fact]
    public async Task The_script_the_running_app_serves_is_the_file_that_ships()
    {
        var served = await app.Client.GetStringAsync(new Uri(ShippedJs, UriKind.Relative));

        Assert.Equal(Normalise(File.ReadAllText(Assets.JsPath)), Normalise(served));
    }

    private static string Normalise(string s) => s.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void AssertOrder(string text, params string[] needles)
    {
        var previous = -1;
        foreach (var needle in needles)
        {
            var at = text.IndexOf(needle, StringComparison.Ordinal);
            Assert.True(at >= 0, $"\"{needle}\" is missing.");
            Assert.True(at > previous, $"\"{needle}\" is out of order.");
            previous = at;
        }
    }
}
