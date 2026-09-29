using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Every class an example writes is a class something actually defines.
/// </summary>
/// <remarks>
/// <para>
/// <c>CoverageTests</c> runs the other direction — every class the stylesheet
/// declares is shown somewhere — and cannot see this failure at all. A class in an
/// example that no stylesheet declares does nothing, silently: the demo still
/// renders, because the surrounding classes carry the look, and the snippet still
/// prints, because it is the bytes on disk. Then someone copies it into an app and
/// inherits a name that has never existed.
/// </para>
/// <para>
/// Every example carries a <c>since</c>, so a copied class reads as shipped in that
/// release. The first run of this test found four: <c>.card-title</c>,
/// <c>.btn-secondary</c>, <c>.toolbar-field</c> and <c>.ri-side-bar-right-line</c> —
/// an icon that is not in the bundled Remix Icon version and rendered as a blank
/// box.
/// </para>
/// <para>
/// The two stylesheets are the whole allowance and there is no exception list. Both
/// ship inside the package, so a class in neither is a class no consuming app has.
/// A value containing <c>@</c> is Razor computing the class at runtime
/// (<c>Examples/Interop/</c>), which this cannot read and does not try to.
/// </para>
/// </remarks>
public class ExampleClassTests
{
    private static readonly Regex ClassAttribute =
        new("""class\s*=\s*["']([^"']*)["']""", RegexOptions.Compiled);

    [Fact]
    public void Every_class_an_example_writes_is_declared_by_a_shipped_stylesheet()
    {
        var declared = Assets.ClassSelectors(Assets.StripComments(Assets.Css));
        declared.UnionWith(Assets.ClassSelectors(File.ReadAllText(Assets.IconCssPath)));

        var offenders = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(
                     CatalogueAssets.ExamplesDir, "*", SearchOption.AllDirectories))
        {
            foreach (Match attribute in ClassAttribute.Matches(File.ReadAllText(path)))
            {
                var value = attribute.Groups[1].Value;
                if (value.Contains('@', StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var name in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (declared.Contains(name))
                    {
                        continue;
                    }

                    if (!offenders.TryGetValue(name, out var files))
                    {
                        offenders[name] = files = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    files.Add(Path.GetRelativePath(CatalogueAssets.ExamplesDir, path));
                }
            }
        }

        Assert.True(offenders.Count == 0,
            $"{offenders.Count} classes are written by an example but declared by no shipped "
            + "stylesheet, so they do nothing and copying them does nothing: "
            + string.Join("; ", offenders.Select(o => $".{o.Key} ({string.Join(", ", o.Value)})")));
    }
}
