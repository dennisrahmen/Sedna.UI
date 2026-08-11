using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// Where the catalogue application's own source lives. The shipped library assets
/// are located by <see cref="Assets"/>, which is linked in from the library's
/// suite rather than copied.
/// </summary>
internal static class CatalogueAssets
{
    public static string AppDir { get; } =
        Path.Combine(Assets.RepoRoot, "src", "Sedna.UI.Catalogue");

    public static string ExamplesDir => Path.Combine(AppDir, "Examples");
    public static string PagesDir => Path.Combine(AppDir, "Components", "Pages");
    public static string LayoutDir => Path.Combine(AppDir, "Components", "Layout");
    public static string NavigationDir => Path.Combine(AppDir, "Navigation");
    public static string CatalogueCssPath => Path.Combine(AppDir, "wwwroot", "catalogue.css");

    /// <summary>
    /// Every file the catalogue's content is written in: the example sources, the
    /// pages, the components and the registry.
    /// </summary>
    /// <remarks>
    /// <c>Examples/</c> is the whole tree rather than <c>*.razor</c> — the code-only
    /// snippets document classes too. <c>wwwroot/catalogue.css</c> is deliberately
    /// excluded, matching what the static catalogue's coverage test did: a comment
    /// in the docs' own chrome mentioning a class is not documentation of it.
    /// </remarks>
    public static IEnumerable<string> ContentFiles()
    {
        foreach (var (dir, pattern) in new[]
                 {
                     (ExamplesDir, "*"),
                     (Path.Combine(AppDir, "Components"), "*.razor"),
                     (NavigationDir, "*.cs"),
                 })
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
                yield return file;
        }
    }
}
