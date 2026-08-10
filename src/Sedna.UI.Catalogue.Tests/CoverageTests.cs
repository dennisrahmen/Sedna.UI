using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Every class the stylesheet defines is shown somewhere in the catalogue.
/// </summary>
/// <remarks>
/// <para>
/// A class nobody can find is a class nobody uses, and then someone writes it again
/// in an app. The first run of this test found 97 undocumented classes.
/// </para>
/// <para>
/// <b>There is no allowlist here and there should never be one.</b> The static
/// catalogue carried three Blazor-owned reconnect classes on an exception list; the
/// Shell &amp; nav page shows all three now, so it is gone.
/// </para>
/// <para>
/// A mention in a page's prose counts as documentation. That is how
/// <c>.menu-scrim</c> is covered — it cannot be demonstrated live without a
/// full-viewport element swallowing every click on the page — and how the three
/// Blazor-owned <c>components-reconnect-*</c> classes are, which an app never
/// writes but may need to recognise.
/// </para>
/// <para>
/// A stricter version of this, asserting every class is <i>rendered or printed</i>
/// by a page rather than merely mentioned in a file, currently reports eleven
/// classes that exist only while JavaScript is mid-interaction —
/// <c>.sedna-tip--visible</c>, <c>.drawer-scrim--open</c>, <c>.dropzone--over</c>,
/// <c>.tab--active</c> and the rest. They are real documentation gaps rather than
/// test noise, and closing them is editorial work on several pages.
/// </para>
/// </remarks>
public class CoverageTests
{
    private static ISet<string> Declared() =>
        Assets.ClassSelectors(Assets.StripComments(Assets.Css));

    [Fact]
    public void Every_class_in_the_stylesheet_appears_somewhere_in_the_catalogue_source()
    {
        // The examples, the pages, the app's own layout, and the registry — the same
        // haystack shape the static catalogue used, over the files that replaced it.
        var haystack = string.Concat(
            CatalogueAssets.ContentFiles()
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        var missing = Declared()
            .Where(name => !haystack.Contains(name, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"{missing.Count} classes are in the stylesheet but nowhere in the catalogue. "
            + "A class with no page is a class nobody can find: "
            + string.Join(", ", missing));
    }

}
