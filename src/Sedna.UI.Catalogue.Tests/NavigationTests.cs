using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The page registry and the pages the app actually serves describe the same
/// catalogue.
/// </summary>
/// <remarks>
/// A page missing from the registry is a page nobody can navigate to; a registry
/// entry with no page is a dead link in the sidebar. Both directions are asserted,
/// because each hides the other.
/// </remarks>
public class NavigationTests
{
    [Fact]
    public void The_catalogue_has_pages() =>
        // The vacuity guard. A reflection query that matches nothing would make
        // every assertion below pass while the site was empty.
        Assert.True(RoutedPages.All.Count >= 10,
            $"Only {RoutedPages.All.Count} routable pages were found.");

    [Fact]
    public void Every_routable_page_has_a_navigation_entry()
    {
        var registered = CataloguePages.All.Select(p => p.Route).ToHashSet(StringComparer.Ordinal);
        var orphans = RoutedPages.All.Where(r => !registered.Contains(r)).ToList();

        Assert.True(orphans.Count == 0,
            "These pages exist but are in no navigation group, so nothing links to them: "
            + string.Join(", ", orphans));
    }

    [Fact]
    public void Every_navigation_entry_points_at_a_routable_page()
    {
        var routed = RoutedPages.All.ToHashSet(StringComparer.Ordinal);
        var dead = CataloguePages.All.Where(p => !routed.Contains(p.Route)).ToList();

        Assert.True(dead.Count == 0,
            "These navigation entries have no page, so the sidebar links to a 404: "
            + string.Join(", ", dead.Select(p => p.Route)));
    }

    [Fact]
    public void Every_page_has_a_label_an_icon_and_a_blurb()
    {
        // The sidebar, the landing-page tiles and the search index all read all
        // three, and a missing one degrades quietly rather than failing.
        foreach (var page in CataloguePages.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(page.Label), $"{page.Route} has no label.");
            Assert.False(string.IsNullOrWhiteSpace(page.Icon), $"{page.Route} has no icon.");
            Assert.False(string.IsNullOrWhiteSpace(page.Blurb), $"{page.Route} has no blurb.");
        }
    }

    [Fact]
    public void Every_icon_is_a_class_the_bundled_font_defines()
    {
        // New guard: a mistyped ri-* name renders a blank box in the sidebar and
        // nothing notices. The bundled font is the only icon set, so its own
        // stylesheet is the whole vocabulary.
        var glyphs = Assets.IconGlyphClasses(File.ReadAllText(Assets.IconCssPath));

        foreach (var page in CataloguePages.All)
        {
            Assert.True(glyphs.Contains(page.Icon),
                $"{page.Route} uses \"{page.Icon}\", which Remix Icon does not define.");
        }
    }

    [Fact]
    public void Every_group_in_the_registry_is_in_the_display_order()
    {
        // Groups drives the sidebar's section order; a group named on a page but
        // missing here would silently drop that page out of the navigation.
        var used = CataloguePages.All.Select(p => p.Group).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(used.Except(CataloguePages.Groups, StringComparer.Ordinal));
        Assert.Empty(CataloguePages.Groups.Except(used, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_legacy_file_name_is_distinct()
    {
        // The legacy names drive the /catalogue/<file>.html redirects. Two routes
        // deriving the same file would make one of them unreachable from a
        // published link.
        var files = CataloguePages.All.Select(p => p.LegacyFile).ToList();

        Assert.Equal(files.Count, files.Distinct(StringComparer.Ordinal).Count());
    }
}
