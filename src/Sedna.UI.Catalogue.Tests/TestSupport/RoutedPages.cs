using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// Every route the app actually serves, read from the <c>[Route]</c> attributes
/// the Razor compiler emits.
/// </summary>
/// <remarks>
/// Reflection rather than a text scan. The static catalogue's navigation test
/// string-searched <c>catalogue.js</c> for <c>const CAT_PAGES</c> and regexed the
/// hrefs out; this asks the compiler instead, so a misspelled route is a build
/// error long before it is a test failure.
/// </remarks>
internal static class RoutedPages
{
    public static IReadOnlyList<string> All { get; } =
        typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
            .Select(a => a.Template)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

    public static TheoryData<string> AsTheoryData()
    {
        var data = new TheoryData<string>();
        foreach (var route in All) data.Add(route);

        // A reflection query that silently matches nothing makes every assertion
        // over it pass vacuously — the same hazard the static catalogue's glob had.
        Assert.NotEmpty(data);
        return data;
    }
}
