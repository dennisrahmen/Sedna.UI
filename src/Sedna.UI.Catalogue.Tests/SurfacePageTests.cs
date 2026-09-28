using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The catalogue half of the tier 3 registry: every surface in <c>docs/surfaces.md</c> has its page,
/// every page of a surface says tier 3 in its badge, and no other page does.
/// </summary>
/// <remarks>
/// A surface's pages are its route and every route that extends it — <c>/graph</c> and
/// <c>/graph-layouts</c>. The badge is the tier rule where a reader meets it, so a page claiming
/// tier 3 for something the registry does not list is as wrong as a surface page that hides it.
/// </remarks>
public class SurfacePageTests
{
    private sealed record Page(string Route, string File, string? Tier);

    private static readonly Regex Route = new(@"^@page\s+""([^""]+)""", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex Tier = new(@"<CatHead\b[^>]*\bTier=""@CatHead\.(\w+)""", RegexOptions.Compiled);

    private static List<Page> Pages() =>
        Directory.GetFiles(CatalogueAssets.PagesDir, "*.razor")
            .Select(f => (File: f, Text: File.ReadAllText(f)))
            .SelectMany(p => Route.Matches(p.Text).Select(m =>
                new Page(m.Groups[1].Value, Path.GetFileName(p.File), Tier.Match(p.Text) is { Success: true } t ? t.Groups[1].Value : null)))
            .ToList();

    private static Surfaces.Surface? SurfaceOf(string route) =>
        Surfaces.All.FirstOrDefault(s =>
            route == s.Catalogue || route.StartsWith(s.Catalogue + "-", StringComparison.Ordinal));

    [Fact]
    public void Every_surface_has_its_page()
    {
        var missing = Surfaces.All.Where(s => !RoutedPages.All.Contains(s.Catalogue))
            .Select(s => $"{s.Name}: {s.Catalogue}").ToList();
        Assert.True(missing.Count == 0, "No catalogue page at: " + string.Join(", ", missing));
    }

    [Fact]
    public void A_surface_page_says_tier_3_and_no_other_page_does()
    {
        var pages = Pages();
        Assert.NotEmpty(pages);

        var problems = new List<string>();
        foreach (var page in pages)
        {
            var surface = SurfaceOf(page.Route);
            if (surface is not null && page.Tier != "Tier3")
                problems.Add($"{page.File} ({page.Route}) belongs to the {surface.Name} surface — give its CatHead Tier=\"@CatHead.Tier3\"");
            if (surface is null && page.Tier == "Tier3")
                problems.Add($"{page.File} ({page.Route}) says tier 3, and docs/surfaces.md lists no surface at that route");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
